using EveOPreview.Configuration.Implementation;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Configuration;

public class ThumbnailZoomFactorRestrictionTests
{
	[Fact]
	public void ThumbnailZoomFactor_abaixo_do_minimo_depois_apply_restrictions_deve_ser_clampado_para_2()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailZoomFactor = 1 // Below minimum of 2
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailZoomFactor.Should().Be(2);
	}

	[Fact]
	public void ThumbnailZoomFactor_dentro_da_faixa_depois_apply_restrictions_deve_manter_o_valor()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailZoomFactor = 5 // Within [2, 10]
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailZoomFactor.Should().Be(5);
	}

	[Fact]
	public void ThumbnailZoomFactor_acima_do_maximo_depois_apply_restrictions_deve_ser_clampado_para_10()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailZoomFactor = 15 // Above maximum of 10
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailZoomFactor.Should().Be(10);
	}
}