using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EveOPreview.Configuration.Implementation
{
	class ConfigurationStorage : IConfigurationStorage
	{
		private const string CONFIGURATION_FILE_NAME = "EVE-O-Preview.json";

		private readonly IAppConfig _appConfig;
		private readonly IThumbnailConfiguration _thumbnailConfiguration;
		private readonly IConfigurationValidator _validator;

		public ConfigurationStorage(IAppConfig appConfig, IThumbnailConfiguration thumbnailConfiguration,
			IConfigurationValidator validator)
		{
			this._appConfig = appConfig;
			this._thumbnailConfiguration = thumbnailConfiguration;
			this._validator = validator;
		}

		/// <summary>
		/// Carrega a configuração e devolve o que precisou ser corrigido, sem lançar exceção:
		/// um arquivo corrompido não pode impedir o aplicativo de abrir.
		/// </summary>
		public ConfigurationValidationResult Load()
		{
			var result = new ConfigurationValidationResult();
			string filename = this.GetConfigFileName();

			if (!File.Exists(filename))
			{
				// Sem arquivo: os padrões valem e não há nada a avisar.
				return result;
			}

			string rawData;
			try
			{
				rawData = File.ReadAllText(filename);
			}
			catch (IOException exception)
			{
				return result.Add(filename, "não foi possível ler (" + exception.Message + ")");
			}

			JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
			{
				ObjectCreationHandling = ObjectCreationHandling.Replace
			};

			// Sintaxe primeiro: JSON quebrado não pode ser aplicado "pela metade".
			JObject parsed;
			try
			{
				parsed = JObject.Parse(rawData);
			}
			catch (JsonException exception)
			{
				return result.Add(filename, "JSON inválido (" + exception.Message + "); padrões mantidos");
			}

			string previous = JsonConvert.SerializeObject(this._thumbnailConfiguration);

			try
			{
				JsonConvert.PopulateObject(rawData, this._thumbnailConfiguration, jsonSerializerSettings);
			}
			catch (JsonException exception)
			{
				// Tipo inesperado (ex.: "ThumbnailSize": "abc"): restaura o que havia
				// para não deixar a configuração num estado misto.
				JsonConvert.PopulateObject(previous, this._thumbnailConfiguration, jsonSerializerSettings);
				return result.Add(filename, "valor com tipo inesperado (" + exception.Message + "); mantido o anterior");
			}

			// Formato do aplicativo anterior: os grupos trocaram de forma e seriam ignorados.
			UpstreamConfigurationImport.TryApply(parsed, this._thumbnailConfiguration, result);

			// Avisa o que estava fora da faixa e só depois corrige (mesmo clamp de antes).
			ConfigurationValidationResult validation = this._validator.Validate(this._thumbnailConfiguration);
			foreach (string aviso in result.Notices)
			{
				validation.AddNotice(aviso);
			}
			foreach (ConfigurationProblem problema in result.Problems)
			{
				validation.Add(problema.Key, problema.Message);
			}
			this._thumbnailConfiguration.ApplyRestrictions();

			return validation;
		}

		public void Save()
		{
			string rawData = JsonConvert.SerializeObject(this._thumbnailConfiguration, Formatting.Indented);
			string filename = this.GetConfigFileName();

			try
			{
				File.WriteAllText(filename, rawData);
			}
			catch (IOException)
			{
				// Ignore error if for some reason the updated config cannot be written down
			}
		}

		private string GetConfigFileName()
		{
			return string.IsNullOrEmpty(this._appConfig.ConfigFileName) ? ConfigurationStorage.CONFIGURATION_FILE_NAME : this._appConfig.ConfigFileName;
		}
	}
}
