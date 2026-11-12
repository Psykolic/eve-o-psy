# TM-04 — Manual Windows 11 6+1 Validation Checklist

**Escopo atual.** O ajuste de captura estática foi separado em [`separate/tm04-gdi`](https://github.com/Psykolic/eve-o-custom/tree/separate/tm04-gdi). Os cenários G1–G4 pertencem à revisão de GDI e ficam aqui como histórico. A validação guiada 6+1 foi executada antes da separação; o CI do PR #16 valida o novo commit.

**Data:** 2026-11-11  
**Branch:** `claude/issue-15` (PR #16)  
**Commits incluídos:** 
- `6a568a1` — fix(TM-04): harden thumbnail activation against stale clients
- `059f3e5` — test(TM-04): correct activation harness nullable annotations
- `69c15be` — fix(TM-04): harden thumbnail activation and add GDI cleanup
- `d669774` — **fix(TM-04): lifecycle, cancellation, and ownership fixes per Codex review**

---

## Preparação do Ambiente

| Item | Detalhes |
|------|----------|
| **SO** | Windows 11 (build/versão exata) |
| **GPU/Driver** | Modelo + versão do driver |
| **Monitores** | Primário + secundário (resolução/DPI) |
| **Modo Janela EVE** | Windowed / Borderless / Fullscreen |
| **EVE Version** | Build exato do cliente |
| **SHA/Binário** | Commit SHA ou tag do build testado |
| **Config EVE-O** | Refresh period, animação, minimização, layout tracking, zoom, click-through |
| **Clientes** | A–F no monitor principal, I (intel) no secundário |
| **Grupo de Ciclo** | A–F apenas; I explicitamente excluído |
| **Prioridade I** | Configurada conforme uso real |

> **Backup:** Salvar configuração antes de iniciar. Ambiente seguro no jogo (estação, docked).

---

## Casos de Validação

| ID | Caso | Procedimento | Critério de Aceite | Status | Evidência |
|----|------|--------------|-------------------|--------|-----------|
| **M1** | Básico | Após 30s ocioso, clicar A–F e I, um por vez; repetir 3x | Janela correta em foreground, destaque coerente após estabilizar | ☐ | Screenshot/vídeo |
| **M2** | Ciclo | 10 voltas p/ frente + 10 p/ trás (hotkeys grupo A–F) | Ordem preservada; I nunca entra no grupo, permanece no secundário | ☐ | Log/keystrokes |
| **M3** | Alternância Rápida | 5 min cliques A–F intercalando I; rajadas A/B/A; aguardar estabilização | Sem crash/travamento; anotar divergência último clique vs foco vs destaque | ☐ | Vídeo com timestamps |
| **M4** | Fechamento | Durante alternância, fechar B normalmente; repetir com kill de processo | View desaparece após refresh; latência registrada; restantes continuam; sem destaque fantasma | ☐ | Latência, vídeo |
| **M5** | Reabertura | Reabrir cliente fechado, aguardar registro, clicar nova miniatura | Nova view ativa corretamente; não presumir reutilização de handle | ☐ | Screenshot |
| **M6** | Intel | Ativar I e voltar a A; repetir durante ciclo e com minimização | I mantém monitor/layout esperado e exclusão do ciclo; prioridade respeitada | ☐ | Vídeo |
| **M7** | Configurações | Repetir M1/M3 com layout tracking + minimização do operador | Sem movimentação/minimização inesperada; foco/destaque convergem | ☐ | Por combinação |
| **M8** | Recuperação | Após M4/M5, 1 min ocioso, repetir M1/M2 | Callbacks e miniaturas restantes respondem | ☐ | - |
| **M9** | Encerramento | Fechar EVE-O durante alternância e reiniciar com 7 clientes | Sem erro visível; configuração restaurada; registrar eventos/logs | ☐ | Event Viewer, Debug output |

---

## Validação Específica TM-04 (Race Conditions)

| ID | Cenário | Como Testar | Critério TM-04 |
|----|---------|-------------|----------------|
| **R1** | Click em thumbnail de cliente já fechado | Fechar cliente via Task Manager, clicar rapidamente sua thumbnail antes do refresh | Sem crash, sem exceção, thumbnail desaparece no próximo ciclo |
| **R2** | Click rápido A→B→A (mesmo handle reutilizado) | Fechar A, reabrir A (mesmo handle possível), clicar thumbnail A antiga/nova | Ativação correta da view atual; view antiga não muta estado |
| **R3** | `ActivateWindow` falha (simulado) | Requer build Debug + debugger; breakpoint em `ActivateWindow` + throw manual | Debug output: `ThumbnailActivated: ActivateWindow failed...`; sem ghost active client |
| **R4** | View oculta mas válida ativada | Minimizar cliente, clicar thumbnail | Ativação completa (IsActive=false mas IsDisposed=false permite) |
| **R5** | View descartada (IsDisposed=true) | Fechar cliente, callback já enfileirado roda antes do detach TM-01 | Sem mutação de estado; continuação aborta no guard IsDisposed |

---

## Validação GDI (Port dos Fixes e27de2c + 8b97899)

| ID | Cenário | Como Testar | Critério GDI |
|----|---------|-------------|--------------|
| **G1** | Stress 1000+ capturas estáticas | Script automatizado ou manual: capturar thumbnail estático em loop | Task Manager / `gdi.exe` handles estáveis; sem vazamento HDC/HBITMAP |
| **G2** | Cliente fecha durante captura | Fechar cliente EVE enquanto `GetStaticThumbnail` roda (race) | Exceção capturada, handles liberados, app continua |
| **G3** | DWM indisponível (troca usuário) | Trocar usuário Windows durante captura DWM (LiveThumbnail) | `COMException` capturada, handles liberados |
| **G4** | Fonte inválida (handle 0) | Chamar `GetStaticThumbnail(IntPtr.Zero)` | Retorna `null` sem crash, sem leak |

---

## Coleta de Evidência Obrigatória

- **Vídeo** com foco/destaque visíveis + timestamps
- **Configuração** exportada (sem dados sensíveis)
- **Event Viewer** (Application / System) filtrado por EVE-O-Preview
- **Debug Output** (Debug build sob debugger) — procurar por:
  - `ThumbnailActivated: ActivateWindow failed...`
  - Qualquer `Debug.WriteLine` do WindowManager
- **Task Manager** → Details → "GDI Objects" column (monitorar durante G1)
- **Logs** de exceção não tratada (se houver)

---

## Riscos Conhecidos / Não Validados (Linux)

| Item | Descrição | Mitigação |
|------|-----------|-----------|
| **Build Windows** | Não compilado/testado localmente | CI Windows deve passar |
| **Testes TM-04** | 8 testes (5 originais + hidden + disposed + faulted + cancelled-doc); requerem `net8.0-windows` + WPF/WinForms | Executar em Windows CI / agent Windows |
| **Testes TM-01** | 3 testes em `ThumbnailManagerStabilityTests.cs` (branch separada); integração não testada | Build combinado + execução 8+3=11 testes |
| **DWM/SetForegroundWindow** | Focus-stealing rules Windows 11 não testadas | Validação manual M3, R3 |
| **GDI real** | Stress 1000+ capturas não executado | Validação manual G1–G4 |
| **Multi-monitor DPI** | Escala diferente entre monitores não testada | Validação manual M6 |
| **CancellationToken** | Não propagado para `ActivateWindow` + `Task.Run` (item futuro) | Teste atual documenta comportamento Faulted; IsCanned branch documentado |
| **Política ordenação A/B** | Não implementada; última solicitação pode não vencer | Documentado; decisão do mantenedor necessária |

---

## Política de Ordenação A/B (Documentada - NÃO Implementada)

**Estado atual:** Não há serialização/geração de solicitações. Duas ativações concorrentes A e B (ambas válidas, ambas no dicionário) podem completar em qualquer ordem.

**Comportamento observado:**
- Continuation que completa primeiro chama `SwitchActiveClient` e `RefreshThumbnails`
- Continuation que completa depois vê `currentView.IsDisposed == false` e `ReferenceEquals == true`, então **também** muta estado
- Resultado: "última continuação a completar vence" (não necessariamente "último clique")

**Opções (requer decisão do mantenedor):**
1. **Last-click-wins:** Adicionar `requestGeneration` (int/long) capturado na UI thread, passado ao worker, revalidado no continuation. Só commita se geração == atual.
2. **First-completes-wins:** Comportamento atual (não determinístico devido a thread pool).
3. **Queue/serialize:** Fila de ativações em UI thread; processa uma por vez.

**Decisão pendente:** Sem autorização para implementar. Documentado para revisão.

---

## Critérios de "Go/No-Go" para Merge

| Critério | Obrigatório? | Status |
|----------|-------------|--------|
| CI Windows: build + **todos os 8 testes TM-04 passam** | ✅ Sim | ⏳ Aguardando Actions |
| CI Windows: **3 testes TM-01 passam** (merge combinado) | ✅ Sim | ⏳ Merge descartável necessário |
| Zero falhas/skips no CI | ✅ Sim | ⏳ |
| Validação manual M1–M9 **todos Passed** | ✅ Sim | ☐ Pendente |
| Validação GDI G1–G4 | Fora do escopo TM-04 | Acompanhar na revisão de GDI |
| Validação Race R1–R5 **todos Passed** | ✅ Sim | ☐ Pendente |
| Zero "Blocked" sem plano de resolução | ✅ Sim | ⏳ |
| Política de ordenação A/B decidida/documentada | ✅ Sim | 📝 Documentada, não implementada |

> **Não autorizar merge** com casos pendentes ou política de ordenação indefinida.

---

## Próximos Passos Pós-Validação

1. **Resolver upload TRX failure** no CI (run 43094658870 — resolvido no run atual)
2. **Logger compartilhado** — substituir `Debug.WriteLine` por abstração injetável
3. **CancellationToken** propagado para `ActivateWindow` + `Task.Run` (cobertura real IsCanceled)
4. **Auditoria completa** pares `DisableViewEvents`/`EnableViewEvents` (4+ restantes)
5. **Extrair `ThumbnailActivated`** para classe testável (remover reflection dos testes)
6. **Testes GDI fault injection** com fronteira nativa substituível
7. **Merge combinado TM-01 + TM-04** + execução 11 testes
