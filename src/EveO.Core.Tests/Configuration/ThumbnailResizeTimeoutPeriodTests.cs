using EveOPreview.Configuration.Implementation;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Configuration
{
	public class ThumbnailResizeTimeoutPeriodTests
	{
		[Fact]
		public void ThumbnailResizeTimeoutPeriod_abaixo_do_minimo_deve_ser_clampado_para_200()
		{
			var configuration = new ThumbnailConfiguration
			{
				ThumbnailResizeTimeoutPeriod = 100 // Below minimum of 200
			};

			configuration.ApplyRestrictions();

			configuration.ThumbnailResizeTimeoutPeriod.Should().Be(200);
		}

		[Fact]
		public void ThumbnailResizeTimeoutPeriod_dentro_da_faixa_deve_manter_o_valor()
		{
			var configuration = new ThumbnailConfiguration
			{
				ThumbnailResizeTimeoutPeriod = 500 // Default value, within [200, 5000]
			};

			configuration.ApplyRestrictions();

			configuration.ThumbnailResizeTimeoutPeriod.Should().Be(500);
		}

		[Fact]
		public void ThumbnailResizeTimeoutPeriod_acima_do_maximo_deve_ser_clampado_para_5000()
		{
			var configuration = new ThumbnailConfiguration
			{
				ThumbnailResizeTimeoutPeriod = 6000 // Above maximum of 5000
			};

			configuration.ApplyRestrictions();

			configuration.ThumbnailResizeTimeoutPeriod.Should().Be(5000);
		}

		[Fact]
		public void ThumbnailResizeTimeoutPeriod_no_limite_inferior_exato_deve_manter_200()
		{
			var configuration = new ThumbnailConfiguration
			{
				ThumbnailResizeTimeoutPeriod = 200 // Exactly at minimum
			};

			configuration.ApplyRestrictions();

			configuration.ThumbnailResizeTimeoutPeriod.Should().Be(200);
		}

		[Fact]
		public void ThumbnailResizeTimeoutPeriod_no_limite_superior_exato_deve_manter_5000()
		{
			var configuration = new ThumbnailConfiguration
			{
				ThumbnailResizeTimeoutPeriod = 5000 // Exactly at maximum
			};

			configuration.ApplyRestrictions();

			configuration.ThumbnailResizeTimeoutPeriod.Should().Be(5000);
		}
	}
}