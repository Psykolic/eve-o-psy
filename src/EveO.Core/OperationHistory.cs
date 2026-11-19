using System;
using System.Collections.Generic;

namespace EveOPreview.Services
{
	/// <summary>
	/// Buffer circular de operacoes recentes do aplicativo. Usado pelo handler de
	/// crash para registrar o que estava acontecendo antes da excecao.
	/// </summary>
	public sealed class OperationHistory
	{
		public const int DefaultCapacity = 20;

		private readonly object _syncRoot = new object();
		private readonly OperationHistoryEntry[] _entries;
		private int _nextIndex;
		private int _count;

		public OperationHistory()
			: this(DefaultCapacity)
		{
		}

		public OperationHistory(int capacity)
		{
			if (capacity <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(capacity), "A capacidade deve ser maior que zero.");
			}

			this._entries = new OperationHistoryEntry[capacity];
		}

		public int Capacity => this._entries.Length;

		public int Count
		{
			get
			{
				lock (this._syncRoot)
				{
					return this._count;
				}
			}
		}

		public void Record(string operation)
		{
			this.Record(DateTime.UtcNow, operation);
		}

		public void Record(DateTime timestampUtc, string operation)
		{
			OperationHistoryEntry entry = new OperationHistoryEntry(timestampUtc, operation ?? string.Empty);

			lock (this._syncRoot)
			{
				this._entries[this._nextIndex] = entry;
				this._nextIndex = (this._nextIndex + 1) % this._entries.Length;

				if (this._count < this._entries.Length)
				{
					this._count++;
				}
			}
		}

		/// <summary>
		/// Retorna uma copia das entradas da mais antiga para a mais recente.
		/// </summary>
		public IReadOnlyList<OperationHistoryEntry> Snapshot()
		{
			lock (this._syncRoot)
			{
				List<OperationHistoryEntry> snapshot = new List<OperationHistoryEntry>(this._count);
				int start = (this._nextIndex - this._count + this._entries.Length) % this._entries.Length;

				for (int index = 0; index < this._count; index++)
				{
					snapshot.Add(this._entries[(start + index) % this._entries.Length]);
				}

				return snapshot;
			}
		}
	}

	public sealed class OperationHistoryEntry
	{
		public OperationHistoryEntry(DateTime timestampUtc, string operation)
		{
			this.TimestampUtc = timestampUtc;
			this.Operation = operation;
		}

		public DateTime TimestampUtc { get; }

		public string Operation { get; }
	}
}
