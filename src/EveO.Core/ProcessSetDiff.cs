using System;
using System.Collections.Generic;

namespace EveOPreview.Services
{
	/// <summary>
	/// Entrada de um processo/janela observado em um ciclo de monitoramento.
	/// </summary>
	public sealed class ProcessSetEntry
	{
		public ProcessSetEntry(IntPtr handle, string title)
			: this(handle, title, 0)
		{
		}

		internal ProcessSetEntry(IntPtr handle, string title, int misses)
		{
			this.Handle = handle;
			this.Title = title;
			this.Misses = misses;
		}

		public IntPtr Handle { get; }
		public string Title { get; }
		public int Misses { get; }
	}

	/// <summary>
	/// Resultado de um ciclo do <see cref="ProcessSetDiff"/>.
	/// </summary>
	public sealed class ProcessSetDiffResult
	{
		internal ProcessSetDiffResult(
			IReadOnlyList<ProcessSetEntry> added,
			IReadOnlyList<ProcessSetEntry> updated,
			IReadOnlyList<ProcessSetEntry> removed)
		{
			this.Added = added;
			this.Updated = updated;
			this.Removed = removed;
		}

		public IReadOnlyList<ProcessSetEntry> Added { get; }
		public IReadOnlyList<ProcessSetEntry> Updated { get; }
		public IReadOnlyList<ProcessSetEntry> Removed { get; }
	}

	/// <summary>
	/// Mantém o conjunto de janelas conhecidas e só confirma uma remoção depois de
	/// vários ciclos consecutivos sem que o handle seja visto (issue #139).
	/// </summary>
	/// <remarks>
	/// O MainWindowHandle fica Zero em situações transitórias comuns (cliente sem
	/// responder, minimizado, tela de login, troca de dispositivo). Tratar essa
	/// ausência como remoção fechava a thumbnail e o spotlight do cliente.
	/// </remarks>
	public sealed class ProcessSetDiff
	{
		public const int DefaultRemovalMissThreshold = 3;

		private sealed class KnownProcess
		{
			public KnownProcess(string title)
			{
				this.Title = title;
			}

			public string Title { get; set; }
			public int Misses { get; set; }
		}

		private readonly Dictionary<IntPtr, KnownProcess> _knownProcesses;
		private readonly int _removalMissThreshold;

		public ProcessSetDiff()
			: this(DefaultRemovalMissThreshold)
		{
		}

		public ProcessSetDiff(int removalMissThreshold)
		{
			if (removalMissThreshold < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(removalMissThreshold), "O limite de misses deve ser pelo menos 1.");
			}

			this._knownProcesses = new Dictionary<IntPtr, KnownProcess>(512);
			this._removalMissThreshold = removalMissThreshold;
		}

		public int RemovalMissThreshold => this._removalMissThreshold;

		/// <summary>
		/// Janelas conhecidas que continuam ativas, incluindo as que estão em miss transitório.
		///
		/// Instantâneo para diagnóstico/teste — aloca uma lista e uma entrada por processo.
		/// O caminho de produção é <see cref="Update"/>, que devolve só o que mudou; não use
		/// isto em laço quente.
		/// </summary>
		public IReadOnlyList<ProcessSetEntry> Known
		{
			get
			{
				List<ProcessSetEntry> result = new List<ProcessSetEntry>(this._knownProcesses.Count);

				foreach (KeyValuePair<IntPtr, KnownProcess> entry in this._knownProcesses)
				{
					result.Add(new ProcessSetEntry(entry.Key, entry.Value.Title, entry.Value.Misses));
				}

				return result;
			}
		}

		public ProcessSetDiffResult Update(IEnumerable<ProcessSetEntry> observedProcesses)
		{
			return this.Update(observedProcesses, null);
		}

		/// <summary>
		/// Aplica um ciclo de monitoramento.
		/// </summary>
		/// <param name="observedProcesses">Janelas com MainWindowHandle válido neste ciclo.</param>
		/// <param name="shouldKeepHandle">
		/// Verificação opcional de que o handle/processo ainda existe. <c>true</c> adia a
		/// remoção mesmo depois do limite de misses; <c>false</c> é prova de que a janela
		/// ficou obsoleta (processo encerrado) e remove no mesmo ciclo.
		/// </param>
		public ProcessSetDiffResult Update(
			IEnumerable<ProcessSetEntry> observedProcesses,
			Func<IntPtr, bool> shouldKeepHandle)
		{
			if (observedProcesses == null)
			{
				throw new ArgumentNullException(nameof(observedProcesses));
			}

			List<ProcessSetEntry> addedProcesses = new List<ProcessSetEntry>(16);
			List<ProcessSetEntry> updatedProcesses = new List<ProcessSetEntry>(16);
			List<ProcessSetEntry> removedProcesses = new List<ProcessSetEntry>(16);
			HashSet<IntPtr> seenHandles = new HashSet<IntPtr>();

			foreach (ProcessSetEntry process in observedProcesses)
			{
				if (process == null || process.Handle == IntPtr.Zero)
				{
					continue;
				}

				seenHandles.Add(process.Handle);

				if (!this._knownProcesses.TryGetValue(process.Handle, out KnownProcess knownProcess))
				{
					this._knownProcesses.Add(process.Handle, new KnownProcess(process.Title));
					addedProcesses.Add(new ProcessSetEntry(process.Handle, process.Title));
					continue;
				}

				// A janela voltou: o miss anterior era transitório.
				knownProcess.Misses = 0;

				if (knownProcess.Title != process.Title)
				{
					knownProcess.Title = process.Title;
					updatedProcesses.Add(new ProcessSetEntry(process.Handle, process.Title));
				}
			}

			List<IntPtr> knownSnapshot = new List<IntPtr>(this._knownProcesses.Keys);
			foreach (IntPtr handle in knownSnapshot)
			{
				if (seenHandles.Contains(handle))
				{
					continue;
				}

				KnownProcess knownProcess = this._knownProcesses[handle];
				knownProcess.Misses++;

				// A histerese existe para a ausência transitória do handle (cliente travado,
				// minimizado, tela de login). Quando o callback prova que o processo saiu, não
				// há o que esperar: a thumbnail ficaria na tela por até 3 ciclos (issue #150).
				bool? keepHandle = shouldKeepHandle?.Invoke(handle);
				if (keepHandle == true || (keepHandle == null && knownProcess.Misses < this._removalMissThreshold))
				{
					continue;
				}

				this._knownProcesses.Remove(handle);
				removedProcesses.Add(new ProcessSetEntry(handle, knownProcess.Title, knownProcess.Misses));
			}

			return new ProcessSetDiffResult(addedProcesses, updatedProcesses, removedProcesses);
		}
	}
}
