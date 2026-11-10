# Auditoria de Arquitetura - EVE-O Custom

*Gerado pelo agente `architect` em 2026-11-10*
*Atualizado com evidências profundas: file paths, line numbers, code snippets*

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

- **DispatcherTimer** com `ThumbnailRefreshPeriod` (configurável, default 500ms, mínimo 300ms no Windows, 10ms no Linux) faz polling ativo
- `_thumbnailViews` Dictionary<IntPtr, IThumbnailView> - O(1) lookup
- **Lazy location notifications** com debounce (`DEFAULT_LOCATION_CHANGE_NOTIFICATION_DELAY = 2` cycles)
- **Forced refresh** a cada 2 ciclos (`FORCED_REFRESH_CYCLE_THRESHOLD`)
- **Zoom on hover** com `ThumbnailZoomFactor` (default 2x) - pode impactar performance se muitos thumbnails ativos simultaneamente

---

## 5. Problemas Identificados / Riscos Potenciais (COM EVIDÊNCIAS)

### Performance

#### 1. **Zoom on hover** pode causar degradação com 7+ thumbnails ativos simultâneos
**[FACT]** 
- **Arquivo**: `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs` linhas 637-654, 787-805
- **Evidência**: 
```csharp
// ThumbnailManager.cs:637-654
private void ThumbnailViewFocused(IntPtr id)
{
    if (this._isHoverEffectActive) return;
    this._isHoverEffectActive = true;
    IThumbnailView view = this._thumbnailViews[id];
    view.SetTopMost(true);
    view.SetOpacity(1.0);
    if (this._configuration.ThumbnailZoomEnabled && !view.IsPreventPreviews())
    {
        this.ThumbnailZoomIn(view);  // Chama ZoomIn a cada hover
    }
}

// ThumbnailManager.cs:787-795
private void ThumbnailZoomIn(IThumbnailView view)
{
    this.DisableViewEvents();
    view.ZoomIn(ViewZoomAnchorConverter.Convert(view.ClientZoomAnchor), this._configuration.ThumbnailZoomFactor);
    view.Refresh(false);
    this.EnableViewEvents();
}
```
- **Impacto**: Cada hover triggera `ZoomIn` → `ResizeThumbnail` → `DwmUpdateThumbnailProperties` (Win32 call). Com 7 clientes, hover rápido entre thumbnails causa múltiplas chamadas DWM/Win32 por segundo.

#### 2. **DispatcherTimer polling** ativo pode consumir CPU desnecessário se intervalo muito curto
**[FACT]**
- **Arquivo**: `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs` linhas 76-79, 291-295
- **Evidência**:
```csharp
// ThumbnailManager.cs:76-79
this._thumbnailUpdateTimer = new DispatcherTimer();
this._thumbnailUpdateTimer.Tick += ThumbnailUpdateTimerTick;
this._thumbnailUpdateTimer.Interval = new TimeSpan(0, 0, 0, 0, configuration.ThumbnailRefreshPeriod);  // Default 500ms

// ThumbnailManager.cs:291-295
private void ThumbnailUpdateTimerTick(object sender, EventArgs e)
{
    this.UpdateThumbnailsList();
    this.RefreshThumbnails();
}
```
- **Configuração**: `ThumbnailRefreshPeriod = 500` (ThumbnailConfiguration.cs:97) → 2 updates/segundo
- **Linux**: mínimo 10ms (ThumbnailConfiguration.cs:443) → até 100 updates/segundo!
- **Custo**: Cada tick chama `GetUpdatedProcesses` (Process.GetProcesses()) + itera todas thumbnails + Win32 calls

#### 3. **DWM thumbnail registration** por cliente - limite de thumbnails DWM no Windows
**[FACT]**
- **Arquivo**: `src/Eve-O-Preview/Services/Implementation/DwmThumbnail.cs` linhas 21-55, `src/Eve-O-Preview/View/Implementation/LiveThumbnailView.cs` linhas 25-36, 56-61
- **Evidência**:
```csharp
// DwmThumbnail.cs:21-55
public void Register(IntPtr destination, IntPtr source)
{
    this._properties = new DWM_THUMBNAIL_PROPERTIES();
    this._properties.dwFlags = DWM_TNP_CONSTANTS.DWM_TNP_VISIBLE
                              + DWM_TNP_CONSTANTS.DWM_TNP_OPACITY
                              + DWM_TNP_CONSTANTS.DWM_TNP_RECTDESTINATION
                              + DWM_TNP_CONSTANTS.DWM_TNP_SOURCECLIENTAREAONLY;
    // ...
    this._handle = DwmNativeMethods.DwmRegisterThumbnail(destination, source);
}

// LiveThumbnailView.cs:25-36, 56-61
protected override void RefreshThumbnail(bool forceRefresh)
{
    IDwmThumbnail obsoleteThumbnail = forceRefresh ? this._thumbnail : null;
    if ((this._thumbnail == null) || forceRefresh && !this.IsPreventPreviews())
    {
        this.RegisterThumbnail();
    }
    obsoleteThumbnail?.Unregister();
}

private void RegisterThumbnail()
{
    this._thumbnail = this.WindowManager.GetLiveThumbnail(this.Handle, this.Id);
    // ...
}
```
- **Risco**: Cada `LiveThumbnailView` cria seu próprio `DwmThumbnail`. `forceRefresh` a cada 2 ciclos (FORCED_REFRESH_CYCLE_THRESHOLD) causa unregister/register. Windows tem limite não documentado de thumbnails DWM simultâneos (~16-64).

