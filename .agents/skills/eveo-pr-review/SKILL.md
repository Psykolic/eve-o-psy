---
name: eveo-pr-review
description: 'Use para revisar PRs EVE-O Custom com base/head corretos, evidencias de regressao, severidade P0/P1/P2 e checklist de aprovacao. Nao use para implementar correcoes, publicar review ou aprovar/mesclar automaticamente.'
---

# Revisão de PR com evidências

## Entrada e limites
Leia AGENTS.md e CLAUDE.md aplicáveis. Obtenha repositório, número/URL do PR, base ref/SHA e head ref/SHA completos por metadados verificáveis. Trabalhe em leitura no PR solicitado; qualquer alteração depende de escopo explicitamente autorizado, sem pressupor labels; não publique review, commit, push ou merge sem instrução específica. Não execute Git destrutivo, broadcast, injeção no jogo ou automação de gameplay.

## Procedimento
1. Confirme que as revisões estão disponíveis e pertencem ao PR correto; não compare apenas o checkout atual com main local. Registre `git merge-base <base-sha> <head-sha>` como M. Use `git diff --stat M <head-sha>` e `git diff M <head-sha>` para as mudanças do autor; a notação `<base-sha>...<head-sha>` expressa essa comparação. Para divergência atual entre pontas, use comparação de dois SHAs separadamente e explique sua finalidade.
2. Leia arquivos completos e consumidores relevantes. Distinga defeito introduzido, problema preexistente e risco de integração com mudanças posteriores na base. Não atribua ao PR diferenças exclusivas da base. Se SHAs mudarem durante a revisão, invalide a conclusão anterior e revise o delta.
3. Examine ownership Win32/GDI/DWM, cleanup parcial, concorrência, configuração/persistência, foco/ciclo, isolamento 6+1, DPI e compatibilidade Windows/.NET 8 conforme o código alterado. Procure cenários alcançáveis e testes que exercitem o comportamento; não registre hipótese sem evidência como defeito confirmado.
4. Verifique build, testes, stress e artefatos associados ao SHA correto. Diferencie head, merge sintético de CI e baseline. Execute verificações locais somente com ambiente e dependências disponíveis; não instale dependências nem altere workflows. Informe o que ficou sem execução.
5. Classifique achados confirmados: P0 = bloqueio crítico imediato, amplo e demonstrável (por exemplo perda grave de dados); P1 = regressão grave em fluxo suportado, crash recorrente, vazamento persistente ou quebra de isolamento; P2 = defeito de impacto limitado ou cenário menos frequente com alternativa. Justifique gravidade por impacto e condições reais, sem promover incerteza a P0/P1.

## Saída e checklist
Abra com achados por severidade. Cada item contém título P0/P1/P2, arquivo e menor intervalo de linhas relevante no head, cenário, evidência, impacto e direção de correção. Liste hipóteses separadamente. Se nenhum defeito foi confirmado, diga isso com os limites de cobertura.

- [ ] Base, head e merge-base registrados; diff correto e escopo integral lido.
- [ ] Regressões distinguidas de problemas preexistentes e de integração.
- [ ] Testes e artefatos vinculados aos SHAs; lacunas explícitas.
- [ ] Ownership/cleanup e isolamento 6+1 avaliados quando afetados.
- [ ] P0/P1 resolvidos e correções relevantes revalidadas; P2 e riscos pendentes explícitos.

Conclua com recomendação de aprovar, solicitar mudanças ou aguardar evidências, sem executar a ação remota. Aprovação recomendada exige checklist aplicável satisfeito e nenhum bloqueador confirmado; aceite de P2 deve ser explícito.

## Falha ou bloqueio
Sem base/head ou ancestral comum verificável, suspenda conclusões sobre regressão e solicite os dados faltantes. Testes inacessíveis não provam falha de produto, mas podem impedir recomendação de aprovação conforme o risco.
