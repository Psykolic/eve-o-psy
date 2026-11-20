using EveOPreview.Configuration.Implementation;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Configuration
{
	// Fase 2.2 (ADR-006): o modo event-driven é o padrão e o período de reconciliação
	// precisa de limites — um valor absurdo no JSON não pode desligar a reconciliação
	// (0) nem deixar o app sem atualização por minutos.
	public class RefreshModeConfigurationTests
	{
		[Fact]
		public void EventDrivenRefresh_vem_ligado_por_padrao()
		{
			new ThumbnailConfiguration().EventDrivenRefresh.Should().BeTrue();
		}

		[Fact]
		public void ReconciliationPeriod_padrao_cinco_segundos()
		{
			new ThumbnailConfiguration().ReconciliationPeriod.Should().Be(5000);
		}

		[Theory]
		[InlineData(0, 1000)]
		[InlineData(500, 1000)]
		[InlineData(60000, 60000)]
		[InlineData(999999, 60000)]
		public void ReconciliationPeriod_respeita_limites(int configured, int expected)
		{
			var configuration = new ThumbnailConfiguration
			{
				ReconciliationPeriod = configured
			};

			configuration.ApplyRestrictions();

			configuration.ReconciliationPeriod.Should().Be(expected);
		}

		[Fact]
		public void Desligar_event_driven_nao_altera_o_periodo_de_polling_antigo()
		{
			var configuration = new ThumbnailConfiguration
			{
				EventDrivenRefresh = false,
				ThumbnailRefreshPeriod = 400
			};

			configuration.ApplyRestrictions();

			configuration.EventDrivenRefresh.Should().BeFalse();
			configuration.ThumbnailRefreshPeriod.Should().Be(400);
		}
	}
}