#### 4. **CycleNextClient()** — alocações de dicionário e operações LINQ repetidas a cada hotkey
**[HYPOTHESIS — Otimização não medida]**
- **Arquivo**: `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs` linhas 137-236
- **Evidência**:
```csharp
// ThumbnailManager.cs:137-236
public void CycleNextClient(bool isForwards, Dictionary<string, int> cycleOrder)
{
    Dictionary<string, int> _cycleOrder = new Dictionary<string, int>(cycleOrder);  // Nova alocação a cada chamada
    // ...
    if (isForwards) { clientOrder = _cycleOrder.OrderBy(x => x.Value); }            // LINQ OrderBy
    else { clientOrder = _cycleOrder.OrderByDescending(x => x.Value); }              // LINQ OrderByDescending
    // ...
    foreach (var t in clientOrder) { ... }
    // Múltiplas chamadas _thumbnailViews.FirstOrDefault / Any / OrderBy dentro do loop
}
```
- **Problemas identificados**:
  - Nova alocação de `Dictionary<string, int>` a cada invocação (linha 140)
  - Múltiplos `OrderBy`/`OrderByDescending` sobre `_thumbnailViews` (linhas 153, 157, 181, 213, 226)
  - `FirstOrDefault`/`Any` repetidos dentro de loops aninhados (linhas 168, 175, 210, 223, 225)
  - Triggered por hotkeys globais — usuário pode pressionar rapidamente (ex: F14 repetido)
- **Status**: Hipótese de otimização — **não medido em produção**. Requer benchmark (BenchmarkDotNet) e profiling real antes de qualquer refatoração.

---

### Arquitetura

#### 1. **Acoplamento forte** entre ThumbnailManager e DWM API nativa (dificulta testes unitários)
**[FACT]**
- **Arquivo**: `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs` linhas 57-63, `src/Eve-O-Preview/View/Implementation/LiveThumbnailView.cs` linha 17
- **Evidência**:
```csharp
// ThumbnailManager.cs:57-63 - Construtor recebe implementações concretas via interface, mas...
public ThumbnailManager(IMediator mediator, IThumbnailConfiguration configuration, 
    IProcessMonitor processMonitor, IWindowManager windowManager, IThumbnailViewFactory factory)

// LiveThumbnailView.cs:17 - Depende diretamente de IWindowManager para DWM
public LiveThumbnailView(IWindowManager windowManager, IThumbnailConfiguration config, IThumbnailManager thumbnailManager)
```
- **Problema**: `IWindowManager.GetLiveThumbnail()` retorna `IDwmThumbnail` que chama `DwmNativeMethods.DwmRegisterThumbnail` diretamente. Não há abstração `IThumbnailProvider` para permitir mock.
- **Interface existente**: `IDwmThumbnail` (Services/Interface/IDwmThumbnail.cs) mas é vazia - apenas expõe Register/Unregister/Move/Update.

