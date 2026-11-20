using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;

namespace EveOPreview.Refresh
{
	/// <summary>
	/// Implementação padrão do sinal: canal limitado a 1 item com <c>DropOldest</c>.
	///
	/// Medido no spike 2.2 (ADR-006): 180 s com 7 clientes reais geraram 123 eventos de
	/// janela e 97 refreshes coalescidos, com 0,33% de um core (incluindo o overhead do
	/// harness). A varredura por polling que este sinal substitui custa 1,42% de um core
	/// a 2 Hz — ou 76% do idle total do aplicativo.
	/// </summary>
	public sealed class ClientChangeSignal : IClientChangeSignal
	{
		private readonly Channel<byte> _channel;

		public ClientChangeSignal()
		{
			this._channel = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
			{
				FullMode = BoundedChannelFullMode.DropOldest,
				SingleReader = true,
				SingleWriter = false
			});
		}

		public void Publish()
		{
			// TryWrite nunca falha com DropOldest: se já existe um sinal pendente,
			// ele é substituído pelo novo e a informação "há mudança" é preservada.
			this._channel.Writer.TryWrite(0);
		}

		public async IAsyncEnumerable<bool> ReadAllAsync(
			[EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			await foreach (byte _ in this._channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return true;
			}
		}
	}
}
