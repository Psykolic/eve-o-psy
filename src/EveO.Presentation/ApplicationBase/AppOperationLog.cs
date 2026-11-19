using System;
using System.Collections.Generic;
using EveOPreview.Services;

namespace EveOPreview
{
	/// <summary>
	/// Ponto unico de registro das operacoes que antecedem um crash. O handler de
	/// excecao consulta este buffer sem depender do IoC.
	/// </summary>
	internal static class AppOperationLog
	{
		private static readonly OperationHistory History = new OperationHistory(OperationHistory.DefaultCapacity);

		public static void Record(string operation)
		{
			History.Record(operation);
		}

		public static IReadOnlyList<OperationHistoryEntry> Snapshot()
		{
			return History.Snapshot();
		}
	}
}