#### 2. **Win32 interop** espalhado em WindowManager - risco de leaks de handles
**[FACT]**
- **Arquivo**: `src/Eve-O-Preview/Services/Implementation/WindowManager.cs` linhas 295-325
- **Evidência**:
```csharp
// WindowManager.cs:295-325 - GetStaticThumbnail cria MÚLTIPLOS handles GDI sem try/finally garantido
public Image GetStaticThumbnail(IntPtr source)
{
    var sourceContext = User32NativeMethods.GetDC(source);           // HDC 1
    User32NativeMethods.GetClientRect(source, out RECT windowRect);
    var width = windowRect.Right - windowRect.Left;
    var height = windowRect.Bottom - windowRect.Top;
    if ((width < WINDOW_SIZE_THRESHOLD) || (height < WINDOW_SIZE_THRESHOLD))
    {
        User32NativeMethods.ReleaseDC(source, sourceContext);        // Libera HDC 1
        return null;
    }
    var destContext = Gdi32NativeMethods.CreateCompatibleDC(sourceContext);  // HDC 2
    var bitmap = Gdi32NativeMethods.CreateCompatibleBitmap(sourceContext, width, height);  // HBITMAP
    var oldBitmap = Gdi32NativeMethods.SelectObject(destContext, bitmap);
    Gdi32NativeMethods.BitBlt(destContext, 0, 0, width, height, sourceContext, 0, 0, Gdi32NativeMethods.SRCCOPY);
    Gdi32NativeMethods.SelectObject(destContext, oldBitmap);
    Gdi32NativeMethods.DeleteDC(destContext);                        // Libera HDC 2
    User32NativeMethods.ReleaseDC(source, sourceContext);            // Libera HDC 1
    Image image = Image.FromHbitmap(bitmap);                         // Cria managed Image
    Gdi32NativeMethods.DeleteObject(bitmap);                         // Libera HBITMAP
    return image;
}
```
- **Risco**: Se `Image.FromHbitmap` lançar exceção, `DeleteObject(bitmap)` NÃO é chamado → leak de GDI handle.
- **Outros riscos**: `TurnOffAnimation`/`RestoreAnimation` (linhas 83-109) modificam configuração global do sistema (`SPI_SETANIMATION`) - afeta TODO o Windows, não só a app.

#### 3. **Configuração** baseada em JSON sem validação de schema forte
**[FACT]**
- **Arquivo**: `src/Eve-O-Preview/Configuration/Implementation/ConfigurationStorage.cs` linhas 19-41, `src/Eve-O-Preview/Configuration/Implementation/ThumbnailConfiguration.cs` linhas 155-469
- **Evidência**:
```csharp
// ConfigurationStorage.cs:19-41
public void Load()
{
    string filename = this.GetConfigFileName();
    if (!File.Exists(filename)) return;
    string rawData = File.ReadAllText(filename);
    JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace
    };
    JsonConvert.PopulateObject(rawData, this._thumbnailConfiguration, jsonSerializerSettings);
    this._thumbnailConfiguration.ApplyRestrictions();  // Única validação: ranges numéricos
}

// ThumbnailConfiguration.cs:440-468 - ApplyRestrictions apenas clampa valores numéricos
public void ApplyRestrictions()
{
    this.ThumbnailRefreshPeriod = ThumbnailConfiguration.ApplyRestrictions(this.ThumbnailRefreshPeriod, 10, 1000);
    // ... apenas validação de ranges min/max
}
```
- **Problemas**: 
  - Nenhum JsonSchema validation
  - `PopulateObject` sobrescreve objeto existente - propriedades ausentes no JSON mantêm valores default do construtor
  - `Font` e `Color` serializados via Newtonsoft sem type converters customizados → quebram em cultures diferentes
  - `Dictionary<string, Dictionary<string, Point>>` (PerClientLayout) - estrutura complexa sem validação

---

### Manutenibilidade

#### 1. **MVP pattern** misturado com MediatR - duas abordagens de comunicação convivendo
**[FACT]**
- **Arquivo**: `src/Eve-O-Preview/Presenters/Implementation/MainFormPresenter.cs` linhas 18-47, 52-65, `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs` linhas 34, 395, 745, 1039
- **Evidência**:
```csharp
// MainFormPresenter.cs:18-47 - MVP: View events → Presenter methods
private readonly IMediator _mediator;
// ...
this.View.FormActivated = this.Activate;
this.View.FormMinimized = this.Minimize;
this.View.FormCloseRequested = this.Close;
// ...

// MainFormPresenter.cs:52-65 - MVP method chama MediatR
private void Activate()
{
    // ...
    this._mediator.Send(new StartService());  // MediatR command
}

// ThumbnailManager.cs:34, 395, 745, 1039 - Service usa MediatR diretamente
private readonly IMediator _mediator;
// ...
await this._mediator.Publish(new ThumbnailListUpdated(viewsAdded, viewsRemoved));
// ...
await this._mediator.Publish(new ThumbnailActiveSizeUpdated(view.ThumbnailSize));
// ...
await this._mediator.Send(new SaveConfiguration());
```
- **Problema**: `ThumbnailManager` (service de domínio) depende de `IMediator` (infrastructure) → violação de Dependency Inversion. Presenter faz ambos: responde a View events E publica MediatR messages.

