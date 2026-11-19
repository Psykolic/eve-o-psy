using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EveOPreview.Configuration.Serialization
{
	/// <summary>
	/// Compatibilidade da configuração entre versões (issue #162).
	///
	/// Antes o modelo guardava <c>System.Drawing.Font</c> e o Json.NET gravava um objeto
	/// ({ "Name": ..., "Size": ..., "Style": ... }). O Core não pode referenciar Font,
	/// então agora a propriedade é uma string no formato do FontConverter. Este conversor
	/// lê os dois formatos e sempre grava a string (formato novo).
	/// </summary>
	public sealed class OverlayLabelFontJsonConverter : JsonConverter<string>
	{
		public override string ReadJson(JsonReader reader, Type objectType, string existingValue,
		                                bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
			{
				return string.Empty;
			}

			if (reader.TokenType == JsonToken.String)
			{
				return (string)reader.Value ?? string.Empty;
			}

			if (reader.TokenType == JsonToken.StartObject)
			{
				JObject obj = JObject.Load(reader);
				string name = (string)(obj["Name"] ?? obj["name"]) ?? "Microsoft Sans Serif";
				double size = 10.0;
				JToken sizeToken = obj["Size"] ?? obj["size"];
				if (sizeToken != null && double.TryParse(sizeToken.ToString(), out double parsed))
				{
					size = parsed;
				}

				bool bold = ReadBool(obj, "Bold");
				bool italic = ReadBool(obj, "Italic");
				int style = 0;
				JToken styleToken = obj["Style"] ?? obj["style"];
				if (styleToken != null && int.TryParse(styleToken.ToString(), out int parsedStyle))
				{
					style = parsedStyle;
				}

				// FontStyle: Bold = 1, Italic = 2, Regular = 0
				if ((style & 1) != 0) bold = true;
				if ((style & 2) != 0) italic = true;

				string text = $"{name}, {size:0.#}pt";
				if (bold && italic) text += ", style=Bold, Italic";
				else if (bold) text += ", style=Bold";
				else if (italic) text += ", style=Italic";
				return text;
			}

			reader.Skip();
			return string.Empty;
		}

		public override void WriteJson(JsonWriter writer, string value, JsonSerializer serializer)
		{
			writer.WriteValue(value ?? string.Empty);
		}

		private static bool ReadBool(JObject obj, string name)
		{
			JToken token = obj[name];
			return token != null && bool.TryParse(token.ToString(), out bool value) && value;
		}
	}
}
