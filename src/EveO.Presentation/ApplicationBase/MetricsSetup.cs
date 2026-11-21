using System;
using System.Diagnostics;
using System.IO;
using EveOPreview.Configuration;
using EveOPreview.Diagnostics;
using EveOPreview.Services.Interop;

namespace EveOPreview
{
	/// <summary>
	/// Liga as métricas que dependem de Windows: handles GDI, CPU do processo e o
	/// retrato em arquivo. Fica na composição porque é o único lugar que conhece
	/// tanto a configuração quanto os interops.
	/// </summary>
	internal static class MetricsSetup
	{
		private const uint GR_GDIOBJECTS = 0;

		public static void Configure(IIocContainer container)
		{
			EveOMetrics.GdiHandleProvider = ReadGdiHandles;
			EveOMetrics.CpuUsageProvider = CpuSampler.Instance.ReadPercent;

			IAppConfig appConfig = container.Resolve(typeof(IAppConfig)) as IAppConfig;
			if (appConfig == null || !appConfig.MetricsEnabled)
			{
				return;
			}

			string path = string.IsNullOrWhiteSpace(appConfig.MetricsSnapshotPath)
				? Path.Combine(Path.GetTempPath(), "EVE-O-Preview-metrics.log")
				: appConfig.MetricsSnapshotPath;

			// Um retrato a cada 30 s dá para ver tendência (handles, CPU, refresh) sem
			// encher o disco: 2.880 linhas por dia, ~0,2 MB.
			EveOMetrics.ConfigureSnapshot(path, TimeSpan.FromSeconds(30));
		}

		private static long ReadGdiHandles()
		{
			try
			{
				using (Process process = Process.GetCurrentProcess())
				{
					return User32NativeMethods.GetGuiResources(process.Handle, GR_GDIOBJECTS);
				}
			}
			catch (Exception)
			{
				return EveOMetrics.Unavailable;
			}
		}

		/// <summary>
		/// CPU do processo em porcentagem, calculada pela diferença de tempo de
		/// processador entre duas leituras — sem PerformanceCounter (que exige
		/// categoria instalada e é lento).
		/// </summary>
		private sealed class CpuSampler
		{
			public static readonly CpuSampler Instance = new CpuSampler();

			private readonly object _sync = new object();
			private TimeSpan _lastProcessorTime;
			private DateTime _lastSampleUtc = DateTime.MinValue;

			public double ReadPercent()
			{
				lock (this._sync)
				{
					using (Process process = Process.GetCurrentProcess())
					{
						process.Refresh();
						TimeSpan processorTime = process.TotalProcessorTime;
						DateTime now = DateTime.UtcNow;

						if (this._lastSampleUtc == DateTime.MinValue)
						{
							this._lastProcessorTime = processorTime;
							this._lastSampleUtc = now;
							return EveOMetrics.Unavailable;
						}

						double elapsedMs = (now - this._lastSampleUtc).TotalMilliseconds;
						if (elapsedMs <= 0)
						{
							return EveOMetrics.Unavailable;
						}

						double usedMs = (processorTime - this._lastProcessorTime).TotalMilliseconds;
						double percent = 100.0 * usedMs / (elapsedMs * Environment.ProcessorCount);

						this._lastProcessorTime = processorTime;
						this._lastSampleUtc = now;

						return Math.Round(Math.Max(0.0, Math.Min(100.0, percent)), 1);
					}
				}
			}
		}
	}
}
