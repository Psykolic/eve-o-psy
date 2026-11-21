using System;

﻿namespace EveOPreview.Configuration.Implementation
{
	class AppConfig : IAppConfig
	{
		public AppConfig()
		{
			// Default values
			this.ConfigFileName = null;

			// Ligado por ambiente: medir não muda o comportamento, só grava um retrato.
			this.MetricsEnabled = Environment.GetEnvironmentVariable("EVE_METRICS") == "1";
			this.MetricsSnapshotPath = Environment.GetEnvironmentVariable("EVE_METRICS_PATH");
		}

		public string ConfigFileName { get; set; }

		public bool MetricsEnabled { get; set; }

		public string MetricsSnapshotPath { get; set; }
	}
}
