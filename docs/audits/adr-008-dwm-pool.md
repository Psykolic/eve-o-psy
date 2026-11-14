# ADR-008 — Avaliar Pool de DwmThumbnail no Spike 2.4

**Date**: 2026-11-14
**Decision**: Decisão pendente de medição do spike 2.4. O gate define se pool de thumbnails será implementado.
**Status**: Draft — aguardando resultados do spike 2.4

---

## 1. Context

O sistema atual registra e desregistra `DwmThumbnail` a cada ciclo de refresh, o que pode abrir e fechar handles DWM repetidamente. O spike 2.4 visa avaliar a implementação de um pool de `DwmThumbnail` — reutilizar handles registrados em vez de criar novos a cada ciclo, o que poderia reduzir o número total de handles abertos simultaneamente e evitar possíveis limites de handles do Windows/DWM.

**Gate do spike 2.4** (ver `docs/roadmap.md` Fase 2, item 2.4):
- Medir: handles DWM abertos, latency refresh, falhas `DwmRegisterThumbnail` sob carga.
- **Gate**: Só prossegue se limite real de handles DWM comprovado (#H3) **OU** latency refresh >30ms comprovado.
- Se gate não passar: descontinuar ideia de pool e documentar decisão neste ADR.

**Referências**: #H3 (DWM handle limit), `docs/roadmap.md` Fase 2 items 2.4 e 1.7 (ADR-005).

---

## 2. Decision (Pending Measurement)

A decisão sobre a implementação de pool de `DwmThumbnail` será definida após a execução do spike 2.4 com as métricas solicitadas. Até então:

- **Manter abordagem atual** de registrar/desregistrar thumbnails a cada ciclo.
- **Documentar baseline** de handles DWM e latency refresh antes do spike para comparação.
- **Caso gate seja passado** (limite de handles comprovado OU latency >30ms): revisar este ADR e implementar `ThumbnailPool : IThumbnailPool` com `Rent()`/`Return()`.
- **Caso gate não seja passado**: descontinuar ideia de pool, manter abordagem atual, documentar que pooling não trouxe benefício mensurado.

---

## 3. Alternatives (A Serem Avaliadas Pós-Spike)

| Alternative | Description | Criteria for Selection |
|---|---|---|
| **Thumbnail Pool** | Implementar `ThumbnailPool : IThumbnailPool` com métodos `Rent()`/`Return()` para reutilizar handles `DwmRegisterThumbnail` em vez de criar novos a cada ciclo. | Limite real de handles DWM comprovado (#H3) OU latency refresh >30ms comprovado no spike 2.4. Redução de handles abertos simultâneos. |
| **Manter abordagem atual (register/unregister)** | Manter o pattern existente de registrar thumbnail a cada ciclo de refresh. | Gate não passado: limite de handles não é gargalo OU latency ≤30ms. Menor risco, código mais simples. |
| **Hybrid: pool condicional** | Pool apenas em cenários de alta carga (muitos clientes), register/unregister otherwise. | Benefício mensurado em carga alta; adiciona complexidade condicional sem ganho global. |

---

## 4. Consequences (Dependendo do Resultado do Spike)

| Scenario | Consequence |
|---|---|
| **Gate passado — Handle limit comprovado ou latency >30ms** | - Implementar `ThumbnailPool` com `Rent()`/`Return()` pattern.<br>- Atualizar `ThumbnailManager` para usar pool em vez de register/desregister direto.<br>- Gerenciar ciclo de vida do pool (init no startup, cleanup no stop).<br>- Atualizar métricas em `docs/roadmap.md` (Fase 2 Target: DWM handles controlled, refresh latency <30ms).<br>- Atualizar ADR-005 (Sem DWM pooling) se pool for adotado. |
| **Gate não passado — Nem handle limit nem latency >30ms** | - Manter abordagem atual de register/unregister.<br>- Documentar que pooling não trouxe benefício mensurável.<br>- Considerar outras otimizações (ex: límites de pool, retry logic no registro).<br>- Manter ADR-008 vigente como "gate não passado" e ADR-005 vigente (Sem DWM pooling). |

---

## 5. Evidence and Gates

- **Baseline measurement**: Registrar número de handles DWM abertos, latency refresh médio, e falhas `DwmRegisterThumbnail` sob carga de 7 clientes antes do spike.
- **Pool prototype**: Implementar `ThumbnailPool : IThumbnailPool` com `Rent()`/`Return()` e medir comportamentos comparados ao pattern atual.
- **Gate thresholds**:
  - #H3 (DWM handle limit) comprovado: número máximo de handles abertos simultâneos identificado como gargalo.
  - latency refresh >30ms comprovado: latência média de refresh excede 30ms no cenário de 7 clientes.
- **Spike duration**: 2 dias (ver `docs/roadmap.md` Fase 2, item 2.4).
- **Output**: Protótipo funcionando + medição registrada + decisão documentada neste ADR.

---

### Checklist para futuras mudanças

- [ ] Executar spike 2.4 com medição de handles DWM baseline e latency refresh.
- [ ] Verificar se #H3 (handle limit) é comprovado OU latency >30ms é comprovado.
- [ ] Se sim, implementar ThumbnailPool e revisar ADR-005.
- [ ] Se não, manter abordagem atual e fechar este ADR como "gate não passado".
- [ ] Atualizar `docs/roadmap.md` targets conforme resultado.