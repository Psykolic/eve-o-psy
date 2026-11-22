# Propostas de interface — estudo (2026-11-22)

> Origem: mockup do mantenedor (`EVE-O-Preview_Layouts_1_e_2.html`) comparando duas
> reorganizações da janela de configuração. Este documento é **estudo**, não decisão de
> implementação: nada foi alterado no app.
>
> Diretriz vigente: o aplicativo cumpre o propósito. Layout entra só como **cosmético**
> (visual, texto, espaçamento) ou como **melhoria de usabilidade** com dor demonstrada —
> nunca junto com mudança de comportamento.

## O que existe hoje

| Item | Estado |
|---|---|
| Estrutura | `TabControl` com 7 abas: General, Thumbnail, Zoom, Overlay, Active Clients, Spotlight, About |
| Navegação | abas no lado esquerdo (`TabAlignment.Left`), desenhadas à mão (`MainForm.Designer.cs`) |
| Tema | nativo do Windows (claro); nenhuma camada de tema |
| Grupos de ciclo | editados por diálogo; não há aba dedicada (`SetCycleGroups` já existe na view) |
| Tamanho | 455×251 px (`ClientSize`), abas com 120 px de largura |

## As duas propostas

| | Layout 1 — claro, menu lateral | Layout 2 — grafite, abas no topo |
|---|---|---|
| Tema | claro, superfícies brancas, acento azul `#075bc2` | escuro (grafite), acento âmbar `#ffc55a` |
| Navegação | coluna lateral de 112 px | barra de abas no topo, quebrando linha |
| Abas | 8 (inclui **Grupos**) | 8 (inclui **Grupos**) |
| Diferença estrutural vs. hoje | renomeia/realinha seções e promove Grupos a aba | idem + tema escuro |
| Esforço em WinForms | **baixo** — nativo, só reposicionar e reestilizar | **alto** — exigiria owner-draw |
| Risco de regressão | baixo | médio/alto |

Os dois mockups mostram o mesmo conteúdo das abas atuais, com três ajustes de informação:

1. **Grupos** vira aba (hoje a edição é por diálogo).
2. "Active Clients" mantém a lista "check to force hide" e ganha a edição de **label** (já
   implementada no #151).
3. About com versão real (o mockup mostra `8.3.4.0`).

> Os dados do mockup ("Example Toon", "Do not display previews" marcado) são exemplo, não
> o estado do mantenedor.

## Por que o tema escuro é caro

WinForms não tem tema: cada controle precisa ser desenhado à mão para manter contraste.

| Peça | O que exigiria |
|---|---|
| `TabControl` | `OwnerDraw` das abas (já existe para as laterais), cores por estado |
| `CheckBox`, `RadioButton` | `FlatStyle` + pintura custom ou `CheckedListBox` ownerdraw |
| `ListBox`/`CheckedListBox` | cores de seleção e item |
| `TextBox`, `NumericUpDown`, `TrackBar` | `BackColor`/`ForeColor` parciais; alguns ignoram cor do sistema |
| `Button`, `Panel`, `Label` | repintura e `Paint` eventos |
| Alto contraste / DPI | testar 100/125/150% e o modo de alto contraste do Windows |

Custo estimado: 3–5 sessões com QA visual, contra 1–2 para o Layout 1.

## Recomendação

**Fase 1 — Layout 1 (reorganização, sem tema novo).**

- Promover **Grupos** a aba, reaproveitando `SetCycleGroups`/`EditCycleGroup`.
- Alinhar rótulos ("Active Clients", "Grupos", "About") e agrupar os controles como no mockup.
- Manter as cores do sistema — nenhuma mudança de comportamento, nenhum owner-draw novo.

Aceite: as 8 abas abrem, os controles respondem igual a hoje, a config continua o mesmo JSON,
`make check` verde e nenhum teste de desktop quebrado.

**Fase 2 — tema (opcional).** Só depois de esgotadas as melhorias do produto:

- Introduzir uma camada mínima de tema (`ITheme` com cores de superfície/acento) e aplicá-la
  primeiro na janela principal.
- Um controle por vez, com captura antes/depois e teste manual em 100/125/150%.
- O tema escuro do mockup (grafite + âmbar) é a referência, mas não precisa ser pixel-perfect.

Aceite: alternar claro/escuro sem reiniciar, nenhuma cor ilegível, nenhuma regressão funcional.

## O que não muda em nenhuma fase

- O arquivo `EVE-O-Preview.json` e o `ConfigVersion`.
- Hotkeys, comportamento de refresh, DWM/GDI, spotlight e grupos.
- O app continua sem funcionalidade nova nesta frente.

## Relacionado

- `docs/roadmap.md` — diretriz feature-complete (2026-11-22).
- `docs/metrics.md` — o que já foi medido (refresh, GDI, CPU).
