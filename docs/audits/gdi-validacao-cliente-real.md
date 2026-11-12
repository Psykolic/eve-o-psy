# GDI: Roteiro de Validação da Captura Estática no Cliente EVE Real

> **Escopo:** Validação manual reproduzível do caminho de captura estática GDI (`WindowManager.GetStaticThumbnail`) no cliente EVE Online acelerado por GPU no Windows 11, após a integração das séries #3, #4 e #5 (correções de segurança GDI, stress test de 1.000 capturas e injeção de falhas).
>
> **Status:** **Rascunho (Draft)** — apenas para revisão. Nenhuma automação de jogo, broadcast de entrada, injeção ou acesso à memória do cliente.
>
> **Validação real no cliente EVE:** **PENDENTE**. O ensaio sintético de 1.000 capturas (`GdiStressTests`) já passa no CI Windows; este documento cobre a validação **fora** do harness, com o cliente real.

---

## 1. Identificação do Build/SHA e Isolamento da Configuração

### 1.1 Build/SHA testado
- Obtenha o SHA do commit exato em `main` que contém as séries #3/#4/#5:
  ```powershell
  git rev-parse HEAD
  ```
- Registre o SHA no **campo `SHA` da tabela de resultados (Seção 5)**.
- Compile em **Release** para Windows (`net8.0-windows`):
  ```powershell
  dotnet build src/EVE-O-Preview.sln --configuration Release
  ```
- O executável estará em `src/Eve-O-Preview/bin/Release/net8.0-windows/EVE-O-Preview.exe`.

### 1.2 Cópia separada de `EVE-O-Preview.json`
A configuração é carregada/gravada por `ConfigurationStorage` (`src/Eve-O-Preview/Configuration/Implementation/ConfigurationStorage.cs`):
- O arquivo padrão é `EVE-O-Preview.json` no diretório de trabalho do processo.
- **Antes de iniciar**, faça uma cópia de segurança da configuração de produção:
  ```powershell
  Copy-Item "EVE-O-Preview.json" "EVE-O-Preview.json.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
  ```
- Para o teste, use um arquivo dedicado (ex.: `EVE-O-Preview.gdi-test.json`) definindo a variável de ambiente ou alterando `IAppConfig.ConfigFileName` no container (atualmente o nome é fixo em `CONFIGURATION_FILE_NAME`; a maneira mais simples é trocar o arquivo no diretório de execução).
- **Após o teste**, restaure o backup:
  ```powershell
  Copy-Item "EVE-O-Preview.json.backup-..." "EVE-O-Preview.json" -Force
  ```

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

> **Confirmação no Windows:** Verifique se `DwmIsCompositionEnabled()` retorna `true` no Windows 11 (sempre true a partir do Windows 8). O caminho DWM é o padrão; o teste **exige** forçar `WineCompatibilityMode = true` para exercitar o código GDI.

### 1.4 Como ativar a captura estática para o teste
Edite o JSON de teste (`EVE-O-Preview.gdi-test.json`) e defina:
```json
{
  "WineCompatibilityMode": true,
  "ThumbnailRefreshPeriod": 500,
  "ThumbnailSize": { "Width": 384, "Height": 216 }
}
```
Inicie o EVE-O-Preview com este arquivo no diretório de trabalho. O log de exceções (`EVE-O-Preview.log`) é gravado por `WindowManager.WriteToLog` (apenas em builds LINUX, mas o arquivo é criado no Windows se houver erro no `finally` de `GetStaticThumbnail`).

---

## 2. Passos de Validação Funcional no Cliente Real

> **Pré-requisitos:**
> - Windows 11 (registre build `winver` e driver GPU).
> - Cliente EVE Online em **modo Windowed** ou **Borderless Windowed** (Fullscreen exclusivo impede `GetDC`/`BitBlt` na superfície do jogo).
> - GPU com aceleração de hardware habilitada (padrão).
> - EVE-O-Preview compilado com o SHA alvo, usando a configuração de teste (`WineCompatibilityMode = true`).
> - Ambiente seguro no jogo (estação, docked, sem combate).

