namespace EveOPreview.Configuration
{
	/// <summary>
	/// Confere a configuração carregada e devolve o que estava fora da faixa.
	/// Não corrige nada: o ajuste continua sendo responsabilidade de
	/// <c>IThumbnailConfiguration.ApplyRestrictions()</c>.
	/// </summary>
	public interface IConfigurationValidator
	{
		ConfigurationValidationResult Validate(IThumbnailConfiguration configuration);
	}
}