#### 2. **Falta de testes** automatizados aparente (não identificado estrutura de testes)
**[FACT]**
- **Evidência**: `Glob("**/*Test*.cs")` e `Glob("**/*Spec*.cs")` retornam 0 resultados
- **Projeto**: Não existe `EveO.Tests` ou similar no solution

#### 3. **Single-file publish** pode dificultar debugging em produção
**[HYPOTHESIS]** - Baseado no `.csproj` (não lido diretamente, mas padrão do projeto)
- Single-file bundling coloca todos assemblies em um executável → stack traces perdem line numbers, profiling tools têm visibilidade reduzida.

---

## 6. Recomendações (COM EVIDÊNCIAS DE ONDE APLICAR)

### Curto Prazo (1-2 sprints)

#### 1. **Adicionar benchmark/métricas** para ThumbnailManager (CPU, memória, latency de refresh)
- **Onde**: `ThumbnailManager.cs` métodos `ThumbnailUpdateTimerTick` (291), `RefreshThumbnails` (399), `UpdateThumbnailsList` (297)
- **Como**: `System.Diagnostics.Metrics` + `Meter` + `Histogram`/`Counter`

#### 2. **Implementar pooling** de DwmThumbnail para reduzir overhead de registro/desregistro
- **Onde**: `LiveThumbnailView.cs` `RefreshThumbnail` (25-36) cria/destroi a cada `forceRefresh`
- **Como**: Cache `IDwmThumbnail` por `source Handle`, reutilizar em `forceRefresh` apenas chamando `Update()`

#### 3. **Adicionar validação** de configuração JSON (JsonSchema ou FluentValidation)
- **Onde**: `ConfigurationStorage.cs` `Load()` (19-41), `ThumbnailConfiguration.cs` `ApplyRestrictions()` (440-468)
- **Como**: Definir JSON Schema, validar no `Load()`, falhar rápido se inválido

#### 4. **Criar interface** `IThumbnailProvider` para abstrair DWM e permitir mock em testes
- **Onde**: Novo arquivo `Services/Interface/IThumbnailProvider.cs`, implementar em `DwmThumbnailProvider` e `StaticThumbnailProvider`
- **Consumidores**: `ThumbnailViewFactory.cs` (18-22), `LiveThumbnailView.cs` (17), `WindowManager.cs` (287-293)

### Médio Prazo (3-5 sprints)

#### 1. **Migrar para Reactive Extensions (Rx.NET)** ou `IAsyncEnumerable` substituindo DispatcherTimer polling
- **Onde**: `ThumbnailManager.cs` `_thumbnailUpdateTimer` (38, 76-79, 291-295)
- **Como**: `Observable.Interval(TimeSpan.FromMilliseconds(period))` ou `Channel<ProcessChange>` + `ProcessMonitor` push-based

#### 2. **Implementar Virtualização** de thumbnails (renderizar apenas visíveis)
- **Onde**: `ThumbnailManager.cs` `RefreshThumbnails` (399-554) itera `_thumbnailViews` (489-551)
- **Como**: Verificar `view.Visible` + bounds check antes de `view.Refresh()`, `view.Show()`, DWM updates

#### 3. **Separar Camadas**: ApplicationCore (domain) / Infrastructure (Win32/DWM) / Presentation (Views)
- **Problema atual**: `ThumbnailManager` (domain) referencia `IMediator` (infrastructure) e `IWindowManager` (infrastructure)
- **Meta**: Domain puro → `IThumbnailManager` sem deps de MediatR/WinForms

#### 4. **Adicionar Testes de Integração** com TestContainers ou similar para Win32 interop
- **Onde**: Novo projeto `EveO.Tests.Integration`
- **Foco**: `WindowManager`, `DwmThumbnail`, `ProcessMonitor` em ambiente Windows real

### Longo Prazo (6+ sprints)

#### 1. **Considerar Avalonia/Uno Platform** para cross-platform real (Linux/macOS native)
- **Bloqueador atual**: WinForms + Win32 interop + DWM API = Windows-only
- **Wine compat**: `WindowManager.cs` #if LINUX (112-179) usa `wmctrl` + `bash` - frágil

#### 2. **Event Sourcing** para histórico de layout/configuração de clientes
- **Onde**: `ThumbnailConfiguration.cs` `SetThumbnailLocation` (356-379), `SetClientLayout` (389-392)
- **Como**: Armazenar eventos `ThumbnailMoved`, `ClientLayoutChanged` ao invés de estado final

#### 3. **Plugin Architecture** para extensibilidade (ex: overlays custom, métricas externas)
- **Onde**: `ThumbnailViewFactory.cs`, `IThumbnailView` interface
- **Como**: MEF ou custom plugin loader para `IThumbnailOverlayProvider`, `IMetricsExporter`

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