### 2.1 Atualização da imagem após mudança visível no cliente
| Passo | Ação | Resultado Esperado | Arquivo/Método de Referência |
|-------|------|-------------------|------------------------------|
| 1 | Inicie 1 cliente EVE logado no espaço (docked/undocked). | Thumbnail aparece com imagem do cliente. | `StaticThumbnailView.RefreshThumbnail` → `WindowManager.GetStaticThumbnail` |
| 2 | No jogo, abra um menu (ex.: inventário, mercado, mapa) que altere a maior parte da tela. | Na próxima atualização (≤ `ThumbnailRefreshPeriod` ms), a thumbnail reflete o novo conteúdo. | `ThumbnailManager.RefreshThumbnails` chama `view.Refresh(true)` a cada ciclo forçado (`FORCED_REFRESH_CYCLE_THRESHOLD = 2`). |
| 3 | Feche o menu, volte à visão de espaço. | Thumbnail atualiza novamente. | `StaticThumbnailGdi.BitBlt` captura o `client area` via `GetClientRect`. |
| **Critério de aceite** | A thumbnail **muda** visualmente acompanhando o cliente. Capturas nulas ou estáticas indicam falha de `GetDC`/`BitBlt` ou superfície GPU não legível por GDI. | |

> **Confirmação no Windows:** O GDI `BitBlt` com `SRCCOPY` lê a superfície da janela. Em aplicações DirectX 11/12 com *swap chain* em *fullscreen borderless*, a DWM compõe a janela e `GetDC`/`BitBlt` **podem** capturar o conteúdo compostado. Em *fullscreen exclusivo* ou com *overlay* de driver, a captura pode vir preta ou falhar. **Teste apenas em Windowed/Borderless.**

### 2.2 Minimizar / Restaurar
| Passo | Ação | Resultado Esperado |
|-------|------|-------------------|
| 1 | Minimize o cliente EVE (botão minimizar ou hotkey `MinimizeInactiveClients`). | `WindowManager.IsWindowMinimized` retorna `true`; `GetStaticThumbnail` recebe `GetClientRect` com largura/altura ≥ `WINDOW_SIZE_THRESHOLD` (300 px) ou retorna `null`. Thumbnail **pode** mostrar último frame válido ou ficar em branco. |
| 2 | Restaure o cliente (clique na thumbnail ou `ActivateWindow`). | Cliente volta ao foreground; thumbnail atualiza com conteúdo real em ≤ 2 ciclos de refresh. |
| **Critério de aceite** | Após restaurar, a thumbnail **volta a mostrar conteúdo vivo** sem reiniciar o EVE-O-Preview. |

> **Confirmação no Windows:** `GetClientRect` em janela minimizada retorna `0,0,0,0` ou tamanho ínfimo. O código em `WindowManager.GetStaticThumbnail` (linha 321) descarta capturas com `width < 300 || height < 300` retornando `null`. `StaticThumbnailView.RefreshThumbnail` (linha 38–43) **só substitui a imagem se `thumbnail != null`**, preservando o último frame válido. Isso é **comportamento esperado**.

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
| **Backup** | Salve `EVE-O-Preview.json` de produção antes de iniciar. |

### 3.2 Sequência repetível de observação de objetos GDI/USER
> **Ferramentas:** Task Manager → Details → colunas **"GDI Objects"** e **"USER Objects"** (ou `GetGuiResources` via script PowerShell).

| Etapa | Ação | Observação (Antes/Depois) |
|-------|------|---------------------------|
| **Baseline** | Inicie EVE-O-Preview com 7 clientes rodando; aguarde 30 s ocioso. | Anote GDI/USER do processo `EVE-O-Preview.exe`. |
| **Ciclo 1** | 10 voltas para frente (hotkey `CycleGroup1ForwardHotkeys`), 10 para trás. | Anote GDI/USER a cada 2 voltas. Delta esperado: **estável** (sem crescimento contínuo). |
| **Alternância rápida** | 5 min de cliques manuais A–F intercalando I; rajadas A/B/A. | Anote picos de GDI/USER; devem retornar ao baseline após GC. |
| **Fechamento B** | Feche cliente B (Quit in-game); aguarde 2 ciclos de refresh. | Thumbnail B some; GDI/USER não sobem permanentemente. |
| **Reabertura B** | Reabra B; aguarde registro e clique na nova thumbnail. | Nova view criada; GDI/USER estáveis. |
| **Intel I** | Ative I e volte a A; repita durante ciclo e com minimização configurada. | I mantém monitor/layout; exclusão do ciclo respeitada; GDI/USER estáveis. |
| **Recuperação** | 1 min ocioso; repita Ciclo 1. | Callbacks e thumbnails respondem; GDI/USER no baseline. |
| **Encerramento** | Feche EVE-O-Preview durante alternância; reinicie com 7 clientes. | Configuração restaurada; sem erro visível; GDI/USER no baseline. |

