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

## Medição real (2026-11-22, 7 clientes abertos)

Primeira execução com `EVE_METRICS=1` no PC do mantenedor:

```
ts=...03:08:15Z refresh_ms=347.95 refreshes=1  thumbnails=7 gdi=79 dwm=7 cpu_pct=-1.0
ts=...03:08:45Z refresh_ms=8.95   refreshes=7  thumbnails=7 gdi=83 dwm=7 cpu_pct=0.1
ts=...03:09:15Z refresh_ms=8.05   refreshes=13 thumbnails=7 gdi=83 dwm=7 cpu_pct=0.0
```

| Observação | Leitura |
|---|---|
| intervalo entre linhas | **30,1 s** (o writer está correto) |
| primeiro ciclo | **348 ms** — custo de subida (7 registros no DWM) |
| ciclos seguintes | **8–9 ms** |
| `refreshes` | +6 a cada 30 s = **reconciliação de 5 s** (ADR-006); sem refresh extra com clientes parados |
| `thumbnails` / `dwm` | **7 / 7** — casa com as 7 janelas abertas |
| `gdi` | 79 → 83 → **83** (estável; +4 na subida) |
| `cpu_pct` | **0,1% / 0,0%** (a 8.2.2 media 0,52%) |

`cpu_pct=-1.0` na primeira linha é esperado: o amostrador precisa de duas leituras para
calcular diferença e devolve "indisponível" na primeira.

## O que gera evento (e o que não gera)

O sinal de mudança de cliente vem de `SetWinEventHook(EVENT_OBJECT_CREATE..EVENT_OBJECT_NAMECHANGE)`.
Isso é mais estreito do que "usuário mexeu no jogo":

Há **dois hooks**:

| Hook | Faixa | Cobre |
|---|---|---|
| objeto | `0x8000..0x800C` | `CREATE`, `DESTROY`, `SHOW`, `HIDE`, `NAMECHANGE` |
| sistema | `0x0003..0x0017` | `FOREGROUND` (foco), `MINIMIZESTART`, `MINIMIZEEND` |

| Ação | Dispara o sinal? |
|---|---|
| Abrir ou fechar um cliente | ✅ `OBJECT_CREATE` / `OBJECT_DESTROY` |
| Minimizar ou restaurar | ✅ `SYSTEM_MINIMIZESTART` / `SYSTEM_MINIMIZEEND` |
| Título da janela muda | ✅ `OBJECT_NAMECHANGE` |
| **Alt-tab / trocar o foco** | ✅ `SYSTEM_FOREGROUND` |
| Ciclar clientes pela hotkey | ❌ o `CycleNextClient` chama o refresh direto (não passa pelo sinal) |
| Pular gate, dockar, abrir inventário | ❌ é conteúdo dentro da mesma janela |

O hook de sistema existe desde 2026-11-22: até então só o de objeto, e por isso alt-tab e
minimizar **não** geravam refresh (medido: `eventos=0` em 1 h 39 de jogo).

### Comprovação em execução (2026-11-22, 8 clientes)

Teste em três fases, separadas por pausas, em jogo real:

| Fase | Ação | Δ eventos | Leitura |
|---|---|---|---|
| A | 3× minimizar/restaurar (fixed window) | **+6** | 3 `MINIMIZESTART` + 3 `MINIMIZEEND` |
| B | alt-tab entre clientes | +3 a +7 por 30 s | `SYSTEM_FOREGROUND` |
| C | 5 ciclagens pela hotkey | **+61** | cada ativação emite uma rajada (~12) |
| — | pausa após as fases | **0** | silêncio sem atividade |

Durante tudo: `gdi` **89 constante**, `cpu_pct` pico **0,2%** (idle 0,04%), `refresh_ms` 4–10 ms,
máximo de 25 eventos por janela de 30 s (~0,8/s) contra o teto do throttle de 100 ms (10/s).

Nota: ciclar pela hotkey chama o refresh direto (não pelo sinal), mas **ativa** janelas — e isso
gera `FOREGROUND`. Foi o que produziu a rajada da fase C.

### Pré-requisito de teste

O cliente EVE em **tela cheia exclusiva não minimiza** — logo, não gera `HIDE`/`SHOW` e o caminho
por evento não pode ser exercitado assim. Para testar minimize/restore, colocar o cliente em modo
**janela (fixed window)**. Sem isso, `eventos=0` é o resultado correto, não um bug.

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
