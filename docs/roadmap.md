# EVE-O Custom - Roadmap de Arquitetura

*Baseado na [Auditoria de Arquitetura](auditoria-architect.md) — gerado em 2026-11-10*

---

## Resumo Executivo

| Fase | Nome | Objetivo | Duração Estimada | Prioridade Geral | Estado |
|------|------|----------|------------------|------------------|--------|
| **0** | **Foundations** | Auditoria completa, baseline estabelecido | Concluído | — | ✅ concluída |
| **1** | **Stabilization** | Correções críticas, infraestrutura de testes, camadas de abstração | 1–2 sprints | **P0/P1** | 🟡 1.3 parcial; 1.1, 1.2, 1.4, 1.5, 1.6 e 1.7 ✅ |
| **2** | **Architecture Cleanup** | Separação de camadas, virtualização, padrões reativos | 3–5 sprints | **P1/P2** | ✅ concluída |
| **3** | **Cross-Platform & Extensibility** | Avalonia/Uno, plugin architecture, event sourcing | 6+ sprints | **P2/P3** | ⬜ não iniciada |

> **Legenda de Prioridade**: P0 = Blocker (impede progresso), P1 = Crítico (risco alto), P2 = Importante (dívida técnica), P3 = Nice-to-have (melhoria futura)

> **Regra de leitura:** este documento é o **plano**. O que **já existe** está na tabela abaixo, com
> evidência (PR, ADR ou teste). Antes de planejar, confira o código — o plano não anda sozinho.

### Estado verificado em 2026-11-21

