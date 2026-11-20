# ADR-007 — Virtualização de thumbnails no spike 2.3

**Date**: 2026-11-20
**Decision**: **Gate não passou.** Manter a renderização atual; não implementar virtualização.
**Status**: Accepted — spike 2.3 concluído com medição em 7 clientes reais

---

## 1. Context

O objetivo do spike era avaliar se renderizar apenas as thumbnails visíveis reduziria a memória em
mais de 20% com 7 clientes EVE.

Duas observações que mudam a premissa:

1. **Não existe viewport.** O EVE-O Preview cria exatamente uma janela de thumbnail por cliente; não há
   lista rolável nem painel virtualizável. "Virtualizar" aqui significaria deixar de criar a janela de
   clientes que o usuário não está vendo — mas o layout do multiboxing é justamente mostrar todos.
2. **O gate de −20% precisa caber no que é virtualizável.** Se as thumbnails custarem menos de 20% da
   memória do processo, o gate é inalcançável por construção, independentemente da implementação.

---

## 2. Decision

**Não implementar virtualização.** Manter uma view por cliente.

A medição mostra que **todas** as thumbnails juntas respondem por 12,0% do working set (18,7% da
memória privada). Eliminar 100% delas ainda ficaria abaixo do gate de −20%.

---

## 3. Evidence and Gates

### Medição (v8.2.2, Windows, 7 clientes `exefile` reais)

Mesmo binário, mesma config, variando apenas `ExecutablesToPreview` para produzir 7 ou 0 thumbnails:

| Cenário | Working set | Privada | GDI | Thumbnails |
|---|---|---|---|---|
| 7 clientes monitorados | **99,9 MB** | **36,8 MB** | 80 | 7 (+7 overlays) |
| Nenhum cliente monitorado | 87,9 MB | 29,9 MB | 47 | 0 |

| Métrica | Valor |
|---|---|
| Custo das 7 thumbnails | **12,0 MB WS** / **6,9 MB privada** |
| Custo por thumbnail | ~1,7 MB WS / ~1,0 MB privada |
| Peso no processo | **12,0% do WS** / **18,7% do privado** |
| **Gate (−20% de memória)** | **inalcançável: o máximo teórico é −12%** |

O piso de 87,9 MB sem nenhuma thumbnail é o runtime .NET (WinForms + WPF) e o interop DWM — não é
memória das thumbnails e nenhuma virtualização o reduz.

### Projeção para mais clientes

A ~1,7 MB por cliente, 15 clientes custariam ~25 MB de 115 MB (~22%). Mesmo nesse cenário extremo o
ganho dependeria de o usuário **não ver** as thumbnails — o que contraria o uso.

---

## 4. Consequences

**Mantido:**

- uma view por cliente, com DWM thumbnail ao vivo;
- sem `VirtualizingThumbnailPanel`, sem lógica de "pausar" thumbnails fora de tela.

**O que este spike descarta para o futuro:**

- não vale abrir issue de virtualização com o gate de memória atual. Se o objetivo for reduzir memória
  de verdade, o caminho é outro: `WineCompatibilityMode`, cache de capturas GDI estáticas, ou reduzir o
  footprint do host (WinForms + WPF no mesmo processo).

**Ação tomada em vez de virtualização:** nada no código — o spike 2.2 (#175) já reduziu o custo de CPU
que acompanhava o refresh, que era o problema real medido.

### Ressalva

Medição feita no Windows com 7 clientes reais (mesmo ambiente do baseline de CPU da 2.2), não na máquina
Linux de jogo como previa a issue. O número de memória do processo não depende do backend gráfico para a
conclusão — o gate falha por duas ordens de grandeza de oportunidade, não por margem apertada.

---

## 5. Checklist

- [x] Baseline de memória com 7 clientes reais
- [x] Custo isolado das thumbnails medido (7 vs 0)
- [x] Gate de −20% avaliado — **não passou** (máximo teórico −12%)
- [x] ADR-007 com a decisão
- [x] Sem mudança de código (decisão: manter)
