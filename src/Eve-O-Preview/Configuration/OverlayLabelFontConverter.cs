using System.Drawing;

namespace EveOPreview.Configuration
{
	/// <summary>
	/// Conversão entre a fonte do rótulo de overlay (neutra, guardada como texto no Core —
	/// <c>IThumbnailConfiguration.OverlayLabelFont</c>) e <see cref="Font"/>.
	///
	/// Fica no executável porque <c>Font</c>/<c>FontConverter</c> exigem
	/// <c>System.Drawing.Common</c>, que o Core não pode referenciar (issue #162).
	/// O formato textual é idêntico ao que o Json.NET já usava ao serializar <c>Font</c>.
	/// </summary>
	internal static class OverlayLabelFontConverter
	{
		/// <summary>Fonte padrão do overlay (equivalente ao antigo padrão do modelo).</summary>
		public static Font CreateDefault() =>
			new Font(FontFamily.GenericSansSerif, 10.0F, FontStyle.Bold);

		public static Font FromInvariantString(string value)
		{
			// O modelo antigo sempre tinha uma Font; nunca devolvemos null para preservar
			// o contrato de quem consome (ex.: SetPropertiesOverlayLabel).
			if (string.IsNullOrWhiteSpace(value))
			{
				return CreateDefault();
			}

			return (Font)(new FontConverter()).ConvertFromInvariantString(value);
		}

		public static string ToInvariantString(Font font)
		{
			return font == null ? string.Empty : (new FontConverter()).ConvertToInvariantString(font);
		}
	}
}