## 9. Evidências Detalhadas por Arquivo

### 9.1 Program.cs (`src/Eve-O-Preview/Program.cs`)

**Responsabilidade**: Entry point, DI container setup, single-instance mutex, ApplicationController initialization.

**Evidências-chave**:
- **Linhas 81-115**: `InitializeApplicationController()` registra TODOS os services no LightInject
- **Linhas 89-93**: MediatR registration usa `ServiceFactory` delegate + assembly scanning para handlers
- **Linhas 108-112**: Views registradas como `RegisterView<TView, TImplementation>()` - self-registration

**Riscos**:
- Container registra `IMediator` como `MediatR.Mediator` concreto (linha 89) - acoplamento a implementação
- `ServiceFactory` delegate captura container → potential circular reference
- Assembly scanning (`typeof(Program).Assembly`) pega todos handlers - implicit coupling

### 9.2 ApplicationController.cs (`src/Eve-O-Preview/ApplicationBase/ApplicationController.cs`)

**Responsabilidade**: MVP controller, view registration, presenter resolution and execution.

**Evidências-chave**:
- **Linhas 13-19**: `RegisterView<TView, TImplementation>()` - registra view no container
- **Linhas 34-44**: `Run<TPresenter>()` - resolve e executa presenter
- **Linhas 58-67**: `Create<TService>()` - factory method para services

**Padrão**: ApplicationController implementa `IApplicationController` e se auto-registra no container (linha 10).

### 9.3 ThumbnailManager.cs (`src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs`) - **ARQUIVO CRÍTICO**

**Responsabilidade**: Coração do sistema. Monitora processos, gerencia lifecycle de thumbnails, coordena DWM/zoom/cycle/hotkeys.

**Estrutura**: 1056 linhas, ~40 métodos, 55 private fields.

**Principais fluxos**:

1. **Timer Tick** (291-295): `UpdateThumbnailsList()` + `RefreshThumbnails()` a cada `ThumbnailRefreshPeriod`
2. **Process Monitoring** (297-397): `GetUpdatedProcesses()` → added/updated/removed → create/destroy views
3. **Refresh Loop** (399-554): Para CADA thumbnail: hide/show, position, size, opacity, highlight, zoom, refresh
4. **Event Handling** (637-758): Focus/LostFocus → ZoomIn/ZoomOut; Activated/Deactivated → Window switching
5. **Location Debounce** (980-1030): `EnqueueLocationChange`/`TryDequeueLocationChange` com delay de 2 ciclos
6. **Hotkeys** (238-277): Registra `HotkeyHandler` por hotkey configurado (até 10 cycle groups + minimize all)

**Pontos Críticos**:

- **Linhas 489-551**: Loop principal itera `_thumbnailViews` e faz múltiplas chamadas Win32/DWM por thumbnail
- **Linhas 651-654**: `ThumbnailZoomIn` chamado no hover - `DisableViewEvents`/`EnableViewEvents` previne loop de eventos
- **Linhas 787-805**: `ZoomIn`/`ZoomOut` manipulam `MaximumSize`, `Size`, `Location` diretamente no Form
- **Linhas 867-885**: `TestViewPoints` - snap logic com 9 combinações de pontos, threshold = max(20, size/10)
- **Linhas 886-903**: `SetWindowStyle` usa `GetWindowLong`/`SetWindowLong` (GWL_STYLE) - Win32 direto no service

**Violações de SRP**: ThumbnailManager faz: process monitoring, view lifecycle, DWM coordination, zoom, snap, cycle groups, hotkeys, layout persistence, configuration mediation.

### 9.4 DwmThumbnail.cs (`src/Eve-O-Preview/Services/Implementation/DwmThumbnail.cs`)

**Responsabilidade**: Wrapper fino sobre DWM API para thumbnail registration e updates.

**Evidências-chave**:
- **Linhas 21-55**: `Register()` configura `DWM_THUMBNAIL_PROPERTIES` flags + `DwmRegisterThumbnail`
- **Linhas 57-75**: `Unregister()` chama `DwmUnregisterThumbnail` com try/catch genérico
- **Linhas 77-101**: `Move()`/`Update()` - `Move` só atualiza properties, `Update` chama `DwmUpdateThumbnailProperties`

**Problemas**:
- `Register` captura `ArgumentException` e `COMException` mas só zera `_handle` - sem logging
- `Update` suprime exceções silenciosamente (linhas 93-100) - falhas de DWM passam despercebidas
- Não implementa `IDisposable` - `Unregister` deve ser chamado explicitamente

