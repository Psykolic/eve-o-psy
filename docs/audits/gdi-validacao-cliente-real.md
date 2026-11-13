# GDI: Roteiro de Validação da Captura Estática no Cliente EVE Real

> **Escopo:** Validação manual reproduzível do caminho de captura estática GDI (`WindowManager.GetStaticThumbnail`) no cliente EVE Online acelerado por GPU no Windows 11, após a integração das séries #3, #4 e #5 (correções de segurança GDI, stress test de 1.000 capturas e injeção de falhas).
>
> **Status:** **Rascunho (Draft)** — apenas para revisão. Nenhuma automação de jogo, broadcast de entrada, injeção ou acesso à memória do cliente.
>
> **Validação real no cliente EVE:** **EXECUTADA EM 13/11/2026 — PARCIAL, COM FALHA FUNCIONAL.** A captura estática só renderiza a thumbnail do cliente em primeiro plano; com 6+1 clientes empilhados, as demais ficam pretas. Handles GDI/USER ficaram estáveis. Detalhes em [4.1](#41-execução-de-13112026). O ensaio sintético de 1.000 capturas (`GdiStressTests`) já passa no CI Windows; este documento cobre a validação **fora** do harness, com o cliente real.

---

## 1. Identificação do Build/SHA e Isolamento da Configuração

### 1.1 Build/SHA testado

No PowerShell, abra a raiz do checkout e confirme que o código corresponde ao commit que será testado. Registre o SHA na tabela da seção 4. O comando abaixo publica o build Windows x64 Release em um diretório novo. O build requer o SDK .NET 8, e a execução requer o .NET 8 Desktop Runtime x64.

```powershell
git status --short
$sha = git rev-parse HEAD
$sha
$testDir = Join-Path $env:TEMP ("eveo-gdi-" + [guid]::NewGuid().ToString("N"))
dotnet publish .\src\Eve-O-Preview\Eve-O-Preview.csproj -c Release -r win-x64 --self-contained false -o $testDir
if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar o build de teste." }
$testExe = Join-Path $testDir 'EVE-O-Preview.exe'
if (-not (Test-Path -LiteralPath $testExe -PathType Leaf)) { throw "Executável de teste não encontrado." }
```

### 1.2 Cópia separada de `EVE-O-Preview.json`

Feche a instância habitual do Preview. Na mesma sessão do PowerShell, informe o caminho completo do JSON habitual quando solicitado. O script lê o arquivo e grava uma cópia com o nome `EVE-O-Preview.json` dentro do diretório temporário.

```powershell
$configPath = Read-Host 'Caminho completo do EVE-O-Preview.json habitual'
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$config | Add-Member -NotePropertyName WineCompatibilityMode -NotePropertyValue $true -Force
$config | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $testDir 'EVE-O-Preview.json') -Encoding UTF8
```

`ConfigurationStorage` carrega e salva `EVE-O-Preview.json` no diretório de trabalho na inicialização normal. O arquivo habitual permanece intacto.

### 1.3 Seleção da captura estática vs. DWM no código
A fábrica de thumbnails decide em `ThumbnailViewFactory.Create()` (`src/Eve-O-Preview/View/Implementation/ThumbnailViewFactory.cs`, linhas 18–22):

```csharp
IThumbnailView view = this._enableWineCompatibilityMode
    ? (IThumbnailView)this._controller.Create<StaticThumbnailView>()
    : (IThumbnailView)this._controller.Create<LiveThumbnailView>();
```

- **`EnableWineCompatibilityMode = true`** → `StaticThumbnailView` → `WindowManager.GetStaticThumbnail()` (GDI `BitBlt`).
- **`EnableWineCompatibilityMode = false`** (padrão no Windows) → `LiveThumbnailView` → `DwmThumbnail` (DWM `DwmRegisterThumbnail`).

A flag é lida de `ThumbnailConfiguration.EnableWineCompatibilityMode` (JSON property `WineCompatibilityMode`).

**Diferenciação prática:**
| Caminho | Classe de View | API Nativa | Composição DWM necessária? |
|---------|----------------|------------|----------------------------|
| Estático (GDI) | `StaticThumbnailView` | `GetDC` / `BitBlt` / `CreateCompatibleBitmap` | Não |
| Dinâmico (DWM) | `LiveThumbnailView` | `DwmRegisterThumbnail` / `DwmUpdateThumbnailProperties` | Sim (`WindowManager.IsCompositionEnabled`) |

### 1.4 Iniciar o build com a configuração de teste

Execute na mesma sessão do PowerShell. O diretório de trabalho deve ser o diretório temporário que contém o JSON.

```powershell
Start-Process -FilePath $testExe -WorkingDirectory $testDir
```

Com `WineCompatibilityMode` em `true`, a fábrica escolhe `StaticThumbnailView`. O caminho padrão com `false` usa DWM. Registre o que aparecer na interface e anote qualquer erro visível. O caminho GDI não garante um arquivo de log para cada falha de captura.

---

## 2. Passos de Validação Funcional no Cliente Real

> **Pré-requisitos:**
> - Windows 11 (registre build `winver` e driver GPU).
> - Cliente EVE Online em **modo Windowed** ou **Borderless Windowed** (comece por um desses modos e registre o modo escolhido).
> - GPU com aceleração de hardware habilitada (padrão).
> - EVE-O-Preview compilado com o SHA alvo, usando a configuração de teste (`WineCompatibilityMode = true`).
> - Ambiente seguro no jogo (estação, docked, sem combate).

### 2.1 Atualização da imagem após mudança visível no cliente
| Passo | Ação | Resultado Esperado | Arquivo/Método de Referência |
|-------|------|-------------------|------------------------------|
| 1 | Inicie 1 cliente EVE logado no espaço (docked/undocked). | Thumbnail aparece com imagem do cliente. | `StaticThumbnailView.RefreshThumbnail` → `WindowManager.GetStaticThumbnail` |
| 2 | No jogo, abra um menu (ex.: inventário, mercado, mapa) que altere a maior parte da tela. | Após uma atualização forçada, a thumbnail reflete o novo conteúdo. Aguarde alguns ciclos e registre o tempo observado. | `ThumbnailManager.RefreshThumbnails` chama `view.Refresh(true)` a cada ciclo forçado (`FORCED_REFRESH_CYCLE_THRESHOLD = 2`). |
| 3 | Feche o menu, volte à visão de espaço. | Thumbnail atualiza novamente. | `StaticThumbnailGdi.BitBlt` captura o `client area` via `GetClientRect`. |
| **Critério de aceite** | A thumbnail **muda** visualmente acompanhando o cliente. Se ficar preta, vazia ou congelada, registre o sintoma e o modo de janela. O resultado visual por si só não identifica a chamada que falhou. | |

> **Confirmação no Windows:** O ensaio verifica se `GetDC` e `BitBlt` capturam o conteúdo do EVE no modo de janela, na GPU e com o driver testados. Registre o que ocorre nesse ambiente.

### 2.2 Minimizar / Restaurar
| Passo | Ação | Resultado Esperado |
|-------|------|-------------------|
| 1 | Minimize o cliente EVE pelo botão da janela. | O Preview continua respondendo. Anote se a miniatura manteve o último quadro ou mudou enquanto o cliente estava minimizado. |
| 2 | Restaure o cliente (clique na thumbnail ou `ActivateWindow`). | A janela volta ao primeiro plano e a miniatura volta a atualizar após os ciclos de refresh. Anote a demora observada. |
| **Critério de aceite** | Após restaurar, a thumbnail **volta a mostrar conteúdo vivo** sem reiniciar o EVE-O-Preview. |

> **Confirmação no Windows:** `GetStaticThumbnail` retorna `null` quando a captura falha ou a área é menor que 300 pixels em largura ou altura. Se a captura retornar `null`, `StaticThumbnailView.RefreshThumbnail` mantém o último quadro válido. O teste observa o que acontece ao minimizar e restaurar o cliente nesse ambiente.

### 2.3 Fechar / Reabrir
| Passo | Ação | Resultado Esperado |
|-------|------|-------------------|
| 1 | Feche o cliente EVE normalmente (botão X ou menu Quit). | `ProcessMonitor` detecta remoção; `ThumbnailManager.TryRemoveThumbnail` remove a view, chama `view.Close()`. Thumbnail desaparece na UI. |
| 2 | Reabra o mesmo cliente (launcher → Play). | Novo processo/handle detectado; `UpdateThumbnailsList` cria nova `StaticThumbnailView`. Thumbnail aparece e responde a cliques. |
| **Critério de aceite** | Thumbnail **some** ao fechar e **reaparece funcional** ao reabrir. Nenhum "ghost thumbnail" (view órfã) permanece. |

> **Referência:** `ThumbnailManager.TryRemoveThumbnail` (linha 394–419) remove do dicionário, limpa 7 callbacks e chama `view.Close()`. `UpdateThumbnailsList` (linha 307–390) cria nova view para `addedProcesses`.

### 2.4 Thumbnails restantes e Preview continuam respondendo
Durante os passos acima, se houver **múltiplos clientes** (A–F + I):
- Clique na thumbnail de cada cliente restante → deve ativar a janela correspondente (`ThumbnailActivated` → `WindowManager.ActivateWindow`).
- Hover sobre thumbnail → zoom/overlay se `ThumbnailZoomEnabled` / `ShowThumbnailOverlays`.
- Nenhum travamento, exceção não tratada ou "destaque fantasma" (borda ativa em cliente errado).

---

## 3. Ensaio Estendido: 6 Clientes Principais + 1 Intel (6+1)

> **Objetivo:** Reproduzir cenário real de multibox com captura estática GDI, observando estabilidade de recursos GDI/USER.

### 3.1 Preparação do ambiente 6+1
| Item | Configuração |
|------|--------------|
| **Clientes A–F** | 6 contas no monitor principal (resolução/DPI nativo). |
| **Cliente I (Intel)** | 1 conta no monitor secundário (pode ter DPI diferente). |
| **Grupo de ciclo** | Apenas A–F no `CycleGroup1ClientsOrder`; I explicitamente **excluído** (`IsExcludedFromCycleGroup = true` via Shift+Click na thumbnail). |
| **Prioridade I** | `PriorityClients` inclui I se o operador usa prioridade real. |
| **Configuração GDI** | `WineCompatibilityMode = true`, `ThumbnailRefreshPeriod = 500`, `MinimizeInactiveClients = false` (para manter janelas visíveis), `HideCaptionOnClients = false`. |
| **Configuração isolada** | Use a cópia da seção 1.2 no diretório temporário. |

### 3.2 Sequência repetível de observação de objetos GDI/USER
> **Ferramentas:** Task Manager → Details → colunas **"GDI Objects"** e **"USER Objects"** (ou `GetGuiResources` via script PowerShell).

| Etapa | Ação | Observação (Antes/Depois) |
|-------|------|---------------------------|
| **Baseline** | Inicie EVE-O-Preview com 7 clientes rodando; aguarde 30 s ocioso. | Anote GDI/USER do processo `EVE-O-Preview.exe`. |
| **Ciclo 1** | 10 voltas para frente (hotkey `CycleGroup1ForwardHotkeys`), 10 para trás. | Anote GDI/USER a cada 2 voltas. Delta esperado: **estável** (sem crescimento contínuo). |
| **Alternância rápida** | 5 min de cliques manuais A–F intercalando I; rajadas A/B/A. | Anote picos de GDI/USER e a leitura após um minuto ocioso. |
| **Fechamento B** | Feche cliente B (Quit in-game); aguarde 2 ciclos de refresh. | Thumbnail B some; GDI/USER não sobem permanentemente. |
| **Reabertura B** | Reabra B; aguarde registro e clique na nova thumbnail. | Nova view criada; GDI/USER estáveis. |
| **Intel I** | Ative I e volte a A; repita durante ciclo e com minimização configurada. | I mantém monitor/layout; exclusão do ciclo respeitada; GDI/USER estáveis. |
| **Recuperação** | 1 min ocioso; repita Ciclo 1. | Callbacks e thumbnails respondem; GDI/USER no baseline. |
| **Encerramento** | Feche EVE-O-Preview durante alternância; reinicie com 7 clientes. | Configuração restaurada; sem erro visível; GDI/USER no baseline. |

> **Critério de aceite GDI/USER:** Compare as leituras após o aquecimento e após os períodos ociosos. Crescimento persistente em rodadas equivalentes exige investigação. Os limites +16 GDI e +8 USER pertencem ao ensaio sintético e servem apenas como referência para este teste manual.

> **Confirmação no Windows:** O `GetGuiResources` conta handles do **processo EVE-O-Preview**, não do cliente EVE. Vazamentos no driver GPU ou no DWM não são contabilizados aqui. Se houver crescimento contínuo no EVE-O-Preview, investigue `StaticThumbnailGdi` (limpeza de `HDC`, `HBITMAP`, `ReleaseDC`).

---

## 4. Tabela de Resultados

Execução de **13/11/2026**, build publicado com `dotnet publish` a partir de `e3a1720` (`WineCompatibilityMode = true`, `PreventPreviews = false`, `ThumbnailsOpacity = 1.0`, cópia separada do JSON).

| SHA Testado | Ambiente (Win11 build, GPU/driver, monitores, DPI, modo janela EVE, versão EVE) | Etapa (Seção) | Aprovação Parcial / Falha | Observações | Evidência |
|-------------|-----------------------------------------------------------------------------------|---------------|----------------------------|-------------|-----------|
| e3a1720 | Windows 11 Pro 26H2 build 26300; AMD Radeon RX 7600, driver 32.0.32015.7029 (17/10/2026); 2560x1080 + 1920x1080; DPI 96 (100%); EVE em janela 2560x1080; cliente TQ atual (launcher 1.17.8) | 2.1 Atualização imagem | **☑ Parcial** | Apenas a thumbnail do cliente em primeiro plano mostra conteúdo; as outras 6 ficam pretas. Repetido em 4 execuções. | `sha2-r1-01-normal.png`, `diag-fullscreen.png` |
| e3a1720 | idem | 2.2 Minimizar/Restaurar | **☑ Parcial** | `Ctrl+F22` minimizou os 7/7 e as 7 thumbnails mantiveram o último quadro (sem thumbnail preta ao minimizar). Restaurar não devolve conteúdo às 6 não ativas. `Ctrl+F22` é minimizar-apenas por implementação (`MinimizeAllClients` → `MinimizeWindow`). | `sha2-r1-02-minimizado.png` |
| e3a1720 | idem | 2.3 Fechar/Reabrir | **☐ Não executado** | Fechar cliente encerra sessão de jogo; fora do escopo desta execução. | — |
| e3a1720 | idem | 2.4 Thumbnails restantes | **☑ Parcial** | Clique em thumbnail ativa o cliente correspondente (mudança de janela em primeiro plano confirmada). Com 6 de 7 sem conteúdo, hover/zoom e destaque ativo não puderam ser avaliados. | `gdi-sha2.log` |
| e3a1720 | idem | 3.2 Sequência 6+1 (GDI/USER) | **☑ Falha** | Handles estáveis (GDI 75→75, USER 88→90, delta +2 em 40 trocas de ciclo, minimize/restore e 20 s ocioso), mas o cenário falha no objetivo: **1 de 7 previews com conteúdo**, 6 pretas. 3 rodadas idênticas neste build e 1 rodada com o binário da produção. | `sha2-r1-*`, `sha2-r2-*`, `sha2-r3-*`, `gdi-sha2.log` |
| e3a1720 | idem | **Validação Real (Cliente EVE)** | **☑ Parcial** | **Executada; falha funcional reprodutível na captura de clientes não ativos.** | ver 4.1 |

> **Legenda:**
> - **Parcial**: Funciona mas com ressalvas (ex.: thumbnail preta ao minimizar, atualização mais lenta que a observada na sequência normal, flicker ocasional).
> - **Falha**: Comportamento fora do especificado (crash, ghost thumbnail, vazamento GDI/USER contínuo, captura nula persistente).
> - **Passou**: Todos os critérios de aceite atendidos.

### 4.1 Execução de 13/11/2026

**Método de verificação de conteúdo.** A checagem não pode usar a variância de toda a thumbnail: o rótulo laranja sobre fundo preto produz desvio alto (17 a 30) e faz uma thumbnail preta passar por "com conteúdo". A medição correta amostra apenas o corpo abaixo do rótulo (`y` a partir de 28 px) e classifica como preta quando média < 6 e desvio < 6. Também é preciso deduplicar por retângulo: cada thumbnail tem duas janelas sobrepostas (`EVE - <cliente>` e `PreviewOverlay`) no mesmo `GetWindowRect`, o que dobra a contagem se não for tratado.

**Medições (7 clientes: 6 no monitor principal, 1 no secundário; 7 thumbnails, 14 janelas no mesmo retângulo):**

| Estado | GDI | USER | Thumbs com conteúdo |
|---|---|---|---|
| Baseline | 75 | 87 | 1/7 |
| Após 20 ciclos (10x F14 + 10x F13) | 75 | 88 | 1/7 |
| Após `Ctrl+F22` (7/7 minimizados) | 75 | 88 | 1/7 (mantiveram o último quadro) |
| Após restaurar | 75 | 88 | 1/7 |
| Após clique em thumbnail | 78 | 90 | 1/7 |
| Após 20 s ocioso | 75 | 90 | 1/7 |

Delta: **GDI 0**, **USER +2**. O +3 em GDI e o +3 em USER medidos logo após o clique sintético voltaram ao patamar na leitura seguinte; não indicam crescimento persistente.

**Achado principal.** A thumbnail com conteúdo é sempre a do cliente cuja janela está em primeiro plano — o "sortudo" muda junto com a ativação (no ciclo F14/F13 passou para outro cliente; após clicar em Alexa Rocker, foi para Alexa Rocker). Os clientes empilhados no mesmo monitor, e mesmo o cliente do monitor secundário quando não está em primeiro plano, aparecem pretos.

**Reprodutibilidade.** 3 rodadas consecutivas com o build de `e3a1720` e 1 rodada com o binário que estava em produção (mesma config, hashes diferentes) deram o mesmo 1/7. Em uma execução anterior do mesmo dia (10:45), com o binário de produção e a mesma configuração, as 7 thumbnails mostraram conteúdo (`real-01-normal.png`) — não reproduzido depois. Ou seja: o resultado depende do estado das janelas dos clientes, não do build. Uma execução isolada não serve como aprovação.

**Não executado.** 2.3 fechar/reabrir cliente; alternância manual de 5 min da seção 3.2; reinício do app no meio da alternância; julgamento visual de brilho/atraso/artefato.

**Evidências.** `Documents\eve-o\evidencias\`: `sha2-r1-01-normal.png` (baseline), `sha2-r1-02-minimizado.png`, `sha2-r1-03-ocioso.png`, `sha2-r2-*`, `sha2-r3-*`, `prodchk-r1-*` (binário de produção), `diag-fullscreen.png` (tela inteira mostrando 6 thumbs pretas) e `real-01-normal.png` (execução das 10:45, quando as 7 tinham conteúdo).


---

## 5. Diferença: Ensaio Sintético (1.000 capturas) vs. Validação no Cliente Real

| Aspecto | Ensaio Sintético (`GdiStressTests`) | Validação no Cliente Real (Este Documento) |
|---------|--------------------------------------|--------------------------------------------|
| **Superfície de desenho** | Janela WinForms `Form` com `Graphics.Clear` (GDI puro) | Cliente EVE Online acelerado por GPU, no modo de janela registrado |
| **Caminho de código** | `WindowManager.GetStaticThumbnail` → `StaticThumbnailGdi` (mesmo) | Idem |
| **Métricas** | `GetGuiResources` do processo de teste (limites +16 GDI / +8 USER) | `GetGuiResources` ou Gerenciador de Tarefas para o `EVE-O-Preview.exe`, com comparação entre rodadas |
| **Cobertura** | 30 warm-up + 1.000 capturas medidas; verifica mudança de pixels | Cenários reais: menus, minimizar, fechar/reabrir, 6+1 clients, DPI multi-monitor |
| **O que NÃO cobre** | Superfície GPU real, DWM composição, fullscreen exclusivo, driver bugs, overclocks, anti-cheat | Stress contínuo de 1.000 capturas em loop apertado (o teste real é interativo) |
| **Status no CI** | **Passa** (Windows CI, categoria `WindowsGdiStress`) | **Executada em 13/11/2026 — parcial com falha funcional** (ver 4.1) |

> **Importante:** O ensaio sintético usa uma janela WinForms. A validação no EVE precisa verificar se a captura funciona com a GPU, o driver e o modo de janela usados no teste.

---

## 6. Condições que Dependem de Confirmação no Windows

As seguintes etapas **não podem ser demonstradas apenas pelo código** e exigem confirmação manual no ambiente Windows 11 alvo:

| Item | O que confirmar no Windows | Como confirmar |
|------|----------------------------|----------------|
| **Superfície legível por GDI** | `GetDC` + `BitBlt` captura pixels do cliente EVE em Windowed/Borderless. | Execute passo 2.1; registre qualquer miniatura preta, vazia ou sem atualização persistente. |
| **Limiar de tamanho (300 px)** | Observe se a miniatura preserva o último quadro quando uma nova captura falha ou a área capturável é insuficiente. | Passo 2.2; thumbnail preserva último frame (comportamento intencional). |
| **Foco/SetForegroundWindow** | `WindowManager.ActivateWindow` traz cliente ao foreground sem *focus stealing* bloqueado. | Passos 2.4 e 3.2; anote falhas de ativação (janela pisca na taskbar mas não vem à frente). |
| **Multi-monitor DPI** | Cliente I no monitor secundário com DPI diferente captura e posiciona corretamente. | Passo 3.2 (Intel I); verifique `ThumbnailSnapToGrid`, `ZoomAnchor`, posicionamento. |
| **Recursos GDI/USER do Preview** | Crescimento em `EVE-O-Preview.exe` (não no cliente EVE) permanece dentro dos limites. | Task Manager → Details → GDI Objects / USER Objects durante sequência 6+1. |

---

## 7. Como Voltar à Configuração Habitual Após o Teste

1. Feche a instância de teste do EVE-O Preview.
2. Abra a instalação habitual pelo atalho ou diretório original. O arquivo de configuração original permaneceu no local inicial.
3. Guarde as evidências e, se necessário, o JSON de teste. O diretório temporário pode ser descartado depois que você confirmar que não contém evidências a preservar.

---

## 8. Checklist Rápido para o Executor

- [x] SHA do commit registrado na tabela (`e3a1720`).
- [x] Cópia da configuração no diretório de teste, com o nome `EVE-O-Preview.json`.
- [x] `WineCompatibilityMode = true` no JSON de teste.
- [x] Cliente(s) EVE em **Windowed/Borderless** (não Fullscreen exclusivo).
- [x] Build **Release** do EVE-O Preview para Windows x64.
- [x] GDI/USER lidos via `GetGuiResources` em script (equivalente às colunas do Task Manager).
- [~] Evidência em imagem com timestamp (`sha2-r*`); sem gravação de vídeo.
- [~] Etapas da seção 2: 2.1, 2.2 e 2.4 registradas; **2.3 não executada**.
- [x] Instância habitual reaberta com a configuração original.

---

## 9. Referências de Código (para rastreabilidade)

| Funcionalidade | Arquivo | Linhas / Método |
|----------------|---------|-----------------|
| Fábrica de thumbnails (estático vs DWM) | `src/Eve-O-Preview/View/Implementation/ThumbnailViewFactory.cs` | `Create()` (18–22) |
| Captura estática GDI | `src/Eve-O-Preview/Services/Implementation/WindowManager.cs` | `GetStaticThumbnail()` (295–409) |
| Operações GDI nativas | `src/Eve-O-Preview/Services/Interop/StaticThumbnailGdi.cs` | `GetDC`…`ReleaseDC` (26–54) |
| View estática (refresh preserva último frame) | `src/Eve-O-Preview/View/Implementation/StaticThumbnailView.cs` | `RefreshThumbnail()` (30–44) |
| View dinâmica DWM | `src/Eve-O-Preview/View/Implementation/LiveThumbnailView.cs` | `RegisterThumbnail()` (56–61) |
| Thumbnail DWM nativo | `src/Eve-O-Preview/Services/Implementation/DwmThumbnail.cs` | `Register`/`Update` (21–101) |
| Configuração `WineCompatibilityMode` | `src/Eve-O-Preview/Configuration/Implementation/ThumbnailConfiguration.cs` | Property `EnableWineCompatibilityMode` (224–225) |
| Armazenamento JSON | `src/Eve-O-Preview/Configuration/Implementation/ConfigurationStorage.cs` | `Load()`/`Save()` (19–56) |
| Ensaio sintético 1.000 capturas | `src/EveO.Tests/Windows/GdiStressTests.cs` | `RunStress()` (62–147) |
| Injeção de falhas GDI | `src/EveO.Tests/Windows/GdiFailureTests.cs` | `FakeGdi` / teorias (31–177) |
| Limiar de tamanho de janela | `src/Eve-O-Preview/Services/Implementation/WindowManager.cs` | Const `WINDOW_SIZE_THRESHOLD = 300` (13) |
| Ciclo forçado de refresh | `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs` | `FORCED_REFRESH_CYCLE_THRESHOLD = 2` (27), `RefreshThumbnails()` (481–490) |
| Remoção segura de views | `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs` | `TryRemoveThumbnail()` (394–419) |

---

> **Fim do roteiro.** A tabela da Seção 4 foi preenchida na execução de 13/11/2026 (seção 4.1). Pela regra acima, o resultado **Parcial/Falha** em 3.2 **bloqueia a promoção do modo de captura estática para produção**: apenas a thumbnail do cliente em primeiro plano recebe imagem, e isso é reprodutível em 4 execuções. Permanecem não executados: 2.3 (fechar/reabrir cliente), alternância manual de 5 min da seção 3.2 e o julgamento visual de brilho/atraso/artefato.
