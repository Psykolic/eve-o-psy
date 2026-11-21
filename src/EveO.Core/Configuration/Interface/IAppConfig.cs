namespace EveOPreview.Configuration
{
	/// <summary>
	/// Application configuration
	/// </summary>
	public interface IAppConfig
	{
		string ConfigFileName { get; set; }

		/// <summary>Liga o retrato local das métricas (item 1.5). Pode vir de EVE_METRICS=1.</summary>
		bool MetricsEnabled { get; set; }

		/// <summary>
		/// Caminho do arquivo de retrato. Vazio significa o padrão do aplicativo.
		/// </summary>
		string MetricsSnapshotPath { get; set; }
	}
}