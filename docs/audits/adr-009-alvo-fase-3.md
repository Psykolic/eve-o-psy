# ADR-009 — Alvo da Fase 3: ficar no Windows, oficializar o caminho Wine, ou reescrever a UI

**Status**: Accepted (2026-11-21)
**Data**: 2026-11-21
**Substitui**: nada. Complementa ADR-006, 007 e 008.

## 1. Context

A Fase 3 do roadmap pede "cross-platform & extensibilidade" (3.1 PoC Avalonia/Uno, 3.2 abstração de
plataforma, 3.3 plugins, 3.4 event sourcing, 3.5 CI multi-plataforma, 3.6 plugin SDK). Antes de
escrever código, vale registrar **o que realmente impede** o app de rodar fora do Windows.

Números do repositório em 2026-11-21:

| Fato | Valor |
|---|---|
| Linhas de produção (C#) | **13.464** |
| Linhas em `EveO.Presentation` | **8.387** (62%) |
| Arquivos com WinForms | **14** |
| Arquivos com WPF | **1** |
| `DllImport("user32.dll")` | **33** |
| `DllImport("dwmapi.dll")` | **9** |
| `DllImport("gdi32.dll")` | **6** |
| Caminho Linux existente | `#if LINUX` + `wmctrl`/`bash` (6 pontos) |

**A UI é WinForms**, não WPF (a descrição "WinForms + WPF" do README está generosa). Isso importa:
Avalonia não é uma troca de 1 arquivo, é a reescrita de 8.387 linhas — a maior parte do app.

### O bloqueio não é a UI

O produto é **espelhar clientes EVE em tempo real**. No Windows isso é `DwmRegisterThumbnail` — o
compositor entrega o conteúdo da janela, inclusive quando ela está atrás de outra ou minimizada.

No Linux **não existe equivalente a DWM para janelas de terceiros**. As opções seriam:

- `PipeWire` / `wlr-screencopy`: captura de tela ou de superfície, exige portal/consentimento e, no
  Wayland, não captura janela individual de forma confiável;
- GDI/`PrintWindow` (o que o app faz hoje como fallback estático): funciona, mas com o problema já
  documentado — cliente DirectX em segundo plano devolve superfície preta/vazia (#46).

Ou seja: **trocar a UI para Avalonia não entrega thumbnail viva no Linux**. Entrega a mesma captura
estática com outra aparência, ao custo de reescrever 62% do código.

## 2. Decision

**Opção D — Windows-first, com o caminho Linux via Wine oficializado.**

1. **Não reescrever a UI agora.** WinForms continua; nenhum projeto Avalonia/Uno é criado.
2. **Oficializar o caminho que já existe**: build `EVEOTARGET=Linux`, `WineCompatibilityMode`,
   captura estática (`StaticThumbnailView` + `PrintWindow`) e `wmctrl` para ativar janelas.
   Documentar como "modo Linux (via Wine/Lutris)" em vez de tratá-lo como acidente.
3. **Separar a Fase 3 em duas trilhas**:
   - **cross-platform** (3.1, 3.2, 3.5): **adiada**, condicionada aos gates do §4;
   - **extensibilidade** (3.3, 3.4, 3.6): **segue normalmente**, porque não depende de UI nem de DWM.

## 3. Alternatives

| Opção | O que custa | Por que não agora |
|---|---|---|
| **A. Só Windows** | zero | descartaria os usuários de Linux que já usam via Wine — e eles existem hoje |
| **B. Avalonia 11** | reescrever 8.387 linhas de UI + resolver captura no compositor | resolve 1 dos 3 bloqueios (UI) e deixa o principal (DWM) intacto |
| **C. Uno Platform** | igual a B + tooling mais pesado | nenhuma vantagem para um app WinForms com 33 P/Invokes de `user32` |
| **D. Windows-first + Wine oficial** (escolhida) | documentação + polir a captura estática | entrega "roda no Linux" hoje, sem apostar semanas numa reescrita que não resolve a thumbnail |

O erro que esta decisão evita: tratar **framework de UI** como se fosse a barreira. Não é. A barreira
é a fonte da imagem — e ela é do sistema operacional, não do toolkit.

## 4. Gates (quando reabrir a decisão)

Reavaliar B/C **somente** se pelo menos um destes for verdadeiro, com evidência registrada:

1. **Demanda**: issues/pedidos recorrentes de rodar em Linux/macOS **sem** Wine, ou de recursos de
   UI que o Wine não entrega (DPI, temas, acessibilidade).
2. **Qualidade**: um spike de 3 dias demonstrar, com medição, que uma captura nativa (PipeWire/X11)
   mantém **≥ 90% dos casos** funcionando — incluindo cliente DirectX em segundo plano.
3. **Custo de manutenção**: o `#if LINUX` e os P/Invokes passarem a quebrar build/testes com
   frequência (hoje: 1 ponto de `wmctrl`, estável).

Sem (1)+(2), reescrever a UI é custo sem entrega.

## 5. Consequences

**Positivo**

- o esforço vai para o que o usuário sente: estabilidade, GDI, e o modo Linux via Wine;
- a Fase 3 deixa de ser um bloco: extensibilidade (plugins, event sourcing) pode começar já;
- nenhuma dependência nova, nenhum runner macOS/Linux-nativo no CI.

**Negativo / riscos**

- o app continua dependente de WinForms (sem tema moderno, sem HiDPI nativo fora do Windows);
- usuários de Linux seguem reféns do Wine — se o Wine quebrar (ex.: atualização do EVE), não há plano B;
- "cross-platform" sai do roadmap ativo: se a demanda aparecer, o custo não diminuiu nesse meio-tempo.

## 6. What to measure before reopening

- quantos usuários baixam o pacote e relatam uso em Linux/Wine (a partir das issues, não de telemetria);
- taxa de falha da captura estática em clientes DirectX em segundo plano (o #46 documentado);
- custo real por release (o asset self-contained tem 67,7 MB — ver release 8.3.2).

## 7. Referências

- ADR-006 (push-based), ADR-007 (virtualização), ADR-008 (pool DWM) — mesmo critério: medir antes.
- `docs/audits/linux-only-pipeline.md`, `docs/audits/validacao-linux-wine.md`
- #46 (superfície preta em cliente DX fora de foco)

---

## Fechamento de 2026-11-22

A trilha de **extensibilidade (3.3, 3.4, 3.6) também foi encerrada**: o projeto não será aberto a
contribuição de terceiros, e sem esse público não há para quem entregar plugin SDK, `CONTRIBUTING.md`,
template `dotnet new` ou plugin de comunidade. Event sourcing cai pelo mesmo motivo.

- **3.3** chegou a ter o incremento 1 no `main` (contrato `IPlugin`/`IPluginContext`, `PluginLoader`
  com contexto isolado, `PluginHost`) e foi **removido** — sem abertura a terceiros não há autor
  para plugin. O código segue no commit `d8a3d2d`; aqui fica só a decisão.
- **3.4** e **3.6** não começaram e não começam.

**Regra para o futuro:** a Fase 3 não volta para o roadmap. Reabrir qualquer um destes itens exige
decisão explícita do mantenedor — não uma releitura deste documento, nem uma "oportunidade" vista
no código já existente. Este ADR é o único lugar onde o assunto continua registrado.
