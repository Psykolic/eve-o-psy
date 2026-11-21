/// <summary>
/// Caracteriza o comportamento de <see cref="ThumbnailConfiguration.ThumbnailSize"/>
/// depois de ApplyRestrictions(): respeita ThumbnailMinimumSize e ThumbnailMaximumSize.
/// </summary>
using System.Drawing;
using EveOPreview.Configuration.Implementation;
using Xunit;

namespace EveO.Core.Tests.Configuration;

public class ThumbnailSizeTests
{
	[Fact]
	public void ApplyRestrictions_TamanhoAbaixoDoMinimo_SobeParaOMinimo()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(100, 60),
			ThumbnailMaximumSize = new Size(800, 450),
			ThumbnailSize = new Size(50, 30)
		};

		config.ApplyRestrictions();

		config.ThumbnailSize.Should().Be(new Size(100, 60));
	}

	[Fact]
	public void ApplyRestrictions_TamanhoDentroDaFaixa_MantemOValor()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(100, 60),
			ThumbnailMaximumSize = new Size(800, 450),
			ThumbnailSize = new Size(400, 225)
		};

		config.ApplyRestrictions();

		config.ThumbnailSize.Should().Be(new Size(400, 225));
	}

	[Fact]
	public void ApplyRestrictions_TamanhoAcimaDoMaximo_DesceParaOMaximo()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(100, 60),
			ThumbnailMaximumSize = new Size(800, 450),
			ThumbnailSize = new Size(2000, 1200)
		};

		config.ApplyRestrictions();

		config.ThumbnailSize.Should().Be(new Size(800, 450));
	}
}
