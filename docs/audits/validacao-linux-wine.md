# TM-XX — Manual Linux/Wine 6+1 Validation Checklist

> **Escopo.** Validação manual reproduzível do EVE-O Preview no Linux (Wine/Lutris) com 7 clientes EVE (6+1). Cobrirá identificação de build/SHA, configuração isolada, capturas de thumbnail com conteúdo, ciclos F13/F14, minimizar/restaurar, abrir/fechar cliente, spotlight, recorte, redimensionamento, opacidade, e coleta de GDI/USER antes/depois com critério de aprovação.

> **Status:** Rascunho (Draft) — apenas para revisão. Validação em ambiente Wine/Lutris, não em nativo Windows.

> **Data:** 2026-11-14
> **Branch:** `claude/issue-104` (PR em rascunho)
> **Commits incluídos:** (a definir após validação)

---

## 1. Preparação do Ambiente

| Item | Detalhes |
|------|----------|
| **SO** | Linux (distro e versão exata) |
| **Wine/Proton** | Versão (ex.: Wine 8.0 / Proton GE 8.8) |
| **Lutris** | Versão e configuração EVE Online |
| **GPU/Driver** | Modelo + versão (GPU nativa ou virturalizada) |
| **Monitores** | Primário + secundário (resolução/DPI) |
| **Modo Janela EVE** | Windowed / Borderless Windowed (registre o modo escolhido) |
| **EVE Version** | Build exato do cliente (número de versão) |
| **SHA/Binário** | Commit SHA ou tag do build testado |
| **Config EVE-O** | `WineCompatibilityMode`, `Refresh period`, `MinimizeInactiveClients`, `Zoom`, `Opacity`, `ShowThumbnailOverlays` |
| **Clientes** | A–F no monitor principal, I (intel) no secundário |
| **Grupo de Ciclo** | A–F apenas; I explicitamente excluído do ciclo |
| **Prioridade I** | Configurada conforme uso real |
| **Ferramentas** | `wmctrl`, `xdotool`, `xvinfo`, `glxinfo`, `taskset` (opcional) |
| **Backup** | Salvar configuração antes de iniciar. Ambiente seguro no jogo (estação, docked). |

> **Comando SHA:** `git rev-parse HEAD` na raiz do checkout. Registre o SHA na tabela da seção 4.

### 1.1 Build/SHA e Isolamento da Configuração

#### 1.1.1 Confirmar build/SHA

```bash
git status --short
git rev-parse HEAD
```

#### 1.1.2 Cópia separada de `EVE-O-Preview.json`

Feche a instância habitual do Preview. Na mesma sessão do terminal, copie o JSON habitual para o diretório de teste com a flag `WineCompatibilityMode = true`.

```bash
# Exemplo de atualização da configuração
python3 -c "
import json, pathlib
config_path = pathlib.Path.read_text(pathlib.Path.home() / '.config' / 'EVE-O-Preview.json')
config = json.loads(config_path)
config['WineCompatibilityMode'] = True
(pathlib.Path('/tmp/eveo-test') / 'EVE-O-Preview.json').write_text(json.dumps(config, indent=2))
"
```

> `ConfigurationStorage` carrega e salva `EVE-O-Preview.json` no diretório de trabalho na inicialização normal. O arquivo habitual permanece intacto.

#### 1.1.3 Seleção da captura estática vs. DWM no código

A fábrica de thumbnails decide em `ThumbnailViewFactory.Create()` (`src/Eve-O-Preview/View/Implementation/ThumbnailViewFactory.cs`, linhas 18–22):

```csharp
IThumbnailView view = this._enableWineCompatibilityMode
    ? (IThumbnailView)this._controller.Create<StaticThumbnailView>()
    : (IThumbnailView)this._controller.Create<LiveThumbnailView>();
```

- **`EnableWineCompatibilityMode = true`** → `StaticThumbnailView` → `WindowManager.GetStaticThumbnail()` (GDI `BitBlt` via Wine).
- **`EnableWineCompatibilityMode = false`** (padrão) → `LiveThumbnailView` → `DwmThumbnail` (DWM `DwmRegisterThumbnail`).

