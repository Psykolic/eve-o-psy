# Arquitetura do EVE-O Preview

Visão de conjunto de como o projeto está organizado depois da Fase 2. Para os diagramas completos
(Context, Container, Component, Code), ver [`docs/audits/architecture-c4.md`](../audits/architecture-c4.md).

---

## Camadas

```
EVE-O-Preview        Program: mutex de instância única, handlers de exceção, ciclo de vida
      │
EveO.Presentation    WinForms/WPF: View, Presenters, Handlers, ThumbnailManager, CompositionRoot
      │
EveO.Infrastructure  Win32, DWM, processos, janelas, captura GDI
      │
EveO.Core            Configuração, mensagens do Mediator, abstrações — sem nenhuma dependência de Windows
```

**Regra**: as setas só apontam para baixo. `EveO.Core` não conhece WinForms, WPF nem P/Invoke; por isso
ele roda e é testado no Linux.

---

## Onde cada coisa vive

| Preocupação | Onde | Notas |
|---|---|---|
| Regras de configuração, limites, migração de JSON | `EveO.Core/Configuration` | testado sem Windows |
| Mensagens e handlers do Mediator | mensagens no Core, handlers no Presentation | handlers usam `IThumbnailManager` |
| Interop (janelas, DWM, GDI, processos) | `EveO.Infrastructure` | `InternalsVisibleTo` para Presentation |
| Regra de composição (DI) | `EveO.Presentation/ApplicationBase/CompositionRoot.cs` | o exe só chama `CompositionRoot.Create()` |
| Refresh das thumbnails | `ThumbnailManager` + `WindowEventSource` | event-driven, ver abaixo |

### Por que a raiz de composição não fica no exe

`CompositionRoot` mora na Presentation porque é ela que conhece todas as implementações. Isso permite
que `EveO.Tests` construa o mesmo container da produção e detecte registros quebrados — o teste que
teria evitado o crash de inicialização da v8.2.0 (ver ADR da Fase 2.1 em
`docs/audits/plano-2.1-separacao-camadas.md`).

**Cuidado com o LightInject**: `Register<TService, TImplementation>()` é *transient*. Serviços que
precisam ser compartilhados (sinal de eventos, fonte de eventos) têm que ser registrados como singleton,
senão produtor e consumidor falam com instâncias diferentes. `CompositionRootTests` cobre isso.

---

## Ciclo de refresh (Fase 2.2)

```
Windows ──evento──► WindowEventSource ──Publish──► ClientChangeSignal (canal de 1, DropOldest)
                                                        │
                                                        ▼
                                  ThumbnailManager (consumidor, fora da thread de UI)
                                                        │ SynchronizationContext.Post
                                                        ▼
                                  UpdateThumbnailsList + RefreshThumbnails (thread de UI)
```

- **Filtro na origem**: só janelas top-level, sem `WS_EX_TOOLWINDOW`, sem owner e de processos listados
  em `ExecutablesToPreview`.
- **Reconciliação**: um `DispatcherTimer` de 5 s garante que nada fique perdido (UIPI/execução elevada).
- **Settle**: após um evento, o timer acelera para 500 ms por ~3 s, porque remover uma thumbnail exige
  três varreduras consecutivas sem o processo.

Configuração: `EventDrivenRefresh` (padrão `true`) e `ReconciliationPeriod` (padrão `5000` ms).
`"EventDrivenRefresh": false` volta ao polling antigo, sem recompilar.

---

## Decisões registradas

As decisões vivem em `docs/audits/adr-00*.md`. As que moldam o código hoje:

| ADR | Efeito no código |
|---|---|
| ADR-006 | refresh por evento com `Channel<T>`, sem Rx.NET |
| ADR-007 | renderização completa: virtualização não passa o gate de memória |
| ADR-008 | sem pool de DWM: registro custa 0,012 ms e não vaza handle |

---

## Testes e CI

- `EveO.Core.Tests` — roda no Linux, sem Windows.
- `EveO.Tests` — roda no Windows; os testes que dependem de desktop interativo (GDI, hotkey global,
  foreground) ficam fora do runner de serviço e são cobertos por smoke manual com clientes reais.
- CI: Gate + Linux + Windows, todos em runner self-hosted.
