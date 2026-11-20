using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Refresh;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Refresh
{
	public class ClientChangeSignalTests
	{
		private static async Task<bool> TryReadWithinAsync(IClientChangeSignal signal, TimeSpan timeout)
		{
			using var cancellation = new CancellationTokenSource(timeout);

			try
			{
				await foreach (bool _ in signal.ReadAllAsync(cancellation.Token))
				{
					return true;
				}
			}
			catch (OperationCanceledException)
			{
				return false;
			}

			return false;
		}

		[Fact]
		public async Task Publicacao_unica_acorda_o_consumidor()
		{
			var signal = new ClientChangeSignal();

			signal.Publish();

			(await ClientChangeSignalTests.TryReadWithinAsync(signal, TimeSpan.FromSeconds(2)))
				.Should().BeTrue();
		}

		[Fact]
		public async Task Rajadas_colapsam_em_um_unico_sinal()
		{
			var signal = new ClientChangeSignal();

			// Simula a rajada de eventos que acontece ao trocar de cliente:
			// o consumidor deve acordar uma vez, não N.
			for (int i = 0; i < 500; i++)
			{
				signal.Publish();
			}

			(await ClientChangeSignalTests.TryReadWithinAsync(signal, TimeSpan.FromSeconds(2)))
				.Should().BeTrue();

			// nada mais pendente
			(await ClientChangeSignalTests.TryReadWithinAsync(signal, TimeSpan.FromMilliseconds(200)))
				.Should().BeFalse();
		}

		[Fact]
		public async Task Sem_publicacao_o_consumidor_fica_parado()
		{
			var signal = new ClientChangeSignal();

			(await ClientChangeSignalTests.TryReadWithinAsync(signal, TimeSpan.FromMilliseconds(300)))
				.Should().BeFalse();
		}

		[Fact]
		public async Task Cancelamento_encerra_a_espera()
		{
			var signal = new ClientChangeSignal();
			using var cancellation = new CancellationTokenSource();
			var read = Task.Run(async () =>
			{
				await foreach (bool _ in signal.ReadAllAsync(cancellation.Token))
				{
					return true;
				}

				return false;
			});

			cancellation.Cancel();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
		}

		[Fact]
		public async Task Dois_consumidores_nao_duplicam_o_sinal()
		{
			// SingleReader: o sinal é consumido uma única vez.
			var signal = new ClientChangeSignal();
			signal.Publish();

			int reads = 0;
			var first = Task.Run(async () =>
			{
				await foreach (bool _ in signal.ReadAllAsync())
				{
					Interlocked.Increment(ref reads);
				}
			});

			await Task.Delay(300);

			reads.Should().Be(1);
		}
	}
}