A flag é lida de `ThumbnailConfiguration.EnableWineCompatibilityMode` (propriedade JSON `WineCompatibilityMode`).

**Diferenciação prática:**

| Caminho | Classe de View | API Nativa | Composição DWM necessária? |
|---------|----------------|------------|----------------------------|
| Estático (GDI) | `StaticThumbnailView` | `BitBlt` / `PrintWindow` via Wine | Não |
| Dinâmico (DWM) | `LiveThumbnailView` | `DwmRegisterThumbnail` via Wine | Sim (se DWM disponível) |

---

## 2. Casos de Validação

> **Notação:** Cada caso pode ser marcado como ☐ Pendente, ☑ Aprovado ou ⛔ Reprovado. Na coluna "Evidência" indique o arquivo/registro correspondente.

### 2.1 Básico — Identificação e inicialização

| ID | Caso | Procedimento | Critério de Aceite | Status | Evidência |
|----|------|--------------|-------------------|--------|-----------|
| **M1** | Build/SHA | Rode `git rev-parse HEAD` e registre o SHA. Inicie o EVE-O Preview com a configuração de teste. | SHA registrado na tabela; Preview sobe sem erros visíveis | ☐ | `git-log.txt` |
| **M2** | Configuração isolada | Use a cópia separada do `EVE-O-Preview.json` no diretório temporário. | Configuração habitual inalterada; configuração de teste usada exclusivamente | ☐ | `EVE-O-Preview.json (teste)` |

### 2.2 Thumbnails com conteúdo — Ciclo F13/F14

> **Contexto:** F13 e F14 são hotkeys de ciclo de grupo de clientes (anterior/próximo). Testar com 7 clientes ativos.

| ID | Caso | Procedimento | Critério de Aceite | Status | Evidência |
|----|------|--------------|-------------------|--------|-----------|
| **M3** | Thumbnail inicial | Com 7 clientes rodando, aguarde 30s ocioso. Clique na thumbnail do cliente A. | A thumbnail mostra conteúdo visível (não preta) e o cliente A vem ao foreground | ☐ | Screenshot com timestamp |
| **M4** | Ciclo F13 (cliente anterior) | Pressione F13 10 vezes seguidas (ciclo A → F → E → D → C → B → I → A → ...). | A thumbnail atualiza a cada pressionamento; a ordem do ciclo é preservada; a thumbnail I nunca entra no grupo A–F | ☐ | Log de keystrokes + screenshots |
| **M5** | Ciclo F14 (cliente próximo) | Pressione F14 10 vezes seguidas (ciclo A → B → C → D → E → F → I → A → ...). | A thumbnail atualiza a cada pressionamento; a ordem do ciclo é preservada; I permanece no secundário / excluído do ciclo | ☐ | Log de keystrokes + screenshots |
| **M6** | Spotlight em thumbnail | Passe o mouse sobre a thumbnail de um cliente minimizado/background. | Hover revela zoom/overlay se `ThumbnailZoomEnabled` / `ShowThumbnailOverlays`; rótulo identifica o cliente corretamente | ☐ | Screenshot/video com hover |

### 2.3 Minimizar / Restaurar

| ID | Caso | Procedimento | Critério de Aceite | Status | Evidência |
|----|------|--------------|-------------------|--------|-----------|
| **M7** | Minimizar cliente A | Minimize o cliente A (botão da janela ou `wmctrl -i -r <id> -b add,minimize`). | A thumbnail A mantém o último quadro ou fica preta; o Preview continua respondendo; as outras thumbnails não mudam | ☐ | Screenshot antes/depois |
| **M8** | Restaurar cliente A | Restaurar cliente A (clique na thumbnail ou `wmctrl -i -r <id> -b remove,minimize`). | A thumbnail A volta a mostrar conteúdo vivo após os ciclos de refresh; não é necessário reiniciar o EVE-O-Preview | ☐ | Screenshot/video com timestamp |
| **M9** | Minimizar todos | Minimize todos os 7 clientes simultaneamente (hotkey ou script). | As 7 thumbnails mantêm o último quadro; Preview não crash; recursos GDI/USER estáveis | ☐ | Screenshot + `GDI/USER` readings |

