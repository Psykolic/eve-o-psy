using System.Drawing;
using EveOPreview.Configuration.Implementation;
using Xunit;

namespace EveO.Core.Tests.Configuration;

/// <summary>
/// Cenários de <see cref="ThumbnailConfiguration.ThumbnailSize"/> após ApplyRestrictions()
/// que não são cobertos por ThumbnailSizeTests: largura/altura independentes,
/// limites exatos e piso absoluto do mínimo.
/// </summary>
public class ThumbnailSizeApplyRestrictionTests
{
	[Fact]
	public void ApplyRestrictions_LarguraAbaixoEAlturaAcima_AjustaCadaEixoSeparadamente()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(100, 60),
			ThumbnailMaximumSize = new Size(800, 450),
			ThumbnailSize = new Size(50, 1200)
		};

		config.ApplyRestrictions();

		config.ThumbnailSize.Should().Be(new Size(100, 450));
	}

	[Fact]
	public void ApplyRestrictions_TamanhoExatamenteNosLimites_MantemOsValores()
	{
		var minimo = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(100, 60),
			ThumbnailMaximumSize = new Size(800, 450),
			ThumbnailSize = new Size(100, 60)
		};
		var maximo = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(100, 60),
			ThumbnailMaximumSize = new Size(800, 450),
			ThumbnailSize = new Size(800, 450)
		};

		minimo.ApplyRestrictions();
		maximo.ApplyRestrictions();

		minimo.ThumbnailSize.Should().Be(new Size(100, 60));
		maximo.ThumbnailSize.Should().Be(new Size(800, 450));
	}

	[Fact]
	public void ApplyRestrictions_MinimoAbaixoDoPisoAbsoluto_TamanhoSobeParaOPiso()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(1, 1),
			ThumbnailMaximumSize = new Size(800, 450),
			ThumbnailSize = new Size(1, 1)
		};

		config.ApplyRestrictions();

		config.ThumbnailMinimumSize.Should().Be(new Size(16, 9));
		config.ThumbnailSize.Should().Be(new Size(16, 9));
	}
}
