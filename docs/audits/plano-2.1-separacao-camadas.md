# Plano de execução — Item 2.1 (Separação de camadas)

*Baseado no inventário real de `src/` em `a954ce6`. Complementa `docs/roadmap.md` (2.1) e `docs/audits/architecture-plan.md`.*

## 1. Estado atual

| Fato | Número |
|---|---|
| Arquivos `.cs` em `src/Eve-O-Preview` (sem `obj/bin`) | 95 |
| Arquivos em `EveO.Core` | 2 (calculadoras puras de spotlight) |
| Arquivos em `EveO.Tests` / `EveO.Core.Tests` | 11 / 2 |
| Projetos na solução | 5 (`Eve-O-Preview`, `Eve-O-Mock`, `EveO.Tests`, `EveO.Core`, `EveO.Core.Tests`) |
| Projetos que ainda não existem | `EveO.Infrastructure`, `EveO.Presentation` |

O `Eve-O-Preview.csproj` é simultaneamente app, UI, Win32 e domínio: `OutputType=WinExe`, `UseWindowsForms=true` **e** `UseWPF=true`, `PublishSingleFile=true`, `net8.0-windows8.0`.

## 2. Alvo da separação

| Projeto | TFM | Responsabilidade | Referências |
|---|---|---|---|
| `EveO.Core` | `net8.0` | Modelo de configuração, mensagens do Mediator, abstrações (`IPresenter`, `IView`, `IApplicationController`, `IIocContainer`), interfaces de serviços, lógica pura (spotlight, ciclagem) | nenhuma (só BCL) |
| `EveO.Infrastructure` | `net8.0-windows8.0` | Interop Win32/DWM/GDI, `DwmThumbnail`, `WindowManager`, `ProcessMonitor`, thumbnail estático | `EveO.Core` |
| `EveO.Presentation` | `net8.0-windows8.0` | Views, presenters, handlers do Mediator, `ThumbnailManager`, formulários e overlays | `EveO.Core`, `EveO.Infrastructure` |
| `Eve-O-Preview` (exe) | `net8.0-windows8.0` | `Program.cs` (composition root), `app.manifest`, ícone, publish single-file | `Core`, `Infrastructure`, `Presentation` |

**Decisão:** `Eve-O-Preview` continua sendo o executável — não criar `EveO.App`. Isso preserva `PublishSingleFile`, `StartupObject=EveOPreview.Program` e o ícone/manifesto sem reescrita do pipeline de release.

## 3. Mapa de dependências atual (achados que definem a ordem)

| Grupo de arquivos | Depende de | Destino |
|---|---|---|
| `Services/Interop/*` (11 arquivos) | só BCL/`System.Drawing` | Infrastructure |
| `Services/Implementation/DwmThumbnail`, `WindowManager`, `ProcessMonitor`, `StaticThumbnailGdi`, `AsyncOperationTracker` | Interop, Configuration | Infrastructure |
| `Services/Interface/*` | — exceto **`IThumbnailManager` → `EveOPreview.View`** e `IWindowManager` → Configuration+Drawing | Core (com refatoração pontual) |
| `Configuration/**` | `ThumbnailConfiguration` e `IThumbnailConfiguration` usam `System.Drawing` | Core — ver decisão 5.2 |
| `Mediator/Messages/**` (13) | 3 usam `System.Drawing` | Core |
| `Mediator/Handlers/**` (13) | Services + Presenters misturados | Presentation |
| `ApplicationBase/*` | abstrações sem dependência; 3 impls usam WinForms | interfaces → Core; impls → Presentation |
| `View/**`, `Presenters/**`, `MainForm*`, `AboutBox*` | WinForms/WPF | Presentation |
| **`Services/Implementation/ThumbnailManager.cs`** (1373 linhas) | Configuration + Mediator + Interop + `EveOPreview.View` + **WPF (`System.Windows.Threading`)** + WinForms | Decisão 5.3 |
| `Hotkeys/HotkeyHandler.cs` | WinForms (`Keys`) | Presentation (native methods → Infrastructure) |

### Achados críticos

1. **`IThumbnailManager` referencia `EveOPreview.View`** — a interface de serviço conhece a View. É o principal bloqueio para deixar `Core` limpo.
2. **`ThumbnailManager` usa WPF e WinForms ao mesmo tempo** (Dispatcher + `Control`), além de Interop. Não é "serviço puro"; é orquestração de UI.
3. **`ThumbnailConfiguration` usa `System.Drawing`** (`Color`, `Size`, `Point`, `Rectangle`). Esses tipos vivem em `System.Drawing.Primitives`, que é cross-platform — não exigem `System.Drawing.Common`.
4. **Handlers do Mediator** têm dependências misturadas (alguns de Services, outros de Presenters); a divisão é feita handler a handler, não por pasta.

## 4. Ondas de execução (uma PR por onda, CI verde em cada)

