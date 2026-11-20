using System.Collections.Generic;
using System.Threading;

namespace EveOPreview.Refresh
{
	/// <summary>
	/// Sinal coalescido de "algo mudou nos clientes" usado pelo modelo push-based (Fase 2.2).
	///
	/// Produtores (hooks de janela, eventos de processo) publicam sem se preocupar com
	/// volume; o consumidor recebe no máximo um sinal pendente por vez. Isso evita que
	/// uma rajada de eventos (ex.: troca de foco entre clientes) vire uma fila de refreshes.
	/// </summary>
	public interface IClientChangeSignal
	{
		/// <summary>Marca que há mudança pendente. Nunca bloqueia e nunca falha.</summary>
		void Publish();

		/// <summary>Sinaliza uma vez por mudança pendente, colapsando publicações repetidas.</summary>
		IAsyncEnumerable<bool> ReadAllAsync(CancellationToken cancellationToken = default);
	}
}
