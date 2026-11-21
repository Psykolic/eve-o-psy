using EveOPreview.Configuration.Implementation;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Configuration;

public class ThumbnailRefreshPeriodTests
{
	[Fact]
	public void ThumbnailRefreshPeriod_acima_do_maximo_deve_ser_clampado_para_1000()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailRefreshPeriod = 2000 // Above Windows maximum of 1000
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailRefreshPeriod.Should().Be(1000);
	}

#if LINUX
	[Fact]
	public void ThumbnailRefreshPeriod_abaixo_do_minimo_deve_ser_clampado_para_10()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailRefreshPeriod = 1 // Below Linux minimum of 10
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailRefreshPeriod.Should().Be(10);
	}
#else
	[Fact]
	public void ThumbnailRefreshPeriod_abaixo_do_minimo_deve_ser_clampado_para_300()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailRefreshPeriod = 1 // Below Windows minimum of 300
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailRefreshPeriod.Should().Be(300);
	}
#endif

	[Fact]
	public void ThumbnailRefreshPeriod_dentro_da_faixa_deve_manter_o_valor()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailRefreshPeriod = 500 // Default value, within [300, 1000] on Windows or [10, 1000] on Linux
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailRefreshPeriod.Should().Be(500);
	}

	[Fact]
	public void ThumbnailRefreshPeriod_no_limite_inferior_exato_deve_manter_o_minimo_plataforma()
	{
#if LINUX
		var expectedMin = 10;
#else
		var expectedMin = 300;
#endif
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailRefreshPeriod = expectedMin // Exactly at platform minimum
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailRefreshPeriod.Should().Be(expectedMin);
	}

	[Fact]
	public void ThumbnailRefreshPeriod_no_limite_superior_exato_deve_manter_1000()
	{
		var configuration = new ThumbnailConfiguration
		{
			ThumbnailRefreshPeriod = 1000 // Exactly at maximum
		};

		configuration.ApplyRestrictions();

		configuration.ThumbnailRefreshPeriod.Should().Be(1000);
	}
}