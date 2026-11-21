using System;

namespace EveOPreview.Configuration.Implementation
{
	/// <summary>
	/// Valida antes do clamp: o usuário precisa saber que o valor dele foi ajustado,
	/// em vez de descobrir depois que a thumbnail mudou sozinha.
	/// </summary>
	sealed class ConfigurationValidator : IConfigurationValidator
	{
		public ConfigurationValidationResult Validate(IThumbnailConfiguration configuration)
		{
			var result = new ConfigurationValidationResult();

			if (configuration == null)
			{
				return result.Add("configuração", "não foi carregada");
			}

			ConfigurationValidator.CheckRange(result, nameof(configuration.ThumbnailRefreshPeriod),
				configuration.ThumbnailRefreshPeriod,
				ConfigurationLimits.RefreshPeriodMin, ConfigurationLimits.RefreshPeriodMax);

			ConfigurationValidator.CheckRange(result, nameof(configuration.ReconciliationPeriod),
				configuration.ReconciliationPeriod,
				ConfigurationLimits.ReconciliationPeriodMin, ConfigurationLimits.ReconciliationPeriodMax);

			ConfigurationValidator.CheckRange(result, nameof(configuration.ThumbnailResizeTimeoutPeriod),
				configuration.ThumbnailResizeTimeoutPeriod,
				ConfigurationLimits.ResizeTimeoutMin, ConfigurationLimits.ResizeTimeoutMax);

			ConfigurationValidator.CheckRange(result, nameof(configuration.ThumbnailMinimumSize),
				configuration.ThumbnailMinimumSize.Width,
				ConfigurationLimits.MinimumWidth, ConfigurationLimits.MaximumWidth);
			ConfigurationValidator.CheckRange(result, nameof(configuration.ThumbnailMinimumSize),
				configuration.ThumbnailMinimumSize.Height,
				ConfigurationLimits.MinimumHeight, ConfigurationLimits.MaximumHeight);

			// Tamanho da thumbnail é validado contra o mínimo já corrigido, para não
			// apontar dois problemas onde só um valor está fora.
			int minimumWidth = ConfigurationValidator.Clamp(configuration.ThumbnailMinimumSize.Width,
				ConfigurationLimits.MinimumWidth, ConfigurationLimits.MaximumWidth);
			int minimumHeight = ConfigurationValidator.Clamp(configuration.ThumbnailMinimumSize.Height,
				ConfigurationLimits.MinimumHeight, ConfigurationLimits.MaximumHeight);
			ConfigurationValidator.CheckRange(result, nameof(configuration.ThumbnailSize),
				configuration.ThumbnailSize.Width, minimumWidth, ConfigurationLimits.MaximumWidth);
			ConfigurationValidator.CheckRange(result, nameof(configuration.ThumbnailSize),
				configuration.ThumbnailSize.Height, minimumHeight, ConfigurationLimits.MaximumHeight);

			ConfigurationValidator.CheckPercent(result, nameof(configuration.ThumbnailOpacity), configuration.ThumbnailOpacity);
			ConfigurationValidator.CheckPercent(result, nameof(configuration.SpotlightOpacity), configuration.SpotlightOpacity);

			ConfigurationValidator.CheckRange(result, nameof(configuration.ThumbnailZoomFactor),
				configuration.ThumbnailZoomFactor,
				ConfigurationLimits.ZoomFactorMin, ConfigurationLimits.ZoomFactorMax);

			ConfigurationValidator.CheckRange(result, nameof(configuration.ActiveClientHighlightThickness),
				configuration.ActiveClientHighlightThickness,
				ConfigurationLimits.HighlightThicknessMin, ConfigurationLimits.HighlightThicknessMax);

			ConfigurationValidator.CheckRange(result, nameof(configuration.SpotlightSize),
				configuration.SpotlightSize.Width,
				ConfigurationLimits.SpotlightMinWidth, ConfigurationLimits.MaximumWidth);
			ConfigurationValidator.CheckRange(result, nameof(configuration.SpotlightSize),
				configuration.SpotlightSize.Height,
				ConfigurationLimits.SpotlightMinHeight, ConfigurationLimits.MaximumHeight);

			return result;
		}

		private static void CheckRange(ConfigurationValidationResult result, string key, int value, int minimum, int maximum)
		{
			if (value < minimum || value > maximum)
			{
				result.Add(key, value + " fora da faixa " + minimum + "–" + maximum +
					"; será ajustado para " + ConfigurationValidator.Clamp(value, minimum, maximum));
			}
		}

		/// <summary>
		/// Opacidade é guardada como 0.00–1.00 e exibida em porcentagem, que é a unidade
		/// que o usuário vê na interface e no arquivo de configuração antigo.
		/// </summary>
		private static void CheckPercent(ConfigurationValidationResult result, string key, double value)
		{
			int percent = (int)(value * 100.0);
			if (percent < ConfigurationLimits.OpacityMinPercent || percent > ConfigurationLimits.OpacityMaxPercent)
			{
				result.Add(key, percent + "% fora da faixa " + ConfigurationLimits.OpacityMinPercent + "–" +
					ConfigurationLimits.OpacityMaxPercent + "%; será ajustado");
			}
		}

		private static int Clamp(int value, int minimum, int maximum) => Math.Min(Math.Max(value, minimum), maximum);
	}
}
