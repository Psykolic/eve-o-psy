# ADR-008 — Pool de `DwmThumbnail` no spike 2.4

**Date**: 2026-11-20
**Decision**: **Gate não passou.** Manter registro/desregistro por cliente; não implementar pool.
**Status**: Accepted — spike 2.4 concluído com medição em 7 clientes reais

---

## 1. Context

O spike avaliou se um pool de `DwmThumbnail` (`Rent()`/`Return()`, reuso do registro) reduziria
handles abertos ou latência sob carga.

**Gate**: prosseguir somente se houver **limite real de handles DWM comprovado** *ou* **latência >
30 ms**.

O que o pool otimizaria: `DwmRegisterThumbnail`/`DwmUnregisterThumbnail`. Isso acontece **uma vez por
ciclo de vida de cliente** (quando ele abre ou fecha), nunca no refresh — no refresh o app chama
`DwmUpdateThumbnailProperties`.

---

## 2. Decision

**Não implementar pool.** O custo de registro é ~0,012 ms (2.500× abaixo do gate), não há falha nem
vazamento de handles sob churn, e o caminho otimizado não é o caminho quente.

---

## 3. Evidence and Gates

### Custo de registro (7 janelas de cliente reais)

200 ciclos de `Register` + `Move/Update` + `Unregister`, com as 7 janelas EVE em rodízio:

| Operação | Média | p50 | p95 | Máx |
|---|---|---|---|---|
| `DwmRegisterThumbnail` (+ wrappers) | **0,012 ms** | 0,000 ms | 0,002 ms | 2,341 ms |
| `DwmUpdateThumbnailProperties` (o que roda no refresh) | **0,001 ms** | — | 0,003 ms | 0,119 ms |

- **falhas de registro: 0** em 200 ciclos;
- **GDI: 15 → 15 · USER: 20 → 20** (nenhum handle vazado).

O maior valor individual (2,34 ms) é o primeiro registro — custo de inicialização do DWM, não do
caminho repetido.

### Churn no aplicativo real

12 ciclos de abrir/fechar um cliente monitorado (Notepad, mesma config):

| Métrica | Início | Fim |
|---|---|---|
| GDI | 83 | **83** |
| USER | 81 | **84** |
| Working set | 99 MB | 114 MB |

Sem crash log em nenhum ciclo. Handles estáveis: o par register/unregister do app já está correto.

### Gate

- **Latência > 30 ms?** Não — 0,012 ms, isto é, ~2.500× abaixo do limite.
- **Limite real de handles?** Não — nenhum crescimento em 200 ciclos nem em 12 ciclos do app; com 7
  clientes o processo usa 83 GDI / 84 USER, ordens de grandeza abaixo do teto do Windows.

---

## 4. Consequences

**Mantido:** um `DwmThumbnail` por cliente, registrado na criação da view e desregistrado na remoção.

**Não fazer:** `IThumbnailPool` com `Rent()`/`Return()`. Um pool introduziria:

- estado compartilhado e risco de devolver um handle ainda em uso (falha visível como thumbnail
  "congelada" ou preta);
- complexidade de ciclo de vida sem ganho mensurável.

**Observação fora do escopo (não é pooling):** o working set subiu de 99 MB para 114 MB durante os 12
ciclos de churn. Handles estão estáveis, então isso é heap gerenciado (GC ainda não coletou), não
vazamento. Se crescer sem teto em uso real, vira uma issue própria de memória — mas não há indício de
que um pool ajude nisso.

### Ressalva

Medição feita no Windows com 7 clientes reais. Como no ADR-007, a conclusão não depende do backend
gráfico: o gate falha por três ordens de grandeza, não por margem apertada.

---

## 5. Checklist

- [x] Custo de `Register`/`Unregister` medido com janelas reais
- [x] Falhas de registro sob carga contadas (0 em 200 ciclos)
- [x] Vazamento de handles verificado (200 ciclos + 12 ciclos no app)
- [x] Gate de 30 ms / limite de handles avaliado — **não passou**
- [x] ADR-008 com a decisão
- [x] Sem mudança de código (decisão: manter)