| Item | Estado | Evidência |
|------|--------|-----------|
| 1.1 Testes | ✅ | `EveO.Core.Tests` (130 `[Fact]`) + `EveO.Tests` (71 `[Fact]`) |
| 1.2 Abstração do provider | ✅ | a intenção foi cumprida com **duas** abstrações em vez de uma: `IDwmThumbnail` (Core) e `IStaticThumbnailProvider` (Infrastructure). Views dependem das interfaces e os testes usam fakes — o gerenciador não conhece DWM |
| 1.3 Handles | 🟡 parcial | DWM: `SafeDwmThumbnailHandle` + `IDwmThumbnail : IDisposable` (antes era `IntPtr` solto, sem finalizer). GDI: mantém `try/finally` na ordem DC→bitmap documentada + `GdiStressTests` (1.000 capturas) — **não** convertido a `SafeHandle` de propósito |
| 1.4 Validação de config | ✅ | `ConfigurationValidator` + `ConfigurationLimits` (faixa única para aviso e clamp); `Load()` devolve `ConfigurationValidationResult` e o app avisa o que foi corrigido; 17 testes em `EveO.Core.Tests/Configuration` |
| 1.5 Métricas | ✅ | `EveOMetrics` (6 instrumentos) no meter `EveO.Preview`, retrato local a cada 30 s (`EVE_METRICS=1`), `docs/metrics.md` + `metrics-dashboard.json`; **sem** exporter de rede por decisão (ver doc) |
| 1.6 Benchmarks | ✅ | `benchmarks/EveO.Benchmarks` (8 benchmarks do Core), `benchmarks/baseline.json` e job semanal que falha acima de 20% (ruído medido: 8,3%); caminhos de DWM/WPF ficam fora e isso está documentado |
| 1.7 ADRs | ✅ | ADR-001, 004, 005, 006, 007, 008 (+ `architecture-c4.md`) |
| 2.1 Camadas | ✅ | ondas 0–4b (#157–#171) |
| 2.2 Push-based | ✅ | ADR-006 aceito; #173 e #175 |
| 2.3 Virtualização | ✅ decidido **não fazer** | ADR-007 (gate não passou) |
| 2.4 Pool DWM | ✅ decidido **não fazer** | ADR-008 (~0,012 ms por registro) |
| 2.5 Testes Win32/GDI | ✅ | `GdiFailureTests`, `GdiStressTests`, `ThumbnailManagerStabilityTests` no job Windows |
| 2.6 Config event-driven | ✅ | `SaveConfigurationHandler` publica `ConfigurationChangedNotification`; `ConfigurationChangedHandler` aplica `RefreshTimerInterval`/`RefreshZoomFactor` (com a proteção da #125); testes em `EveO.Tests/Mediator/ConfigurationChangedTests.cs` |
| 2.7 Docs de arquitetura | ✅ | ADRs + `docs/architecture/README.md` + `audits/architecture-c4.md` |


---

## Fase 0 — Foundations (Concluída)

| Item | Detalhe |
|------|---------|
| **Objetivo** | Completar auditoria de arquitetura e estabelecer métricas de baseline para 7 clientes EVE |
| **Deliverables** | `docs/auditoria-architect.md` com findings, métricas de referência (Tabela 8), recomendações priorizadas |
| **Acceptance Criteria** | ✅ Auditoria documentada com 15+ findings categorizados<br>✅ Baseline métricas definidas (startup, CPU, memória, latency, handles)<br>✅ Riscos mapeados por categoria (Performance, Arquitetura, Manutenibilidade) |
| **Prioridade** | — |
| **Dependências** | Nenhuma |
| **Esforço** | Concluído (análise estática) |
| **Riscos** | Baseline baseado em análise estática — requer validação com profiling real (dotnet-trace, PerfView, BenchmarkDotNet) antes de otimizações |

---

## Fase 1 — Stabilization (concluída em parte — ver estado acima)

### Objetivo
Eliminar blockers de produção, estabelecer infraestrutura de testes automatizados e introduzir abstrações para desacoplar DWM/Win32 do núcleo da aplicação. **Prioridade absoluta: estabilidade, gerenciamento correto de recursos GDI/DWM, integridade das configurações e testes de regressão.**

### Comportamento a Preservar (Regressão Zero)
- **Ciclagem de 6 clientes principais**: 6 clientes EVE no monitor principal ciclam via hotkeys (F13/F14, F15/F16, etc.) conforme `CycleGroupNClientsOrder`
- **Cliente de intel no monitor secundário**: 1 cliente EVE dedicado a "intel/scout" permanece **fora do ciclo principal** (`IsExcludedFromCycleGroup = true`), posicionado fixo no monitor secundário
- **Hotkeys globais**: Ciclagem forward/backward, minimize all, toggle cycle group — funcionamento idêntico ao atual
- **Configuração JSON**: Estrutura existente mantida; validação apenas adiciona rejeição de inválidos no startup

### Deliverables & Acceptance Criteria

| ID | Deliverable | Acceptance Criteria (Mensurável) | Prioridade | Auditoria Ref. |
|----|-------------|----------------------------------|------------|----------------|
| **1.1** | Projeto de testes `EveO.Tests` (xUnit + Moq + FluentAssertions) | • Build passa em CI<br>• Coverage mínimo 60% em `ThumbnailManager`, `WindowManager`, `Configuration`<br>• Testes executam em < 30s<br>• **Regression suite**: 7-client cycle, minimize all, layout save/load, config validation | **P0** | #6.5, #7.2 |
| **1.2** | Interface `IThumbnailProvider` + implementação `DwmThumbnailProvider` | • `ThumbnailManager` depende apenas de `IThumbnailProvider`<br>• Mock `TestThumbnailProvider` permite testes unitários sem DWM<br>• Zero breaking changes na API pública<br>• **Regression**: 6-client cycle + 1 intel excluded funciona idêntico | **P0** | #6.1, #6.4, #6.8 |
| **1.3** | `SafeHandle`/`CriticalFinalizerObject` para todos handles Win32/GDI | • `DwmThumbnail` implementa `IDisposable` + `SafeHandle`<br>• `WindowManager.GetStaticThumbnail`: `try/finally` garante `DeleteObject` mesmo em exceção<br>• Teste de estresse: 1000 ciclos activate/minimize sem leak (verified via `dotnet-counters` GDI handles) | **P0** | #6.2 |
| **1.4** | Validação de configuração JSON (FluentValidation) | • Regras em código C# (não JsonSchema) — mais testáveis, flexíveis<br>• Falha de validação retorna `ValidationResult` estruturado (não exception genérica)<br>• Configurações inválidas rejeitadas no startup com mensagem clara<br>• **Regression**: Configs válidas atuais continuam carregando; defaults aplicados se ausentes | **P1** | #6.3 |
| **1.5** | Performance Counters / `System.Diagnostics.Metrics` no `ThumbnailManager` | • Métricas expostas: `thumbnail.refresh.duration`, `thumbnail.count.active`, `dwm.handles.open`, `cpu.usage.percent`, `gdi.handles.open`<br>• Exportador OTLP/Prometheus configurável via `IAppConfig`<br>• Dashboard de referência (Grafana JSON) documentado em `docs/metrics-dashboard.json` | **P1** | #6.1, #8.1 |
| **1.6** | BenchmarkDotNet suite para paths críticos | • Benchmarks: `ThumbnailManager.RefreshCycle`, `CycleNextClient`, `DwmThumbnail.Register/Update`, `WindowManager.Activate`, `WindowManager.GetStaticThumbnail`<br>• Baseline commitado em `benchmarks/baseline-<date>.json`<br>• Regression detection em CI (falha se >10% degrade em qualquer benchmark)<br>• **Hipóteses a validar**: #H1 (CycleNextClient allocs), #H2 (Timer CPU), #H4 (GDI leaks) | **P1** | #8.1, #H1–H6 |
| **1.7** | ADRs (Architecture Decision Records) para decisões-chave | • ADR-001: MVP vs MediatR coexistence<br>• ADR-002: Abstração DWM via `IThumbnailProvider`<br>• ADR-003: JSON config + FluentValidation (não JsonSchema)<br>• ADR-004: **Sem Rx.NET / Reactive Extensions na Fase 1** — só após medição que comprove necessidade (ver #H2)<br>• ADR-005: **Sem DWM pooling na Fase 1** — só após medição que comprove handle limit real (ver #H3)<br>• Armazenados em `docs/adr/` | **P2** | #7.5 |

### Dependências
- 1.1 → 1.2 (testes precisam da abstração para mockar)
- 1.2 → 1.3 (handle cleanup testável via interface)
- 1.3 independente (pode iniciar em paralelo)
- 1.4 independente
- 1.5, 1.6 podem rodar em paralelo após 1.1
- 1.7 no final da fase

### Riscos & Mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| DWM API behavior difere em Wine/Proton | Média | Alto | Testes de integração em container Wine; fallback `StaticThumbnailView` validado |
| FluentValidation adiciona dependência | Baixa | Baixo | Biblioteca madura, zero-runtime-cost, amplamente usada |
| BenchmarkDotNet em single-file publish | Média | Baixo | Benchmarks em projeto separado `EveO.Benchmarks`; não incluídos no publish final |
| Mudança de `IDwmThumbnail` para `IThumbnailProvider` quebra Linux/Wine | Baixa | Médio | `StaticThumbnailProvider` implementa mesma interface; Wine path inalterado |
| Config validation rejeita configs legítimas de usuários existentes | Baixa | Alto | Testes de migration: carregar configs reais de usuários (anonymized) em CI |

---

## Fase 2 — Architecture Cleanup (concluída)

### Objetivo
Separar camadas (Domain/Infrastructure/Presentation), **avaliar com medições** se padrões reativos e virtualização são necessários, e implementar apenas o que dados reais justificarem.

### Princípio Orientador da Fase 2
> **"Measure first, implement second."** Nenhuma das seguintes tecnologias será adotada sem evidência técnica de benchmark/profiling real:
> - Rx.NET / Reactive Extensions / `IAsyncEnumerable` (ver #H2)
> - DWM Thumbnail Pooling (ver #H3)
> - Virtualização de thumbnails (ver #H6)
> - Separação completa de camadas (avaliar custo/benefício vs risco de regressão)

### Deliverables & Acceptance Criteria

| ID | Deliverable | Acceptance Criteria (Mensurável) | Prioridade | Auditoria Ref. |
|----|-------------|----------------------------------|------------|----------------|
| **2.1** ✅ | Separação de camadas: `EveO.Core` (Domain), `EveO.Infrastructure` (Win32/DWM), `EveO.Presentation` (Views) | • `EveO.Core` zero dependências Windows-only (netstandard2.1 ou net8.0)<br>• `ThumbnailManager` → `ThumbnailOrchestrator` em Core, usa `IThumbnailProvider`/`IWindowManager` de Infrastructure<br>• Build `EveO.Core` passa em Linux (`dotnet build --os linux`)<br>• **Regression**: Comportamento 6+1 clientes idêntico | **P1** | #6.8, #6.9 |
| **2.2** ✅ | **Spike técnico (2 dias)**: Avaliar `DispatcherTimer` → Reactive Streams (`IAsyncEnumerable` + `Channel<T>` ou Rx.NET) | • Prototype com `ProcessMonitor` push-based (event-driven process changes)<br>• Medir CPU idle 7 clientes: baseline (timer 500ms) vs push-based<br>• **Gate**: Só prossegue se redução >30% CPU idle comprovada<br>• Documentar decisão no ADR-006 | **P1** | #6.2, #6.7, #H2 |
| **2.3** ✅ | **Spike técnico (2 dias)**: Avaliar virtualização de thumbnails (render apenas visíveis) | • Prototype: `VirtualizingThumbnailPanel` (WPF) ou ocultar DWM thumbnails fora do viewport<br>• Medir memória/CPU 7 clientes: baseline vs virtualizado<br>• **Gate**: Só prossegue se redução >20% memória comprovada<br>• Documentar decisão no ADR-007 | **P1** | #6.8, #5.1, #H6 |
| **2.4** ✅ | **Spike técnico (2 dias)**: Avaliar pool de `DwmThumbnail` (registro/desregistro reutilizado) | • Prototype: `ThumbnailPool : IThumbnailPool` com `Rent()`/`Return()`<br>• Medir: handles DWM abertos, latency refresh, falhas `DwmRegisterThumbnail` sob carga<br>• **Gate**: Só prossegue se limite real de handles DWM comprovado (#H3) OU latency >30ms comprovado<br>• Documentar decisão no ADR-008 | **P2** | #6.2, #6.4, #H3 |
| **2.5** ✅ | Testes de integração Win32 com TestContainers / Docker | • Container Windows Server Core ou Wine para CI<br>• Testes: `DwmThumbnailProvider.Register`, `WindowManager.Activate`, hotkeys globais<br>• Executam em CI (GitHub Actions self-hosted runners Windows) | **P2** | #6.9 |
| **2.6** ✅ | Event-driven config changes (MediatR notifications) | • `ConfigurationChangedNotification` publicado ao salvar<br>• `ThumbnailOrchestrator` reage via handler (não polling)<br>• Hot-reload de `ThumbnailRefreshPeriod`, `ZoomFactor` sem restart<br>• **Regression**: Config save/load funciona idêntico | **P2** | #5.3, #6.7 |
| **2.7** ✅ | Documentação de arquitetura atualizada (C4 + ADRs) | • Diagramas C4: Context, Container, Component, Code<br>• ADR-004 a ADR-008 documentando decisões da Fase 2<br>• `docs/architecture/README.md` com visão geral | **P3** | #7.5 |

### Evidências da Fase 2

| Item | Resultado |
|---|---|
| 2.1 | `EveO.Core` (Domain), `EveO.Infrastructure` (Win32/GDI/DWM), `EveO.Presentation` (Views); ondas #157–#171 |
| 2.2 | `IClientChangeSignal` push-based (#173) + refresh event-driven (#175); ADR-006 |
| 2.3 | **Não implementar** — gate de memória não passou em 7 clientes reais (ADR-007) |
| 2.4 | **Não implementar** — registro/desregistro custa ~0,012 ms, 2.500× abaixo do gate (ADR-008) |
| 2.5 | Testes de falha GDI e de estabilidade rodam no job Windows do CI |
| 2.7 | ADR-004 a ADR-008 + C4 |

### Dependências
- 2.1 → 2.2, 2.3, 2.4 (core abstractions first)
- 2.2, 2.3, 2.4 são **spikes independentes** — podem rodar em paralelo após 2.1
- 2.5 requer 2.1 (Infrastructure isolado)
- 2.6 requer 2.1 + 2.2 (event-driven precisa de abstrações estáveis)
- 2.7 no final da fase

### Riscos & Mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| Spikes mostram que otimizações não valem a pena | Média | Baixo | **Resultado aceitável** — documenta-se no ADR e mantém implementação atual |
| Layer separation quebra single-file publish | Baixa | Médio | Manter `EveO.App` como entry point; testar ILMerge/assembly loading em CI |
| TestContainers Windows em CI Linux | Alta | Alto | Usar runners Windows self-hosted para testes Win32; Linux tests apenas Core |
| MediatR notifications causam loops de eventos | Baixa | Médio | `DisableViewEvents`/`EnableViewEvents` pattern já existe; estender para config changes |

---

## Fase 3 — Cross-Platform & Extensibility (não iniciada)

### Objetivo
Habilitar execução nativa no Linux/macOS via Avalonia/Uno, introduzir plugin architecture para extensibilidade comunitária e implementar event sourcing para auditoria/histórico de layouts.

### Deliverables & Acceptance Criteria

| ID | Deliverable | Acceptance Criteria (Mensurável) | Prioridade | Auditoria Ref. |
|----|-------------|----------------------------------|------------|----------------|
| **3.1** | Proof-of-Concept Avalonia 11 / Uno Platform | • `EveO.Avalonia` project compila e roda no Linux (Wayland + X11)<br>• Thumbnail preview via compositor nativo (não DWM) — ex: `wlr-screencopy` / PipeWire<br>• Feature parity: cycle clients, minimize all, layout save/load | **P2** | #6.13 |
| **3.2** | Abstração de plataforma `IPlatformWindowManager` + `IPlatformThumbnailProvider` | • Windows: `DwmThumbnailProvider`, `Win32WindowManager`<br>• Linux: `PipeWireThumbnailProvider`, `WaylandWindowManager` / `X11WindowManager`<br>• macOS: `ScreenCaptureKitThumbnailProvider`, `CocoaWindowManager` | **P2** | #6.1, #6.8 |
| **3.3** | Plugin Architecture (MEF / System.ComponentModel.Composition ou custom) | • `IPlugin` interface com `Initialize(IServiceCollection)`, `Metadata`<br>• Plugins carregados de `%APPDATA%\EVE-O\Plugins\`<br>• Exemplo plugin: "Discord Rich Presence", "Overlay Custom", "Metrics Exporter"<br>• Sandbox: plugins rodam em `AssemblyLoadContext` isolado | **P2** | #6.15 |
| **3.4** | Event Sourcing para layout/histórico de clientes | • `ClientLayoutEventStore` (EventStoreDB ou SQLite + append-only)<br>• Eventos: `ClientAdded`, `ClientRemoved`, `LayoutSaved`, `CycleGroupChanged`, `FrameSettingsChanged`<br>• Projeções: `CurrentLayoutView`, `ClientSessionHistory`<br>• Time-travel debug: replay eventos para reproduzir estado | **P3** | #6.14 |
| **3.5** | CI/CD Multi-platform (Windows, Linux, macOS) | • Build matrix: win-x64, linux-x64, osx-x64, osx-arm64<br>• Publish: MSIX (Windows), AppImage/Flatpak (Linux), .app/dmg (macOS)<br>• Auto-update via GitHub Releases + custom updater | **P3** | #7.6 |
| **3.6** | Documentação de contribuição + plugin SDK | • `CONTRIBUTING.md`, `PLUGIN_DEVELOPMENT.md`<br>• NuGet package `EveO.Plugin.Sdk`<br>• Template `dotnet new eveo-plugin` | **P3** | #6.15 |

### Dependências
- 3.1 → 3.2 (PoC valida abstrações)
- 3.2 → 3.3 (plugin SDK usa platform abstractions)
- 3.4 independente (pode começar após Fase 2)
- 3.5 requer 3.1, 3.2 funcionando
- 3.6 por último

### Riscos & Mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| Avalonia/Uno não suporta live thumbnail nativo no Linux | Alta | Blocker | Spike técnico de 1 sprint antes de commit; fallback: polling bitmap (como `StaticThumbnailView`) |
| Plugin sandboxing complexo no .NET 8 (AssemblyLoadContext) | Média | Alto | Usar `Microsoft.Extensions.DependencyInjection` + `AssemblyLoadContext` custom; documentar limitações |
| EventStoreDB adiciona dependência operacional | Média | Médio | Opção SQLite append-only para embedded; EventStoreDB apenas para deployments multi-user |
| Feature parity Windows vs Linux/macOS | Alta | Médio | Definir "MVP cross-platform" claramente; aceitar gaps documentados (ex: hotkeys globais no Wayland) |

---

## Rastreabilidade: Auditoria → Roadmap

| Auditoria Finding | Fase(s) | Items |
|-------------------|---------|-------|
| #5.1 Zoom on hover perf | 1.5, 2.3 (spike) | Benchmarks + Virtualization spike |
| #5.2 DispatcherTimer polling | 2.2 (spike) | Reactive streams spike (gate: >30% CPU reduction) |
| #5.3 DWM thumbnail limits | 2.4 (spike) | Thumbnail pool spike (gate: handle limit comprovado) |
| #6.1 Strong coupling DWM | 1.2, 2.1, 3.2 | IThumbnailProvider + Layer separation |
| #6.2 Win32 handle leaks | 1.3, 2.4 (spike) | SafeHandle + Pool spike |
| #6.3 JSON config no schema | 1.4 | FluentValidation |
| #6.4 MVP + MediatR mixed | 2.1, 2.6 | Layer separation + Event-driven |
| #6.5 No automated tests | 1.1, 1.5, 2.5 | Test project + Benchmarks + Integration |
| #6.6 Single-file debug | 2.1 | Layered build (Core testable) |
| #6.7 Reactive patterns | 2.2 (spike), 2.6 | Spike first, then event-driven config |
| #6.8 Virtualization | 2.3 (spike) | Virtualization spike (gate: >20% memory reduction) |
| #6.9 TestContainers | 2.5 | Windows/Wine container tests |
| #6.13 Avalonia/Uno | 3.1, 3.2 | Cross-platform PoC |
| #6.14 Event Sourcing | 3.4 | ClientLayoutEventStore |
| #6.15 Plugin Architecture | 3.3, 3.6 | IPlugin + SDK |
| #7.1–#7.6 Immediate next steps | 1.1–1.7 | All Phase 1 items |
| #H1 CycleNextClient allocs | 1.5 | BenchmarkDotNet CycleNextClient |
| #H2 Timer CPU idle | 2.2 (spike) | Reactive streams spike |
| #H3 DWM handle limit | 2.4 (spike) | Pool spike |
| #H4 GDI leaks | 1.3, 1.5 | SafeHandle + Benchmark GetStaticThumbnail |
| #H5 SPI_SETANIMATION | 1.3 | WindowManager cleanup |
| #H6 Virtualization benefit | 2.3 (spike) | Virtualization spike |

---

## Métricas de Sucesso por Fase

| Métrica | Baseline (Fase 0) | Fase 1 Target | Fase 2 Target | Fase 3 Target |
|---------|-------------------|---------------|---------------|---------------|
| Startup time | ~2–3s | <2s | <1.5s | <1s (AOT) |
| CPU idle (7 clientes) | ~3–5% | <2% | **<1%** | <1% |
| Memória (7 clientes) | ~150–200MB | <150MB | **<100MB** | <80MB |
| Refresh latency | ~50–100ms | <50ms | **<30ms** | <20ms |
| DWM handles abertos | 7+ | 7 (pool) | **Pool size** | N/A (abstração) |
| Test coverage | 0% | 60% | 80% | 85% |
| Platforms suportados | Windows | Windows | Windows | Win/Linux/macOS |

---

## Governança

- **Review cadence**: Fim de cada sprint (Phase 1–2), mensal (Phase 3)
- **Gate criteria**: Todos P0/P1 "Done" + métricas validadas com profiling real
- **Rollback plan**: Branch `main` protegido; feature flags para novas abstrações
- **Stakeholders**: Tech Lead (arquitetura), Maintainer (release), Community (plugins)

---

*Documento vivo — atualizar a cada sprint review. Próxima revisão: fim da Fase 1.*