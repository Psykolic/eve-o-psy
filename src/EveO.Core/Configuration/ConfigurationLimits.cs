namespace EveOPreview.Configuration
{
	/// <summary>
	/// Faixas aceitas pela configuração. Fonte única: a validação (que avisa o usuário)
	/// e o clamp de <c>ApplyRestrictions</c> têm de concordar — senão o app avisa uma
	/// coisa e faz outra.
	/// </summary>
	public static class ConfigurationLimits
	{
#if LINUX
		public const int RefreshPeriodMin = 10;
#else
		public const int RefreshPeriodMin = 300;
#endif
		public const int RefreshPeriodMax = 1000;
		public const int ReconciliationPeriodMin = 1000;
		public const int ReconciliationPeriodMax = 60000;
		public const int ResizeTimeoutMin = 200;
		public const int ResizeTimeoutMax = 5000;
		public const int MinimumWidth = 16;
		public const int MinimumHeight = 9;
		public const int MaximumWidth = 7680;
		public const int MaximumHeight = 4320;
		public const int OpacityMinPercent = 20;
		public const int OpacityMaxPercent = 100;
		public const int ZoomFactorMin = 2;
		public const int ZoomFactorMax = 10;
		public const int HighlightThicknessMin = 1;
		public const int HighlightThicknessMax = 6;
		public const int SpotlightMinWidth = 160;
		public const int SpotlightMinHeight = 90;
	}
}
