# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/). O detalhe de cada
versão (medições, decisões, limitações) está em [`docs/releases`](docs/releases).

## [Não publicado]

Nada ainda.

## [8.3.11] — 2026-11-23

### Corrigido

- O diálogo de criação e edição de grupos acompanha a opção de janela sempre no topo,
  evitando que o diálogo fique encoberto e bloqueie o uso do aplicativo.

## [8.3.10] — 2026-11-22

### Adicionado

- **Tema escuro** na janela de configuração (azul-marinho + acento mint), com interruptor
  *General → Dark theme*; a troca aplica na hora e fica salva em `DarkTheme`.
- Checkboxes, rádios e o combo de animação desenhados com a paleta; barra de título escura.

### Limitações

- Setas do `NumericUpDown` e `TrackBar` seguem nativos; alto contraste do Windows não testado.

## [8.3.9] — 2026-11-22

### Corrigido

- **Spotlight no login**: o cliente entra com título `EVE` e vira `EVE - Personagem`; o spotlight
  agora é criado nessa troca, sem precisar mexer na aba Spotlight.
- **Resize do spotlight**: o refresh periódico não reaplica mais tamanho/posição na janela que
  está sendo arrastada.
- `Lock Thumbnail Location` deixa de bloquear o redimensionamento (continua travando a posição).
- Rodapé da aba Spotlight explica que `Shift` libera a proporção no resize.

## [8.3.8] — 2026-11-22

### Alterado

- Navegação em **grade 4×2 de blocos** com ícones (`Segoe MDL2 Assets`), no lugar das abas laterais.
- Janela cresce de 455×251 para **455×388**; as telas usam a largura toda.

## [8.3.7] — 2026-11-22

### Alterado

- Abas com fundo chapado, barra de acento na ativa e texto cinza quando inativa.
- Alinhamento de *Thumbnail* (Snap X/Y) e *General*; link do *About* não encosta mais na borda.
- Recursos de desenho das abas passam a ser descartados a cada pintura (antes vazavam).

## [8.3.6] — 2026-11-22

### Corrigido

- Clicar no nome do cliente na aba *Active Clients* só seleciona; apenas a caixinha alterna
  “não mostrar”. Teclado (espaço) continua alternando.

## [8.3.5] — 2026-11-22

### Adicionado

- **Label por cliente**, com limite de 8 caracteres; vazio usa os 8 primeiros do nome. Vale para
  thumbnail e spotlight e acompanha a troca de personagem.

### Corrigido

- Thumbnail e spotlight saem da tela quando o cliente fecha (antes ficavam 5–15 s).
- Na saída do app, as janelas somem antes do encerramento terminar.

## [8.3.4] — 2026-11-22

### Adicionado

- Refresh por **evento de sistema**: alt-tab, minimizar e restaurar atualizam na hora.

### Corrigido

- Gravação atômica da configuração (`.tmp` + `File.Replace`, com `.bak`).
- Configuração inválida não some mais: o app avisa o que corrigiu e guarda o original em
  `.corrupt-<data>`.

## [8.3.3] — 2026-11-22

### Adicionado

- **Grupos de ciclo configuráveis na tela**: aba *Grupos*, diálogo de edição, atalhos e ordem.
- Importação da configuração do aplicativo anterior (`ConfigVersion: 3`, `"CycleGroups"`).

## [8.3.2] — 2026-11-21

### Corrigido

- Configuração inválida não fecha o app: padrões aplicados com aviso.
- Handle de thumbnail do DWM em `SafeHandle`.
- Métricas opcionais (`EVE_METRICS=1`) com retrato local a cada 30 s.

## [8.3.1] — 2026-11-20

### Alterado

- Primeira release publicada inteiramente no Forgejo (tag, build, smoke e assets). O aplicativo é
  idêntico ao da 8.3.0.
