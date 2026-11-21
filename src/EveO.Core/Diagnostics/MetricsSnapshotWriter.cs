using System;
using System.IO;
using System.Text;

namespace EveOPreview.Diagnostics
{
	/// <summary>
	/// Grava um retrato das métricas em arquivo, no máximo uma vez por intervalo.
	/// É o "exportador" padrão do app: local, sem rede e sem dependência — um
	/// listener de OTLP/Prometheus pode ser plugado por quem precisar.
	/// </summary>
	public sealed class MetricsSnapshotWriter
	{
		private readonly string _path;
		private readonly TimeSpan _interval;
		private DateTime _nextWriteUtc = DateTime.MinValue;

		public MetricsSnapshotWriter(string path, TimeSpan interval)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				throw new ArgumentException("caminho do snapshot é obrigatório", nameof(path));
			}

			if (interval <= TimeSpan.Zero)
			{
				throw new ArgumentOutOfRangeException(nameof(interval), "o intervalo precisa ser positivo");
			}

			this._path = path;
			this._interval = interval;
		}

		public string Path => this._path;

		/// <summary>Escreve quando o intervalo já passou; devolve true se gravou.</summary>
		public bool WriteIfDue(DateTime nowUtc)
		{
			if (nowUtc < this._nextWriteUtc)
			{
				return false;
			}

			this._nextWriteUtc = nowUtc + this._interval;

			try
			{
				string directory = System.IO.Path.GetDirectoryName(this._path);
				if (!string.IsNullOrEmpty(directory))
				{
					Directory.CreateDirectory(directory);
				}

				File.AppendAllText(this._path, EveOMetrics.Snapshot().ToLine() + Environment.NewLine, Encoding.UTF8);
				return true;
			}
			catch (Exception)
			{
				// Métrica não pode derrubar o aplicativo: caminho inválido, disco cheio
				// ou falta de permissão só significam "sem retrato".
				return false;
			}
		}
	}
}
