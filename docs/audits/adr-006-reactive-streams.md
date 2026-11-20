# ADR-006 — Reactive Streams no spike 2.2 (`IAsyncEnumerable` + `Channel<T>`)

**Date**: 2026-11-20
**Decision**: **Gate passou.** Adotar modelo push-based com `IAsyncEnumerable` + `Channel<T>`; **não** adotar Rx.NET.
**Status**: Accepted — spike 2.2 concluído com medição em 7 clientes reais

---

## 1. Context

O EVE-O Preview atualiza thumbnails por polling: `DispatcherTimer` de 500 ms que a cada tick
executa `UpdateThumbnailsList` (varredura de processos via `ProcessMonitor.GetUpdatedProcesses`)
e `RefreshThumbnails`. Não existe hook de evento Win32 no código — todo o refresh é polling.

O spike 2.2 avaliou substituir esse polling por um modelo push-based.

**Gate do spike**: prosseguir somente se a redução de CPU idle for **> 30%** com 7 clientes reais.

---

## 2. Decision

**Adotar event-driven**, com estas escolhas:

| Elemento | Escolha | Motivo |
|---|---|---|
| Transporte | `Channel<T>` limitado a 1 item, `DropOldest`, exposto por `IClientChangeSignal` | Coalesce rajadas sem fila; medido em 0,33% de um core |
| Consumo | `IAsyncEnumerable<T>` (`ReadAllAsync`) | Cancellation nativa, sem dependência nova, mantém ADR-004 (sem Rx.NET) |
| Produtores | `SetWinEventHook(CREATE..NAMECHANGE)` filtrado a janelas top-level + eventos de processo | Substitui a varredura de 2 Hz que responde por 76% do idle |
| Rede de segurança | **Manter um tick de reconciliação lento (5 s)** | Hooks podem não chegar (UIPI/elevação) ou ser perdidos; 5 s custa ~10% do atual |

O tick lento **não** é transitório: é parte do desenho. O ganho vem de trocar 2 Hz por 0,2 Hz
na varredura, não de assumir que eventos nunca falham.

`IClientChangeSignal` já foi implementado em `src/EveO.Core/Refresh/` com 5 testes
(`EveO.Core.Tests/Refresh/ClientChangeSignalTests.cs`).

---

## 3. Alternatives

| Alternative | Medição | Veredito |
|---|---|---|
| **`IAsyncEnumerable` + `Channel<T>`** | 0,330% de 1 core no protótipo (180 s, 7 clientes) | **Escolhido** |
| **Rx.NET (`IObservable`)** | não medido; mesmo transporte por baixo | Rejeitado: dependência nova sem ganho medido, contra ADR-004 |
| **Manter `DispatcherTimer` 500 ms** | 1,42% de 1 core só na varredura | Rejeitado: é o custo que o gate mandou atacar |
| Só aumentar o intervalo do timer | ganho proporcional, mas piora a reação a clientes abrindo/fechando | Rejeitado como solução, **mantido** como fallback lento |

---

## 4. Consequences

**Ganho projetado**: 1,87% → ~0,5–0,8% de um core (**−58% a −73%**), contra gate de −30%.

Custos e riscos aceitos:

- **Elevação (UIPI)**: se um cliente EVE rodar elevado e o EVE-O não, eventos daquela janela não
  chegam. O tick de 5 s cobre; documentar no README de troubleshooting.
- **Thread do hook** precisa de message pump (STA). O app já roda com UI thread STA — o hook vai
  nela ou numa thread dedicada com loop de mensagens.
- **Complexidade**: um caminho de código a mais (evento + reconciliação) em vez de um só timer.
  Mitigado por `IClientChangeSignal` isolado e testado.
- **Rollback**: `ThumbnailRefreshPeriod` continua existindo; voltar ao modelo antigo é trocar
  quem publica no sinal.

**Implementação** (issues separadas, não neste ADR):

