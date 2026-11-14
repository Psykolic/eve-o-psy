# Plano de Arquitetura da Fase 2

*Baseado nos deliverables 2.1–2.7 do `docs/roadmap.md` — gerado a partir da auditação de 2026-11-10*

---

## 1. Resumo dos Deliverables (2.1–2.7)

A Fase 2 — *Architecture Cleanup* tem como objetivo separar camadas, avaliar padrões com medições reais e implementar apenas o que dados técnicos justificarem. Princípio orientador: **"Measure first, implement second."**

| ID | Deliverable | Linha de Referência |
|---|-------------|---------------------|
| **2.1** | Separação de camadas: `EveO.Core` (Domain), `EveO.Infrastructure` *futuro* (Win32/DWM), `EveO.Presentation` *futuro* (Views) | #6.8, #6.9 |
| **2.2** | Spike técnico: Avaliar `DispatcherTimer` → Reactive Streams (`IAsyncEnumerable` + `Channel<T>` ou Rx.NET) | #6.2, #6.7, #H2 |
| **2.3** | Spike técnico: Avaliar virtualização de thumbnails (render apenas visíveis) | #6.8, #5.1, #H6 |
| **2.4** | Spike técnico: Avaliar pool de `DwmThumbnail` (registro/desregistro reutilizado) | #6.2, #6.4, #H3 |
| **2.5** | Testes de integração Win32 com TestContainers / Docker | #6.9 |
| **2.6** | Event-driven config changes (MediatR notifications) | #5.3, #6.7 |
| **2.7** | Documentação de arquitetura atualizada (C4 + ADRs) | #7.5 |

---

## 2. Ordem Recomendada e Dependências

2.1 **deve ser concluído antes** dos spikes 2.2–2.4, pois as abstrações de interface (IThumbnailProvider, IWindowManager) são dependências para todos os spikes.

- 2.1 → 2.2, 2.3, 2.4 (core abstractions first)
- 2.2, 2.3, 2.4 são **spikes independentes** — podem rodar em paralelo após 2.1
- 2.5 requer 2.1 (Infrastructure isolado)
- 2.6 requer 2.1 + 2.2 (event-driven precisa de abstrações estáveis)
- 2.7 no final da fase

### Ordem recomendada:

1. **2.1** — Separação de camadas (fundação)
2. **2.2** — Spike Reactive Streams (depõe 2.1)
3. **2.3** — Spike Virtualização (depõe 2.1)
4. **2.4** — Spike Pool de thumbnails (depõe 2.1)
5. **2.5** — Testes de integração Win32 (depõe 2.1)
6. **2.6** — Event-driven config changes (depõe 2.1 + 2.2)
7. **2.7** — Documentação (última)

---

## 3. Detalhamento por Item

### 2.1 Separação de Camadas

- **Escopo mínimo:**
  - Criar projeto `EveO.Core` (netstandard2.1 ou net8.0) contendo domínio puro — **zero dependências Windows-only**.
  - Mover `ThumbnailManager` para `EveO.Core` como `ThumbnailOrchestrator`, usando `IThumbnailProvider`/`IWindowManager` da Infrastructure.
  - Criar projeto `EveO.Infrastructure` com implementações Win32/DWM.
  - Criar projeto `EveO.Presentation` com Views (UserControls/Windows).
  - Garantir build do `EveO.Core` em Linux (`dotnet build --os linux`).
  - Verificar regressão: comportamento 6+1 clientes idêntico.

- **Critério de aceite verificável:**
  - [ ] `EveO.Core` build bem-sucedido em Linux sem erros de referência Windows.
  - [ ] `ThumbnailOrchestrator` depende apenas de abstrações (`IThumbnailProvider`, `IWindowManager`).
  - [ ] Build em CI do `EveO.Core` passa.
  - [ ] Comportamento 6 clientes + 1 intel inalterado (testado manualmente ou via suite existente).

- **Riscos:**
  - [Quebra de single-file publish] — Camadas separadas podem complicar publish de arquivo único; mitigação: manter `EveO.App` como entry point, testar ILMerge/assembly loading em CI.
  - [Coupling inadvertido] — Se `EveO.Core` ganhar referência acidental a DLLs Windows, build em Linux falha; mitigação: auditoria de dependências (`dotnet list dependencies`) em cada commit.

---

### 2.2 Spike Técnico: DispatcherTimer → Reactive Streams

- **Escopo mínimo:**
  - Prototype push-based com `ProcessMonitor` (event-driven process changes).
  - Medir CPU idle com 7 clientes: baseline (DispatcherTimer 500ms) vs push-based.
  - Documentar decisão no ADR-006.

