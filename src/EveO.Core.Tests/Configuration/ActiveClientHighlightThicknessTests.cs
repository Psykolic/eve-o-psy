using EveOPreview.Configuration.Implementation;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Configuration
{
	/// <summary>
	/// Caracteriza o comportamento de ActiveClientHighlightThickness depois de ApplyRestrictions()
	/// (limites 1..6 no código).
	/// </summary>
	public class ActiveClientHighlightThicknessTests
	{
		[Fact]
		public void ActiveClientHighlightThickness_abaixo_do_minimo_deve_retornar_1()
		{
			var configuration = new ThumbnailConfiguration
			{
				ActiveClientHighlightThickness = 0
			};

			configuration.ApplyRestrictions();

			configuration.ActiveClientHighlightThickness.Should().Be(1);
		}

		[Fact]
		public void ActiveClientHighlightThickness_dentro_da_faixa_deve_manter_o_valor()
		{
			var configuration = new ThumbnailConfiguration
			{
				ActiveClientHighlightThickness = 3
			};

			configuration.ApplyRestrictions();

			configuration.ActiveClientHighlightThickness.Should().Be(3);
		}

		[Fact]
		public void ActiveClientHighlightThickness_acima_do_maximo_deve_retornar_6()
		{
			var configuration = new ThumbnailConfiguration
			{
				ActiveClientHighlightThickness = 7
			};

			configuration.ApplyRestrictions();

			configuration.ActiveClientHighlightThickness.Should().Be(6);
		}
	}
}