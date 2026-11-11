---
name: eveo-gdi-safety
description: 'Use para auditar recursos Win32/GDI e DWM, cleanup e injecao de falhas no EVE-O Custom. Nao use para layout multibox, pesquisa de forks ou revisao generica sem recursos nativos.'
---

# Auditoria de recursos nativos

## Entrada e limites
Leia AGENTS.md e CLAUDE.md aplicáveis, quando presentes. Fixe o SHA e os caminhos auditados; obtenha o cenário de captura e descarte. Auditoria é somente leitura; alterações e testes novos dependem do escopo solicitado. Respeite o escopo autorizado da tarefa: alterações em produção, workflows ou PRs exigem instrução explícita correspondente; não deduza autorização de números de PR, nomes de branch ou labels. Não execute Git destrutivo, broadcast de comandos, injeção no jogo ou automação de gameplay.

## Procedimento
1. Localize aquisições e consumidores em `Services/Interop`, `Services/Implementation` e `View/Implementation` sob `src/Eve-O-Preview`. Siga todos os caminhos de sucesso, retorno antecipado, exceção e Dispose; não conclua vazamento apenas pela ausência local de um finally.
2. Monte uma tabela recurso → aquisição → proprietário → transferência → liberação. Distinga handles emprestados dos próprios: GetDC/GetWindowDC usam ReleaseDC com a janela correspondente; CreateCompatibleDC usa DeleteDC; objetos GDI próprios usam DeleteObject; registro DWM bem-sucedido usa DwmUnregisterThumbnail. Confira contratos e valores de erro nas fontes oficiais quando houver dúvida.
3. Para SelectObject, retenha e restaure o objeto anterior antes de destruir o objeto selecionado e seu DC; não destrua o objeto anterior emprestado. Confira validade de handles, HRESULTs, retorno do cleanup, descarte de imagens gerenciadas, dupla liberação e concorrência entre captura, fechamento e Dispose.
4. Identifique o caminho, revisão, comando e capacidades reais do harness isolado. Para captura real, exija cenário reproduzível, PID medido, tentativas e resultados contabilizados, séries GDI/USER com timestamps e descarte observável; mocks validam caminhos de falha, não stress real. Sem suporte à injeção, reporte a lacuna e proponha teste, sem presumir ferramenta existente. Em harness isolado com suporte confirmado, injete falhas nas fronteiras Win32/DWM por wrappers ou mocks, nunca no jogo: falha de aquisição inicial, aquisição parcial, seleção, cópia, conversão e liberação. Verifique que apenas recursos adquiridos são liberados, na ordem correta, que o cleanup restante continua após falha e que o erro original não é mascarado. Não repita liberação de handle com estado incerto sem contrato que permita isso.
5. Registre contagens GDI/USER antes, durante e após descarte, com aquecimento e repetições comparáveis. Separe caches e ruído de crescimento persistente; correlacione a medição ao caminho de ownership. Testes ausentes devem ser propostas, nunca resultados presumidos.

## Saída
Informe SHA, escopo, tabela de ownership, testes/comandos e resultados. Para cada achado: arquivo:linha, caminho alcançável, evidência, impacto e correção sugerida. Separe defeitos confirmados de hipóteses e lacunas de cobertura.

## Falha ou bloqueio
Sem ambiente Windows, harness ou cenário reproduzível, entregue a análise estática e liste a validação pendente. Falha de cleanup comprovada reprova o caso; contagem isolada ou ausência de teste não prova vazamento.