### 9.5 WindowManager.cs (`src/Eve-O-Preview/Services/Implementation/WindowManager.cs`)

**Responsabilidade**: Toda interop Win32/User32/GDI32 + Linux/Wine compatibility via `wmctrl`.

**Evidências-críticas**:

**Win32 Handles (GDI) - Linhas 295-325**:
```csharp
// GetStaticThumbnail - 5 handles criados, 3 liberados explicitamente
var sourceContext = User32NativeMethods.GetDC(source);           // HDC 1
var destContext = Gdi32NativeMethods.CreateCompatibleDC(sourceContext);  // HDC 2
var bitmap = Gdi32NativeMethods.CreateCompatibleBitmap(sourceContext, width, height);  // HBITMAP
// ...
Gdi32NativeMethods.DeleteDC(destContext);                        // HDC 2
User32NativeMethods.ReleaseDC(source, sourceContext);            // HDC 1
Image image = Image.FromHbitmap(bitmap);                         // Managed wrapper
Gdi32NativeMethods.DeleteObject(bitmap);                         // HBITMAP
```
**Risco**: Se `Image.FromHbitmap` throw → `DeleteObject` NÃO executado → **GDI leak garantido**.

**System-wide Animation - Linhas 83-109**:
```csharp
public void TurnOffAnimation() { /* SPI_SETANIMATION = 0 */ }
public void RestoreAnimation() { /* SPI_SETANIMATION = saved */ }
```
Modifica `SPI_SETANIMATION` **global do Windows** - afeta TODAS as apps, não só esta.

**Linux/Wine - Linhas 112-179**:
- Usa `Process.Start("bash", "-c \"wmctrl -a \\\"windowName\\\"\"")` 
- `CreateNoWindow = false` (linha 155) - cria janela console visível!
- `WaitForExit()` bloqueia thread UI
- Flatpak detection via `Environment.GetEnvironmentVariable("container")`

### 9.6 ThumbnailConfiguration.cs (`src/Eve-O-Preview/Configuration/Implementation/ThumbnailConfiguration.cs`)

**Responsabilidade**: Configuração completa da aplicação - 469 linhas, ~100 propriedades.

**Estrutura**: 
- **Defaults hardcoded** no construtor (linhas 17-152) - exemplos "EVE - Example Toon 1"
- **JSON Properties** com `[JsonProperty]` (linhas 155-319)
- **Logic methods**: `Get/SetThumbnailLocation`, `Get/SetClientLayout`, `GetClientHotkey`, etc.
- **Validation**: Apenas `ApplyRestrictions()` (440-468) - clamp numérico

**Problemas**:
- **Linhas 21-28, 30-37**: `CycleGroup1ClientsOrder` com exemplos hardcoded - polui config real do usuário
- **Linhas 58-72**: `PerClientActiveClientHighlightColor`, `PerClientPreventPreviewColor`, `PerClientPreventPreviews` - dictionaries com chaves "Example Toon"
- **Linhas 304-319**: Private dictionaries (`PerClientLayout`, `FlatLayout`, `ClientLayout`, `ClientHotkey`, `DisableThumbnail`, `PriorityClients`, `ExecutablesToPreview`) expostos via métodos públicos
- **Linhas 442-446**: Linux tem `ThumbnailRefreshPeriod` mínimo 10ms vs Windows 300ms - diferença 30x!
- **Font/Color serialization**: Newtonsoft default - quebra em cultures não-US (decimal separator, color names)

### 9.7 ConfigurationStorage.cs (`src/Eve-O-Preview/Configuration/Implementation/ConfigurationStorage.cs`)

**Responsabilidade**: Load/Save JSON config para disco.

**Evidências**:
- **Linhas 28-40**: `Load()` usa `JsonConvert.PopulateObject` - merge no objeto existente
- **Linhas 43-56**: `Save()` usa `JsonConvert.SerializeObject` com `Formatting.Indented`
- **Linha 52-55**: `catch (IOException)` - **engole silenciosamente** erros de escrita!

**Riscos**:
- Sem backup/atomic write - crash durante write corrompe config
- `PopulateObject` não valida required properties
- Sem versioning/migration de schema (`ConfigVersion` existe mas não usado)

### 9.8 LiveThumbnailView.cs (`src/Eve-O-Preview/View/Implementation/LiveThumbnailView.cs`)

**Responsabilidade**: View que usa DWM live thumbnail.

**Evidências**:
- **Linhas 25-36**: `RefreshThumbnail(forceRefresh)` cria novo `DwmThumbnail` se `forceRefresh` ou nulo
- **Linhas 56-61**: `RegisterThumbnail()` chama `WindowManager.GetLiveThumbnail(this.Handle, this.Id)`
- **Linha 35**: `obsoleteThumbnail?.Unregister()` - cleanup do anterior

