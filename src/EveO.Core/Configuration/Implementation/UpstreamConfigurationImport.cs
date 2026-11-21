using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace EveOPreview.Configuration.Implementation
{
	/// <summary>
	/// Importa a configuração do aplicativo anterior (<c>ConfigVersion: 3</c>, com
	/// <c>"CycleGroups": [...]</c>) para o formato deste app.
	///
	/// O JSON do app anterior carrega quase tudo pelos nomes iguais; o que se perde são os
	/// **grupos de ciclo**, que mudaram de forma — e é justamente o que deixa o usuário sem
	/// hotkeys. A ordem dos cliente também é invertida:
	/// antigo <c>"2": "EVE - Bozo Nolen"</c> (posição → nome) contra <c>"EVE - Bozo": 2</c>.
	/// </summary>
	internal static class UpstreamConfigurationImport
	{
		public const string NoticeKey = "importação";

		private const int MaxGroups = 5;

		public static bool TryApply(JObject source, IThumbnailConfiguration configuration, ConfigurationValidationResult result)
		{
			if (source == null || configuration == null)
			{
				return false;
			}

			JArray groups = source["CycleGroups"] as JArray;
			if (groups == null)
			{
				return false;
			}

			int importados = 0;
			for (int i = 0; i < Math.Min(groups.Count, UpstreamConfigurationImport.MaxGroups); i++)
			{
				if (!(groups[i] is JObject group))
				{
					continue;
				}

				List<string> forward = UpstreamConfigurationImport.ReadHotkeys(group, "ForwardHotkeys");
				List<string> backward = UpstreamConfigurationImport.ReadHotkeys(group, "BackwardHotkeys");
				Dictionary<string, int> order = UpstreamConfigurationImport.ReadClientsOrder(group);

				UpstreamConfigurationImport.SetGroup(configuration, i + 1, forward, backward, order);

				if (forward.Count > 0 || backward.Count > 0 || order.Count > 0)
				{
					importados++;
				}
			}

			// O nome da opção mudou entre as versões.
			if (source["CompatibilityMode"] != null && source["WineCompatibilityMode"] == null)
			{
				configuration.EnableWineCompatibilityMode = source.Value<bool?>("CompatibilityMode") ?? false;
			}

			if (importados == 0)
			{
				return false;
			}

			result.AddNotice(importados == 1
				? "1 grupo de ciclo importado do formato do aplicativo anterior"
				: importados + " grupos de ciclo importados do formato do aplicativo anterior");

			return true;
		}

		private static List<string> ReadHotkeys(JObject group, string name)
		{
			JArray array = group[name] as JArray;
			if (array == null)
			{
				return new List<string>();
			}

			return array
				.Select(item => item?.Type == JTokenType.String ? (string)item : null)
				.Where(item => !string.IsNullOrWhiteSpace(item))
				.Select(item => item.Trim())
				.ToList();
		}

		private static Dictionary<string, int> ReadClientsOrder(JObject group)
		{
			var ordem = new Dictionary<string, int>();
			JObject clients = group["ClientsOrder"] as JObject;
			if (clients == null)
			{
				return ordem;
			}

			List<KeyValuePair<int, string>> pares = new List<KeyValuePair<int, string>>();
			foreach (JProperty propriedade in clients.Properties())
			{
				if (!int.TryParse(propriedade.Name, out int posicao))
				{
					continue;
				}

				string nome = propriedade.Value?.Type == JTokenType.String ? (string)propriedade.Value : null;
				if (!string.IsNullOrWhiteSpace(nome))
				{
					pares.Add(new KeyValuePair<int, string>(posicao, nome.Trim()));
				}
			}

			// Posições são normalizadas para 1..n preservando a ordem relativa.
			int posicaoFinal = 0;
			foreach (KeyValuePair<int, string> par in pares.OrderBy(p => p.Key))
			{
				posicaoFinal++;
				ordem[par.Value] = posicaoFinal;
			}

			return ordem;
		}

		private static void SetGroup(IThumbnailConfiguration configuration, int grupo,
			List<string> forward, List<string> backward, Dictionary<string, int> order)
		{
			switch (grupo)
			{
				case 1:
					configuration.CycleGroup1ForwardHotkeys = forward;
					configuration.CycleGroup1BackwardHotkeys = backward;
					configuration.CycleGroup1ClientsOrder = order;
					break;
				case 2:
					configuration.CycleGroup2ForwardHotkeys = forward;
					configuration.CycleGroup2BackwardHotkeys = backward;
					configuration.CycleGroup2ClientsOrder = order;
					break;
				case 3:
					configuration.CycleGroup3ForwardHotkeys = forward;
					configuration.CycleGroup3BackwardHotkeys = backward;
					configuration.CycleGroup3ClientsOrder = order;
					break;
				case 4:
					configuration.CycleGroup4ForwardHotkeys = forward;
					configuration.CycleGroup4BackwardHotkeys = backward;
					configuration.CycleGroup4ClientsOrder = order;
					break;
				case 5:
					configuration.CycleGroup5ForwardHotkeys = forward;
					configuration.CycleGroup5BackwardHotkeys = backward;
					configuration.CycleGroup5ClientsOrder = order;
					break;
			}
		}
	}
}
