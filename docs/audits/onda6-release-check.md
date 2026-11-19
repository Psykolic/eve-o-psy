# Onda 6 — validação do release com 4 assemblies

Data: 19/11/2026 · commit base: `c01abca` (após a Onda 5)

## Objetivo

Confirmar que o pacote de release continua funcionando com a solução dividida em quatro assemblies
(`EveO.Core`, `EveO.Infrastructure`, `EveO.Presentation`, `EVE-O-Preview`) e que o app sobe no Wine.

## Publish

| Variante | Comando | Resultado |
|---|---|---|
| single-file | `dotnet publish src/Eve-O-Preview/Eve-O-Preview.csproj -c Release -r win-x64 --self-contained true` | **OK** (157 MB) |
| multi-file (release) | idem + `-p:PublishSingleFile=false` | **OK** (164 MB) |

## Conteúdo do pacote (multi-file)

```
EveO.Core.dll
EveO.Infrastructure.dll
EveO.Presentation.dll
EVE-O-Preview.dll
EVE-O-Preview.exe
```

Zip gerado a partir do publish: os quatro assemblies + exe estão presentes (`EveO.Core.dll` 56 KB,
`EveO.Infrastructure.dll` 22 KB, `EveO.Presentation.dll` 1,24 MB, `EVE-O-Preview.exe` 183 KB).

## Smoke (Wine + Xvfb)

```
WINEPREFIX=$HOME/wine-eveo WINEDEBUG=-all
xvfb-run -a wine wineboot -u
cd <publish>; xvfb-run -a wine EVE-O-Preview.exe &
sleep 20; pgrep -f EVE-O-Preview   # vivo
```

Resultado: **app vivo após 20 s**. `wineserver -k` encerra sem crash.

### Observação de ambiente

Executar o publish a partir de `/tmp` falha no Wine 9 deste host com
`Failed to load System.Private.CoreLib.dll`. O mesmo binário copiado para `$HOME` (ou `C:` no prefixo)
sobe normalmente. O workflow de release já roda a partir do workspace do runner (em `$HOME`), então
**não é necessária mudança** em `release.yml`.

## Conclusão

- Nenhuma alteração de empacotamento foi necessária.
- `PublishSingleFile` continua funcionando com os quatro assemblies; a variante multi-file (usada no
  release por causa do Wine 9) contém todas as dependências.
- Smoke validado; `release.yml` permanece intocado (área protegida).

## Smoke manual no Windows (19/11/2026)

Executado no PC do mantenedor, com 9 clientes EVE reais abertos, sobre o build do `main`
(`efb382e`, pos-Onda 5) empacotado com `dotnet publish -r win-x64 --self-contained
-p:PublishSingleFile=false`.

| Item | Resultado |
|---|---|
| Janela principal | OK (minimizada para a tray conforme `MinimizeToTray`) |
| Thumbnails | 9 views `EVE - <personagem>` com conteudo DWM ao vivo |
| Overlay label | OK, com `OverlayLabelFont` migrado (`MS Gothic, 8.25pt, style=Bold`) |
| Hotkeys globais | F14 ciclo para frente (`IsTupiDo` -> `Red Spikee`) e F13 para tras (volta) |
| Spotlight | 3 views de spotlight criadas (invisiveis, como esperado) |
| Config real | lida e reescrita **identica** (6496 bytes, diff linha a linha vazio) |
| Fechamento | sem crash log |

### Regressoes encontradas (e corrigidas antes do release)

O smoke pegou dois erros de injecao de dependencia que a CI nao cobria, ambos
consequencia das Ondas 4a/4b:

1. **Handlers do MediatR** ??? `Program` registrava os handlers a partir de
   `typeof(Program).Assembly`, mas desde a 4b eles vivem em `EveO.Presentation`.
   Resultado: `Error constructing handler ... StartService` no primeiro
   `MainFormPresenter.Activate()`.
2. **`IProcessMonitor`** ??? o contrato ficou no Core e a implementacao no Infrastructure
   (Onda 2); `Register<IProcessMonitor>()` varria o assembly do contrato e nao achava
   a implementacao. O `ThumbnailManager` nao era construido.

Correcao: a raiz de composicao saiu do exe para `EveO.Presentation/ApplicationBase/CompositionRoot.cs`
(com `Create(IIocContainer)` para os testes), o registro dos handlers aponta para o
assembly da propria CompositionRoot e os servicos cujo contrato vive no Core apontam
explicitamente para o Infrastructure. `Program` ficou so com o ciclo de vida.

Guarda de regressao: `EveO.Tests/CompositionRootTests.cs` valida o grafo real
(handler registrado + `ThumbnailManager` resolvivel). Verificado que o teste falha
quando a correcao e revertida.
