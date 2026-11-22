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
| Estrutura | `TabControl` com 8 abas: General, Thumbnail, Zoom, Overlay, Active Clients, **Grupos** (criada em código, `BuildCycleGroupsTab`), Spotlight, About |
| Navegação | abas no lado esquerdo (`TabAlignment.Left`), desenhadas à mão (`MainForm.Designer.cs`) |
| Tema | nativo do Windows (claro); nenhuma camada de tema |
| Grupos de ciclo | aba própria (montada em código) com árvore somente leitura + diálogo de edição |
| Tamanho | 455×251 px (`ClientSize`), abas com 120 px de largura |

## As três propostas

| | Layout 1 — claro, menu lateral | Layout 2 — grafite, abas no topo | Layout 3 — azul-marinho, blocos com ícones |
|---|---|---|---|
| Tema | claro, superfícies brancas, acento azul `#075bc2` | escuro (grafite), acento âmbar `#ffc55a` | escuro (azul-marinho `#111e31`), acento mint `#73ebc8` |
| Navegação | coluna lateral de 112 px | barra de abas no topo, quebrando linha | **grade de 4 blocos** com ícone + rótulo, título por tela |
| Abas | 8 (inclui **Grupos**) | 8 (inclui **Grupos**) | 8 (inclui **Grupos**) |
| Diferença estrutural vs. hoje | renomeia/realinha seções e promove Grupos a aba | idem + tema escuro | idem + navegação em blocos + General em 2 colunas |
| Esforço em WinForms | **baixo** — nativo, só reposicionar e reestilizar | **alto** — exigiria owner-draw | **médio** — blocos são botões comuns; tema ainda exige owner-draw |
| Risco de regressão | baixo | médio/alto | médio |

As três mostram o mesmo conteúdo das abas atuais — inclusive **Grupos**, que já existe como aba
montada em código. O que muda é organização e aparência:

1. **Grupos** passa a caber no desenho das abas (hoje é inserida em código na posição 5).
2. "Active Clients" mantém a lista "check to force hide" e ganha a edição de **label** (já
   implementada no #151).
3. About com versão real (os mockups mostram `8.3.4.0`).

> Os dados dos mockups ("Example Toon", "Do not display previews" marcado) são exemplo, não
> o estado do mantenedor.

### Layout 3 em detalhe

- **Navegação**: 4 colunas de blocos (General, Thumbnail, Zoom, Overlay / Active Clients,
  Grupos, Spotlight, About), cada um com ícone e rótulo, quebrando para 2 colunas em telas
  estreitas. Em WinForms isso é um `TableLayoutPanel` com `Button`s `FlatStyle.Flat` — sem
  owner-draw.
- **Ícones**: os glifos do mockup podem ser substituídos por `Segoe MDL2 Assets` /
  `Segoe Fluent Icons`, presentes no Windows 10/11, sem dependência nova.
- **Tema**: a mesma tabela de custo vale aqui (checkbox, radio, lista, trackbar, abas); a
  diferença é que o acento mint sobre azul-marinho exige conferir contraste em 100/125/150%.
- **General em duas colunas**: ganho real de espaço — hoje os 9 controles ficam em coluna
  única numa aba de 327 px.


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

- Manter a aba **Grupos** (já construída em código) e alinhar seu desenho ao das demais.
- Alinhar rótulos ("Active Clients", "Grupos", "About") e agrupar os controles como no mockup.
- Manter as cores do sistema — nenhuma mudança de comportamento, nenhum owner-draw novo.

Aceite: as 8 abas abrem, os controles respondem igual a hoje, a config continua o mesmo JSON,
`make check` verde e nenhum teste de desktop quebrado.

**Fase 2 — navegação em blocos (Layout 3, sem tema novo).**

Se a direção visual for a do Layout 3, o passo seguinte é trocar as abas laterais por uma
grade de botões (4×2) com ícones de `Segoe MDL2 Assets` — ainda com as cores do sistema. Isso
entrega a navegação nova sem o custo do tema e pode ser medido sozinho.

**Fase 3 — tema (opcional).** Só depois de esgotadas as melhorias do produto:

- Introduzir uma camada mínima de tema (`ITheme` com cores de superfície/acento) e aplicá-la
  primeiro na janela principal.
- Um controle por vez, com captura antes/depois e teste manual em 100/125/150%.
- O azul-marinho/mint do Layout 3 é o candidato natural (contraste já desenhado no mockup);
  o grafite do Layout 2 fica como alternativa.

Aceite: alternar claro/escuro sem reiniciar, nenhuma cor ilegível, nenhuma regressão funcional.

## O que não muda em nenhuma fase

- O arquivo `EVE-O-Preview.json` e o `ConfigVersion`.
- Hotkeys, comportamento de refresh, DWM/GDI, spotlight e grupos.
- O app continua sem funcionalidade nova nesta frente.

## Inventário de controles (medido no `MainForm.Designer.cs`)

| Tipo | Qtde |
|---|---|
| `CheckBox` | 16 |
| `RadioButton` | 27 |
| `NumericUpDown` | 5 |
| `TrackBar` | 2 |
| `CheckedListBox` | 2 |
| `Button` | 2 |
| `ComboBox` | 1 |
| **Total interativo** | **55** |
| `Label` / `Panel` / `LinkLabel` | 26 / 13 / 1 |

Só o cabeçalho das abas é desenhado à mão hoje (`ContentTabControl_DrawItem`). Um tema escuro
teria de cobrir os **55** controles interativos.

## Custo de altura por navegação

| Proposta | Navegação | Conteúdo restante (janela atual) | Janela necessária |
|---|---|---|---|
| Hoje | lateral (124 px de largura) | 243 px | 455×251 |
| Layout 1 | lateral (sem mudança) | 243 px | **455×251** |
| Layout 2 | abas no topo, 2 linhas (~70 px) | ~170 px | ~455×330 |
| Layout 3 | grade 4×2 (~110 px) + título (~30 px) | ~100 px | ~455×400 |

Ou seja: **só o Layout 1 cabe no tamanho atual**. Layouts 2 e 3 exigem decidir entre crescer a
janela ou aceitar rolagem — decisão que precisa vir antes de mexer no tema.

## Estado da implementação

| Fase | Estado |
|---|---|
| 1 — reorganização (rótulos, alinhamentos, barra chapada) | ✅ 8.3.7 (#159) |
| 2 — navegação em blocos, sem tema | ✅ 8.3.8 (#160) — janela 455×388 |
| 3 — tema escuro | pendente (cosmético) |

## Relacionado

- `docs/roadmap.md` — diretriz feature-complete (2026-11-22).
- `docs/metrics.md` — o que já foi medido (refresh, GDI, CPU).
