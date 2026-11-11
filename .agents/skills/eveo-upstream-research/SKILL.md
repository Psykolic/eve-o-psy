---
name: eveo-upstream-research
description: 'Use para pesquisar e comparar forks Proopai, Muppet, BIGMOS e EveOPlus, com procedencia, licencas e riscos. Nao use para importar codigo externo, presumir compatibilidade de licencas ou implementar gameplay automatizado.'
---

# Pesquisa upstream com procedência

## Entrada e limites
Leia AGENTS.md e CLAUDE.md aplicáveis. Defina a pergunta técnica, SHA local e recursos a comparar. Pesquise os quatro nomes sem assumir que nomes semelhantes identificam o mesmo projeto. Use conectores de leitura ou navegação disponíveis, sem instalar dependências. Respeite o escopo autorizado da tarefa: alterações em produção, workflows ou PRs exigem instrução explícita correspondente; não deduza autorização de números de PR, nomes de branch ou labels. Não publique comentários, faça Git destrutivo, broadcast, injeção no jogo ou automação de gameplay.

## Procedimento
1. Para Proopai, Muppet, BIGMOS e EveOPlus, encontre repositório canônico, proprietário, URL, relação de fork/upstream e sinais de identidade. Se houver ambiguidade, liste candidatos e deixe a atribuição pendente.
2. Fixe SHA completo, branch consultada e data de acesso. Abra README, LICENSE, cabeçalhos relevantes, histórico e os arquivos da implementação; cite permalinks ao SHA e linhas. Diferencie documentação, código observado e comportamento efetivamente testado.
3. Compare mecanismos concretos relacionados à pergunta: captura GDI/DWM, ownership/cleanup, ciclo/foco, isolamento intel, monitores/DPI e testes/CI. Marque ausente, não encontrado ou não verificável com escopo de busca; não confunda esses estados.
4. Registre licença na revisão consultada, exceções por arquivo e origem das dependências relevantes. Não copie nem transplante código GPLv3. Não interprete ausência de LICENSE como permissão. Se termos forem incertos ou incompatíveis, limite a entrega à análise conceitual e sinalize revisão de licenciamento antes de qualquer reutilização.
5. Avalie riscos com evidência: recursos nativos, concorrência, plataforma, manutenção, cobertura de testes e licenciamento. Popularidade e atividade recente não provam segurança. Proponha princípios ou requisitos para implementação local independente, sem reproduzir trechos externos; não sugira broadcast, injeção ou automação de gameplay.

## Saída
Matriz dos quatro forks: identidade/URL, SHA/data, mecanismo/arquivo, licença/origem, evidência, risco e aplicabilidade local. Separe fatos, inferências e desconhecidos. Inclua recomendação técnica, alternativas e verificações locais necessárias; não declare conclusão jurídica definitiva.

## Falha ou bloqueio
Sem acesso, SHA verificável, identidade ou licença, marque o item inconclusivo e explique o dado faltante. Não invente URLs, licenças ou funcionalidades e não reutilize código para contornar o bloqueio.
