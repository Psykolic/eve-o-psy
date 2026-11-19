# Crash log rotativo (issue #141)

O `ExceptionHandler` passou a gravar um arquivo novo por crash, em vez de
sobrescrever sempre `EVE-O-Preview.log`.

## Nome e retenção

- Formato: `EVE-O-Preview-<yyyyMMdd-HHmmss>.log`.
- Dois crashes no mesmo segundo recebem sufixo: `EVE-O-Preview-20261119-123456-1.log`,
  `...-2.log`, e assim por diante.
- Os **5 mais recentes** são mantidos. Arquivos mais antigos são removidos somente
  depois que o dump atual foi gravado com sucesso.
- O diretório é o diretório de trabalho atual do processo. Se ele não existir ou
  tiver caracteres inválidos de Windows, o nome é sanitizado e o diretório é criado
  quando possível. O handler não lança exceção por causa do caminho.
- O antigo `EVE-O-Preview.log` (usado pelo log de compatibilidade do
  `WindowManager`) não é apagado nem reaproveitado.

## Conteúdo do dump

Sem dependências novas, o dump inclui:

- data/hora UTC do crash, PID e thread de origem;
- versão do aplicativo, versão do .NET, arquitetura, descrição do SO e se o build
  é `LINUX` (`#if LINUX`);
- início do processo, uptime e memória de trabalho;
- contagem de handles GDI e USER do processo (`GetGuiResources`);
- caminho do `EVE-O-Preview.json`, tamanho e SHA-256 (ou indicação de ausência);
- as 20 operações mais recentes registradas por `AppOperationLog` (criação e
  remoção de views/spotlights, resize), da mais antiga para a mais recente;
- stack trace completo da exceção.

O `MessageBox` continua sendo exibido e agora inclui o caminho do arquivo de log.

## Validação manual (PR draft)

1. Em um build de debug, provocar uma exceção controlada com um `throw`
   temporário **fora do commit**.
2. Conferir os campos do arquivo gerado.
3. Repetir o crash pelo menos 6 vezes e confirmar que existem no máximo 5 arquivos
   `EVE-O-Preview-*.log`, do mais novo para o mais antigo.
