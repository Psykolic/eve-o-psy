# Onda 4a ??? escopo reduzido (depend??ncias reais)

## Movido para EveO.Presentation

- `ApplicationBase/`: `ApplicationController`, `Presenter`, `PresenterGeneric`, `LightInjectContainer`,
  `ExceptionHandler`, `AppOperationLog` (as abstra????es j?? estavam no Core desde a Onda 3).
- `Hotkeys/HotkeyHandler.cs`.

Ajustes de projeto: `LightInject 7.0.1` no Presentation (mesma vers??o do exe) e `InternalsVisibleTo`
para `EveO.Presentation` em Core/Infrastructure (native methods internas) e de Presentation para
`Eve-O-Preview`/`EveO.Tests` (`HotkeyHandler` ?? internal).

## Handlers ficaram para a Onda 4b

Os handlers **n??o** s??o independentes da View como o grep inicial sugeria:
- usam `IThumbnailManager`, que referencia `EveOPreview.View`;
- `SpotlightRegionUpdatedHandler` usa `IMainFormPresenter`/`MainFormPresenter`.

Ou seja, dependem da pr??pria camada de apresenta????o. V??o junto com View/Presenters/ThumbnailManager
na #166.
