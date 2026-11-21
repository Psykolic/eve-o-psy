using EveOPreview.Configuration.Implementation;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Configuration;

public class ThumbnailOpacityTests
{
	[Fact]
	public void ThumbnailOpacity_abaixo_do_minimo_deve_ser_clampado_para_0_2()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailOpacity = 0.1 // Below minimum of 0.2
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailOpacity.Should().Be(0.2);
	}

	[Fact]
	public void ThumbnailOpacity_dentro_da_faixa_deve_manter_o_valor()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailOpacity = 0.5 // Within [0.2, 1.0]
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailOpacity.Should().Be(0.5);
	}

	[Fact]
	public void ThumbnailOpacity_acima_do_maximo_deve_ser_clampado_para_1_0()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailOpacity = 1.5 // Above maximum of 1.0
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailOpacity.Should().Be(1.0);
	}

	[Fact]
	public void ThumbnailOpacity_no_limite_inferior_exato_deve_manter_o_minimo()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailOpacity = 0.2 // Exactly at minimum
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailOpacity.Should().Be(0.2);
	}

	[Fact]
	public void ThumbnailOpacity_no_limite_superior_exato_deve_manter_o_maximo()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailOpacity = 1.0 // Exactly at maximum
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailOpacity.Should().Be(1.0);
	}
}