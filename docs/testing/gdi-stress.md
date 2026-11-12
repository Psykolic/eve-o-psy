# F1-04A — Stress test de recursos GDI (Windows)

Este teste valida o caminho de código real `WindowManager.GetStaticThumbnail()` sem iniciar EVE Online e **sem** ativar `WineCompatibilityMode` no EVE-O Preview do jogador.

## Cobertura

- Cria uma janela WinForms de 640 × 480 pixels com HWND válido, em thread STA.
- Alterna o conteúdo desenhado via GDI (duas cores) e confirma que as imagens capturadas mudam.
- Executa 30 capturas de aquecimento e 1.000 capturas medidas.
- Descarta cada `Image` com `Dispose()` antes da próxima captura.
- Usa `GetGuiResources` para medir o número de objetos GDI e USER do próprio processo de testes.
- Registra o início e os pontos de 100, 500 e 1.000 capturas. Os limites são +16 objetos GDI e +8 objetos USER relativamente à leitura após aquecimento.
- Gera CSV quando `EVE_O_GDI_METRICS_PATH` está definido; a execução no GitHub Actions publica o CSV e os arquivos TRX.

Limites são tolerâncias operacionais do harness, **não** uma prova matemática de ausência de vazamentos nem medições de FPS/latência do EVE.

## Como executar no Windows 11

No PowerShell na raiz do repositório:

```powershell
$env:EVE_O_GDI_METRICS_PATH = "src/EveO.Tests/TestResults/gdi-stress-local.csv"
dotnet test .\src\EveO.Tests\EveO.Tests.csproj --configuration Release --filter "Category=WindowsGdiStress" --logger "console;verbosity=normal"
```

O teste de recursos gráficos deve ser executado em uma sessão de desktop Windows **com acesso às APIs Win32**. Se o executor de CI não tiver um desktop que permita desenhar em uma janela, a falha de captura deve ser investigada, não considerada automaticamente um vazamento.

## Como interpretar

| Resultado | Interpretação |
|---|---|
| Capturas mudam e delta de GDI/USER permanece dentro dos limites | Teste do caminho GDI aprovado **na janela de teste** |
| Imagens não mudam | Falha de captura, atualização de conteúdo ou ambiente de desktop |
| Capturas nulas | Falha de alguma API Win32 (GetDC / BitBlt etc.) |
| Crescimento acima do limite em pontos de controle | Possível vazamento / instabilidade, exige reprodução e investigação |

## Limites do ensaio

A janela de teste é desenhada por GDI, não pelo motor gráfico do EVE. A aprovação do harness **não** implica que capturas estáticas de jogos acelerados pela GPU funcionem no Windows, e não cobre falhas forçadas de APIs nativas. Para jogar normalmente, mantenha `WineCompatibilityMode = false` e use as miniaturas DWM.

A correção de segurança GDI do [PR #3](https://github.com/Psykolic/eve-o-custom/pull/3) já está em `main`. O CI usa o mesmo ensaio com `WindowManager.cs` do commit anterior à correção `ea235957ddf55895792b3a1a2a68001ff8584722` como referência fixa. A comparação é diagnóstica.
