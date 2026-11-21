using EveOPreview.Configuration.Implementation;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Configuration;

public class ThumbnailZoomFactorTests
{
	[Fact]
	public void ThumbnailZoomFactor_abaixo_do_minimo_deve_ser_clampado_para_2()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailZoomFactor = 1 // Below minimum of 2
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailZoomFactor.Should().Be(2);
	}

	[Fact]
	public void ThumbnailZoomFactor_dentro_da_faixa_deve_manter_o_valor()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailZoomFactor = 5 // Within [2, 10]
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailZoomFactor.Should().Be(5);
	}

	[Fact]
	public void ThumbnailZoomFactor_acima_do_maximo_deve_ser_clampado_para_10()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailZoomFactor = 15 // Above maximum of 10
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailZoomFactor.Should().Be(10);
	}

	[Fact]
	public void ThumbnailZoomFactor_no_limite_inferior_exato_deve_manter_o_minimo()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailZoomFactor = 2 // Exactly at minimum
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailZoomFactor.Should().Be(2);
	}

	[Fact]
	public void ThumbnailZoomFactor_no_limite_superior_exato_deve_manter_o_maximo()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailZoomFactor = 10 // Exactly at maximum
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailZoomFactor.Should().Be(10);
	}
}