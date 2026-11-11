---
name: eveo-ci-validation
description: 'Use para validar build e testes .NET 8 no Windows, resultados do GitHub Actions e stress GDI de 1.000 capturas com baseline fixado. Nao use para editar CI, publicar releases ou substituir auditoria de ownership.'
---

# Validação Windows e CI

## Entrada e limites
Leia AGENTS.md e CLAUDE.md aplicáveis. Registre SHA candidato, SHA completo do baseline aprovado, configuração, arquitetura, Windows, SDK .NET 8, GPU/driver, monitores e cenário. Se o baseline não foi informado, proponha um SHA verificável e marque a comparação pendente de sua definição; nunca use uma referência móvel como baseline registrado. Respeite o escopo autorizado da tarefa: alterações em produção, workflows ou PRs exigem instrução explícita correspondente; não deduza autorização de números de PR, nomes de branch ou labels. Não instale dependências externas, execute Git destrutivo, broadcast, injeção no jogo ou automação de gameplay.

## Procedimento
1. Confira os projetos e `.github/workflows/build-and-test.yml` na revisão alvo; não suponha que um job fornece binários ao seguinte. Verifique `dotnet --info` e dependências já disponíveis. Se restore exigir instalação/download não autorizado, pare essa etapa.
2. Em Windows, use os comandos existentes, registrando stdout, stderr e exit code: `dotnet restore src/EVE-O-Preview.sln`; `dotnet build src/EVE-O-Preview.sln --configuration Release --no-restore`; restore do projeto `src/EveO.Tests/EveO.Tests.csproj`; `dotnet test src/EveO.Tests/EveO.Tests.csproj --configuration Release --no-restore --logger "trx;LogFileName=test-results-windows.trx"`. Execute cada etapa apenas após sucesso de seus pré-requisitos. Não trate zero testes descobertos como aprovação.
3. Compare baseline e candidato em diretórios isolados e ambiente equivalente, sem trocar a árvore de trabalho do usuário. Identifique primeiro o harness real de captura. Se inexistente, registre o bloqueio e proponha um teste separado; não invente um comando de stress.
4. Após aquecimento registrado fora da amostra, execute exatamente 1.000 tentativas de captura por revisão com o mesmo cenário. Registre sucessos, falhas e contagens GDI/USER por processo antes, em intervalos fixos, após a última captura e após descarte. Preserve séries brutas, duração, exceções e condições de execução. Inclua fechamento/reabertura no cenário quando suportado pelo harness.
5. Fixe antes da execução o limite aceito de crescimento residual e a regra de comparação com o baseline, usando o contrato existente ou acordo explícito; não escolha tolerância após ver os dados. Investigue crescimento persistente com ownership e repetições; diferença isolada não confirma leak. Sem limite definido, reporte medidas sem declarar aprovação do stress.
6. No Actions, confirme run ID/URL, evento, SHA efetivamente testado (inclusive merge sintético), jobs e conclusão. Abra logs e TRX do artefato `test-results-windows` quando disponível; confira totais, falhas, skips, timestamps e vínculo com a execução. Verifique separadamente evidências de stress: o workflow atual não deve ser presumido como executor das 1.000 capturas.

## Contrato mínimo do harness de captura
Antes do stress, identifique caminho, revisão e comando reais do harness e confirme que ele exercita o caminho de captura e descarte auditado. Um loop vazio ou mock de captura não valida stress GDI real. O harness deve permitir:
- Cenário reproduzível, processo/PID medido identificado, baseline e candidato isolados em condições equivalentes.
- Aquecimento separado e exatamente 1.000 tentativas contabilizadas por revisão, com índice, sucesso/falha, duração e exceções.
- Amostragem GDI/USER do processo antes, em intervalos fixados previamente, ao final e após descarte, com timestamps e dados brutos exportáveis.
- Descarte explícito e evidência de conclusão; limites residuais e regra de comparação definidos antes da execução.

Não imponha nome de ferramenta, API ou formato de exportação inexistente. Registre as capacidades verificadas e ausentes; se faltar requisito mínimo, reporte a etapa inconclusiva. Fechamento/reabertura e injeção de falhas são cenários adicionais quando suportados, não capacidades presumidas.

## Saída
Tabela etapa → comando/run → SHA → resultado → evidência; comparação baseline/candidato com 1.000 tentativas contabilizadas, séries e limites; artefatos com origem e hash quando baixados. Classifique aprovado, reprovado ou inconclusivo por etapa e no conjunto.

## Falha ou bloqueio
Build/teste falho, captura falha ou limite excedido reprova a etapa correspondente. SDK, Windows, credencial, artefato expirado, harness ou baseline ausente tornam a respectiva verificação inconclusiva. Não substitua TRX ausente por um selo verde nem resultados ausentes por estimativas.