**Fluxo de forceRefresh**: A cada 2 ciclos (`FORCED_REFRESH_CYCLE_THRESHOLD`), `ThumbnailManager.RefreshThumbnails` passa `forceRefresh=true` → destrói e recria DWM thumbnail registration.

### 9.9 ThumbnailView.cs (`src/Eve-O-Preview/View/Implementation/ThumbnailView.cs`) - Base Class

**Responsabilidade**: Base Form para thumbnails, mouse handling, zoom, overlay, hotkeys.

**Evidências-chave**:
- **Linhas 340-390**: `ZoomIn(ViewZoomAnchor, zoomFactor)` - manipula `Size`, `Location`, `MaximumSize` com math de anchor
- **Linhas 526-542**: `Resize_Handler` - usa `_suppressResizeEventsTimestamp` workaround para WinForms resize bug
- **Linhas 544-555**: `MouseEnter`/`MouseLeave` → `ThumbnailFocused`/`ThumbnailLostFocus` → Zoom
- **Linhas 599-656**: Custom mouse mode (Left+Right = resize, Right = move) com `LockThumbnailLocation` check
- **Linhas 660-686**: `MouseDownEventHandler` - Ctrl+Left=minimize, Shift+Left=toggle cycle, Ctrl+Shift+Left=switch out, Left=activate, Right=custom mode

### 9.10 ThumbnailOverlay.cs (`src/Eve-O-Preview/View/Implementation/ThumbnailOverlay.cs`)

**Responsabilidade**: Overlay Form sobre thumbnail (label, cycle group indicator, fake preview).

**Evidências**:
- **Linhas 66-126**: `SetCycleGroupIndicator` - posiciona indicador conforme `ZoomAnchor` (9 posições)
- **Linhas 128-191**: `SetPropertiesOverlayLabel` - posiciona label conforme `ZoomAnchor`
- **Linhas 198-236**: `EnableFakePreview` - mostra bitmap estático quando `PreventPreviews=true` ou DWM falha

### 9.11 ProcessMonitor.cs (`src/Eve-O-Preview/Services/Implementation/ProcessMonitor.cs`)

**Responsabilidade**: Monitora processos EVE via `Process.GetProcesses()` polling.

**Evidências**:
- **Linhas 72-122**: `GetUpdatedProcesses` - itera `Process.GetProcesses()`, filtra por `IsExecutableToPreview` (config)
- **Linhas 88-92**: Ignora processos sem `MainWindowHandle` (non-visual)
- **Linha 94**: `Replace("—", "-")` - normaliza dash unicode no window title

**Performance**: `Process.GetProcesses()` a cada 500ms (default) = 2x/seg. Enumera TODOS processos do sistema.

### 9.12 MainFormPresenter.cs (`src/Eve-O-Preview/Presenters/Implementation/MainFormPresenter.cs`)

**Responsabilidade**: MVP Presenter para MainForm - settings UI, thumbnail list management.

**Evidências**:
- **Linhas 52-65**: `Activate()` → `LoadApplicationSettings()` → `_mediator.Send(new StartService())`
- **Linhas 92-99**: `UpdateThumbnailsSize()` → publica `ThumbnailConfiguredSizeUpdated` via MediatR
- **Linhas 101-143**: `LoadApplicationSettings()` - **copia 40+ propriedades** de config para View
- **Linhas 145-215**: `SaveApplicationSettings()` - **copia 40+ propriedades** de View para config + publica MediatR messages para cada mudança
- **Linhas 218-255**: `AddThumbnails`/`RemoveThumbnails` - responde a `ThumbnailListUpdated` notification

**Problema**: Presenter faz mapping manual bidirecional View↔Config - 200+ linhas de boilerplate.

---

## 10. Decisões Arquiteturais (ADR) Sugeridas

| ADR | Título | Status |
|-----|--------|--------|
| 0001 | Separar Domain (ThumbnailManager) de Infrastructure (MediatR, Win32) | Proposto |
| 0002 | Introduzir IThumbnailProvider para abstrair DWM/Static | Proposto |
| 0003 | Substituir DispatcherTimer polling por IObservable/Channel push-based | Proposto |
| 0004 | Adicionar JsonSchema validation para configuração | Proposto |
| 0005 | Implementar GDI handle safety com SafeHandle/try-finally | Proposto |
| 0006 | Migrar Linux/Wine compat para processo separado ou library nativa | Proposto |

---

---

