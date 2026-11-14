# ADR-006 — Avaliar Reactive Streams (IAsyncEnumerable + Channel<T>) no Spike 2.2

**Date**: 2026-11-14
**Decision**: Decisão pendente de medição do spike 2.2. O gate define se Rx.NET/Reactive Streams substituem o DispatcherTimer.
**Status**: Draft — aguardando resultados do spike 2.2

---

## 1. Context

O Eve-O-Preview utiliza atualmente `DispatcherTimer` com intervalo de 500ms para polling periódico de mudanças de processo e atualização de thumbnails (ver `ThumbnailManager.cs` — `_thumbnailUpdateTimer`). O spike 2.2 visa avaliar se a substituição por Reactive Streams (`IAsyncEnumerable` + `Channel<T>` ou Rx.NET) reduz o CPU idle em mais de 30% em um cenário de 7 clientes EVE.

**Gate do spike 2.2** (ver `docs/roadmap.md` Fase 2, item 2.2):
- Medir CPU idle 7 clientes: baseline (DispatcherTimer 500ms) vs push-based.
- **Gate**: Só prossegue se redução >30% CPU idle comprovada.
- Se gate não passar: manter DispatcherTimer e documentar decisão neste ADR.

**Referências**: #H2 (Timer CPU idle), `docs/roadmap.md` Fase 2 items 2.2 e 1.7.

---

## 2. Decision (Pending Measurement)

A decisão sobre a adoção de Reactive Streams será definida após a execução do spike 2.2 com as métricas solicitadas. Até lá:

- **Manter DispatcherTimer** como abordagem atual.
- **Documentar baseline** de CPU idle antes do spike para comparação.
- **Caso gate seja passado** (redução >30% CPU idle): revisar este ADR e introduzir `IAsyncEnumerable` + `Channel<T>` ou Rx.NET.
- **Caso gate não seja passado**: manter DispatcherTimer, documentar que otimização reativa não trouxe benefício mensurado.

---

## 3. Alternatives (A Serem Avaliadas Pós-Spike)

| Alternative | Description | Criteria for Selection |
|---|---|---|
| **IAsyncEnumerable + Channel<T>** | Produz-consumidor usando `System.Channels.Channel` e `IAsyncEnumerable` para fluxo event-driven de alterações de processo e atualização de thumbnails. | Redução >30% CPU idle comprovada no spike 2.2. Sem bloqueio no UI thread (operam via `await` no Dispatcher). |
| **Rx.NET (IObservable)** | Introduzir observables `IObservable<T>` com operadores para transformar eventos de processo em atualizações de thumbnail. | Redução >30% CPU idle comprovada. Curva de aprendizado da equipe considerada aceitável. |
| **Manter DispatcherTimer** | Manter a abordagem atual, possivelmente ajustando o intervalo ou adicionando otimizações locais. | Gate não passado (>30% redução não comprovada). Menor risco, código existente validado. |

---

## 4. Consequences (Dependendo do Resultado do Spike)

| Scenario | Consequence |
|---|---|
| **Gate passado — Redução >30% CPU idle** | - Substituir DispatcherTimer por `IAsyncEnumerable` + `Channel<T>` ou Rx.NET.<br>- Atualizar `ThumbnailManager` para consumir streams em vez de timer tick.<br>- Revisar ADR-004 (Sem Rx.NET) se Rx.NET for adotado.<br>- Atualizar métricas em `docs/roadmap.md` (Fase 2 Target: CPU idle <2%). |
| **Gate não passado — Redução ≤30% CPU idle** | - Manter DispatcherTimer como padrão.<br>- Documentar que abordagens reativas não trouxeram benefício mensurável neste cenário.<br>- Manter ADR-004 vigente (Sem Rx.NET na Fase 1).<br>- Considerar outras otimizações (ex: ajuste de intervalo, GDI optimizations). |

---

## 5. Evidence and Gates

- **Baseline measurement**: Registrar `CPU idle` com 7 clientes e `DispatcherTimer` interval 500ms antes do spike.
- **Push-based prototype**: Implementar prototype com `ProcessMonitor` push-based (event-driven process changes).
- **Gate threshold**: Redução >30% em `CPU idle` comparando baseline vs push-based.
- **Spike duration**: 2 dias (ver `docs/roadmap.md` Fase 2, item 2.2).
- **Output**: Protótipo funcionando + medição registrada + decisão documentada neste ADR.

---

### Checklist para futuras mudanças

- [ ] Executar spike 2.2 com medição de CPU idle baseline e push-based.
- [ ] Verificar se redução >30% CPU idle é atingida.
- [ ] Se sim, revisar ADR-004 e introduzir Reactive Streams.
- [ ] Se não, manter DispatcherTimer e fechar este ADR como "gate não passado".
- [ ] Atualizar `docs/roadmap.md` targets de CPU idle conforme resultado.