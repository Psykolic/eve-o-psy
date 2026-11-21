namespace EveOPreview.Configuration
{
	public interface IConfigurationStorage
	{
		ConfigurationValidationResult Load();
		void Save();
	}
}