using System;
using System.Drawing;

namespace EveOPreview.Services
{
	internal enum SpotlightResizeEdge
	{
		None,
		Left,
		Right,
		Top,
		Bottom,
		TopLeft,
		TopRight,
		BottomLeft,
		BottomRight
	}

	internal static class SpotlightResizeCalculator
	{
		public static Rectangle Calculate(
			Rectangle startBounds,
			SpotlightResizeEdge edge,
			int deltaX,
			int deltaY,
			double aspectRatio,
			bool freeResize,
			Size minimumSize)
		{
			int left = startBounds.Left;
			int top = startBounds.Top;
			int right = startBounds.Right;
			int bottom = startBounds.Bottom;

			if (edge == SpotlightResizeEdge.Left || edge == SpotlightResizeEdge.TopLeft || edge == SpotlightResizeEdge.BottomLeft)
			{
				left += deltaX;
			}

			if (edge == SpotlightResizeEdge.Right || edge == SpotlightResizeEdge.TopRight || edge == SpotlightResizeEdge.BottomRight)
			{
				right += deltaX;
			}

			if (edge == SpotlightResizeEdge.Top || edge == SpotlightResizeEdge.TopLeft || edge == SpotlightResizeEdge.TopRight)
			{
				top += deltaY;
			}

			if (edge == SpotlightResizeEdge.Bottom || edge == SpotlightResizeEdge.BottomLeft || edge == SpotlightResizeEdge.BottomRight)
			{
				bottom += deltaY;
			}

			bool preserveAspect = !freeResize && aspectRatio > 0 && edge != SpotlightResizeEdge.None;
			if (!preserveAspect)
			{
				ClampFreeResize(ref left, ref top, ref right, ref bottom, startBounds, minimumSize);
				return Rectangle.FromLTRB(left, top, right, bottom);
			}

			bool horizontalEdge = edge == SpotlightResizeEdge.Left || edge == SpotlightResizeEdge.Right;
			bool verticalEdge = edge == SpotlightResizeEdge.Top || edge == SpotlightResizeEdge.Bottom;
			bool widthDriven = horizontalEdge || (!verticalEdge && Math.Abs(deltaX) >= Math.Abs(deltaY));

			int width = right - left;
			int height = bottom - top;

			if (widthDriven)
			{
				height = (int)Math.Round(width / aspectRatio);
			}
			else
			{
				width = (int)Math.Round(height * aspectRatio);
			}

			if (width < minimumSize.Width)
			{
				width = minimumSize.Width;
				height = (int)Math.Round(width / aspectRatio);
			}

			if (height < minimumSize.Height)
			{
				height = minimumSize.Height;
				width = (int)Math.Round(height * aspectRatio);
			}

			bool anchorRight = edge == SpotlightResizeEdge.Left || edge == SpotlightResizeEdge.TopLeft || edge == SpotlightResizeEdge.BottomLeft;
			bool anchorBottom = edge == SpotlightResizeEdge.Top || edge == SpotlightResizeEdge.TopLeft || edge == SpotlightResizeEdge.TopRight;

			if (horizontalEdge)
			{
				anchorBottom = false;
			}
			else if (verticalEdge)
			{
				anchorRight = false;
			}

			if (anchorRight)
			{
				left = right - width;
			}
			else
			{
				right = left + width;
			}

			if (anchorBottom)
			{
				top = bottom - height;
			}
			else
			{
				bottom = top + height;
			}

			return Rectangle.FromLTRB(left, top, right, bottom);
		}

		private static void ClampFreeResize(
			ref int left,
			ref int top,
			ref int right,
			ref int bottom,
			Rectangle startBounds,
			Size minimumSize)
		{
			if (right - left < minimumSize.Width)
			{
				if (left != startBounds.Left)
				{
					left = right - minimumSize.Width;
				}
				else
				{
					right = left + minimumSize.Width;
				}
			}

			if (bottom - top < minimumSize.Height)
			{
				if (top != startBounds.Top)
				{
					top = bottom - minimumSize.Height;
				}
				else
				{
					bottom = top + minimumSize.Height;
				}
			}
		}
	}
}
