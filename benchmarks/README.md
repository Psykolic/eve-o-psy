# Benchmarks

Mede os caminhos quentes que rodam **em Linux** — o mesmo ambiente do gate e do CI.
Serve para os gates da Fase 3 ("medir antes de decidir") terem número, em vez de opinião.

```bash
bash scripts/bench.sh            # mede e compara com benchmarks/baseline.json
bash scripts/bench.sh --update   # grava um novo baseline (depois de uma melhoria proposital)
BENCH_THRESHOLD=15 bash scripts/bench.sh   # tolerância diferente (padrão: 10%)
```

Sai **1** quando algum benchmark piora mais que o limite — é isso que o job agendado do CI usa.

O limite padrão é **20%**, não 10%: medindo duas rodadas idênticas no host, a variação chegou a
**+8,3%** (job `short`, 3 iterações). Um gate de 10% acusaria regressão onde só houve ruído — e um
alarme falso é pior que nenhum. Para apertar, use um job com mais iterações e meça o ruído de novo
antes de baixar o limite.

Além do percentual, o comparador exige que a diferença **absoluta** passe de 50 ns e ignora
benchmarks abaixo de 100 ns. Sem isso, o primeiro disparo do job acusou "regressão" de 22% num
benchmark de **2,4 ns** — ou seja, 0,5 ns, ruído puro. Percentual em escala de nanossegundos
não é sinal; é arredondamento.

## O que é medido

| Benchmark | Por quê |
|---|---|
| `ProcessSetDiff.Update` / `.Known` | roda a cada ciclo de monitoramento; `Known` aloca uma lista por chamada |
| `ThumbnailConfiguration.GetThumbnailSize` | chamado por thumbnail em cada refresh (com e sem sobreposição por cliente) |
| `IsThumbnailDisabled` | consultado em cada view ao montar a lista de clientes |
| `SpotlightResizeCalculator.Calculate` | roda durante o arrasto da borda do spotlight |
| `SpotlightRegionCalculator.MapSelection` | roda ao confirmar a seleção do spotlight |
| `ThumbnailSizePolicy.ClampToLimits` | chamado a cada proposta de redimensionamento |

## O que **não** é medido, e por quê

Registro/atualização de `DwmThumbnail`, `ThumbnailManager` e qualquer coisa de WPF exigem
Windows com DWM. Não há como medir isso no runner Linux sem medir outra coisa.

Para esses caminhos, a evidência é outra:

- números de CPU idle e handles por cliente estão nos ADRs (ADR-006, 007, 008), medidos no
  PC do mantenedor com 7 clientes reais;
- o smoke Wine garante que o app sobe e fica vivo, não que ele está rápido.

Se um dia a Fase 3 trouxer um runner Windows com DWM, aí sim vale um segundo conjunto.

## Baseline

`benchmarks/baseline.json` guarda a média (ns) por benchmark. Máquina diferente = número
diferente: o baseline vale para o host `e52680v3` (Xeon E5-2680 v3, 12/24 threads).

Regenerar depois de mudar hardware **e** revisar o diff: um baseline atualizado sem olhar
apaga justamente o sinal que o benchmark existe para dar.