### 2.4 Abrir / Fechar Cliente

| ID | Caso | Procedimento | Critério de Aceite | Status | Evidência |
|----|------|--------------|-------------------|--------|-----------|
| **M10** | Fechar cliente B | Feche o cliente B normalmente (Quit in-game ou fechar janela). | O processo B é removido; a thumbnail B some na UI; não permanece "ghost thumbnail" | ☐ | Screenshot após fechar; logs do processo |
| **M11** | Reabrir cliente B | Reabra o mesmo cliente B (launcher → Play). | Novo processo/handle detectado; nova thumbnail aparece e responde a cliques; a thumbnail antiga some definitivamente | ☐ | Screenshot após reabrir; logs |
| **M12** | Fechar durante ciclo | Durante alternância de ciclo, feche cliente B. | As thumbnails restantes continuam; sem destaque fantasma; GDI/USER estáveis | ☐ | Log de recursos durante e após |

### 2.5 Spotlight, Recorte, Redimensionamento e Opacidade

> **Contexto:** Estas etapas validam a apresentação visual das thumbnails em diferentes condições de layout e opacidade.

| ID | Caso | Procedimento | Critério de Aceite | Status | Evidência |
|----|------|--------------|-------------------|--------|-----------|
| **M13** | Opacidade de thumbnail | No JSON de teste, altere `ThumbnailsOpacity` para 0.5. Reinicie o Preview. | As thumbnails aparecem com 50% de opacidade; cliques ainda funcionam | ☐ | Screenshot com opacidade 0.5 |
| **M14** | Redimensionamento de janela | Redimensione a janela do Preview (arraste a borda). | As thumbnails se readequam ao novo tamanho; não há sobreposição de conteúdo ou quebra de layout | ☐ | Screenshot antes/depois do redimensionamento |
| **M15** | Spotlight ativo | Clique em uma thumbnail; verifique se o spotlight (sinal visual de foco) aparece sobre o cliente correspondente. | Spotlight coincide com o cliente ativado; destaque coerente após estabilizar | ☐ | Screenshot/video com spotlight |
| **M16** | Recorte (crop) em multi-monitor | Tenha cliente I no monitor secundário com DPI diferente. Clique na thumbnail I. | A thumbnail I reflete o conteúdo do monitor secundário; posicionamento correto; não mistura com thumbnails do monitor principal | ☐ | Screenshot da thumbnail I |

### 2.6 Coleta de GDI/USER antes/depois

> **Ferramentas:** No Linux, use `wmctrl` + `xprop` ou scripts `GetGuiResources` via Wine. O equivalente das colunas GDI/USER do Task Manager em Windows pode ser verificado com scripts de monitoramento.

| ID | Caso | Procedimento | Critério de Aceite | Status | Evidência |
|----|------|--------------|-------------------|--------|-----------|
| **M17** | Baseline GDI/USER | Inicie EVE-O-Preview com 7 clientes rodando; aguarde 30s ocioso. | Anote GDI/USER do processo `EVE-O-Preview.exe` (via script Wine ou `wmctrl`/`xprop`) | ☐ | `gdi-user-baseline.txt` |
| **M18** | Após ciclo F13/F14 | 10 voltas F13 + 10 F14. | Anote GDI/USER a cada 2 voltas. Delta esperado: **estável** (sem crescimento contínuo) | ☐ | `gdi-user-after-cycles.txt` |
| **M19** | Alternância rápida | 5 min de cliques manuais A–F intercalando I; rajadas A/B/A. | Anote picos de GDI/USER e a leitura após 1 min ocioso. Sem crescimento persistente | ☐ | `gdi-user-rapid-switch.txt` |
| **M20** | Fechar/reabrir B | Feche B; depois reabra. | GDI/USER não sobem permanentemente após fechamento/reabertura; retornam ao baseline | ☐ | `gdi-user-close-reopen.txt` |
| **M21** | Recuperação | 1 min ocioso após M19/M20. | Callbacks e thumbnails respondem; GDI/USER no baseline | ☐ | `gdi-user-recovery.txt` |

