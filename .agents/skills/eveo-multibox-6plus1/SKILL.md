---
name: eveo-multibox-6plus1
description: 'Use para projetar ou testar seis clientes EVE principais no monitor primario e um intel independente no secundario. Nao use para broadcast, injecao, automacao de gameplay ou layouts sem o requisito 6+1.'
---

# Arquitetura e testes 6+1

## Entrada e limites
Leia AGENTS.md e CLAUDE.md aplicáveis. Registre SHA, configuração, identidade das sete janelas, monitores, resolução, DPI e comandos de foco/ciclo existentes. Confirme quais seis clientes integram o grupo principal e qual é intel; não deduza papel apenas por título ou ordem de enumeração. Respeite o escopo autorizado da tarefa: alterações em produção, workflows ou PRs exigem instrução explícita correspondente; não deduza autorização de números de PR, nomes de branch ou labels. Não execute Git destrutivo. Exclua broadcast, injeção no jogo e automação de gameplay; limite-se à organização de janelas e foco explicitamente comandado pelo usuário.

## Procedimento
1. Mapeie descoberta de processos/janelas, identidade persistida, configuração, grupos de ciclo, foco e thumbnails. Distinga PID/HWND transitórios de identidade persistente; cheque o comportamento quando uma janela é recriada.
2. Estabeleça invariantes: seis principais no primário, intel no secundário; ciclo principal contém só os seis e nunca move, minimiza ou ativa intel por efeito colateral. Intel mantém layout e seleção próprios. Ação de foco individual afeta somente a janela escolhida.
3. Descreva responsabilidades e estado mínimo para grupo principal e intel, indicando os componentes reais a adaptar. Não presuma que isolamento já existe. Proponha mudanças apenas no escopo solicitado, sem implementá-las durante uma auditoria.
4. Faça uma matriz com resultado esperado e observado: sete janelas presentes; percurso completo e retorno do ciclo de seis; foco explícito em intel; fechamento/reabertura de principal e intel; título alterado; HWND obsoleto; minimizar/restaurar; reinício e persistência; DPI misto; coordenadas negativas; troca de primário e desconexão/reconexão do secundário.
5. Para monitor ausente, exija fallback visível dentro da área útil sem incorporar intel silenciosamente ao ciclo principal. Verifique limites físicos/lógicos e restauração após reconexão. Use mocks ou janelas de teste para passos repetidos; no jogo, ações manuais de janela somente, sem emitir comandos de gameplay.

## Saída
Mapa de componentes e identidades; invariantes; matriz cenário → esperado → observado → evidência; regressões com arquivo:linha ou reprodução; propostas mínimas e validação pendente. Registre explicitamente qualquer decisão de fallback ainda não definida.

## Falha ou bloqueio
Contaminação entre grupos, foco involuntário ou janela inacessível reprova o cenário. Sem sete janelas ou dois monitores, reporte cobertura simulada separada da validação física; não declare suporte 6+1 confirmado.
