# Métricas

Instrumentação com `System.Diagnostics.Metrics` (biblioteca nativa, sem pacote externo).
Existe para responder "o app está degradando com 7 clientes?" com número em vez de impressão.

## O que é medido

| Instrumento | Tipo | Unidade | O que responde |
|---|---|---|---|
| `thumbnail.refresh.duration` | histograma | ms | o ciclo de refresh está ficando mais lento? |
| `thumbnail.refresh.count` | contador | ciclos | quantos ciclos rodaram |
| `thumbnail.count.active` | gauge | thumbnails | quantas thumbnails estão vivas |
| `gdi.handles.open` | gauge | handles | vazamento de recurso GDI (Windows) |
| `dwm.handles.open` | gauge | thumbnails | thumbnails DWM registradas |
| `cpu.usage.percent` | gauge | % | CPU do processo (0–100) |

`-1` significa **indisponível** (ex.: GDI fora do Windows).

## Como ligar o retrato em arquivo

O app **não** exporta pela rede. O retrato local é o exportador padrão:

```bash
EVE_METRICS=1 EVE_METRICS_PATH=~/eveo-metrics.log ./EVE-O-Preview.exe
```

Sem `EVE_METRICS_PATH`, grava em `%TEMP%\EVE-O-Preview-metrics.log`. Uma linha a cada 30 s:

```
ts=2026-11-21T21:30:00.0000000Z refresh_ms=12.40 refreshes=1842 thumbnails=7 gdi=312 dwm=7 cpu_pct=1.4
```

O mesmo liga/desliga está em `IAppConfig.MetricsEnabled` / `MetricsSnapshotPath`, para quem
preferir configurar por código.

## Consumir ao vivo

Qualquer `MeterListener` escuta o meter `EveO.Preview` — inclusive ferramentas de fora:

```bash
dotnet-counters monitor --process-id <pid> --counters EveO.Preview
```

## Por que não há exporter OTLP/Prometheus

O roadmap pedia um. A decisão foi **não** embutir: o app é desktop, roda na máquina do jogador e
não tem consumidor de métricas por perto. Abrir porta HTTP ou manter um exporter dentro do
processo com 7 clientes abertos adiciona risco (e superfície) sem ninguém para ler.

Se houver consumidor, a receita é um `MeterListener` que empurra para OTLP — decisão de uma
tarde, sem tocar na instrumentação. Até lá, o retrato em arquivo cobre o caso real: "comparar
ontem com hoje".

## O que não é medido

- tempo de registro DWM por thumbnail (exigiria instrumentar o caminho quente do Windows);
- CPU do sistema (só a do processo, que é a que o app controla);
- memória — os handles GDI são o indicador de vazamento que importa aqui.

## Onde encostar

| Arquivo | Papel |
|---|---|
| `src/EveO.Core/Diagnostics/EveOMetrics.cs` | instrumentos e retrato |
| `src/EveO.Core/Diagnostics/MetricsSnapshotWriter.cs` | gravação periódica (nunca lança) |
| `src/EveO.Presentation/ApplicationBase/MetricsSetup.cs` | provedores GDI/CPU e ligação do writer |
| `src/EveO.Presentation/Services/Implementation/ThumbnailManager.cs` | mede o ciclo de refresh |

`docs/metrics-dashboard.json` descreve os painéis de referência para quando existir um consumidor.
