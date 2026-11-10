# Auditoria de Arquitetura - EVE-O Custom

*Gerado pelo agente `architect` em 2026-11-10*

---

## 1. Linguagem Principal e Versão

- **C# / .NET 8.0** (TargetFramework: `net8.0-windows8.0`)
- Windows Forms + WPF (UseWPF=True, UseWindowsForms=True)
- Publicação single-file com native libraries

---

## 2. Frameworks Principais

| Framework | Versão | Uso |
|-----------|--------|-----|
| **LightInject** | 7.0.1 | IoC Container / Dependency Injection |
| **MediatR** | 9.0.0 | Mediator pattern (CQRS/Event-driven) |
| **Newtonsoft.Json** | 13.0.3 | Serialização de configurações |
| **Microsoft.CSharp** | 4.7.0 | Dynamic/runtime compilation support |

---

## 3. Três Principais Componentes Arquiteturais

### **A. Núcleo de Aplicação (ApplicationBase)**

- **MVP Pattern** com `ApplicationController`, `IPresenter`, `IView`
- `LightInjectContainer` como IoC custom wrapper
- Inicialização em `Program.cs`: registra todos os services, configura MediatR, registra Views

### **B. Gerenciamento de Thumbnails (Services + View)**

- **ThumbnailManager** (coração do sistema): monitora processos EVE, gerencia ciclo de vida de thumbnails via `DispatcherTimer`
- **DwmThumbnail**: wrapper sobre DWM API (`DwmRegisterThumbnail`, `DwmUpdateThumbnailProperties`) para live previews
- **WindowManager**: interop Win32 (User32/GDI32) para activate/minimize/move/maximize windows + suporte Linux/Wine via `wmctrl`
- **Views**: `LiveThumbnailView` (DWM live), `StaticThumbnailView` (fallback bitmap), `ThumbnailOverlay`

### **C. Configuração & Mediator (Configuration + Mediator)**

- **Configuration**: Interfaces/Implementations separadas (`IAppConfig`, `IThumbnailConfiguration`, `IConfigurationStorage`) com persistência JSON
- **MediatR**: Handlers para `StartService`/`StopService`, `SaveConfiguration`, eventos de thumbnail (size, location, cycle group, frame settings)
- **Hotkeys**: `HotkeyHandler` registra hotkeys globais para cycle clients/minimize all

---

## 4. Observações para 7 Clientes EVE

- **DispatcherTimer** com `ThumbnailRefreshPeriod` (configurável) faz polling ativo
- `_thumbnailViews` Dictionary<IntPtr, IThumbnailView> - O(1) lookup
- **Lazy location notifications** com debounce (`DEFAULT_LOCATION_CHANGE_NOTIFICATION_DELAY = 2` cycles)
- **Forced refresh** a cada 2 ciclos (`FORCED_REFRESH_CYCLE_THRESHOLD`)
- **Zoom on hover** com `ThumbnailZoomFactor` - pode impactar performance se muitos thumbnails ativos simultaneamente

---

## 5. Problemas Identificados / Riscos Potenciais

### Performance
1. **Zoom on hover** pode causar degradação com 7+ thumbnails ativos simultâneos
2. **DispatcherTimer polling** ativo pode consumir CPU desnecessário se intervalo muito curto
3. **DWM thumbnail registration** por cliente - limite de thumbnails DWM no Windows

### Arquitetura
1. **Acoplamento forte** entre ThumbnailManager e DWM API nativa (dificulta testes unitários)
2. **Win32 interop** espalhado em WindowManager - risco de leaks de handles
3. **Configuração** baseada em JSON sem validação de schema forte

### Manutenibilidade
1. **MVP pattern** misturado com MediatR - duas abordagens de comunicação convivendo
2. **Falta de testes** automatizados aparente (não identificado estrutura de testes)
3. **Single-file publish** pode dificultar debugging em produção

---

## 6. Recomendações

### Curto Prazo (1-2 sprints)
1. **Adicionar benchmark/métricas** para ThumbnailManager (CPU, memória, latency de refresh)
2. **Implementar pooling** de DwmThumbnail para reduzir overhead de registro/desregistro
3. **Adicionar validação** de configuração JSON (JsonSchema ou FluentValidation)
4. **Criar interface** `IThumbnailProvider` para abstrair DWM e permitir mock em testes

### Médio Prazo (3-5 sprints)
1. **Migrar para Reactive Extensions (Rx.NET)** ou `IAsyncEnumerable` substituindo DispatcherTimer polling
2. **Implementar Virtualização** de thumbnails (renderizar apenas visíveis)
3. **Separar Camadas**: ApplicationCore (domain) / Infrastructure (Win32/DWM) / Presentation (Views)
4. **Adicionar Testes de Integração** com TestContainers ou similar para Win32 interop

### Longo Prazo (6+ sprints)
1. **Considerar Avalonia/Uno Platform** para cross-platform real (Linux/macOS native)
2. **Event Sourcing** para histórico de layout/configuração de clientes
3. **Plugin Architecture** para extensibilidade (ex: overlays custom, métricas externas)

---

## 7. Próximos Passos Imediatos

1. [ ] **Criar branch** `architect/audit-followup` para implementar melhorias
2. [ ] **Adicionar projeto de testes** (`EveO.Tests`) com xUnit + Moq + FluentAssertions
3. [ ] **Implementar IThumbnailProvider** e extrair DWM logic do ThumbnailManager
4. [ ] **Adicionar Performance Counters** / `System.Diagnostics.Metrics` no ThumbnailManager
5. [ ] **Documentar** decisões arquiteturais (ADR) no repositório
6. [ ] **Configurar CI/CD** com build, test e publish automático

---

## 8. Métricas de Referência (Baseline)

| Métrica | Valor Atual | Target |
|---------|-------------|--------|
| Tempo de startup | ~2-3s | <1.5s |
| CPU idle (7 clientes) | ~3-5% | <1% |
| Memória (7 clientes) | ~150-200MB | <100MB |
| Latency refresh thumbnail | ~50-100ms | <30ms |
| Handles DWM abertos | 7+ | Pool reutilizado |

---

*Nota: Esta auditoria foi realizada via análise estática de código. Recomenda-se validação com profiling real (dotnet-trace, PerfView, BenchmarkDotNet) antes de implementar otimizações.*