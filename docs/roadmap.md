# EVE-O Custom - Roadmap de Arquitetura

*Baseado na [Auditoria de Arquitetura](auditoria-architect.md) — gerado em 2026-11-10*

---

## Resumo Executivo

| Fase | Nome | Objetivo | Duração Estimada | Prioridade Geral |
|------|------|----------|------------------|------------------|
| **0** | **Foundations** (atual) | Auditoria completa, baseline estabelecido | Concluído | — |
| **1** | **Stabilization** | Correções críticas, infraestrutura de testes, camadas de abstração | 1–2 sprints | **P0/P1** |
| **2** | **Architecture Cleanup** | Separação de camadas, virtualização, padrões reativos | 3–5 sprints | **P1/P2** |
| **3** | **Cross-Platform & Extensibility** | Avalonia/Uno, plugin architecture, event sourcing | 6+ sprints | **P2/P3** |

> **Legenda de Prioridade**: P0 = Blocker (impede progresso), P1 = Crítico (risco alto), P2 = Importante (dívida técnica), P3 = Nice-to-have (melhoria futura)

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

## Fase 1 — Stabilization (1–2 Sprints)

### Objetivo
Eliminar blockers de produção, estabelecer infraestrutura de testes automatizados e introduzir abstrações para desacoplar DWM/Win32 do núcleo da aplicação.

### Deliverables & Acceptance Criteria

| ID | Deliverable | Acceptance Criteria (Mensurável) | Prioridade | Auditoria Ref. |
|----|-------------|----------------------------------|------------|----------------|
| **1.1** | Projeto de testes `EveO.Tests` (xUnit + Moq + FluentAssertions) | • Build passa em CI<br>• Coverage mínimo 60% em `ThumbnailManager`, `WindowManager`, `Configuration`<br>• Testes executam em < 30s | **P0** | #7.2 |
| **1.2** | Interface `IThumbnailProvider` + implementação `DwmThumbnailProvider` | • `ThumbnailManager` depende apenas de `IThumbnailProvider`<br>• Mock `TestThumbnailProvider` permite testes unitários sem DWM<br>• Zero breaking changes na API pública | **P0** | #6.1, #6.4, #6.8 |
| **1.3** | Validação de configuração JSON (JsonSchema ou FluentValidation) | • Schema JSON versionado em `docs/config-schema.json`<br>• Falha de validação retorna erros estruturados (não exception genérica)<br>• Configurações inválidas rejeitadas no startup | **P1** | #6.3 |
| **1.4** | Performance Counters / `System.Diagnostics.Metrics` no `ThumbnailManager` | • Métricas expostas: `thumbnail.refresh.duration`, `thumbnail.count.active`, `dwm.handles.open`, `cpu.usage.percent`<br>• Exportador OTLP/Prometheus configurável<br>• Dashboard de referência (Grafana/JSON) documentado | **P1** | #6.1, #8.1 |
| **1.5** | BenchmarkDotNet suite para paths críticos | • Benchmarks: `ThumbnailManager.RefreshCycle`, `DwmThumbnail.Register/Update`, `WindowManager.Activate`<br>• Baseline commitado em `benchmarks/baseline-<date>.json`<br>• Regression detection em CI (falha se >10% degrade) | **P1** | #8.1 |
| **1.6** | Handle leak detection & cleanup em `WindowManager` | • `SafeHandle`/`CriticalFinalizerObject` para todos handles Win32<br>• Teste de estresse: 1000 ciclos activate/minimize sem leak (verified via `dotnet-counters`)<br>• `IDisposable` implementado corretamente em `DwmThumbnail` | **P1** | #6.2 |
| **1.7** | ADRs (Architecture Decision Records) para decisões-chave | • ADR-001: MVP vs MediatR coexistence<br>• ADR-002: Abstração DWM via `IThumbnailProvider`<br>• ADR-003: JSON config + JsonSchema validation<br>• Armazenados em `docs/adr/` | **P2** | #7.5 |

### Dependências
- 1.1 → 1.2 (testes precisam da abstração para mockar)
- 1.2 → 1.6 (handle cleanup testável via interface)
- 1.3 independente
- 1.4, 1.5 podem rodar em paralelo após 1.1

### Riscos & Mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| DWM API behavior difere em Wine/Proton | Média | Alto | Testes de integração em container Wine; fallback `StaticThumbnailView` validado |
| JsonSchema adiciona dependência/rigidez | Baixa | Médio | Usar FluentValidation com regras em código (mais flexível, testável) |
| BenchmarkDotNet em single-file publish | Média | Baixo | Benchmarks em projeto separado; não incluídos no publish final |

---

## Fase 2 — Architecture Cleanup (3–5 Sprints)

### Objetivo
Separar camadas (Domain/Infrastructure/Presentation), substituir polling por padrões reativos e implementar virtualização de thumbnails para suportar 7+ clientes com CPU <1% idle.

### Deliverables & Acceptance Criteria

