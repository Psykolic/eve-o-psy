using System.Drawing;
using EveOPreview.Services;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Services
{
	public class SpotlightResizeCalculatorTests
	{
		private static readonly Size MinimumSize = new Size(160, 90);
		private const double WideAspect = 16.0 / 9.0;

		[Fact]
		public void Test_PreserveAspect_BottomRight_WidthDriven()
		{
			var start = new Rectangle(100, 100, 640, 360);

			var result = SpotlightResizeCalculator.Calculate(
				start,
				SpotlightResizeEdge.BottomRight,
				160,
				0,
				WideAspect,
				false,
				MinimumSize);

			result.Should().Be(new Rectangle(100, 100, 800, 450));
		}

		[Fact]
		public void Test_PreserveAspect_BottomRight_HeightDriven()
		{
			var start = new Rectangle(100, 100, 640, 360);

			var result = SpotlightResizeCalculator.Calculate(
				start,
				SpotlightResizeEdge.BottomRight,
				0,
				90,
				WideAspect,
				false,
				MinimumSize);

			result.Should().Be(new Rectangle(100, 100, 800, 450));
		}

		[Fact]
		public void Test_PreserveAspect_LeftEdge_KeepsRightEdgeAnchored()
		{
			var start = new Rectangle(100, 100, 640, 360);

			var result = SpotlightResizeCalculator.Calculate(
				start,
				SpotlightResizeEdge.Left,
				-160,
				0,
				WideAspect,
				false,
				MinimumSize);

			result.Should().Be(new Rectangle(-60, 100, 800, 450));
		}

		[Fact]
		public void Test_PreserveAspect_TopLeft_KeepsBottomRightAnchored()
		{
			var start = new Rectangle(100, 100, 640, 360);

			var result = SpotlightResizeCalculator.Calculate(
				start,
				SpotlightResizeEdge.TopLeft,
				-160,
				0,
				WideAspect,
				false,
				MinimumSize);

			result.Should().Be(new Rectangle(-60, 10, 800, 450));
		}

		[Fact]
		public void Test_FreeResize_DoesNotPreserveAspect()
		{
			var start = new Rectangle(100, 100, 640, 360);

			var result = SpotlightResizeCalculator.Calculate(
				start,
				SpotlightResizeEdge.BottomRight,
				160,
				0,
				WideAspect,
				true,
				MinimumSize);

			result.Should().Be(new Rectangle(100, 100, 800, 360));
		}

		[Fact]
		public void Test_PreserveAspect_RespectsMinimumSize()
		{
			var start = new Rectangle(100, 100, 640, 360);

			var result = SpotlightResizeCalculator.Calculate(
				start,
				SpotlightResizeEdge.BottomRight,
				-1000,
				-1000,
				WideAspect,
				false,
				MinimumSize);

			result.Width.Should().Be(160);
			result.Height.Should().Be(90);
		}
	}
}