- **Critério de aceite verificável:**
  - [ ] Prototype funcionando com `IAsyncEnumerable` + `Channel<T>` **OU** Rx.NET.
  - [ ] Medição de CPU idle: baseline registrada; valores push-based registrados.
  - [ ] Se redução >30% CPU idle: gate passou. Se não, decisão documentada no ADR-006 de manter timer.

- **Riscos:**
  - [Spike mostra que otimização não vale a pena] — Resultado aceitável; documentar no ADR e manter implementação atual.
  - [Complexidade de aprendizado] — Rx.NET ou patterns reativos podem exigir curva de equipe; mitigação: Documentação inline no código e ADR.

---

### 2.3 Spike Técnico: Virtualização de Thumbnails

- **Escopo mínimo:**
  - Prototype `VirtualizingThumbnailPanel` (WPF) ou lógica de ocultar/DWM thumbnails fora do viewport.
  - Medir memória/CPU com 7 clientes: baseline vs virtualizado.

- **Critério de aceite verificável:**
  - [ ] Prototype criado e medido.
  - [ ] Redução de memória >20% comprovada entre baseline e virtualizado.
  - [ ] Se gate passou: decisão no ADR-007 de prosseguir. Se não: documentar e manter implementação atual.

- **Riscos:**
  - [Benefício marginal] — Virtualização pode não reduzir memória suficiente para justificar complexidade.
  - [Quebra de compatibilidade Windows] — Alterações no layout de thumbnail podem afetar comportamento em Wine; mitigação: testar em container Wine.

---

### 2.4 Spike Técnico: Pool de DwmThumbnail

- **Escopo mínimo:**
  - Prototype `ThumbnailPool : IThumbnailPool` com métodos `Rent()`/`Return()`.
  - Medir: handles DWM abertos, latency refresh, falhas `DwmRegisterThumbnail` sob carga.

