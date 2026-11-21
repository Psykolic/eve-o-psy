/// <summary>
/// Tests for <see cref="ThumbnailConfiguration.ApplyRestrictions()"/> that characterize
/// the behavior of <see cref="ThumbnailConfiguration.ThumbnailMaximumSize"/>.
/// After ApplyRestrictions, valid ThumbnailMaximumSize values are preserved,
/// and the size interacts with ThumbnailMinimumSize when clamping ThumbnailSize.
/// </summary>
using System.Drawing;
using EveOPreview.Configuration.Implementation;
using Xunit;

namespace EveO.Core.Tests.Configuration;

public class ThumbnailMaximumSizeTests
{
	/// <summary>
	/// Valid width value within limits is preserved after ApplyRestrictions.
	/// </summary>
	[Fact]
	public void ApplyRestrictions_ValidWidth_Preserved()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMaximumSize = new Size(640, 400)
		};

		config.ApplyRestrictions();

		config.ThumbnailMaximumSize.Should().Be(new Size(640, 400));
	}

	/// <summary>
	/// Valid height value within limits is preserved after ApplyRestrictions.
	/// </summary>
	[Fact]
	public void ApplyRestrictions_ValidHeight_Preserved()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMaximumSize = new Size(800, 500)
		};

		config.ApplyRestrictions();

		config.ThumbnailMaximumSize.Should().Be(new Size(800, 500));
	}

	/// <summary>
	/// ThumbnailMaximumSize width at the system maximum (7680) is preserved
	/// after ApplyRestrictions is called.
	/// </summary>
	[Fact]
	public void ApplyRestrictions_MaxWidthAtSystemLimit_Preserved()
	{
		var config = new ThumbnailConfiguration
		{
			ThumbnailMaximumSize = new Size(7680, 540)
		};

		config.ApplyRestrictions();

		config.ThumbnailMaximumSize.Should().Be(new Size(7680, 540));
	}
}