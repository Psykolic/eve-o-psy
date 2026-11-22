using System.Linq;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Configuration
{
	public class OverlayLabelTests
	{
		[Theory]
		[InlineData("EVE - Psykolic", "Psykolic")]
		[InlineData("EVE - Irineu Evangelista", "Irineu E")]
		[InlineData("EVE Frontier - Scout", "*Scout")]
		[InlineData("EVE", "EVE")]
		[InlineData("", "")]
		public void FromTitle_RemovePrefixoECortaEmOito(string title, string expected)
		{
			OverlayLabel.FromTitle(title).Should().Be(expected);
		}

		[Fact]
		public void Resolve_UsaOTextoCustomQuandoDefinido()
		{
			OverlayLabel.Resolve("EVE - Psykolic", "PSY").Should().Be("PSY");
		}

		[Fact]
		public void Resolve_VazioEmBrancoOuNuloUsaONome()
		{
			OverlayLabel.Resolve("EVE - Psykolic", "").Should().Be("Psykolic");
			OverlayLabel.Resolve("EVE - Psykolic", "   ").Should().Be("Psykolic");
			OverlayLabel.Resolve("EVE - Psykolic", null).Should().Be("Psykolic");
		}

		[Fact]
		public void Sanitize_AparaEEscondeAcimaDeOito()
		{
			OverlayLabel.Sanitize("  ABCDEFGHIJ  ").Should().Be("ABCDEFGH");
		}

		[Fact]
		public void Truncate_ContaElementoDeTextoNaoByte()
		{
			// "e" + acento combinante: dois chars UTF-16, um elemento de texto.
			string composto = string.Concat(Enumerable.Repeat("e\u0301", 9));

			OverlayLabel.Truncate(composto)
				.Should().Be(string.Concat(Enumerable.Repeat("e\u0301", OverlayLabel.MaxLength)));
		}

		[Fact]
		public void Configuracao_NormalizaAoGravarERemoveQuandoVazio()
		{
			var config = new ThumbnailConfiguration();

			config.SetClientLabel("EVE - Psykolic", "PSYCHOBLAST");
			config.GetClientLabel("EVE - Psykolic").Should().Be("PSYCHOBL");

			config.SetClientLabel("EVE - Psykolic", "  ");
			config.GetClientLabel("EVE - Psykolic").Should().BeEmpty();
			config.PerClientLabel.Should().NotContainKey("EVE - Psykolic");
		}

		[Fact]
		public void Configuracao_MoveALabelQuandoOClienteTrocaDeNome()
		{
			var config = new ThumbnailConfiguration();
			config.SetClientLabel("EVE - Old Toon", "OLD");

			config.RenameClientLabel("EVE - Old Toon", "EVE - New Toon");

			config.GetClientLabel("EVE - Old Toon").Should().BeEmpty();
			config.GetClientLabel("EVE - New Toon").Should().Be("OLD");
		}

		[Fact]
		public void Configuracao_CorrigeArquivoEditadoAMaoAoCarregar()
		{
			var config = new ThumbnailConfiguration();
			config.PerClientLabel["EVE - X"] = "1234567890";
			config.PerClientLabel["EVE - Y"] = "   ";

			config.ApplyRestrictions();

			config.PerClientLabel["EVE - X"].Should().Be("12345678");
			config.PerClientLabel.Should().NotContainKey("EVE - Y");
		}
	}
}
