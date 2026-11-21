using FluentAssertions;
using Xunit;

namespace EveO.Tests.Configuration
{
	/// <summary>
	/// Helper puro de Clamp (sem efeitos colaterais) e 3 testes de caracterização.
	/// Não altera nenhum código de produção existente.
	/// </summary>
	internal static class ClampHelper
	{
		public static int Clamp(int value, int minimum, int maximum)
		{
			if (value < minimum)
			{
				return minimum;
			}

			return value > maximum ? maximum : value;
		}
	}

	public class ClampTests
	{
		[Fact]
		public void Clamp_valor_dentro_do_intervalo_deve_ser_mantido()
		{
			ClampHelper.Clamp(5, 1, 10).Should().Be(5);
		}

		[Fact]
		public void Clamp_abaixo_do_minimo_deve_retornar_o_minimo()
		{
			ClampHelper.Clamp(-3, 1, 10).Should().Be(1);
		}

		[Fact]
		public void Clamp_acima_do_maximo_deve_retornar_o_maximo()
		{
			ClampHelper.Clamp(99, 1, 10).Should().Be(10);
		}
	}
}
