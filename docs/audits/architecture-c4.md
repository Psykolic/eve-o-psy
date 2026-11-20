# Documentação de Arquitetura C4

*Atualizada em 2026-11-20 — Fase 2.1 (camadas), Fase 2.2 (refresh event-driven) e Fase 3 (Forgejo como fonte, GitHub como espelho de release).*

> Ver `docs/audits/spike-forgejo.md` para as medições e o plano da Fase 3.

Para a visão de conjunto e as regras de dependência, ver `docs/architecture/README.md`.
Decisões: `docs/audits/adr-00*.md`.

---

## Visão geral

O sistema tem **quatro assemblies de produção** e dois de teste:

| Assembly | Papel | Depende de |
|---|---|---|
| `EveO.Core` | domínio, configuração, mensagens do Mediator, abstrações | nada de Windows |
| `EveO.Infrastructure` | interop Win32/DWM, processos, janelas, captura GDI | Core |
| `EveO.Presentation` | WinForms/WPF: views, presenters, handlers, `ThumbnailManager`, raiz de composição | Core, Infrastructure |
| `EVE-O-Preview` | `Program`: mutex, exceções, ciclo de vida | Presentation |
| `EveO.Core.Tests` | testes sem Windows | Core |
| `EveO.Tests` | testes com Windows (GDI, integração) | Core, Infrastructure, Presentation |

**Regra de ouro**: `EveO.Core` não referencia `System.Windows.Forms`, `System.Windows.Threading` nem
P/Invoke. Se algum tipo do Core puxar Windows, a camada está errada.

---

## 1. Contexto (Level 1)

```mermaid
flowchart TB
    U["Usuário (multiboxing)"] -->|hotkeys, config, tray| APP
    subgraph APP["EVE-O Preview"]
        direction TB
        UI["Thumbnails, spotlights e overlay"]
    end
    APP -->|DWM thumbnail e Win32| EVE["Clientes EVE Online (exefile.exe)"]
    APP -->|EVE-O-Preview.json| FS[("Sistema de arquivos")]
    APP -->|ciclo por hotkey| EVE
```

O EVE-O não injeta nada no jogo: ele registra thumbnails de janelas alheias via DWM e reage a eventos
do sistema. Se o cliente rodar elevado e o EVE-O não, o Windows bloqueia a entrega de eventos (UIPI) —
por isso a reconciliação periódica existe.

---

## 2. Container (Level 2)

```mermaid
flowchart TB
    subgraph PROD["Produção"]
        EXE["EVE-O-Preview<br/>Program + mutex + exceções"]
        PRES["EveO.Presentation<br/>View, Presenters, Handlers,<br/>ThumbnailManager, CompositionRoot"]
        INFRA["EveO.Infrastructure<br/>WindowManager, DwmThumbnail,<br/>ProcessMonitor, WindowEventSource,<br/>StaticThumbnailGdi"]
        CORE["EveO.Core<br/>configuração, mensagens,<br/>abstrações, IClientChangeSignal"]
    end

    subgraph TESTS["Testes"]
        T1["EveO.Core.Tests"]
        T2["EveO.Tests"]
    end

    subgraph CI["CI — Forgejo (fonte) e GitHub (releases)"]
        FJ["Forgejo<br/>fonte da verdade"]
        RUN["Runner self-hosted<br/>host mode (sem Docker)"]
        WF[".forgejo/workflows/ci.yml"]
        AM["Auto-merge<br/>timer de 5 min"]
        GH["GitHub<br/>espelho de main + tags"]

        WF --> RUN
        FJ --> RUN
        RUN --> AM
        AM -->|merge + fechamento da issue| FJ
        FJ -->|push main + tags| GH
    end

    EXE --> PRES
    PRES --> INFRA
    PRES --> CORE
    INFRA --> CORE

    T1 --> CORE
    T2 --> PRES
    T2 --> INFRA
    T2 --> CORE

    RUN -.-> T1
    RUN -.-> T2
```

