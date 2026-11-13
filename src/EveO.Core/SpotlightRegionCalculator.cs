using System;
using System.Drawing;

namespace EveOPreview.Services
{
	public static class SpotlightRegionCalculator
	{
		/// <summary>
		/// Maps a rectangle selected in a preview window to the corresponding
		/// rectangle of the source client that is currently displayed.
		/// </summary>
		public static Rectangle MapSelection(Rectangle selection, Rectangle displayedSource, Size destinationSize)
		{
			if (destinationSize.Width <= 0 || destinationSize.Height <= 0)
			{
				return Rectangle.Empty;
			}

			if (displayedSource.Width <= 0 || displayedSource.Height <= 0)
			{
				return Rectangle.Empty;
			}

			double scaleX = (double)displayedSource.Width / destinationSize.Width;
			double scaleY = (double)displayedSource.Height / destinationSize.Height;

			Rectangle mapped = Rectangle.FromLTRB(
				displayedSource.Left + (int)Math.Round(selection.Left * scaleX),
				displayedSource.Top + (int)Math.Round(selection.Top * scaleY),
				displayedSource.Left + (int)Math.Round(selection.Right * scaleX),
				displayedSource.Top + (int)Math.Round(selection.Bottom * scaleY));

			mapped.Intersect(displayedSource);

			return mapped.Width < 4 || mapped.Height < 4 ? Rectangle.Empty : mapped;
		}

		/// <summary>
		/// Ensures a stored region is inside the source client area.
		/// </summary>
		public static Rectangle ClampToClient(Rectangle region, Size clientSize)
		{
			if (region.Width <= 0 || region.Height <= 0 || clientSize.Width <= 0 || clientSize.Height <= 0)
			{
				return Rectangle.Empty;
			}

			region.Intersect(new Rectangle(Point.Empty, clientSize));

			return region.Width < 4 || region.Height < 4 ? Rectangle.Empty : region;
		}

		/// <summary>
		/// Finds a preview size with the same aspect ratio as the source region.
		/// </summary>
		public static Size FitAspect(Size currentSize, Rectangle sourceRegion, Size minimumSize, Size maximumSize)
		{
			if (sourceRegion.Width <= 0 || sourceRegion.Height <= 0)
			{
				return currentSize;
			}

			double aspect = (double)sourceRegion.Width / sourceRegion.Height;
			int width = Math.Max(currentSize.Width, minimumSize.Width);
			int height = (int)Math.Round(width / aspect);

			if (height < minimumSize.Height)
			{
				height = minimumSize.Height;
				width = (int)Math.Round(height * aspect);
			}

			if (width > maximumSize.Width)
			{
				width = maximumSize.Width;
				height = (int)Math.Round(width / aspect);
			}

			if (height > maximumSize.Height)
			{
				height = maximumSize.Height;
				width = (int)Math.Round(height * aspect);
			}

			return new Size(Math.Max(width, minimumSize.Width), Math.Max(height, minimumSize.Height));
		}
	}
}