> **Critério de aceite GDI/USER:** Crescimento **não sustentado** acima de +16 GDI / +8 USER relativos ao baseline pós-aquecimento (mesmos limites do ensaio sintético `GdiStressTests`). Picos temporários durante `BitBlt`/`FromHbitmap` são aceitáveis se coletados pelo GC.

> **Confirmação no Windows:** O `GetGuiResources` conta handles do **processo EVE-O-Preview**, não do cliente EVE. Vazamentos no driver GPU ou no DWM não são contabilizados aqui. Se houver crescimento contínuo no EVE-O-Preview, investigue `StaticThumbnailGdi` (limpeza de `HDC`, `HBITMAP`, `ReleaseDC`).

---

## 4. Tabela de Resultados Preenchível

| SHA Testado | Ambiente (Win11 build, GPU/driver, monitores, DPI, modo janela EVE, versão EVE) | Etapa (Seção) | Aprovação Parcial / Falha | Observações | Evidência (screenshot, vídeo, log, CSV GDI/USER) |
|-------------|-----------------------------------------------------------------------------------|---------------|----------------------------|-------------|--------------------------------------------------|
|             |                                                                                   | 2.1 Atualização imagem | ☐ Parcial / ☐ Falha / ☐ Passou |             |                                                  |
|             |                                                                                   | 2.2 Minimizar/Restaurar | ☐ Parcial / ☐ Falha / ☐ Passou |             |                                                  |
|             |                                                                                   | 2.3 Fechar/Reabrir | ☐ Parcial / ☐ Falha / ☐ Passou |             |                                                  |
|             |                                                                                   | 2.4 Thumbnails restantes | ☐ Parcial / ☐ Falha / ☐ Passou |             |                                                  |
|             |                                                                                   | 3.2 Sequência 6+1 (GDI/USER) | ☐ Parcial / ☐ Falha / ☐ Passou |             |                                                  |
|             |                                                                                   | **Validação Real (Cliente EVE)** | **☐ PENDENTE** | **Não executado** | **—** |

> **Legenda:**
> - **Parcial**: Funciona mas com ressalvas (ex.: thumbnail preta ao minimizar, latência > 2 ciclos, flicker ocasional).
> - **Falha**: Comportamento fora do especificado (crash, ghost thumbnail, vazamento GDI/USER contínuo, captura nula persistente).
> - **Passou**: Todos os critérios de aceite atendidos.

---

## 5. Diferença: Ensaio Sintético (1.000 capturas) vs. Validação no Cliente Real

| Aspecto | Ensaio Sintético (`GdiStressTests`) | Validação no Cliente Real (Este Documento) |
|---------|--------------------------------------|--------------------------------------------|
| **Superfície de desenho** | Janela WinForms `Form` com `Graphics.Clear` (GDI puro) | Cliente EVE Online (DirectX 11/12, swap chain, composição DWM) |
| **Caminho de código** | `WindowManager.GetStaticThumbnail` → `StaticThumbnailGdi` (mesmo) | Idem |
| **Métricas** | `GetGuiResources` do processo de teste (limites +16 GDI / +8 USER) | `GetGuiResources` / Task Manager do `EVE-O-Preview.exe` (mesmos limites) |
| **Cobertura** | 30 warm-up + 1.000 capturas medidas; verifica mudança de pixels | Cenários reais: menus, minimizar, fechar/reabrir, 6+1 clients, DPI multi-monitor |
| **O que NÃO cobre** | Superfície GPU real, DWM composição, fullscreen exclusivo, driver bugs, overclocks, anti-cheat | Stress contínuo de 1.000 capturas em loop apertado (o teste real é interativo) |
| **Status no CI** | **Passa** (Windows CI, categoria `WindowsGdiStress`) | **PENDENTE** — requer execução manual |

