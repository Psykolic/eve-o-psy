# Biblioteca compartilhada de Agent Skills v1.1

Esta biblioteca fornece procedimentos para EVE-O Custom em Codex CLI e Claude Code. Não altera produção e não executa automaticamente build, captura ou operações remotas.

## Arquitetura

Cada skill é um diretório autônomo contendo somente `SKILL.md`, com frontmatter YAML limitado a `name` e `description`. A descrição define uso e exclusões; o corpo define entrada, procedimento, saída e falhas.

| Skill | Finalidade |
| --- | --- |
| `eveo-gdi-safety` | Ownership Win32/GDI/DWM, ordem de cleanup e falhas injetadas em harness isolado. |
| `eveo-ci-validation` | Build/testes Windows .NET 8, evidências Actions e 1.000 capturas comparadas com baseline fixado. |
| `eveo-multibox-6plus1` | Seis principais no primário e intel independente no secundário, com matriz de testes. |
| `eveo-upstream-research` | Comparar Proopai, Muppet, BIGMOS e EveOPlus com SHAs, procedência, licença e riscos. |
| `eveo-pr-review` | Diff desde merge-base, regressões demonstráveis, P0/P1/P2 e recomendação de aprovação. |

`.agents/skills/<nome>/SKILL.md` atende ao Codex CLI; `.claude/skills/<nome>/SKILL.md` atende ao Claude Code. Os pares são cópias físicas idênticas, sem symlinks, caminhos pessoais ou metadados exclusivos de um agente. `.agents` é a fonte de manutenção; `.claude` é o espelho distribuído. Cada agente carrega somente seu diretório nativo.

## Instalação e invocação

Mantenha ambos os diretórios na raiz do checkout do projeto e inicie o agente nessa raiz. Para outro checkout, copie os cinco diretórios para os mesmos caminhos, preservando o conteúdo; não é necessário instalar pacotes ou configurar servidores. Abra uma nova sessão se o agente já estava aberto. A descoberta exige uma versão do agente com suporte ao respectivo diretório de skills; se não aparecerem, confira a versão e sua documentação local de descoberta.

No Codex CLI, exemplos de prompts:

```text
$eveo-gdi-safety Audite ownership e cleanup de StaticThumbnailView no SHA atual, sem editar arquivos.
$eveo-ci-validation Valide este SHA contra o baseline <SHA-completo>; reporte etapas indisponíveis.
$eveo-multibox-6plus1 Revise o isolamento do intel e proponha a matriz de testes de dois monitores.
$eveo-upstream-research Compare o cleanup GDI dos quatro forks, sem importar código.
$eveo-pr-review Revise o PR <URL> em leitura e registre base, head e merge-base.
```

No Claude Code, invoque os comandos de skills:

```text
/eveo-gdi-safety Audite ownership e cleanup sem editar arquivos.
/eveo-ci-validation Compare o candidato <SHA> com o baseline <SHA-completo>.
/eveo-multibox-6plus1 Teste o ciclo dos seis e a independência do intel.
/eveo-upstream-research Compare GDI/DWM nos quatro forks com permalinks.
/eveo-pr-review Revise o PR <URL> sem publicar comentários.
```

Substitua os placeholders por dados reais. A descrição também permite seleção automática em tarefas correspondentes. Invocar uma skill não autoriza publicação, instalação de dependências ou alterações fora do escopo solicitado.

## Manutenção e verificação

Leia primeiro as instruções locais AGENTS.md e CLAUDE.md quando existirem. Edite a cópia em `.agents`, replique a alteração para o mesmo caminho em `.claude` e rode na raiz:

```powershell
powershell -NoProfile -File .\scripts\verify-agent-skills.ps1
# Em PowerShell 7, use pwsh no lugar de powershell.
```

Se a política local bloquear scripts e as regras do ambiente permitirem uma exceção por processo, use `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-agent-skills.ps1`. Isso não altera a política persistente do sistema.

De outro diretório, passe `-RepositoryRoot <caminho-do-checkout>`. O script não modifica arquivos nem instala dependências; resolve o padrão pela localização do próprio script. Exit code 0 indica sucesso; 1 indica arquivo ausente, UTF-8 inválido, frontmatter inválido, nome divergente, corpo vazio ou diferença de bytes. A execução informa os problemas encontrados.

O validador aceita um subconjunto deliberado e válido de YAML: delimitadores `---`, `name` simples e `description` em aspas simples, nessa ordem, sem campos adicionais. Aspas simples internas devem ser duplicadas. Ele rejeita outros estilos YAML válidos para evitar um parser externo; não é um parser YAML genérico. A igualdade é byte a byte, incluindo BOM e finais de linha. Use UTF-8 sem BOM e finais de linha LF nos dois arquivos. O .gitattributes fixa LF especificamente para os dez SKILL.md; a comparação continua byte a byte, sem normalizar conteúdo no verificador.

