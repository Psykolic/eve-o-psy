using System;
using System.Diagnostics.Metrics;

namespace EveOPreview.Diagnostics
{
	/// <summary>
	/// Métricas do aplicativo usando <see cref="Meter"/> (System.Diagnostics.Metrics, nativo).
	///
	/// O app publica os instrumentos e mantém um retrato local; quem quiser exportar
	/// (OTLP, Prometheus, dotnet-counters) registra um MeterListener — não é preciso
	/// nenhuma dependência externa no processo só para medir.
	/// </summary>
	public static class EveOMetrics
	{
		public const string MeterName = "EveO.Preview";

		public const long Unavailable = -1;

		private static readonly Meter Meter = new Meter(MeterName, "1.0");

		private static readonly Histogram<double> RefreshDuration = Meter.CreateHistogram<double>(
			"thumbnail.refresh.duration", unit: "ms", description: "Duração de um ciclo de refresh das thumbnails");

		private static readonly Counter<long> RefreshCount = Meter.CreateCounter<long>(
			"thumbnail.refresh.count", unit: "ciclos", description: "Ciclos de refresh executados");

		private static int _activeThumbnails;
		private static double _lastRefreshMs;
		private static long _refreshTotal;

		static EveOMetrics()
		{
			Meter.CreateObservableGauge("thumbnail.count.active",
				() => _activeThumbnails, unit: "thumbnails", description: "Thumbnails ativas no momento");
			Meter.CreateObservableGauge("gdi.handles.open",
				() => ReadLong(GdiHandleProvider), unit: "handles", description: "Handles GDI do processo");
			Meter.CreateObservableGauge("dwm.handles.open",
				() => ReadLong(DwmHandleProvider), unit: "thumbnails", description: "Thumbnails DWM registradas");
			Meter.CreateObservableGauge("cpu.usage.percent",
				() => ReadDouble(CpuUsageProvider), unit: "%", description: "CPU do processo (0-100)");
		}

		/// <summary>Handles GDI — só existe em Windows; sem provedor devolve <see cref="Unavailable"/>.</summary>
		public static Func<long> GdiHandleProvider { get; set; }

		/// <summary>Thumbnails DWM registradas — vem do gerenciador de thumbnails.</summary>
		public static Func<long> DwmHandleProvider { get; set; }

		/// <summary>CPU do processo em porcentagem.</summary>
		public static Func<double> CpuUsageProvider { get; set; }

		public static void RecordRefresh(double durationMs, int activeThumbnails)
		{
			_lastRefreshMs = durationMs;
			_activeThumbnails = activeThumbnails;
			_refreshTotal++;

			RefreshDuration.Record(durationMs);
			RefreshCount.Add(1);
		}

		public static void SetActiveThumbnails(int count) => _activeThumbnails = count;

		private static MetricsSnapshotWriter _writer;

		/// <summary>Liga o retrato em arquivo (uma vez, na composição do app).</summary>
		public static void ConfigureSnapshot(string path, TimeSpan interval) =>
			_writer = new MetricsSnapshotWriter(path, interval);

		/// <summary>Desliga o retrato (usado em teste).</summary>
		public static void ResetSnapshot() => _writer = null;

		/// <summary>Grava o retrato se o intervalo venceu. Nunca lança.</summary>
		public static bool WriteSnapshotIfDue(DateTime nowUtc) =>
			_writer != null && _writer.WriteIfDue(nowUtc);

		public static MetricsSnapshot Snapshot() => new MetricsSnapshot(
			DateTime.UtcNow,
			_lastRefreshMs,
			_refreshTotal,
			_activeThumbnails,
			ReadLong(GdiHandleProvider),
			ReadLong(DwmHandleProvider),
			ReadDouble(CpuUsageProvider));

		private static long ReadLong(Func<long> provider)
		{
			if (provider == null)
			{
				return Unavailable;
			}

			try
			{
				return provider();
			}
			catch (Exception)
			{
				return Unavailable;
			}
		}

		private static double ReadDouble(Func<double> provider)
		{
			if (provider == null)
			{
				return Unavailable;
			}

			try
			{
				return provider();
			}
			catch (Exception)
			{
				return Unavailable;
			}
		}
	}
}
