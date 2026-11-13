using System.Drawing;
using EveOPreview.Services;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Services
{
	public class SpotlightRegionCalculatorTests
	{
		[Fact]
		public void Test_MapSelection_FullWindow_MapsProportionally()
		{
			var destination = new Size(640, 360);
			var displayedSource = new Rectangle(0, 0, 1920, 1080);
			var selection = new Rectangle(320, 0, 320, 360);

			var region = SpotlightRegionCalculator.MapSelection(selection, displayedSource, destination);

			region.Should().Be(new Rectangle(960, 0, 960, 1080));
		}

		[Fact]
		public void Test_MapSelection_OnCroppedPreview_MapsInsideCurrentCrop()
		{
			var destination = new Size(400, 200);
			var displayedSource = new Rectangle(100, 50, 400, 200);
			var selection = new Rectangle(0, 0, 200, 100);

			var region = SpotlightRegionCalculator.MapSelection(selection, displayedSource, destination);

			region.Should().Be(new Rectangle(100, 50, 200, 100));
		}

		[Fact]
		public void Test_ClampToClient_IntersectsWithClientArea()
		{
			var region = new Rectangle(10, 10, 100, 100);

			var clamped = SpotlightRegionCalculator.ClampToClient(region, new Size(50, 50));

			clamped.Should().Be(new Rectangle(10, 10, 40, 40));
		}

		[Fact]
		public void Test_ClampToClient_FullyOutside_ReturnsEmpty()
		{
			var region = new Rectangle(100, 100, 20, 20);

			var clamped = SpotlightRegionCalculator.ClampToClient(region, new Size(50, 50));

			clamped.Should().Be(Rectangle.Empty);
		}

		[Fact]
		public void Test_FitAspect_SquareRegion_ProducesSquarePreview()
		{
			var source = new Rectangle(0, 0, 1000, 1000);

			var size = SpotlightRegionCalculator.FitAspect(
				new Size(640, 360),
				source,
				new Size(160, 90),
				new Size(2000, 1000));

			size.Should().Be(new Size(640, 640));
		}

		[Fact]
		public void Test_FitAspect_RespectsMinimumHeight()
		{
			var source = new Rectangle(0, 0, 2000, 100);

			var size = SpotlightRegionCalculator.FitAspect(
				new Size(640, 360),
				source,
				new Size(160, 90),
				new Size(2000, 1000));

			size.Should().Be(new Size(1800, 90));
		}
	}
}
