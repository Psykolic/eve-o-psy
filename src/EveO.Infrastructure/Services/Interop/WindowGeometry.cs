using System;
using System.Drawing;

namespace EveOPreview.Services.Interop
{
	internal static class WindowGeometry
	{
		public static Size GetClientSize(IntPtr handle)
		{
			if (!User32NativeMethods.GetClientRect(handle, out RECT rectangle))
			{
				return Size.Empty;
			}

			return new Size(rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
		}
	}
}
