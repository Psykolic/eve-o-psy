using EveOPreview.Configuration.Implementation;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Configuration;

/// <summary>
/// Caracteriza o comportamento de SpotlightOpacity depois de ApplyRestrictions()
/// (limites 0.2..1.0).
/// </summary>
public class SpotlightOpacityTests
{
	[Fact]
	public void SpotlightOpacity_abaixo_do_minimo_deve_ser_clampado_para_0_2()
	{
		var configuration = new ThumbnailConfiguration
		{
			SpotlightOpacity = 0.1 // Below minimum of 0.2
		};

		configuration.ApplyRestrictions();

		configuration.SpotlightOpacity.Should().Be(0.2);
	}

	[Fact]
	public void SpotlightOpacity_dentro_da_faixa_deve_manter_o_valor()
	{
		var configuration = new ThumbnailConfiguration
		{
			SpotlightOpacity = 0.5 // Within [0.2, 1.0]
		};

		configuration.ApplyRestrictions();

		configuration.SpotlightOpacity.Should().Be(0.5);
	}

	[Fact]
	public void SpotlightOpacity_acima_do_maximo_deve_ser_clampado_para_1_0()
	{
		var configuration = new ThumbnailConfiguration
		{
			SpotlightOpacity = 1.5 // Above maximum of 1.0
		};

		configuration.ApplyRestrictions();

		configuration.SpotlightOpacity.Should().Be(1.0);
	}
}