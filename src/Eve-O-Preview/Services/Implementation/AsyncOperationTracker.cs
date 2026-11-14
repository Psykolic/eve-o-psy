using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EveOPreview.Services
{
	/// <summary>
	/// Rastreia as operações assíncronas disparadas pelos handlers do manager para que o
	/// encerramento possa aguardá-las. O dreno tira snapshots até que nenhuma tarefa reste,
	/// cobrindo também as que forem registradas enquanto o próprio dreno espera.
	/// </summary>
	internal sealed class AsyncOperationTracker
	{
		// Janela curta para que handlers já enfileirados registrem seu trabalho antes de
		// o dreno concluir que não há mais nada em voo.
		private static readonly TimeSpan QUIET_PERIOD = TimeSpan.FromMilliseconds(5);

		private readonly object _syncRoot;
		private readonly HashSet<Task> _pending;

		internal AsyncOperationTracker()
		{
			this._syncRoot = new object();
			this._pending = new HashSet<Task>();
		}

		internal int PendingCount
		{
			get
			{
				lock (this._syncRoot)
				{
					return this._pending.Count;
				}
			}
		}

		// Registra a operação ANTES de iniciá-la: um DrainAsync concorrente nunca perde
		// trabalho que ainda não começou a rodar.
		internal void Run(Func<Task> operation)
		{
			TaskCompletionSource<object> completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

			lock (this._syncRoot)
			{
				this._pending.Add(completion.Task);
			}

			_ = this.ExecuteAsync(operation, completion);
		}

		private async Task ExecuteAsync(Func<Task> operation, TaskCompletionSource<object> completion)
		{
			try
			{
				await operation().ConfigureAwait(false);
			}
			catch (Exception error)
			{
				// Defesa em profundidade: o chamador normal já passa por RunGuardedAsync.
				Trace.TraceError($"AsyncOperationTracker: exceção assíncrona suprimida: {error}");
			}
			finally
			{
				lock (this._syncRoot)
				{
					this._pending.Remove(completion.Task);
				}

				completion.TrySetResult(null);
			}
		}

		internal async Task DrainAsync(CancellationToken cancellationToken)
		{
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				Task[] snapshot;
				lock (this._syncRoot)
				{
					snapshot = this._pending.ToArray();
				}

				if (snapshot.Length > 0)
				{
					await Task.WhenAll(snapshot).ConfigureAwait(false);
					continue;
				}

				await Task.Delay(QUIET_PERIOD, cancellationToken).ConfigureAwait(false);

				lock (this._syncRoot)
				{
					if (this._pending.Count == 0)
					{
						return;
					}
				}
			}
		}
	}
}