> **Importante:** A aprovação do ensaio sintético **não garante** que capturas estáticas funcionem no cliente EVE real acelerado por GPU. O GDI `BitBlt` lê a superfície **compostada pela DWM**; se o jogo usar *fullscreen exclusivo*, *HDR*, *overlay de driver* ou *formato de swap chain não legível por GDI*, a captura virá preta ou falhará. **Teste apenas em Windowed/Borderless.**

---

## 6. Condições que Dependem de Confirmação no Windows

As seguintes etapas **não podem ser demonstradas apenas pelo código** e exigem confirmação manual no ambiente Windows 11 alvo:

| Item | O que confirmar no Windows | Como confirmar |
|------|----------------------------|----------------|
| **Composição DWM ativa** | `DwmIsCompositionEnabled()` retorna `true` (sempre no Win 11). | `WindowManager.IsCompositionEnabled` (linha 33–36) usa versão OS + API. Logar no startup. |
| **Superfície legível por GDI** | `GetDC` + `BitBlt` captura pixels do cliente EVE em Windowed/Borderless. | Execute passo 2.1; se thumbnail vier preta ou `null` persistente, a superfície **não** é legível por GDI. |
| **Limiar de tamanho (300 px)** | Janela minimizada tem `GetClientRect` < 300 px → `GetStaticThumbnail` retorna `null`. | Passo 2.2; thumbnail preserva último frame (comportamento intencional). |
| **Foco/SetForegroundWindow** | `WindowManager.ActivateWindow` traz cliente ao foreground sem *focus stealing* bloqueado. | Passos 2.4 e 3.2; anote falhas de ativação (janela pisca na taskbar mas não vem à frente). |
| **Multi-monitor DPI** | Cliente I no monitor secundário com DPI diferente captura e posiciona corretamente. | Passo 3.2 (Intel I); verifique `ThumbnailSnapToGrid`, `ZoomAnchor`, posicionamento. |
| **Recursos GDI/USER do driver** | Crescimento em `EVE-O-Preview.exe` (não no cliente EVE) permanece dentro dos limites. | Task Manager → Details → GDI Objects / USER Objects durante sequência 6+1. |
| **Anti-cheat / Proteção de memória** | Nenhuma interferência do Easy Anti-Cheat ou similar na captura GDI da janela. | Se thumbnail falhar **apenas** quando EVE está logado no espaço (não no login), pode ser proteção de superfície. |

---

## 7. Como Voltar à Configuração Habitual Após o Teste

1. **Pare o EVE-O-Preview** (feche a janela principal ou `Exit` no tray).
2. **Restaure o JSON de produção:**
   ```powershell
   Copy-Item "EVE-O-Preview.json.backup-<timestamp>" "EVE-O-Preview.json" -Force
   ```
3. **Remova o arquivo de teste** (opcional):
   ```powershell
   Remove-Item "EVE-O-Preview.gdi-test.json" -ErrorAction SilentlyContinue
   ```
4. **Reinicie o EVE-O-Preview** normalmente. Verifique se `WineCompatibilityMode = false` (padrão) e thumbnails voltam a ser DWM (`LiveThumbnailView`).

---

## 8. Checklist Rápido para o Executor

- [ ] SHA do commit registrado na tabela.
- [ ] Backup da configuração original feito.
- [ ] `WineCompatibilityMode = true` no JSON de teste.
- [ ] Cliente(s) EVE em **Windowed/Borderless** (não Fullscreen exclusivo).
- [ ] Build **Release** do EVE-O-Preview (net8.0-windows).
- [ ] Task Manager com colunas GDI Objects / USER Objects visíveis.
- [ ] Vídeo/screen recording iniciado (com timestamps).
- [ ] Todas as etapas das Seções 2 e 3 executadas e resultados preenchidos na tabela.
- [ ] Configuração original restaurada ao final.

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

> **Fim do roteiro.** Preencha a tabela da Seção 4 durante a execução. Qualquer **Falha** ou **Parcial** com impacto em estabilidade ou vazamento de handles bloqueia a promoção do modo de captura estática para produção. A validação real permanece **PENDENTE** até execução completa e assinatura do executor.