- **Critério de aceite verificável:**
  - [ ] Pool implementado e medido.
  - [ ] Limite real de handles DWM comprovado (**#H3**) **OU** latency refresh >30ms comprovado.
  - [ ] Se gate passou: decisão no ADR-008 de prosseguir com pool. Se não: descontinuar ideia.

- **Riscos:**
  - [Limite de handles não é gargalo] — Se handles DWM não forem limitados, pool traz pouco benefício.
  - [Falhas em carga] — Pool pode causar falhas `DwmRegisterThumbnail` sob carga alta; mitigação: límites e retry no código.

---

### 2.5 Testes de Integração Win32 com TestContainers / Docker

- **Escopo mínimo:**
  - Configurar container Windows Server Core ou Wine para CI.
  - Testes: `DwmThumbnailProvider.Register`, `WindowManager.Activate`, hotkeys globais.
  - Executar em CI (GitHub Actions self-hosted runners Windows).

- **Critério de aceite verificável:**
  - [ ] Container Windows configurado em runners self-hosted.
  - [ ] Suite de testes Win32 executa: Register/DwmThumbnail, Activate, hotkeys.
  - [ ] Todos testes passam em ambiente Windows.

- **Riscos:**
  - [TestContainers Windows em CI Linux] — Alta probabilidade; mitigação: usar runners Windows self-hosted exclusivamente para testes Win32; Linux tests apenas Core.
  - [Instabilidade de container] — Containers Wine podem ter comportamentos imprevisíveis; mitigação: tests isolados, fallback manual.

---

### 2.6 Event-driven Config Changes (MediatR notifications)

- **Escopo mínimo:**
  - `ConfigurationChangedNotification` publicado ao salvar configuração.
  - `ThumbnailOrchestrator` reage via handler (não polling).
  - Hot-reload de `ThumbnailRefreshPeriod`, `ZoomFactor` sem restart.
  - Regression: Config save/load funciona idêntico.

- **Critério de aceite verificável:**
  - [ ] Notification publicada após save config.
  - [ ] Handler atualiza `ThumbnailOrchestrator` instantaneamente (sem polling).
  - [ ] Hot-reload de RefreshPeriod e ZoomFactor funciona.
  - [ ] Config save/load regressão: configs válidas atuais continuam carregando.

- **Riscos:**
  - [Loops de eventos] — MediatR notifications podem causar loops; mitigação: pattern `DisableViewEvents`/`EnableViewEvents` já existente; estender para config changes.
  - [Atualização tardia] — Handler pode não atualizar UI a tempo; mitigação: testar cenário de mudança de RefreshPeriod em tempo real.

---

### 2.7 Documentação de Arquitetura Atualizada (C4 + ADRs)

- **Escopo mínimo:**
  - Diagramas C4: Context, Container, Component, Code.
  - ADR-004 a ADR-008 documentando decisões da Fase 2.
  - `docs/architecture/README.md` com visão geral.

- **Critério de aceite verificável:**
  - [ ] Diagramas C4 gerados (ou atualizados) para Context, Container, Component, Code levels.
  - [ ] ADR-004 a ADR-008 escritos e revisados.
  - [ ] `docs/architecture/README.md` criado com visão geral da arquitetura.

- **Riscos:**
  - [Documentação desatualizada] — Diagramos C4 podem ficar desatualizados rapidamente; mitigação: tratar como entregável "done" apenas após review de sprint.
  - [Conflito com ADRs existentes] — Novos ADRs podem conflitar com ADRs da Fase 1; mitigação: revisão cruzada com maintainer.

---

## 4. Escopo: Worker vs Humano/Windows

### O que o worker pode fazer (paths permitidos):

| Área | Caminhos | Exemplos |
|------|----------|----------|
| **Análise e escrita de documentação** | `docs/audits/` | Criar `architecture-plan.md`, diagramas C4, ADRs |
| **Projetos de teste e abstrações** | `src/EveO.Core/`, `src/EveO.Core.Tests/`, `src/EveO.Tests/` | Mover `ThumbnailManager` → `ThumbnailOrchestrator`, criar interfaces, testes unitários |
| **Spikes e prototipagem** | `src/EveO.Core/`, `src/EveO.Core.Tests/` | Prototypes para 2.2, 2.3, 2.4 (code em C#, não produção) |
| **Refatoração de camadas** | `src/EveO.Core/` | Separação de projetos, interface extração, build em Linux |
| **Configurações e CI** | `docs/audits/` (apenas leitura/escrita de docs) | Atualizar workflows — **EXIGE humano** |

### O que exige humano/Windows:

| Tarefa | Motivo |
|--------|--------|
| **Testes de integração Win32** | Requer container Windows ou Wine; GitHub Actions self-hosted runners Windows |
| **Build e publish do aplicativo** | Windows-only projetos (single-file, MSIX) |
| **Decisões de gate de spike** | Requer análise de méquinas reais; o worker pode coletar dados, mas aprovar gate é decisão humana |
| **Setup de runners self-hosted** | Configuração de infraestrutura de CI |
| **Qualquer mudança em workflows, configs de CI, ou publish** | Exigem permissões de repositório e conhecimento de ambiente de deploy |

---

## 5. Fila dos 3 Primeiros Jobs Pequenos

Cada job é uma unidade pequena, com início e fim definidos, que pode ser feita em um sprint curto. Ordem: 2.1 → 2.2 → 2.3.

### Job 1: Configurar Separação de Camadas (EveO.Core)

- **Título:** Mover ThumbnailManager para EveO.Core como ThumbnailOrchestrator com interfaces
- **Critério de aceite:** `EveO.Core` builda em Linux (`dotnet build --os linux`) sem erros de referência Windows; `ThumbnailOrchestrator` depende apenas de `IThumbnailProvider`/`IWindowManager` abstratos.
- **Paths:** `src/EveO.Core/`
- **Dependências:** Nenhuma (é o ponto de partida da Fase 2)

### Job 2: Spike Reactive Streams (2.2)

- **Título:** Prototype push-based com IAsyncEnumerable + Channel<T> (ou Rx.NET)
- **Critério de aceite:** Prototype criado e medição de CPU idle baseline vs push-based registrada; decisão documentada no ADR-006 (se gate >30% CPU reduction passa, ou mantém timer).
- **Paths:** `src/EveO.Core.Tests/`, `src/EveO.Core/`
- **Dependências:** 2.1 concluído (abstrações disponíveis)

### Job 3: Spike Virtualização de Thumbnails (2.3)

- **Título:** Prototype VirtualizingThumbnailPanel ou lógica de ocultar thumbnails fora viewport
- **Critério de aceite:** Prototype criado e medição de memória baseline vs virtualizada registrada; se redução >20% memória: gate passou, decisão no ADR-007. Se não, documento.
- **Paths:** `src/EveO.Core/`
- **Dependências:** 2.1 concluído (core abstrações disponíveis)

---

## Referências Reais do Repositório

- Deliverables referenciados de `docs/roadmap.md` (Fase 2, lines 77-99)
- IDs de auditoria: #6.8, #6.9, #6.2, #6.7, #H2, #H3, #H6, #5.3
- Spikes gates referem-se a findings #H2, #H3, #H6 da auditoria
- Dependências entre items seguem a tabela de `docs/roadmap.md` lines 101-106
- Caminhos de source: `src/EveO.Core/`, `src/EveO.Core.Tests/`, `src/EveO.Tests/`