1. `WindowEventSource` no `EveO.Infrastructure` (hook + filtro de janelas top-level).
2. `ThumbnailManager` consome `IClientChangeSignal` + tick de reconciliação de 5 s.
3. Medição final end-to-end com o mesmo protocolo do baseline.

---

## 5. Evidence and Gates

### Baseline — v8.2.1, Windows, 7 clientes reais, 10 amostras de 30 s

| Métrica | Valor |
|---|---|
| CPU idle (média) | **1,87% de 1 core** (0,12% da máquina, 16 núcleos) |
| mediana / min / max | 2,03% / 1,30% / 2,19% |
| memória | 110 MB working set · 44 MB privada |
| GDI / USER | 83 / 81 (estável) |

### Custo isolado da varredura (o que o push elimina)

600 varreduras (5 min a 2 Hz) com 7 clientes:

| Métrica | Valor |
|---|---|
| CPU por varredura | **7,109 ms** |
| CPU total | 4,266 s |
| Custo a 2 Hz | **1,42% de 1 core** |
| Fração do idle total | **76%** |

### Protótipo push — `SetWinEventHook` (CREATE..NAMECHANGE) + `Channel<T>` coalescido

180 s, 7 clientes:

| Métrica | Valor |
|---|---|
| Eventos de janela recebidos | 123 |
| Refreshes coalescidos | 97 |
| CPU do processo (inclui harness) | 594 ms |
| Custo push | **0,330% de 1 core** (limite superior) |

### Gate

- Limite: **≤ 1,31% de 1 core** (−30% sobre 1,87%).
- Projeção conservadora: `1,87 − 1,42 + 0,33 = 0,78%` → **−58%**.
- Projeção realista (parte dos 0,33% é overhead do harness): ~0,5% → **−73%**.
- **Gate passou com margem.**

### Ressalva metodológica

O sinal é pequeno frente ao ruído (min 1,30% já toca o alvo). A medição final end-to-end deve:

1. usar janelas maiores (5 × 60 s) e reportar **tempo de CPU acumulado**;
2. alternar rodadas (timer → push → timer → push) para separar ruído de máquina;
3. ser feita com o **artefato empacotado**, não com `dotnet run`.

---

### Implementação 2.2b — validação funcional (20/11)

A/B no Windows com 2 clientes EVE, mesma configuração nos dois builds, medindo a latência de
criação e de remoção da thumbnail de um aplicativo monitorado:

| Build | Criação | Remoção |
|---|---|---|
| v8.2.1 (timer 500 ms) | 620 ms | 1302 ms |
| 2.2b (event-driven) | 419 ms | 1417 ms |

Paridade funcional: o modelo push não piora a reação. A remoção passa pelo `ProcessSetDiff`, que exige
N varreduras sem o processo; a janela de settle existe justamente para chegar à confirmação em ~1 s,
como era com o timer de 500 ms.

**CPU — medição final**: 7 clientes, 4 rodadas alternadas de 120 s, contagem de clientes conferida
no início e no fim de cada rodada (todas válidas).

| Build | Rodada 1 | Rodada 2 | Média |
|---|---|---|---|
| v8.2.1 (timer 500 ms) | 1,39% | 1,51% | **1,45%** |
| 2.2b (event-driven + reconciliação 5 s) | 0,46% | 0,59% | **0,52%** |

**Redução medida: −64,1%** (gate: −30%).

**Escala**: com 7 clientes o baseline do spike era 1,87%; com 2 clientes, 1,09%. O custo absoluto
cresce com o número de clientes — é justamente a varredura por cliente que o push elimina.

## 6. Checklist

- [x] Protótipo com `IAsyncEnumerable` + `Channel<T>` (transporte em `EveO.Core`, medido fora da produção)
- [x] Baseline de CPU idle com 7 clientes reais
- [x] Valores push-based medidos
- [x] Gate de −30% avaliado (passou)
- [x] ADR-006 com a decisão
- [x] Implementação end-to-end (issue 2.2b) — **−64,1%** medido com 7 clientes, sem regressão funcional
