# ADR-007 — Avaliar Virtualização de Thumbnails no Spike 2.3

**Date**: 2026-11-14
**Decision**: Decisão pendente de medição do spike 2.3. O gate define se virtualização de thumbnails será implementada.
**Status**: Draft — aguardando resultados do spike 2.3

---

## 1. Context

O Eve-O-Preview atualmente renderiza thumbnails para todos os clientes EVE visíveis, independentemente de estarem no viewport ou não. O spike 2.3 visa avaliar se a virtualização — renderizar apenas thumbnails visíveis e ocultar/optimizar os demais — reduz o uso de memória em mais de 20% em um cenário de 7 clientes EVE.

**Gate do spike 2.3** (ver `docs/roadmap.md` Fase 2, item 2.3):
- Medir memória/CPU 7 clientes: baseline vs virtualizado.
- **Gate**: Só prossegue se redução >20% memória comprovada.
- Se gate não passar: manter implementação atual e documentar decisão neste ADR.

**Referências**: #H6 (Virtualization benefit), #5.1 (Zoom on hover perf), `docs/roadmap.md` Fase 2 items 2.3 e 1.7.

---

## 2. Decision (Pending Measurement)

A decisão sobre a adoção de virtualização de thumbnails será definida após a execução do spike 2.3 com as métricas solicitadas. Até então:

- **Manter renderização completa** para todos os clientes visíveis.
- **Documentar baseline** de memória antes do spike para comparação.
- **Caso gate seja passado** (redução >20% memória): revisar este ADR e implementar `VirtualizingThumbnailPanel` ou lógica equivalente.
- **Caso gate não seja passado**: manter implementação atual, documentar que virtualização não trouxe benefício mensurado.

---

## 3. Alternatives (A Serem Avaliadas Pós-Spike)

| Alternative | Description | Criteria for Selection |
|---|---|---|
| **VirtualizingThumbnailPanel (WPF)** | Painel WPF que renderiza apenas itens visíveis e oculta/descarrega os demais. | Redução >20% memória comprovada no spike 2.3. Compatibilidade com layout atual de thumbnails. |
| **Ocultar thumbnails fora do viewport** | Lógica para ocultar/DwmUnregisterThumbnail thumbnails que estão fora da área visível, mantendo-os em estado "pausado". | Redução >20% memória comprovada. Menor mudança de código comparado a painel virtualizing full. |
| **Manter renderização completa** | Manter a abordagem atual de renderizar todos os thumbnails visíveis. | Gate não passado (>20% redução não comprovada). Menor risco, código existente validado. |

---

## 4. Consequences (Dependendo do Resultado do Spike)

| Scenario | Consequence |
|---|---|
| **Gate passado — Redução >20% memória** | - Implementar virtualização de thumbnails (painel ou lógica de ocultar).<br>- Atualizar `ThumbnailManager` para gerenciar estado de visibilidade por cliente.<br>- Revisar `EnsureUiThread` e bounds checking para novos caminhos de código.<br>- Atualizar métricas em `docs/roadmap.md` (Fase 2 Target: Memória <100MB para 7 clientes). |
| **Gate não passado — Redução ≤20% memória** | - Manter renderização completa para todos os clientes.<br>- Documentar que virtualização não trouxe benefício mensurável neste cenário.<br>- Considerar outras otimizações (ex: redução de resolução, caching mais agressivo).<br>- Manter ADR-007 vigente como "gate não passado". |

---

## 5. Evidence and Gates

- **Baseline measurement**: Registrar uso de memória com 7 clientes em estado baseline (renderização completa).
- **Virtualized prototype**: Implementar protótipo de `VirtualizingThumbnailPanel` ou lógica de ocultar thumbnails fora do viewport.
- **Gate threshold**: Redução >20% em memória comparando baseline vs virtualizado.
- **Spike duration**: 2 dias (ver `docs/roadmap.md` Fase 2, item 2.3).
- **Output**: Protótipo funcionando + medição registrada + decisão documentada neste ADR.

---

### Checklist para futuras mudanças

- [ ] Executar spike 2.3 com medição de memória baseline e virtualizada.
- [ ] Verificar se redução >20% memória é atingida.
- [ ] Se sim, revisar implementação e introduzir virtualização.
- [ ] Se não, manter renderização completa e fechar este ADR como "gate não passado".
- [ ] Atualizar `docs/roadmap.md` targets de memória conforme resultado.