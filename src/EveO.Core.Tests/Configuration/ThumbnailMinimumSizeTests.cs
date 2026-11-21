/// <summary>
/// Tests for <see cref="ThumbnailConfiguration.ApplyRestrictions()"/> that characterize
/// the real limits of <see cref="ThumbnailConfiguration.ThumbnailMinimumSize"/>.
/// Width is clamped to [16, 7680] and Height to [9, 4320].
/// </summary>
using System.Drawing;
using EveOPreview.Configuration.Implementation;
using Xunit;

namespace EveO.Core.Tests.Configuration;

public class ThumbnailMinimumSizeTests
{
	/// <summary>
	/// Width below minimum (16) gets clamped up to 16.
	/// </summary>
	[Fact]
	public void ApplyRestrictions_ClampsWidthBelowMinimum_ToMinimum()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(10, 32)
		};

		config.ApplyRestrictions();

		config.ThumbnailMinimumSize.Should().Be(new Size(16, 32));
	}

	/// <summary>
	/// Width above maximum (7680) gets clamped down to 7680.
	/// </summary>
	[Fact]
	public void ApplyRestrictions_ClampsWidthAboveMaximum_ToMaximum()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(8000, 32)
		};

		config.ApplyRestrictions();

		config.ThumbnailMinimumSize.Should().Be(new Size(7680, 32));
	}

	/// <summary>
	/// Height below minimum (9) gets clamped up to 9.
	/// </summary>
	[Fact]
	public void ApplyRestrictions_ClampsHeightBelowMinimum_ToMinimum()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(32, 5)
		};

		config.ApplyRestrictions();

		config.ThumbnailMinimumSize.Should().Be(new Size(32, 9));
	}

	/// <summary>
	/// Height above maximum (4320) gets clamped down to 4320.
	/// </summary>
	[Fact]
	public void ApplyRestrictions_ClampsHeightAboveMaximum_ToMaximum()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(32, 5000)
		};

		config.ApplyRestrictions();

		config.ThumbnailMinimumSize.Should().Be(new Size(32, 4320));
	}

	/// <summary>
	/// Valid values within limits are preserved unchanged.
	/// </summary>
	[Fact]
	public void ApplyRestrictions_ValidValuesPreserved()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(100, 200)
		};

		config.ApplyRestrictions();

		config.ThumbnailMinimumSize.Should().Be(new Size(100, 200));
	}

	/// <summary>
	/// Width exactly at minimum (16) is preserved.
	/// </summary>
	[Fact]
	public void ApplyRestrictions_WidthAtMinimum_Preserved()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(16, 32)
		};

		config.ApplyRestrictions();

		config.ThumbnailMinimumSize.Should().Be(new Size(16, 32));
	}

	/// <summary>
	/// Height exactly at minimum (9) is preserved.
	/// </summary>
	[Fact]
	public void ApplyRestrictions_HeightAtMinimum_Preserved()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(32, 9)
		};

		config.ApplyRestrictions();

		config.ThumbnailMinimumSize.Should().Be(new Size(32, 9));
	}

	/// <summary>
	/// Width exactly at maximum (7680) is preserved.
	/// </summary>
	[Fact]
	public void ApplyRestrictions_WidthAtMaximum_Preserved()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(7680, 32)
		};

		config.ApplyRestrictions();

		config.ThumbnailMinimumSize.Should().Be(new Size(7680, 32));
	}

	/// <summary>
	/// Height exactly at maximum (4320) is preserved.
	/// </summary>
	[Fact]
	public void ApplyRestrictions_HeightAtMaximum_Preserved()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMinimumSize = new Size(32, 4320)
		};

		config.ApplyRestrictions();

		config.ThumbnailMinimumSize.Should().Be(new Size(32, 4320));
	}
}