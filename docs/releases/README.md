# Notas de release

Registro das versões do EVE-O Preview. Existe porque as releases **8.2.1, 8.2.2 e 8.3.0**
foram publicadas no GitHub antes da migração (Fase 3) e não têm release correspondente no
Forgejo — aqui é o único lugar onde o texto sobreviveu.

| Versão | Tema |
|---|---|
| [8.2.1](8.2.1.md) | Arquitetura em camadas e correção de inicialização |
| [8.2.2](8.2.2.md) | Refresh por evento: −64% de CPU idle |
| [8.3.0](8.3.0.md) | Migração para Forgejo (o app não muda) |
| [8.3.1](8.3.1.md) | Primeira release publicada no Forgejo |
| [8.3.2](8.3.2.md) | Manutenção: config inválida não fecha o app, handle DWM em `SafeHandle`, métricas opcionais |
| [8.3.3](8.3.3.md) | Grupos de ciclo configuráveis na tela (editor, teclas, ordem) e importação do app anterior |
| [8.3.4](8.3.4.md) | Refresh imediato ao trocar de cliente (hook de foco/minimizar) e configuração à prova de perda |
| [8.3.5](8.3.5.md) | Label de até 8 caracteres por cliente e thumbnail que sai da tela quando o cliente fecha |
| [8.3.6](8.3.6.md) | Clique no nome do cliente só seleciona; a caixinha é que marca "não mostrar" |
| [8.3.7](8.3.7.md) | Fase 1 do layout: barra lateral chapada, acento e alinhamentos |
| [8.3.8](8.3.8.md) | Fase 2 do layout: navegação em blocos com ícones e janela 455×388 |
| [8.3.9](8.3.9.md) | Spotlight: aparece no login, resize não volta no meio do gesto e Lock não bloqueia |
| [8.3.10](8.3.10.md) | Fase 3 do layout: tema escuro (azul-marinho + mint) com interruptor no General |

A partir da 8.3.1 a fonte da verdade é a release no Forgejo (`scripts/release.sh` publica o corpo).
Estas notas são histórico, não são republicadas.

## Dois pacotes por release

| Asset | Tamanho | Requer |
|---|---|---|
| `Release-vX.Y.Z.zip` | ~67 MB | nada — runtime .NET embutido (**use este se não souber**) |
| `EVE-O-Preview-vX.Y.Z-win-x64-singlefile.zip` | ~2,5 MB | **.NET 8 Desktop Runtime** instalado |

O pacote pequeno é um único `EVE-O-Preview.exe`; conveniente para atualizar, mas não abre sem o
runtime. O smoke da CI roda só no self-contained (o prefixo Wine do runner não tem o runtime).
