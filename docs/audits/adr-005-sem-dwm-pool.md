# ADR-005 — Sem Pooling de DWM na Fase 1

**Date**: 2026-11-14
**Decision**: Pool de `DwmThumbnail` não será implementado na Fase 1.
**Status**: Draft — revisão pós-spike 2.4

---

## 1. Context

O sistema atual utiliza `DwmRegisterThumbnail`/`DwmUpdateThumbnail` para gerar thumbnails de clientes EVE Online. Cada chamada `Register` aumenta o contador de handles DWM abertos. A ideia de pooling seria reutilizar handles registrados em vez de registrar/desregistrar repetidamente, o que poderia reduzir o número total de handles abertos simultaneamente e evitar possíveis limites de handles do Windows/DWM.

**Motivação da decisão**:
- O principleo orientador da Fase 2 é *"Measure first, implement second"* (ver `docs/roadmap.md` Fase 2).
- Não há evidência de que o limite de handles DWM seja um gargalo real (gate do spike 2.4, ver #H3).
- O código atual funciona corretamente registrando/desregistrando thumbnails por ciclo; não foram relatados erros de "handle limit" em produção.
- Adicionar pooling introduz complexidade de sincronização e risco de falhas `DwmRegisterThumbnail` sob carga (ver riscos do ADR-008).

**Decisão em conformidade com**: `docs/roadmap.md` Fase 2, item 2.4 (gate: limite real de handles DWM comprovado #H3 OU latency >30ms) e item 1.7 (ADR-005: Sem DWM pooling na Fase 1).

---

## 2. Decision

**Pool de `DwmThumbnail` não será implementado na Fase 1.**

A abordagem atual de registrar e desregistrar thumbnails por ciclo permanece. Se o spike 2.4 demonstrar que o limite de handles DWM é um gargalo real ou latency >30ms, o ADR será revisado para considerar pooling na Fase 3.

---

## 3. Alternatives Consideradas

| Alternative | Description | Why Rejected |
|---|---|---|
| **Thumbnail Pool** | Implementar `ThumbnailPool : IThumbnailPool` com métodos `Rent()`/`Return()` para reutilizar handles `DwmRegisterThumbnail` em vez de criar novos a cada ciclo. | — Não comprovado que limite de handles seja gargalo no spike 2.4. — Introduz complexidade de sincronização e risco de falhas `DwmRegisterThumbnail` sob carga (ver consequences). — Gate não atingido: handle limit real não comprovado (#H3). |
| **Manter abordagem atual (register/unregister)** | Manter o pattern existente de registrar thumbnail a cada ciclo de refresh. | — Atende ao gate da Fase 2 (medir primeiro). — Menor risco, código mais simples. — Consistente com a filosofia *"Measure first, implement second"*. |
| **Hybrid: pool apenas em carga alta** | Pool parcial — reutilizar handles quando número de clientes for alto, registrar novos otherwise. | — Complexidade adicional sem benefício mensurado. — Mais difícil de testar e validar. — Não justifica-se sem evidência do spike 2.4. |

---

## 4. Consequences

| Area | Consequence |
|---|---|
| **Performance** | Sem otimização de handles DWM garantida até que spike 2.4 seja executado e medido. O pattern atual continua com overhead de register/desregister a cada ciclo. |
| **Complexidade** | Manter a abordagem atual evita adicionar lógica de pooling e sincronização, mantendo o código mais simples e de mais fácil manutenção. |
| **Handling of Handles** | O número máximo de handles DWM abertos simultâneos seguirá o pattern atual; se o limite for atingido em cenários extremos, será necessário reavaliar. |
| **Gate para Fase 3** | Se o spike 2.4 comprovar #H3 (limite real de handles DWM) ou latency >30ms, o ADR será revisado e pooling poderá ser introduzido na Fase 3 ou posterior. |

---

## 5. Evidence

- **Spike 2.4**: Avaliar pool de `DwmThumbnail` (ver `docs/roadmap.md` linha 96).
- **Finding #H3**: DWM handle limit (ver `docs/roadmap.md` linha 175).
- **Referência**: `docs/roadmap.md` Fase 2, item 2.4 gate e item 1.7 (ADR-005).

---

### Checklist para futuras mudanças

- [ ] Se nova proposta de pooling surgir, deve passar pelo spike 2.4 com gate: #H3 comprovado OU latency >30ms.
- [ ] Qualquer introdução de pooling fora do gate deve ser documentada como nova decisão ADR.
- [ ] Revisar este ADR ao final do spike 2.4.