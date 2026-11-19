using System;
using System.Drawing;
using EveOPreview.Services;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Services
{
	public class ThumbnailSizePolicyTests
	{
		private static readonly Size MinimumSize = new Size(32, 18);
		private static readonly Size MaximumSize = new Size(960, 540);

		[Fact]
		public void Test_ResolveInitialThumbnailSize_PerClientSize_UsedForLoggedInClient()
		{
			var size = ThumbnailSizePolicy.ResolveInitialThumbnailSize(
				new Size(384, 216),
				new Size(200, 120),
				"EVE - Example Toon 1");

			size.Should().Be(new Size(200, 120));
		}

		[Fact]
		public void Test_ResolveInitialThumbnailSize_WithoutPerClientSize_UsesConfiguration()
		{
			var size = ThumbnailSizePolicy.ResolveInitialThumbnailSize(
				new Size(384, 216),
				null,
				"EVE - Example Toon 1");

			size.Should().Be(new Size(384, 216));
		}

		[Fact]
		public void Test_ResolveInitialThumbnailSize_LoginClient_IgnoresPerClientSize()
		{
			var size = ThumbnailSizePolicy.ResolveInitialThumbnailSize(
				new Size(384, 216),
				new Size(200, 120),
				ThumbnailSizePolicy.DefaultClientTitle);

			size.Should().Be(new Size(384, 216));
		}

		[Fact]
		public void Test_ClampToLimits_ClampsBothDimensions()
		{
			ThumbnailSizePolicy.ClampToLimits(new Size(10, 1000), MinimumSize, MaximumSize)
				.Should().Be(new Size(32, 540));
		}

		[Fact]
		public void Test_ShouldPropagateResize_UserResizeToDifferentSize_ReturnsTrue()
		{
			ThumbnailSizePolicy.ShouldPropagateResize(
				new Size(384, 216),
				new Size(400, 225),
				MinimumSize,
				MaximumSize,
				isUserResize: true)
				.Should().BeTrue();
		}

		[Fact]
		public void Test_ShouldPropagateResize_UserResizeWithoutChange_ReturnsFalse()
		{
			ThumbnailSizePolicy.ShouldPropagateResize(
				new Size(384, 216),
				new Size(384, 216),
				MinimumSize,
				MaximumSize,
				isUserResize: true)
				.Should().BeFalse();
		}

		[Fact]
		public void Test_ShouldPropagateResize_ProgrammaticResize_ReturnsFalse()
		{
			ThumbnailSizePolicy.ShouldPropagateResize(
				new Size(384, 216),
				new Size(52, 39),
				MinimumSize,
				MaximumSize,
				isUserResize: false)
				.Should().BeFalse();
		}

		[Fact]
		public void Test_ShouldPropagateResize_ProposedBelowMinimumAfterClamp_ReturnsFalse()
		{
			// O tamanho atual já é o mínimo: um resize para algo menor não muda nada.
			ThumbnailSizePolicy.ShouldPropagateResize(
				MinimumSize,
				new Size(10, 10),
				MinimumSize,
				MaximumSize,
				isUserResize: true)
				.Should().BeFalse();
		}

		[Fact]
		public void Test_CalculateAspectRatioSize_WidthChange_KeepsRatio()
		{
			var size = ThumbnailSizePolicy.CalculateAspectRatioSize(
				new Size(52, 39), ThumbnailSizePolicy.Dimension.Width, 80,
				MinimumSize, MaximumSize, keepAspectRatio: true);

			size.Should().Be(new Size(80, 60));
		}

		[Fact]
		public void Test_CalculateAspectRatioSize_HeightChange_KeepsRatio()
		{
			var size = ThumbnailSizePolicy.CalculateAspectRatioSize(
				new Size(52, 39), ThumbnailSizePolicy.Dimension.Height, 90,
				MinimumSize, MaximumSize, keepAspectRatio: true);

			size.Should().Be(new Size(120, 90));
		}

		[Fact]
		public void Test_CalculateAspectRatioSize_WidthAboveMaximum_RecomputesHeight()
		{
			var size = ThumbnailSizePolicy.CalculateAspectRatioSize(
				new Size(52, 39), ThumbnailSizePolicy.Dimension.Width, 5000,
				MinimumSize, MaximumSize, keepAspectRatio: true);

			size.Should().Be(new Size(720, 540));
		}

		[Fact]
		public void Test_CalculateAspectRatioSize_HeightBelowMinimum_RecomputesWidth()
		{
			var size = ThumbnailSizePolicy.CalculateAspectRatioSize(
				new Size(52, 39), ThumbnailSizePolicy.Dimension.Height, 10,
				MinimumSize, MaximumSize, keepAspectRatio: true);

			size.Should().Be(new Size(32, 24));
		}

		[Fact]
		public void Test_CalculateAspectRatioSize_WithoutLock_ChangesOnlyOneAxis()
		{
			var size = ThumbnailSizePolicy.CalculateAspectRatioSize(
				new Size(52, 39), ThumbnailSizePolicy.Dimension.Width, 80,
				MinimumSize, MaximumSize, keepAspectRatio: false);

			size.Should().Be(new Size(80, 39));
		}

		[Fact]
		public void Test_CalculateAspectRatioSize_RatioStaysWithinOnePixel()
		{
			double original = 52.0 / 39.0;

			foreach (int width in new[] { 53, 77, 101, 333 })
			{
				var size = ThumbnailSizePolicy.CalculateAspectRatioSize(
					new Size(52, 39), ThumbnailSizePolicy.Dimension.Width, width,
					MinimumSize, MaximumSize, keepAspectRatio: true);

				double achieved = (double)size.Width / size.Height;
				Math.Abs(achieved - original).Should().BeLessThan(0.05);
			}
		}

		[Fact]
		public void Test_ShouldPropagateResize_ProposedAboveMaximumAfterClamp_ReturnsTrueWhenCurrentDiffers()
		{
			ThumbnailSizePolicy.ShouldPropagateResize(
				new Size(384, 216),
				new Size(5000, 5000),
				MinimumSize,
				MaximumSize,
				isUserResize: true)
				.Should().BeTrue();
		}
	}
}