Confira também o diff para garantir que produção, workflows existentes e instruções de agentes existentes não foram alterados. Faça revisão comportamental: aquisição parcial deve exigir cleanup dos recursos adquiridos; artefato ausente deve resultar em inconclusivo; intel não deve entrar no ciclo principal; licença desconhecida deve impedir reutilização; head/base incorretos devem impedir conclusão de regressão. A validação estrutural não comprova essas decisões do agente.

## Limites e segurança

As skills não incluem harness de captura nem implementam funcionalidades de produto. Build e testes de aplicação exigem Windows, SDK .NET 8 e dependências disponíveis. Stress exige harness real, cenário, limites definidos previamente e baseline por SHA completo; sem isso não há aprovação de stress. CI verde não comprova as 1.000 capturas. Pesquisa depende de acesso às fontes e revisão de licença; nomes de forks não garantem identidade e nenhum código GPLv3 deve ser copiado.

As instruções vedam Git destrutivo automático, broadcast, injeção no jogo e automação de gameplay. Falhas são injetadas em wrappers/mocks de teste, nunca no jogo. Revisões priorizam evidência verificável e separam hipótese de defeito. A autorização vem do escopo explícito da tarefa, sem depender de números de PR ou labels presumidos. A biblioteca não substitui controles de permissão dos agentes.

Nesta entrega, não fazer commit, push ou merge; revisar os arquivos antes de publicar. O workflow independente .github/workflows/agent-skills-integrity.yml executa o verificador sem alterar o workflow de build/testes existente.

## Contrato YAML restrito da versão 1

A versão 1 aceita somente as chaves name e description, nesta ordem e sem campos personalizados, comentários ou linhas extras no frontmatter. Formatação canônica:

```yaml
---
name: eveo-gdi-safety
description: 'Descrição não vazia em uma única linha; aspas internas como ''exemplo''.'
---

# Corpo Markdown não vazio
```

name deve coincidir com o diretório e conter somente letras minúsculas ASCII, dígitos e hífens. description usa aspas simples e duplica aspas simples internas; não usa blocos multilinha, aliases ou tags YAML. Outros estilos YAML válidos são rejeitados deliberadamente. O verificador rejeita BOM, UTF-8 inválido e corpo vazio; aceita CRLF por compatibilidade, mas a forma de manutenção recomendada e fixada no checkout é LF.

## Encaminhamento para agentes especializados

A matriz orienta a escolha conforme a tarefa; não inicia agentes automaticamente nem amplia suas permissões. Os cinco agentes reais estão em .claude/agents. Uma skill também pode ser usada diretamente sem delegação.

| Skill | Agente principal | Apoio conforme o escopo |
| --- | --- | --- |
| eveo-gdi-safety | performance | developer para correção autorizada; qa-tester para cobertura |
| eveo-ci-validation | qa-tester | performance para interpretar stress; developer para correção autorizada |
| eveo-multibox-6plus1 | ui-designer | architect para isolamento; qa-tester para matriz |
| eveo-upstream-research | architect | performance para captura; ui-designer para monitores |
| eveo-pr-review | architect | developer para análise C#; qa-tester para evidências |

Somente developer declara ferramentas de edição e Bash; os outros quatro declaram Read, Glob e Grep. Não encaminhe execução de testes ou edição a um agente que não tenha ferramentas para isso: use o executor disponível e autorizado ou registre a pendência.

### Modelos Claude conferidos no checkout

Os arquivos existentes declaram: architect e performance = nvidia/nemotron-3-ultra-550b-a55b:free; developer = cohere/north-mini-code:free; qa-tester = nvidia/nemotron-3.5-lightning:free; ui-designer = nvidia/nemotron-3.5-super-vl:free. Foram preservados. Não há configuração de provedor/roteamento no checkout que comprove sua resolução no Claude Code. Confirmar no ambiente integrado antes de substituir identificadores; a inspeção estática não comprova disponibilidade dos modelos.

## Harness e integração

Os critérios mínimos de captura estão em eveo-ci-validation e os requisitos de injeção isolada em eveo-gdi-safety. Eles descrevem um contrato para avaliar um harness real, não uma ferramenta fornecida por esta biblioteca. Sem harness adequado, reporte stress inconclusivo e mantenha os resultados estáticos separados.

A descoberta das cinco skills está confirmada no catálogo nativo desta sessão Codex. Isso não comprova carregamento do conteúdo atualizado em uma nova sessão nem descoberta no Claude Code. O workflow de integridade só terá execução remota após integração; a validação local não é um run de GitHub Actions.