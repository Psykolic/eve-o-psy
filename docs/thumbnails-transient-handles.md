# Thumbnails e spotlight durante ausência transitória da janela (#139)

O `ProcessMonitor` não trata mais a ausência momentânea de `MainWindowHandle` como
fechamento definitivo do cliente EVE.

## Comportamento anterior

Quando `Process.MainWindowHandle` retornava `IntPtr.Zero` por um único ciclo de
monitoramento, o handle continuava no cache de processos conhecidos e era
classificado como removido no fim do ciclo. A thumbnail era fechada e
`CloseSpotlightView` também encerrava o spotlight. O cliente podia voltar, mas o
spotlight dependia da configuração para ser recriado, o que obrigava a
desabilitar/habilitar o cliente na UI.

`MainWindowHandle` fica Zero em situações transitórias comuns: cliente "não
respondendo" em loading/warp/dock, janela minimizada ou oculta, tela de login e
troca de dispositivo/resolução.

## Comportamento atual

- O handle permanece conhecido e a thumbnail/spotlight continuam abertos.
- A remoção só é confirmada após **3 ciclos consecutivos** sem o handle
  (`ProcessSetDiff.DefaultRemovalMissThreshold`). Com o refresh padrão de 500 ms,
  isso equivale a cerca de 1,5 s.
- Mesmo após o limite de ciclos, a remoção é adiada enquanto o processo ou o
  handle da janela ainda existir. Se o processo vivo já expôs outro
  `MainWindowHandle`, o handle antigo é considerado obsoleto e pode ser removido.
- Cada remoção confirmada registra em log o handle, o título, o número de misses
  e o motivo.
- Se o cliente voltar depois de uma remoção confirmada, ele é adicionado de novo
  normalmente.

A máquina de estados é pura e testável em `EveO.Core` (`ProcessSetDiff`), com
testes em `EveO.Core.Tests`. A captura de janelas (GDI/DWM) não foi alterada.
