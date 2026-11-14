# Documentação de Arquitetura C4

*Baseada no repositório Eve-O-Preview — gerada em 2026-11-14*

---

## Visão geral

Este documento contém os diagramas C4 (Context, Container, Component e Code) que descrevem a arquitetura atual do sistema Eve-O-Preview. Os diagramas refletem as quatro camadas principais: `Eve-O-Preview` (aplicação de UI), `EveO.Core` (domain/abstrações), `EveO.Tests` (testes de integração) e `EveO.Core.Tests` (testes unitários).

**Referências cruzadas**: `docs/roadmap.md` (Fase 2, item 2.7) e `docs/audits/architecture-plan.md` (plano detalhado de deliverables 2.1–2.7).

---

### 1. Contexto (Level 1)

Diagrama de nível Contextual mostrando o sistema Eve-O-Preview e seus atores externos.

```mermaid
contextDiagram
    title Contexto - Eve-O-Preview

    actor Usuário as User
    actor "Outros Aplicativos" as OtherApps

    package "Eve-O-Preview" {
        description "Aplicação de gerenciamento de thumbnails para clientes EVE Online"
        url "https://example.com/eve-o-preview"

        interface "Hotkey Input" as HK
        interface "JSON Configuration" as Config
        interface "System Tray" as Tray

        other "EVE Online Client" as EVE_Client
    }

    User --> HK
    User --> Config
    User --> Tray
    HK --> Eve-O-Preview
    Config --> Eve-O-Preview
    Tray --> Eve-O-Preview
    Eve_O_Preview --> EVE_Client

    note right of User: Controla ciclo de clientes, hotkeys, configuração
    note right of OtherApps: Integração com ferramentas externas, overlays
    note right of EVE_Client: Cliente do jogo EVE Online que fornece dados de janela
```

---

### 2. Container (Level 2)

Diagrama de nível Container mostrando os principais contêineres de software e suas relações.

```mermaid
containerDiagram
    title Container - Eve-O-Preview

    user Usuário
    system "Eve-O-Preview (UI)" as Eve_Preview_UI
        # Tetradro de tecnologias: .NET 8, WPF, WF, DWM, GDI+

    system "EveO.Core (Domain/Abstrações)" as Eve_Core
        # netstandard2.1 / net8.0, zero dependências Windows-only
        package "Projetos:" {
            component "Core domain" as Core_Domain
            component "Abstrações (IThumbnailProvider, IWindowManager)" as Core_Abstractions
            component "Configurações" as Core_Config
        }

    system "EveO.Tests (Testes de Integração)" as Eve_Tests
        # xUnit, Moq, testes de 7-client cycle, regressão

    system "EveO.Core.Tests (Testes Unitários)" as Eve_Core_Tests
        # xUnit, FluentAssertion, benchmarks mockados

    OtherApps --> Eve_Preview_UI
    EVE_Client --> Eve_Preview_UI

    Eve_Preview_UI --> Eve_Core
    Eve_Core --> Eve_Tests
    Eve_Core --> Eve_Core_Tests

    note right of Eve_Preview_UI: Camada de UI (WPF + Win32/DWM), hotkeys, thumbnails
    note right of Eve_Core: Interfaces e lógica domain; build em Linux confirmado
    note right of Eve_Tests: Integração Win32, cycle de 6+1 clientes
    note right of Eve_Core_Tests: Testes unitários de calculadoras, cálculos de região
```

---

### 3. Component (Level 3)

Diagrama de nível Component mostrando os componentes principais dentro de cada projeto.

```mermaid
componentDiagram
    title Component - Eve-O-Preview e Dependências

    class Eve_Preview_UI "Eve-O-Preview (UI)\n- ThumbnailManager\n- WindowManager\n- ProcessMonitor\n- Hotkey handlers\n- DispatcherTimer\n- StaticThumbnailView" {
        <<application>>
    }

    class Eve_Core "EveO.Core:\n- IThumbnailProvider\n- IWindowManager\n- IProcessMonitor\n- IThumbnailConfiguration\n- ThumbnailOrchestrator\n- SpotlightRegionCalculator" {
        <<interface>>
    }

    class Eve_Tests "EveO.Tests:\n- ThumbnailManagerUiThreadConfinementTests\n- ThumbnailManagerActivationTests\n- ThumbnailManagerStopAsyncTests\n- GdiStressTests\n- StabilityTests" {
        <<test>>
    }

    class Eve_Core_Tests "EveO.Core.Tests:\n- SpotlightRegionCalculatorTests\n- SpotlightResizeCalculatorTests" {
        <<test>>
    }

    Eve_Preview_UI --> Eve_Core : uses abstractions
    Eve_Preview_UI --> Eve_Tests : references
    Eve_Core --> Eve_Core_Tests : references

    note for Eve_Preview_UI "UI thread confinement\nEnsureUiThread invariant" {
        Thread safety via UI confinement
    }
    note for Eve_Core "netstandard2.1\nLinux build verified" {
        No Windows-only deps
    }
```

---

### 4. Code (Level 4)

Diagrama de nível Code destacando classes e interfaces chave com suas responsabilidades.

```mermaid
classDiagram
    title Code - Classes e Interfaces Principais

    class IThumbnailProvider "Interface:\n- RegisterThumbnail()\n- UpdateThumbnail()\n- UnregisterThumbnail()\n- Dispose()"
    class DwmThumbnailProvider "Implementação Windows:\n- DwmRegisterThumbnail\n- DwmUpdateThumbnail\n- GDI fallback\n- SafeHandle management"
    class ThumbnailManager "Classe principal:\n- _thumbnailViews (Dictionary<IntPtr, IThumbnailView>)\n- EnsureUiThread invariant\n- ThumbnailUpdateTimerTick\n- RefreshThumbnails\n- CycleNextClient\n- MinimizeAllClients" {
        + Start() + StopAsync() + EnsureThumbnailViewsThread()
    }
    class WindowManager "Gerencia janelas:\n- GetForegroundWindowHandle\n- IsCompositionEnabled\n- TurnOffAnimation\n- RestoreAnimation\n- ActivateWindow" {
        + GetStaticThumbnail()
    }
    class IWindowManager "Interface:\n- RegisterThumbnail\n- UpdateThumbnail\n- UnregisterThumbnail\n- GetStaticThumbnail" }
    class ProcessMonitor "Monitor de processos:\n- ProcessChanged event\n- Client lifecycle tracking" }
    class IProcessMonitor "Interface:\n- StartMonitoring()\n- StopMonitoring()\n- Events" }

    IThumbnailProvider --> DwmThumbnailProvider : implements
    IWindowManager --> WindowManager : implements
    IProcessMonitor --> ProcessMonitor : implements

    ThumbnailManager ..> IThumbnailProvider : uses
    WindowManager ..> IWindowManager : uses
    ThumbnailManager ..> ProcessMonitor : uses
    ThumbnailManager ..> IProcessMonitor : uses

    note for IThumbnailProvider "Abstração para pooling (ADR-008)\nSem Rx.NET na Fase 1 (ADR-004)" }
    note for IWindowManager "Sem DispatcherTimer direto\nUsar event-driven after spike (ADR-006)" }
    note for ThumbnailManager "UI thread confinement\nFail-fast via EnsureUiThread" }
    note for DwmThumbnailProvider "SafeHandle + GDI fallback\nSem DWM pooling na Fase 1 (ADR-005)" }