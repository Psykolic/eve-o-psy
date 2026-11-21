using System;
using System.Globalization;

namespace EveOPreview.Diagnostics
{
	/// <summary>
	/// Retrato das métricas em um instante. Uma linha de texto, para log e para o
	/// arquivo de snapshot — legível sem ferramenta.
	/// </summary>
	public sealed class MetricsSnapshot
	{
		public MetricsSnapshot(DateTime timestampUtc, double refreshMs, long refreshCount,
			int activeThumbnails, long gdiHandles, long dwmHandles, double cpuPercent)
		{
			this.TimestampUtc = timestampUtc;
			this.RefreshMs = refreshMs;
			this.RefreshCount = refreshCount;
			this.ActiveThumbnails = activeThumbnails;
			this.GdiHandles = gdiHandles;
			this.DwmHandles = dwmHandles;
			this.CpuPercent = cpuPercent;
		}

		public DateTime TimestampUtc { get; }
		public double RefreshMs { get; }
		public long RefreshCount { get; }
		public int ActiveThumbnails { get; }
		public long GdiHandles { get; }
		public long DwmHandles { get; }
		public double CpuPercent { get; }

		/// <summary>Uma linha com chave=valor; -1 significa "indisponível nesta plataforma".</summary>
		public string ToLine() => string.Format(
			CultureInfo.InvariantCulture,
			"ts={0:O} refresh_ms={1:F2} refreshes={2} thumbnails={3} gdi={4} dwm={5} cpu_pct={6:F1}",
			this.TimestampUtc, this.RefreshMs, this.RefreshCount,
			this.ActiveThumbnails, this.GdiHandles, this.DwmHandles, this.CpuPercent);

		public override string ToString() => this.ToLine();
	}
}
