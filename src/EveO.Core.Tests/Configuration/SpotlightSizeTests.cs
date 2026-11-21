/// <summary>
/// Tests for <see cref="ThumbnailConfiguration.ApplyRestrictions()"/> that characterize
/// the behavior of <see cref="ThumbnailConfiguration.SpotlightSize"/>.
/// After ApplyRestrictions, valid SpotlightSize values are preserved,
/// and the size is clamped to [160, 7680] x [90, 4320].
/// </summary>
using System.Drawing;
using EveOPreview.Configuration.Implementation;
using Xunit;

namespace EveO.Core.Tests.Configuration;

public class SpotlightSizeTests
{
	/// <summary>
	/// Valid width and height within limits are preserved unchanged
	/// (default 640x360 stays intact).
	/// </summary>
	[Fact]
	public void SpotlightSize_Valores_Within_Limits_Sao_Preservados()
	{
		var config = new ThumbnailConfiguration
		{
			SpotlightSize = new Size(640, 360)
		};

		config.ApplyRestrictions();

		config.SpotlightSize.Should().Be(new Size(640, 360));
	}

	/// <summary>
	/// Size abaixo do mínimo (160x90) é clampado para os limites inferiores.
	/// </summary>
	[Fact]
	public void SpotlightSize_Abaixo_Do_Minimo_Deve_ser_Clampado_Para_160x90()
	{
		var config = new ThumbnailConfiguration
		{
			SpotlightSize = new Size(100, 50)
		};

		config.ApplyRestrictions();

		config.SpotlightSize.Should().Be(new Size(160, 90));
	}

	/// <summary>
	/// Size acima do máximo (7680x4320) é clampado para os limites superiores.
	/// </summary>
	[Fact]
	public void SpotlightSize_Acima_Do_Maximo_Deve_ser_Clampado_Para_7680x4320()
	{
		var config = new ThumbnailConfiguration
		{
			SpotlightSize = new Size(8000, 5000)
		};

		config.ApplyRestrictions();

		config.SpotlightSize.Should().Be(new Size(7680, 4320));
	}
}