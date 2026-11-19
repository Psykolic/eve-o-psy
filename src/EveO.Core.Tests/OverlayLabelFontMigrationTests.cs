using EveOPreview.Configuration.Implementation;
using Newtonsoft.Json;
using Xunit;

namespace EveO.Core.Tests
{
	public class OverlayLabelFontMigrationTests
	{
		[Fact]
		public void Le_Formato_Antigo_Com_Objeto()
		{
			const string json = "{\"OverlayLabelFont\":{\"Name\":\"Arial\",\"Size\":12.0,\"Style\":1,\"Bold\":true}}";
			ThumbnailConfiguration cfg = JsonConvert.DeserializeObject<ThumbnailConfiguration>(json);
			Assert.Contains("Arial", cfg.OverlayLabelFont);
			Assert.Contains("12", cfg.OverlayLabelFont);
			Assert.Contains("Bold", cfg.OverlayLabelFont);
		}

		[Fact]
		public void Le_Formato_Novo_Com_String()
		{
			const string json = "{\"OverlayLabelFont\":\"Consolas, 9pt, style=Italic\"}";
			ThumbnailConfiguration cfg = JsonConvert.DeserializeObject<ThumbnailConfiguration>(json);
			Assert.Equal("Consolas, 9pt, style=Italic", cfg.OverlayLabelFont);
		}

		[Fact]
		public void Grava_Sempre_String()
		{
			ThumbnailConfiguration cfg = new ThumbnailConfiguration { OverlayLabelFont = "Arial, 10pt" };
			string json = JsonConvert.SerializeObject(cfg);
			Assert.Contains("\"OverlayLabelFont\":\"Arial, 10pt\"", json);
			Assert.DoesNotContain("\"Name\":", json);
		}
	}
}