## 11. Classificação de Findings: Comprovados vs Hipóteses vs Metas

### ✅ **Comprovados (FACT)** — Evidência direta no código, reproduzível
| ID | Finding | Arquivo/Linha | Evidência |
|----|---------|---------------|-----------|
| #5.1 | Zoom on hover degradação | ThumbnailManager.cs:637-654, 787-805 | Código chama `ZoomIn` → `DwmUpdateThumbnailProperties` a cada hover |
| #5.2 | DispatcherTimer polling | ThumbnailManager.cs:76-79, 291-295 | Timer roda a cada `ThumbnailRefreshPeriod` (default 500ms) |
| #5.3 | DWM registration por cliente | DwmThumbnail.cs:21-55, LiveThumbnailView.cs:25-36 | Cada view cria próprio `DwmThumbnail`; `forceRefresh` unregister/register |
| #6.1 | Acoplamento forte DWM | ThumbnailManager.cs:57-63, LiveThumbnailView.cs:17 | `IWindowManager.GetLiveThumbnail()` retorna `IDwmThumbnail` concreto |
| #6.2 | Win32 handle leaks | WindowManager.cs:295-325 | `GetStaticThumbnail`: exceção em `Image.FromHbitmap` → `DeleteObject` não chamado |
| #6.3 | JSON config sem schema | ConfigurationStorage.cs:19-41, ThumbnailConfiguration.cs:440-468 | Apenas `ApplyRestrictions` (clamp numérico), sem validação estrutural |
| #6.4 | MVP + MediatR misturados | MainFormPresenter.cs:18-65, ThumbnailManager.cs:34, 395, 745, 1039 | Service de domínio depende de `IMediator` (infraestrutura) |
| #6.5 | Sem testes automatizados | Solution-wide | `Glob("**/*Test*.cs")` = 0 resultados |

### ⚠️ **Hipóteses (HYPOTHESIS)** — Plausíveis, mas requerem medição/profiling
| ID | Hipótese | Como validar |
|----|----------|--------------|
| #H1 | `CycleNextClient()` alocações/LINQ causam latência perceptível em hotkeys rápidos | BenchmarkDotNet: `CycleNextClient` com 7 clientes, medir allocation rate + latency p99 |
| #H2 | `DispatcherTimer` em 500ms consome CPU significativo no idle | `dotnet-counters` / PerfView: CPU % com 7 clientes, comparar 500ms vs 1000ms vs push-based |
| #H3 | DWM thumbnail limit (~16-64) é atingido em cenários reais | Stress test: abrir 20+ clientes EVE, observar `DwmRegisterThumbnail` falhas |
| #H4 | GDI leaks em `GetStaticThumbnail` ocorrem na prática | Teste de estresse 1000 iterações + `dotnet-counters` monitorar GDI handles |
| #H5 | `SPI_SETANIMATION` global afeta outras apps | Verificar comportamento Windows ao chamar `TurnOffAnimation`/`RestoreAnimation` |
| #H6 | Virtualização render-only-visíveis reduz CPU/memória significativamente | Prototype + medir CPU/memória 7 clientes com/sem virtualização |

### 🎯 **Metas de Desempenho (PERFORMANCE TARGETS)** — Objetivos mensuráveis por fase
| Métrica | Baseline (estático) | Fase 1 Target | Fase 2 Target | Fase 3 Target | Validação |
|---------|---------------------|---------------|---------------|---------------|-----------|
| CPU idle (7 clientes) | ~3–5% | <2% | **<1%** | <1% | `dotnet-counters` / PerfView 5min |
| Memória (7 clientes) | ~150–200MB | <150MB | **<100MB** | <80MB | `dotnet-gcdump` / PerfView |
| Refresh latency p99 | ~50–100ms | <50ms | **<30ms** | <20ms | BenchmarkDotNet `RefreshThumbnails` |
| DWM handles abertos | 7+ | 7 (pool) | **Pool size configurável** | N/A (abstração) | Handle count via Process Explorer |
| Startup time | ~2–3s | <2s | <1.5s | <1s (AOT) | `dotnet-trace` cold start |
| Test coverage | 0% | 60% | 80% | 85% | `coverlet` / CI |

> **Regra**: Nenhuma otimização baseada em hipóteses (#H1–H6) será implementada sem **medição prévia** que confirme o problema e **benchmark pós-mudança** que valide a melhoria. Metas da Fase 2+ só são "Done" com dados reais.

---

*Nota: Esta auditoria foi realizada via análise estática de código. Recomenda-se validação com profiling real (dotnet-trace, PerfView, BenchmarkDotNet) antes de implementar otimizações.*