using System;
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
				string copia = ConfigurationStorage.Quarantine(filename);
				string onde = copia == null ? string.Empty : " — original guardado em " + Path.GetFileName(copia);
				return result.Add(filename, "JSON inválido (" + exception.Message + "); padrões mantidos" + onde);
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

		/// <summary>
		/// Grava de forma atômica: escreve num temporário e troca de uma vez, guardando a
		/// versão anterior em <c>.bak</c>. Sem isso, uma interrupção no meio do save deixa
		/// o JSON pela metade — e a configuração do usuário se perde.
		/// </summary>
		public void Save()
		{
			string rawData = JsonConvert.SerializeObject(this._thumbnailConfiguration, Formatting.Indented);
			string filename = this.GetConfigFileName();
			string temporario = filename + ".tmp";

			try
			{
				File.WriteAllText(temporario, rawData);

				if (File.Exists(filename))
				{
					// Replace troca e guarda o anterior em .bak, na mesma operação.
					File.Replace(temporario, filename, filename + ".bak", true);
				}
				else
				{
					File.Move(temporario, filename);
				}
			}
			catch (IOException)
			{
				// Ignore error if for some reason the updated config cannot be written down
				try
				{
					if (File.Exists(temporario))
					{
						File.Delete(temporario);
					}
				}
				catch (IOException)
				{
				}
			}
			catch (UnauthorizedAccessException)
			{
			}
		}

		/// <summary>
		/// Copia o arquivo com problema para <c>nome.corrupt-AAAAMMDD-HHMMSS</c>, para o
		/// próximo save não apagar a única evidência do que o usuário tinha.
		/// </summary>
		private static string Quarantine(string filename)
		{
			try
			{
				string copia = filename + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
				File.Copy(filename, copia, true);
				return copia;
			}
			catch (Exception)
			{
				return null;
			}
		}

		public string GetConfigurationFilePath()
		{
			try
			{
				return System.IO.Path.GetFullPath(this.GetConfigFileName());
			}
			catch (System.Exception)
			{
				return this.GetConfigFileName();
			}
		}

		private string GetConfigFileName()
		{
			return string.IsNullOrEmpty(this._appConfig.ConfigFileName) ? ConfigurationStorage.CONFIGURATION_FILE_NAME : this._appConfig.ConfigFileName;
		}
	}
}
