namespace EveOPreview.Configuration
{
	public interface IConfigurationStorage
	{
		ConfigurationValidationResult Load();

		/// <summary>
		/// Caminho absoluto do arquivo que o aplicativo lê e grava. Existe porque a
		/// configuração pode não ter caminho explícito e o padrão é relativo.
		/// </summary>
		string GetConfigurationFilePath();
		void Save();
	}
}