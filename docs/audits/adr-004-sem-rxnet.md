# ADR-004 — Sem Rx.NET / Reactive Extensions na Fase 1

**Date**: 2026-11-14
**Decision**: Rx.NET não será adicionado ao código da Fase 1.
**Status**: Draft — revisão pós-spike 2.2

---

## 1. Context

A arquitetura atual do Eve-O-Preview utiliza `DispatcherTimer` para operações periodicas de atualização de thumbnails e monitoramento de processos. A equipe tem avaliado a introdução de Reactive Extensions (Rx.NET) para substituir padrões de polling por abordagens baseadas em eventos `IAsyncEnumerable` + `Channel<T>` ou observables.

**Motivação da decisão**:
- O principleo orientador da Fase 2 é *"Measure first, implement second"* (ver `docs/roadmap.md` Fase 2).
- Não há evidência técnica de que Rx.NET traria redução de CPU >30% sobre o timer atual (gate do spike 2.2, ver #H2).
- Adicionar Rx.NET introduz uma nova dependência e curva de aprendizado para a equipe.
- O código atual já funciona com `DispatcherTimer` e o pattern de UI-thread confinement está estabelecido (ADR-001).

**Decisão em conformidade com**: `docs/roadmap.md` Fase 2, item 2.2 (gate: redução >30% CPU idle comprovada) e item 1.7 (ADR-004: Sem Rx.NET na Fase 1).

---

## 2. Decision

**Rx.NET / Reactive Extensions não será adotado na Fase 1.**

A continuação do uso de `DispatcherTimer` para polling periodicamente permanece como a abordagem padrão. Caso o spike 2.2 demonstre que padrões reativos reduzem significativamente o uso de CPU idle, o ADR será revisado na Fase 3.

---

## 3. Alternatives Consideradas

| Alternative | Description | Why Rejected |
|---|---|---|
| **Rx.NET + IAsyncEnumerable** | Introduzir `Channel<T>`, `IAsyncEnumerable` e observables para fluxo event-driven de mudanças de processo e atualização de thumbnails. | — Não comprovado que reduz >30% CPU idle no spike 2.2. — Nova dependência e curva de aprendizado. — Código existente (`DispatcherTimer`) já funciona; refatoração trairia o princípio "measure first". |
| **Rx.NET com DispatcherTimer coexistente** | Manter timer e introduzir observables apenas para casos de uso específicos (ex: mudanças de processo). | — Fragmentação de patterns no código. — Sem benefício mensurado. — Aumento de complexidade sem ganho garantido. |
| **Manter DispatcherTimer** | Manter a abordagem atual, possivelmente otimizando o intervalo ou adicionando flags de controle. | — Atende ao gate da Fase 2 (medir primeiro). — Menor risco e mudança necessária. — Consistente com ADR-001 (confinamento à thread de UI). |

---

## 4. Consequences

| Area | Consequence |
|---|---|
| **Performance** | Sem redução de CPU garantida até que spike 2.2 seja executado e medido. O timer de 500ms continua como referência baseline (ver métricas `CPU idle (7 clientes)` em `docs/roadmap.md`). |
| **Complexidade** | Manter o pattern existente evita adicionar uma nova dependência e curva de aprendizado para a equipe. |
| **Manutenibilidade** | Código mais simples, consistente com a decisão ADR-001 (confinamento à thread de UI). Futuras revisões podem considerar Rx.NET se o spike 2.2 comprovar benefício. |
| **Gate para Fase 3** | Se o spike 2.2 comprovar redução >30% CPU idle, o ADR será revisado e Rx.NET poderá ser introduzido na Fase 3 ou posterior. |

---

## 5. Evidence

- **Spike 2.2**: Avaliar `DispatcherTimer` → Reactive Streams (ver `docs/roadmap.md` linha 94).
- **Finding #H2**: BenchmarkDotNet — `Timer CPU idle` (ver `docs/roadmap.md` linha 174).
- **Referência**: `docs/roadmap.md` Fase 2, item 2.2 gate e item 1.7 (ADR-004).

---

### Checklist para futuras mudanças

- [ ] Se nova proposta de pattern reativo surgir, deve passar pelo spike 2.2 com gate >30% CPU reduction.
- [ ] Qualquer introdução de Rx.NET fora do gate deve ser documentada como nova decisão ADR.
- [ ] Revisar este ADR ao final do spike 2.2.