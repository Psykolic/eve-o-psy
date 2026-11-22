using System.Globalization;

namespace EveOPreview.Configuration
{
	/// <summary>
	/// Texto da label do overlay (thumbnail e spotlight).
	///
	/// O usuario pode definir um texto curto por cliente; quando nao define, vale o
	/// comeco do nome do cliente. O limite e 8 caracteres porque a label e desenhada
	/// sobre uma thumbnail pequena — acima disso cobre a previa (issue #151).
	/// </summary>
	public static class OverlayLabel
	{
		public const int MaxLength = 8;

		private const string ClientPrefix = "EVE - ";
		private const string FrontierPrefix = "EVE Frontier - ";

		/// <summary>Remove o prefixo do cliente e aplica o limite. Nunca devolve null.</summary>
		public static string FromTitle(string title)
		{
			if (string.IsNullOrEmpty(title))
			{
				return string.Empty;
			}

			string name = title.Replace(ClientPrefix, string.Empty).Replace(FrontierPrefix, "*");

			return Truncate(name);
		}

		/// <summary>Preparado para gravar: sem espacos nas pontas e cortado em 8; em branco vira vazio.</summary>
		public static string Sanitize(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return string.Empty;
			}

			return Truncate(text.Trim());
		}

		/// <summary>O que aparece: o texto custom quando definido, senao o padrao derivado do titulo.</summary>
		public static string Resolve(string title, string customLabel)
		{
			string sanitized = Sanitize(customLabel);

			return sanitized.Length > 0 ? sanitized : FromTitle(title);
		}

		/// <summary>Corta por elementos de texto: acento composto e emoji contam como um caractere.</summary>
		public static string Truncate(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return string.Empty;
			}

			StringInfo info = new StringInfo(text);

			return info.LengthInTextElements <= MaxLength
				? text
				: info.SubstringByTextElements(0, MaxLength);
		}
	}
}
