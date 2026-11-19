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