| ID | Deliverable | Acceptance Criteria (Mensurável) | Prioridade | Auditoria Ref. |
|----|-------------|----------------------------------|------------|----------------|
| **2.1** | Separação de camadas: `EveO.Core` (Domain), `EveO.Infrastructure` (Win32/DWM), `EveO.Presentation` (Views) | • `EveO.Core` zero dependências Windows-only (netstandard2.1 ou net8.0)<br>• `ThumbnailManager` → `ThumbnailOrchestrator` em Core, usa `IThumbnailProvider`/`IWindowManager` de Infrastructure<br>• Build `EveO.Core` passa em Linux (dotnet build --os linux) | **P1** | #6.8, #6.9 |
| **2.2** | Migração `DispatcherTimer` → Reactive Extensions (Rx.NET) / `IAsyncEnumerable` | • `ThumbnailRefreshStream : IObservable<RefreshTick>` substitui timer<br>• Backpressure: `Buffer`/`Throttle` configurável via `IAppConfig`<br>• CPU idle 7 clientes < 1% (target Tabela 8) | **P1** | #6.2, #6.7 |
| **2.3** | Virtualização de thumbnails (render apenas visíveis) | • `VirtualizingThumbnailPanel` (WPF) ou `VirtualizingStackPanel` custom<br>• Apenas N thumbnails visíveis registram DWM (N = viewport rows × cols)<br>• Memória 7 clientes < 100MB (target Tabela 8) | **P1** | #6.8, #5.1 |
| **2.4** | Pool de `DwmThumbnail` (registro/desregistro reutilizado) | • `ThumbnailPool : IThumbnailPool` com `Rent()`/`Return()`<br>• Handles DWM abertos = pool size (configurável, default 7)<br>• Latency refresh < 30ms (target Tabela 8) | **P2** | #6.2, #6.4 |
| **2.5** | Testes de integração Win32 com TestContainers / Docker | • Container Windows Server Core ou Wine para CI<br>• Testes: `DwmThumbnailProvider.Register`, `WindowManager.Activate`, hotkeys globais<br>• Executam em CI (GitHub Actions self-hosted ou Azure DevOps) | **P2** | #6.9 |
| **2.6** | Event-driven config changes (MediatR notifications) | • `ConfigurationChangedNotification` publicado ao salvar<br>• `ThumbnailOrchestrator` reage via handler (não polling)<br>• Hot-reload de `ThumbnailRefreshPeriod`, `ZoomFactor` sem restart | **P2** | #5.3, #6.7 |
| **2.7** | Documentação de arquitetura atualizada (C4 + ADRs) | • Diagramas C4: Context, Container, Component, Code<br>• ADR-004: Layer separation<br>• ADR-005: Reactive streams over polling<br>• ADR-006: Thumbnail virtualization strategy | **P3** | #7.5 |

### Dependências
- 2.1 → 2.2, 2.3, 2.4 (core abstractions first)
- 2.2 → 2.6 (reactive stream enables event-driven config)
- 2.3, 2.4 podem rodar em paralelo após 2.1
- 2.5 requer 2.1 (Infrastructure isolado)
- 2.7 no final da fase

### Riscos & Mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| Rx.NET adiciona complexidade/curva de aprendizado | Média | Médio | Spike de 2 dias; alternativa `IAsyncEnumerable` + `Channel<T>` se Rx muito pesado |
| Virtualização WPF pode quebrar DWM thumbnail positioning | Alta | Alto | Prototype early; fallback: manter DWM ativo mas ocultar visual (opacity=0) |
| Layer separation quebra single-file publish | Baixa | Médio | Manter `EveO.App` como entry point; ILMerge/assembly loading testado em CI |
| TestContainers Windows em CI Linux | Alta | Alto | Usar runners Windows self-hosted para testes Win32; Linux tests apenas Core |

---

## Fase 3 — Cross-Platform & Extensibility (6+ Sprints)

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
| #5.1 Zoom on hover perf | 1.4, 2.3 | Metrics + Virtualization |
| #5.2 DispatcherTimer polling | 2.2 | Reactive streams |
| #5.3 DWM thumbnail limits | 2.4 | Thumbnail pool |
| #6.1 Strong coupling DWM | 1.2, 2.1, 3.2 | IThumbnailProvider + Layer separation |
| #6.2 Win32 handle leaks | 1.6, 2.4 | SafeHandle + Pool |
| #6.3 JSON config no schema | 1.3 | FluentValidation/JsonSchema |
| #6.4 MVP + MediatR mixed | 2.1, 2.6 | Layer separation + Event-driven |
| #6.5 No automated tests | 1.1, 1.5, 2.5 | Test project + Benchmarks + Integration |
| #6.6 Single-file debug | 2.1 | Layered build (Core testable) |
| #6.7 Reactive patterns | 2.2, 2.6 | Rx.NET / IAsyncEnumerable |
| #6.8 Virtualization | 2.3 | VirtualizingThumbnailPanel |
| #6.9 TestContainers | 2.5 | Windows/Wine container tests |
| #6.13 Avalonia/Uno | 3.1, 3.2 | Cross-platform PoC |
| #6.14 Event Sourcing | 3.4 | ClientLayoutEventStore |
| #6.15 Plugin Architecture | 3.3, 3.6 | IPlugin + SDK |
| #7.1–#7.6 Immediate next steps | 1.1–1.7 | All Phase 1 items |

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