---

## 3. Component (Level 3)

### `EveO.Presentation`

```mermaid
flowchart TB
    CR["CompositionRoot<br/>(raiz de composição / DI)"]
    TM["ThumbnailManager<br/>dono das views e do ciclo de refresh"]
    PRESENTER["MainFormPresenter"]
    HANDLERS["Mediator/Handlers<br/>Services, Configuration, Thumbnails"]
    VIEWS["View (MainForm, ThumbnailView,<br/>ThumbnailOverlay, Spotlight)"]
    APP["ApplicationBase<br/>ApplicationController, Presenter,<br/>LightInjectContainer, ExceptionHandler"]

    CR --> TM
    CR --> PRESENTER
    CR --> HANDLERS
    CR --> VIEWS
    TM --> VIEWS
    PRESENTER --> VIEWS
    HANDLERS --> TM
    APP -.-> CR
```

### `EveO.Infrastructure`

```mermaid
flowchart TB
    WM["WindowManager<br/>ativação, minimização, títulos"]
    DWM["DwmThumbnail<br/>Register / Update / Unregister"]
    PM["ProcessMonitor<br/>varredura e diff de processos"]
    WES["WindowEventSource<br/>SetWinEventHook CREATE..NAMECHANGE"]
    GDI["StaticThumbnailGdi<br/>captura estática"]

    WES -->|Publish| SIGNAL["IClientChangeSignal<br/>(Core)"]
    WM -.->|Win32| OS["Windows"]
    DWM -.->|DWM| OS
    GDI -.->|GDI| OS
```

---

## 4. Code (Level 4) — o ciclo de refresh

Este é o fluxo que a Fase 2.2 mudou (ADR-006). Antes: um `DispatcherTimer` de 500 ms varria processos a
2 Hz. Agora: eventos disparam o refresh e o timer só reconcilia.

```mermaid
sequenceDiagram
    participant OS as Windows
    participant WES as WindowEventSource
    participant SIG as ClientChangeSignal
    participant CONS as Consumidor (ThumbnailManager)
    participant UI as Thread de UI
    participant PM as ProcessMonitor
    participant DWM as DwmThumbnail

    OS->>WES: EVENT_OBJECT_CREATE/DESTROY/SHOW/HIDE/NAMECHANGE
    WES->>WES: filtra toolwindow, owner e processo monitorado
    WES->>SIG: Publish()  (canal de 1 item, DropOldest)
    SIG-->>CONS: ReadAllAsync
    CONS->>CONS: piso de 100 ms entre refreshes
    CONS->>UI: SynchronizationContext.Post
    UI->>PM: GetUpdatedProcesses()
    UI->>DWM: Update / Register / Unregister
    Note over UI: após o evento,<br/>janela de settle acelera o timer<br/>para 500 ms por 3 s
    Note over UI: reconciliação a cada 5 s<br/>cobre eventos perdidos (UIPI)
```

Consequências medidas (7 clientes): CPU idle `1,45% → 0,52%`; criação de thumbnail `620 → 419 ms`.

---

## 5. ADRs

| ADR | Decisão | Status |
|---|---|---|
| ADR-001 | MVP vs MediatR coexistindo | aceito |
| ADR-002 | Abstração DWM via `IThumbnailProvider` | aceito |
| ADR-003 | JSON config (não JsonSchema) | aceito |
| ADR-004 | Sem Rx.NET na Fase 1 | aceito |
| ADR-005 | Sem DWM pooling na Fase 1 | aceito |
| ADR-006 | **Refresh event-driven** (`Channel<T>` + reconciliação 5 s) | aceito, implementado (#175) |
| ADR-007 | Virtualização de thumbnails | **rejeitada** (gate −20% inalcançável: máx. −12%) |
| ADR-008 | Pool de `DwmThumbnail` | **rejeitada** (registro 0,012 ms vs limite de 30 ms) |
