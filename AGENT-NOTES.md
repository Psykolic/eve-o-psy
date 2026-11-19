# Onda 3 ??? o que foi movido e o que ficou

## Movido para EveO.Core

- `Mediator/Messages/**` (13 arquivos) ??? namespace preservado (`EveOPreview.Mediator.Messages`).
  Como as classes s??o `internal`, o Core ganhou `InternalsVisibleTo` para `Eve-O-Preview`, `EveO.Tests`
  e `EveO.Core.Tests`; e a depend??ncia `MediatR 9.0.0` (mesma vers??o j?? usada pelo exe).
- `ApplicationBase/` ??? apenas as abstra????es: `IApplicationController`, `IIocContainer`, `IPresenter`,
  `IPresenterGeneric`, `IView`. As implementa????es (`ApplicationController`, `Presenter`, `LightInjectContainer`,
  `ExceptionHandler`, `AppOperationLog`) continuam no exe e v??o para Presentation na Onda 4.

## Ficou para a Onda 3b (bloqueio real, n??o pregui??a)

- `Configuration/**` ??? `ThumbnailConfiguration` e `IThumbnailConfiguration` usam
  **`System.Windows.Forms.Keys`** (hotkeys), al??m de `System.Drawing`. O plano s?? previa o `Drawing`
  (permitido em `System.Drawing.Primitives`); `Keys` arrasta WinForms, que o Core n??o pode referenciar.
  Solu????o proposta: extrair um tipo de hotkey neutro (ex.: `HotkeyBinding` com `int`/enum pr??prio) e
  adaptar no exe ??? ?? mudan??a de tipo, ent??o pertence a uma issue pr??pria.
- `Services/Interface/IThumbnailManager.cs` ??? referencia `EveOPreview.View` (documentado no plano).
- `Services/Interface/IWindowManager.cs` (hoje em Infrastructure) ??? depende de `Configuration`; s?? pode
  ir para o Core depois do item acima.

## Verifica????o

- `dotnet build src/EveO.Core/EveO.Core.csproj` em Linux: verde, sem `UseWindowsForms`/`UseWPF`/`net8.0-windows`.
- `dotnet build src/EVE-O-Preview.sln -c Release`: verde (5 warnings pr??-existentes).
- `EveO.Core.Tests`: 50/50.