> **Critério de aceite GDI/USER:** Compare as leituras após o aquecimento e após os períodos ociosos. Crescimento persistente em rodadas equivalentes exige investigação. O limite +8 GDI e +8 USER (aproximado, varia por ambiente) serve como referência para este teste manual; qualquer coisa além disso em múltiplas rodadas idênticas indica possível vazamento.

### 2.7 Tabela preenchível de evidências

> **Preencher durante a execução dos testes.** Cada linha representa um registro de validação.

| SHA Testado | Ambiente (Distro, Wine/Proton versão, GPU/driver, monitores, DPI, modo janela EVE, versão EVE) | Etapa (Seção) | Aprovação Parcial / Falha / Passou | Observações | Evidência |
|-------------|-----------------------------------------------------------------------------------|---------------|-----------------------------------|-------------|-----------|
| `PLACEHOLDER` | (preencher) | 2.1 Build/SHA | | | |
| `PLACEHOLDER` | idem | 2.2 Thumbnails conteúdo | | | |
| `PLACEHOLDER` | idem | 2.3 Minimizar/Restaurar | | | |
| `PLACEHOLDER` | idem | 2.4 Abrir/Fechar cliente | | | |
| `PLACEHOLDER` | idem | 2.5 Spotlight/Opacidade/Recorte | | | |
| `PLACEHOLDER` | idem | 2.6 GDI/USER coleta | | | |

> **Legenda:**
> - **Parcial:** Funciona mas com ressalvas (ex.: thumbnail preta ao minimizar, crescimento lento de GDI, opacidade não aplicada corretamente).
> - **Falha:** Comportamento fora do especificado (crash, ghost thumbnail, vazamento GDI/USER contínuo, thumbnail sempre preta, destaque incoerente).
> - **Passou:** Todos os critérios de aceite atendidos.

> **Nota:** o resultado desta tabela depende do build, configuração Wine e estado dos clientes EVE. Uma execução isolada não serve como aprovação definitiva — recomenda-se repetir em pelo menos 3 rodadas consecutivas.

---

## 3. Referências de Código (para rastreabilidade)

| Funcionalidade | Arquivo | Linhas / Método |
|----------------|---------|-----------------|
| Fábrica de thumbnails (estático vs DWM) | `src/Eve-O-Preview/View/Implementation/ThumbnailViewFactory.cs` | `Create()` (18–22) |
| Captura estática GDI | `src/Eve-O-Preview/Services/Implementation/WindowManager.cs` | `GetStaticThumbnail()` (298–434) |
| Ativação no Wine | `src/Eve-O-Preview/Services/Implementation/WindowManager.cs` | `WineActivateWindow()` (129–170) |
| Minimizar janela | `src/Eve-O-Preview/Services/Implementation/WindowManager.cs` | `MinimizeWindow()` (184–198) |
| Configuração `WineCompatibilityMode` | `src/Eve-O-Preview/Configuration/Implementation/ThumbnailConfiguration.cs` | Property `EnableWineCompatibilityMode` (242–243) |
| Armazenamento JSON | `src/Eve-O-Preview/Configuration/Implementation/ConfigurationStorage.cs` | `Load()`/`Save()` (19–56) |
| Refresh threshold (forçado) | `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs` | `FORCED_REFRESH_CYCLE_THRESHOLD = 2` (27) |
| Coleta de recursos GDI/USER | (script manual) | `wmctrl`/`xprop` ou equivalente Wine |

---

## 4. Próximos Passos Pós-Validação

1. **Resolver problemas identificados** na tabela da seção 2 (GDI/USER growth, thumbnails pretas, crash, etc.)
2. **Ajustar configuração** `WineCompatibilityMode` / `ThumbnailsOpacity` / `Refresh period` conforme necessário
3. **Revalidar** em novas rodadas caso haja alterações no build ou configuração
4. **Preencher tabela de evidências** (seção 2.7) e gerar artifacts (screenshots, logs, GDI/USER readings)
5. **Definir política de ordenação A/B** se houver ativações concorrentes no Linux/Wine

---