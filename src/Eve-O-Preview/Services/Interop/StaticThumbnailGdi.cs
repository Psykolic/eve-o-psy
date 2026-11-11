using System;
using System.Drawing;
using System.Diagnostics;

namespace EveOPreview.Services.Interop
{
	internal interface IStaticThumbnailGdi
	{
		IntPtr GetDC(IntPtr window);
		bool GetClientRect(IntPtr window, out RECT rect);
		IntPtr CreateCompatibleDC(IntPtr source);
		IntPtr CreateCompatibleBitmap(IntPtr source, int width, int height);
		IntPtr SelectObject(IntPtr context, IntPtr bitmap);
		bool BitBlt(IntPtr destination, int width, int height, IntPtr source);
		Image FromHbitmap(IntPtr bitmap);
		bool DeleteDC(IntPtr context);
		bool DeleteObject(IntPtr bitmap);
		bool ReleaseDC(IntPtr window, IntPtr context);
		void ReportCleanupFailure(string operation, IntPtr handle);
	}

	internal sealed class StaticThumbnailGdi : IStaticThumbnailGdi
	{
		internal static readonly StaticThumbnailGdi Instance = new StaticThumbnailGdi();

		private StaticThumbnailGdi() { }

		public IntPtr GetDC(IntPtr window) => User32NativeMethods.GetDC(window);
		public bool GetClientRect(IntPtr window, out RECT rect) => User32NativeMethods.GetClientRect(window, out rect);
		public IntPtr CreateCompatibleDC(IntPtr source) => Gdi32NativeMethods.CreateCompatibleDC(source);
		public IntPtr CreateCompatibleBitmap(IntPtr source, int width, int height) => Gdi32NativeMethods.CreateCompatibleBitmap(source, width, height);
		public IntPtr SelectObject(IntPtr context, IntPtr bitmap) => Gdi32NativeMethods.SelectObject(context, bitmap);
		public bool BitBlt(IntPtr destination, int width, int height, IntPtr source) =>
			Gdi32NativeMethods.BitBlt(destination, 0, 0, width, height, source, 0, 0, Gdi32NativeMethods.SRCCOPY);
		public Image FromHbitmap(IntPtr bitmap) => Image.FromHbitmap(bitmap);
		public bool DeleteDC(IntPtr context) => Gdi32NativeMethods.DeleteDC(context);
		public bool DeleteObject(IntPtr bitmap) => Gdi32NativeMethods.DeleteObject(bitmap);
		public bool ReleaseDC(IntPtr window, IntPtr context) =>
			User32NativeMethods.ReleaseDC(window, context) != IntPtr.Zero;
		public void ReportCleanupFailure(string operation, IntPtr handle)
		{
			try
			{
				Trace.TraceWarning($"EVE-O GDI cleanup failure: {operation} (handle=0x{handle.ToInt64():X}).");
			}
			catch (Exception)
			{
				// Diagnostics must never interrupt native resource cleanup.
			}
		}
	}
}
