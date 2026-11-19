# Tamanho das miniaturas

As miniaturas usam o tamanho definido em **Thumbnail Width/Height** (campo `ThumbnailSize` do
`EVE-O-Preview.json`). A partir da issue #138, o tamanho configurado não é mais sobrescrito por
redimensionamentos internos do app.

## O que propaga o tamanho

- Arrastar a borda de uma miniatura (ou usar o modo customizado com os botões esquerdo+direito)
  propaga o tamanho final para as demais miniaturas e para o campo *Thumbnail Width/Height*.
- O valor propagado é limitado por `ThumbnailMinimumSize` e `ThumbnailMaximumSize` antes de ser
  aplicado. Se o tamanho final for igual ao já configurado, nada é propagado.
- Ao fechar o app, o `ThumbnailSize` gravado no JSON corresponde ao valor exibido na UI.

## O que não propaga o tamanho

- Ajustes programáticos (aplicar molduras, limites de tamanho, restauração de layout, zoom, DPI).
  Eles redesenham a miniatura, mas não alteram a configuração global.
- Redimensionar a miniatura do cliente de login (janela com título `EVE`). Ela não dita o tamanho
  das demais miniaturas.

## Tamanho por cliente

`PerClientThumbnailSize` é usado como tamanho inicial das miniaturas dos clientes já logados. A
miniatura de login sempre usa o `ThumbnailSize` global.

A resolução do tamanho inicial e a decisão de propagação ficam em `EveO.Core`
(`ThumbnailSizePolicy`) e são cobertas por testes em `EveO.Core.Tests`.