| Onda | Conteúdo | Critério de aceite |
|---|---|---|
| **0 — Scaffold** | Criar `EveO.Infrastructure` e `EveO.Presentation` (classlibs, TFM do alvo), adicionar à solução, sem mover nenhum arquivo | `dotnet build` da solução verde; `Checks (Linux)` verde; nenhum comportamento alterado |
| **1 — Interop** | Mover `Services/Interop/*` para Infrastructure; ajustar `using` | CI verde; smoke Wine do release continua passando |
| **2 — Serviços de plataforma** | Mover `DwmThumbnail`, `WindowManager`, `ProcessMonitor`, `StaticThumbnailGdi`, `AsyncOperationTracker` + `HotkeyHandlerNativeMethods` | CI verde; testes de `EveO.Tests` ajustados apenas em `using` |
| **3 — Core de domínio** | Mover Configuration (modelo + storage), `Mediator/Messages`, abstrações de `ApplicationBase` e interfaces de serviços; resolver `IThumbnailManager`/`IWindowManager` | `dotnet build src/EveO.Core` em Linux **sem** referência Windows; `EveO.Core.Tests` verde |
| **4 — Apresentação** | Mover `View/**`, `Presenters/**`, `Mediator/Handlers/**`, impls de `ApplicationBase`, `MainForm*`, `AboutBox*`, `ThumbnailOverlay`, `ThumbnailManager` | CI verde; validação manual 6+1 no Linux/Wine |
| **5 — Testes e visibilidade** | Repontar `EveO.Tests` para Presentation/Infrastructure; ajustar `InternalsVisibleTo` | Suíte completa verde no CI |
| **6 — Release** | Verificar `PublishSingleFile` + smoke Wine no `release.yml` | Release de teste `v8.1.x-rc` publica e sobe no Wine |

**Regra:** nenhuma onda mistura movimento de arquivo com mudança de lógica. Renomear/splitar vem depois (item 2.1b).

## 5. Decisões de projeto

### 5.1 `Eve-O-Preview` permanece como executável
Evita reescrever `StartupObject`, `app.manifest`, ícone e o job de release. O executável passa a ser apenas composition root.

### 5.2 `System.Drawing` no Core
Usar `System.Drawing.Primitives` (parte do shared framework, cross-platform) para `Color`/`Size`/`Point`/`Rectangle`. **Não** referenciar `System.Drawing.Common` no Core. Se algum tipo exigir GDI+, o arquivo fica em Presentation.

### 5.3 `ThumbnailManager` — dividir em 2.1a e 2.1b
- **2.1a (esta issue):** `ThumbnailManager` vai inteiro para `EveO.Presentation`. A separação de camadas é estrutural; o Core fica com lógica pura.
- **2.1b (issue separada):** extrair `ThumbnailOrchestrator` para o Core e deixar na Presentation apenas o adaptador de UI. Exige quebrar `IThumbnailManager → View` e trocar Dispatcher por abstração de agendamento.

Tentar fazer 2.1b junto com a movimentação multiplica o risco de regressão sem ganho imediato.

### 5.4 `InternalsVisibleTo`
Hoje `EveO.Tests` enxerga internals de `Eve-O-Preview`. Após a separação, replicar o atributo em `EveO.Infrastructure` e `EveO.Presentation` (e remover do exe quando não houver mais internals).

## 6. Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Arquivos `*.Designer.cs` separados do partial | Alta se mover manualmente | Build quebra | Mover sempre o trio `.cs` + `.Designer.cs` + `.resx` junto |
| Publish single-file quebrar com 3 assemblies | Média | Alto | Onda 6 dedicada; testar `PublishSingleFile` antes de release |
| Namespaces antigos `EveOPreview.Services` no Core (`RootNamespace` atual) | Alta | Confusão | Alinhar `RootNamespace` ao nome do projeto na onda 3 |
| Testes que dependem de internals | Média | Médio | Onda 5 com `InternalsVisibleTo` explícito |
| `Eve-O-Mock` referenciar tipos movidos | Média | Médio | Ajustar referências na onda 2/4 |
| WPF + WinForms no mesmo assembly de Presentation | Baixa | Baixo | Manter `UseWPF` e `UseWindowsForms` no projeto de Presentation |

## 7. Checklist de validação por onda

- [ ] `dotnet build src/EVE-O-Preview.sln -p:EnableWindowsTargeting=true` local
- [ ] `Checks (Linux)` verde no CI
- [ ] `EveO.Core.Tests` verde
- [ ] Nenhuma mudança de comportamento (diff só de movimentação/`using`/csproj)
- [ ] Ondas 4 e 6: smoke Wine + validação manual 6+1

## 8. Fora de escopo desta issue

- Extração do `ThumbnailOrchestrator` (2.1b).
- Troca de `DispatcherTimer` por streams (2.2).
- Alterações de comportamento de captura/DWM.
