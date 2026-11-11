# TM-04 — revisão de nulabilidade, integração TM-01 e validação Windows 11

Data: 2026-11-11. PR #16 permanece Draft.

## Referências e escopo

- Base comum dos PRs #9 e #16: `3479ae71621c08a0843b03ab226752af933f3a1b`.
- TM-01 (#9): `664884ce6a9d6c78355aefca970bb65c46c13558`.
- TM-04 original: `6a568a1890c92fb5e947024e733c8e0f220814c4`.
- Correção de nulabilidade: `059f3e519ab3e3ed2bb27a42cdae3fee4f32cc43`.
- [CI original](https://github.com/Psykolic/eve-o-custom/actions/runs/43092665611): jobs de build, testes e resumo concluídos com success; log de testes confirma 12 passed, 0 failed, 0 skipped. Os cinco resultados individuais no TRX original são contexto confirmado pelo solicitante.
- Executor local indisponível (falha ao iniciar processo); nenhum build/teste local é alegado.

A única alteração de código desta revisão está no arquivo de testes. `Post`/`Send` e a fila aceitam `object?`, conforme SynchronizationContext. A supressão `!` no valor obtido por reflexão explicita a invariável de que o campo readonly inicializado pelo construtor contém o dicionário. Não adiciona condição, exceção ou operação em runtime. Corrige os quatro diagnósticos originais: CS8765 (linhas 37/42), CS8600 e CS8603 (linha 126). As cinco Facts, sincronização, timeouts, mocks e asserts permanecem iguais.

## Compatibilidade #9 × #16

Revisão dos patches completos nos SHAs acima, com a mesma base:

| Área | TM-01 (#9) | TM-04 (#16) | Avaliação por código |
| --- | --- | --- | --- |
| Hotkeys | Ignora enumeráveis nulos | Não altera | Independente; fixture TM-04 mantém listas vazias |
| Remoção | TryRemoveThumbnail, desliga sete callbacks, remove antes de Close | Revalida identidade após ativação | Complementar: callback já enfileirado continua possível após detach; revalidação continua necessária |
| Refresh | try/finally reabilita eventos | Chama refresh somente após ativação bem-sucedida e view atual | Complementar; finally não absorve exceção de refresh |
| Ativação | Não altera ThumbnailActivated | Guarda ID, fault/cancel e identidade | Nenhuma alteração concorrente no mesmo método |
| Testes | Três Facts em Windows/ThumbnailManagerStabilityTests.cs | Cinco Facts em Services/ThumbnailManagerActivationTests.cs | Arquivos distintos; execução conjunta ainda pendente |

Os hunks de produção são distintos e não se sobrepõem na base comum; não foi identificado conflito textual por inspeção. Ambos apareciam individualmente mergeable com main na consulta inicial. Isso **não comprova** merge limpo entre os dois heads: não foi executado git merge-tree/rebase nem build combinado. Não foi realizado merge, nem alterado #9/main.

Não foi identificada incompatibilidade comportamental direta entre as duas correções. A remoção TM-01 reforça a condição esperada pela revalidação TM-04. Porém, a suite TM-04 retorna antes do corpo de refresh (foreground zero); não exercita o finally TM-01 nem a remoção real. Antes de integrar, executar uma combinação descartável dos SHAs, revisar eventual conflito preservando ambas as proteções e executar toda a suite (incluindo as três Facts TM-01 e cinco TM-04). Bloquear integração se houver conflito funcional ou falha; não resolver por escolha automática de um dos lados.

## Limites e decisões humanas pendentes

- **Bloqueio de aceite funcional:** ativações A/B concorrentes de views ainda válidas podem completar fora de ordem; a identidade da view não estabelece “último clique vence”. Nenhuma política de ordenação foi introduzida. Requer decisão do mantenedor e reprodução/validação manual antes de prometer essa semântica.
- A checagem ocorre após o trabalho em background: não garante que ActivateWindow nunca leia uma view retirada durante o trabalho. Os testes demonstram proteção da transição de estado, não toda a vida útil da view.
- Exceções em SwitchActiveClient/UpdateClientLayouts/RefreshThumbnails pertencem à continuação; o tratamento de task.IsFaulted observa a tarefa de ativação antecedente. TM-01 restaura eventos, mas não transforma falha posterior em sucesso.
- Cancelamento só foi revisado por código; nenhuma das cinco Facts o testa. Shutdown, foco real Win32, DWM/GDI, minimização e layouts reais também não estão cobertos.
- Debug.WriteLine exige build Debug para coleta; ausência da mensagem no Release não prova ausência de falhas.
- Nenhuma alteração de produção será feita nesta revisão para esses pontos. Aceite e eventual correção precisam de autorização específica.

## Roteiro manual Windows 11 — seis principais + intel

**Status de todos os casos abaixo: NÃO EXECUTADO.** Preencher resultado real, horário, build SHA e evidência; CI não substitui esta sessão.

Preparar backup da configuração e ambiente seguro no jogo. Registrar versão/build Windows 11, GPU/driver, monitores/resolução/DPI, modo de janela, versão EVE, SHA/binário, período de refresh, animação, minimização, layout tracking, zoom e click-through. Identificar clientes A–F no principal e I (intel) no secundário. Configurar grupo de ciclo só A–F e exclusão explícita de I; registrar configuração de prioridade de I, se usada. Sem broadcasts ou automação de gameplay.

| Caso | Procedimento manual | Critério / evidência |
| --- | --- | --- |
| M1 Básico | Após 30 s ocioso, clicar A–F e I, um por vez; repetir 3 vezes | Janela correta em foreground, destaque coerente após estabilizar; screenshot/vídeo |
| M2 Ciclo | 10 voltas para frente e 10 para trás usando hotkeys do grupo A–F | Ordem configurada preservada; I nunca entra no grupo e permanece no secundário |
| M3 Alternância rápida | 5 min de cliques A–F, intercalando I; incluir rajadas A/B/A, aguardar estabilização entre rajadas | Sem crash/travamento; anotar qualquer divergência entre último clique, foco real e destaque, sem presumir política de ordenação |
| M4 Fechamento | Durante alternância, fechar B normalmente; repetir com outro cliente via encerramento do processo em ambiente seguro | View desaparece após atualização da lista; registrar latência e período configurado; restantes continuam; nenhum destaque fantasma persistente |
| M5 Reabertura | Reabrir cliente fechado, aguardar registro e clicar sua nova miniatura | Nova view ativa corretamente; não presumir reutilização de handle (essa condição é simulada no teste unitário) |
| M6 Intel | Ativar I e voltar a A; repetir durante ciclo e com minimização na configuração de uso real | I mantém monitor/layout esperado e exclusão do ciclo; confirmar prioridade configurada |
| M7 Configurações | Repetir M1/M3 com layout tracking e minimização usados pelo operador; registrar cada combinação | Sem movimentação/minimização inesperada; foco/destaque convergem |
| M8 Recuperação | Após M4/M5, deixar 1 min ocioso e repetir M1/M2 | Callbacks e miniaturas restantes continuam respondendo |
| M9 Encerramento | Fechar EVE-O durante alternância e reiniciar com os sete clientes | Sem erro visível; configuração restaurada; registrar eventos/logs |

Coletar vídeo com foco/destaque visíveis, timestamps, configuração sem dados sensíveis, Event Viewer e saída Debug se disponível. Uma falha real de ActivateWindow não deve ser induzida alterando produção; registrar ocorrência espontânea e correlacionar com o foco. A injeção de exceção pertence ao teste automatizado.

Para cada caso preencher: executor, data, SHA, configuração, esperado, observado, Passed/Failed/Blocked, anexos e passos de reprodução. Em crash, estado fantasma ou foco incoerente persistente, interromper o cenário afetado, preservar evidência e registrar bloqueio no PR #16. Não autorizar merge com casos pendentes ou política de ordenação indefinida.
