using System.Drawing;
using System.Runtime.InteropServices;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interop;

namespace EveO.Tests.Windows;

public sealed class GdiFailureTests
{
    private const string AcquireBitmap = "GetDC GetClientRect CreateCompatibleDC CreateCompatibleBitmap";
    private const string CopyBitmap = AcquireBitmap + " SelectBitmap BitBlt";
    private const string DeleteBitmap = " DeleteDC DeleteObject ReleaseDC";

    [Theory]
    [InlineData(Failure.GetDC, "GetDC")]
    [InlineData(Failure.GetClientRect, "GetDC GetClientRect ReleaseDC")]
    [InlineData(Failure.CreateCompatibleDC, "GetDC GetClientRect CreateCompatibleDC ReleaseDC")]
    [InlineData(Failure.CreateCompatibleBitmap, AcquireBitmap + " DeleteDC ReleaseDC")]
    [InlineData(Failure.SelectZero, AcquireBitmap + " SelectBitmap" + DeleteBitmap)]
    [InlineData(Failure.SelectMinusOne, AcquireBitmap + " SelectBitmap" + DeleteBitmap)]
    [InlineData(Failure.BitBlt, CopyBitmap + " RestoreBitmap" + DeleteBitmap)]
    [InlineData(Failure.RestoreZero, CopyBitmap + " RestoreBitmap RestoreBitmap" + DeleteBitmap)]
    [InlineData(Failure.RestoreMinusOne, CopyBitmap + " RestoreBitmap RestoreBitmap" + DeleteBitmap)]
    [InlineData(Failure.RestoreOnce, CopyBitmap + " RestoreBitmap RestoreBitmap" + DeleteBitmap)]
    [InlineData(Failure.RestoreAndDeleteDC, CopyBitmap + " RestoreBitmap RestoreBitmap DeleteDC ReportFailure:DeleteDC ReportFailure:DeleteObjectSkipped ReleaseDC")]
    [InlineData(Failure.RestoreThrows, CopyBitmap + " RestoreBitmap RestoreBitmap" + DeleteBitmap)]
    [InlineData(Failure.FromHbitmap, CopyBitmap + " RestoreBitmap FromHbitmap" + DeleteBitmap)]
    public void CaptureFailure_CleansUpOnlyOwnedResourcesInOrder(Failure failure, string expectedCalls)
    {
        var gdi = new FaultingGdi(failure);

        if (failure is Failure.FromHbitmap or Failure.RestoreThrows)
        {
            Assert.Same(gdi.Error, Assert.Throws<ExternalException>(
                () => WindowManager.GetStaticThumbnail(FaultingGdi.Window, gdi)));
        }
        else
        {
            Assert.Null(WindowManager.GetStaticThumbnail(FaultingGdi.Window, gdi));
        }

        Assert.Equal(expectedCalls.Split(' '), gdi.Calls);
    }

    [Theory]
    [InlineData(Failure.DeleteDC, " DeleteDC ReportFailure:DeleteDC DeleteObject ReleaseDC")]
    [InlineData(Failure.DeleteObject, " DeleteDC DeleteObject ReportFailure:DeleteObject ReleaseDC")]
    [InlineData(Failure.ReleaseDC, " DeleteDC DeleteObject ReleaseDC ReportFailure:ReleaseDC")]
    public void CleanupFailure_IsReported_WithoutInterruptingOtherCleanup(Failure failure, string expectedCleanup)
    {
        var gdi = new FaultingGdi(failure);

        using Image? image = WindowManager.GetStaticThumbnail(FaultingGdi.Window, gdi);

        Assert.NotNull(image);
        Assert.Equal((CopyBitmap + " RestoreBitmap FromHbitmap" + expectedCleanup).Split(' '), gdi.Calls);
    }

    [Theory]
    [InlineData(Failure.DeleteDCThrows)]
    [InlineData(Failure.DeleteObjectThrows)]
    [InlineData(Failure.ReleaseDCThrows)]
    public void CleanupException_AttemptsRemainingCleanup(Failure failure)
    {
        var gdi = new FaultingGdi(failure);
        try
        {
            Assert.Same(gdi.Error, Assert.Throws<ExternalException>(
                () => WindowManager.GetStaticThumbnail(FaultingGdi.Window, gdi)));
            Assert.Equal((CopyBitmap + " RestoreBitmap FromHbitmap" + DeleteBitmap).Split(' '), gdi.Calls);
        }
        finally
        {
            gdi.Image?.Dispose();
        }
    }

    [Fact]
    public void AllCleanupFailures_AreReported()
    {
        var gdi = new FaultingGdi(Failure.AllCleanup);
        using var image = WindowManager.GetStaticThumbnail(FaultingGdi.Window, gdi);
        Assert.NotNull(image);
        Assert.Equal((CopyBitmap + " RestoreBitmap FromHbitmap DeleteDC ReportFailure:DeleteDC DeleteObject ReportFailure:DeleteObject ReleaseDC ReportFailure:ReleaseDC").Split(' '), gdi.Calls);
    }

    public enum Failure
    {
        GetDC, GetClientRect, CreateCompatibleDC, CreateCompatibleBitmap, SelectZero, SelectMinusOne,
        BitBlt, RestoreZero, RestoreMinusOne, RestoreOnce, RestoreAndDeleteDC, RestoreThrows, FromHbitmap,
        DeleteDC, DeleteObject, ReleaseDC, DeleteDCThrows, DeleteObjectThrows, ReleaseDCThrows, AllCleanup
    }

    private sealed class FaultingGdi(Failure failure) : IStaticThumbnailGdi
    {
        internal static readonly IntPtr Window = new(1);
        private static readonly IntPtr Source = new(2);
        private static readonly IntPtr Destination = new(3);
        private static readonly IntPtr Bitmap = new(4);
        private static readonly IntPtr OriginalBitmap = new(5);

        internal List<string> Calls { get; } = new();
        internal ExternalException Error { get; } = new("Injected GDI failure.");
        internal Image? Image { get; private set; }
        private bool _restoreAttempted;
        private bool _bitmapSelected;

        public IntPtr GetDC(IntPtr window)
        {
            Calls.Add(nameof(GetDC));
            Assert.Equal(Window, window);
            return failure == Failure.GetDC ? IntPtr.Zero : Source;
        }

        public bool GetClientRect(IntPtr window, out RECT rect)
        {
            Calls.Add(nameof(GetClientRect));
            Assert.Equal(Window, window);
            rect = new RECT(0, 0, 640, 480);
            return failure != Failure.GetClientRect;
        }

        public IntPtr CreateCompatibleDC(IntPtr source)
        {
            Calls.Add(nameof(CreateCompatibleDC));
            Assert.Equal(Source, source);
            return failure == Failure.CreateCompatibleDC ? IntPtr.Zero : Destination;
        }

        public IntPtr CreateCompatibleBitmap(IntPtr source, int width, int height)
        {
            Calls.Add(nameof(CreateCompatibleBitmap));
            Assert.Equal((Source, 640, 480), (source, width, height));
            return failure == Failure.CreateCompatibleBitmap ? IntPtr.Zero : Bitmap;
        }

        public IntPtr SelectObject(IntPtr context, IntPtr bitmap)
        {
            Assert.Equal(Destination, context);
            if (bitmap == Bitmap)
            {
                Calls.Add("SelectBitmap");
                if (failure == Failure.SelectZero) return IntPtr.Zero;
                if (failure == Failure.SelectMinusOne) return new IntPtr(-1);

                _bitmapSelected = true;
                return OriginalBitmap;
            }

            Calls.Add("RestoreBitmap");
            Assert.Equal(OriginalBitmap, bitmap);
            Assert.True(_bitmapSelected);
            bool firstAttempt = !_restoreAttempted;
            _restoreAttempted = true;

            if (failure == Failure.RestoreThrows)
                throw Error;
            if (failure == Failure.RestoreMinusOne)
                return new IntPtr(-1);
            if (failure is Failure.RestoreZero or Failure.RestoreAndDeleteDC ||
                (failure == Failure.RestoreOnce && firstAttempt))
                return IntPtr.Zero;

            _bitmapSelected = false;
            return Bitmap;
        }

        public bool BitBlt(IntPtr destination, int width, int height, IntPtr source)
        {
            Calls.Add(nameof(BitBlt));
            Assert.Equal((Destination, 640, 480, Source), (destination, width, height, source));
            Assert.True(_bitmapSelected);
            return failure != Failure.BitBlt;
        }

        public Image FromHbitmap(IntPtr bitmap)
        {
            Calls.Add(nameof(FromHbitmap));
            Assert.Equal(Bitmap, bitmap);
            Assert.False(_bitmapSelected);
            if (failure == Failure.FromHbitmap)
                throw Error;

            return Image = new Bitmap(1, 1);
        }

        public bool DeleteDC(IntPtr context)
        {
            Calls.Add(nameof(DeleteDC));
            Assert.Equal(Destination, context);
            if (failure == Failure.DeleteDCThrows) throw Error;
            if (failure is Failure.RestoreAndDeleteDC or Failure.DeleteDC or Failure.AllCleanup)
                return false;

            _bitmapSelected = false;
            return true;
        }

        public bool DeleteObject(IntPtr bitmap)
        {
            Calls.Add(nameof(DeleteObject));
            Assert.Equal(Bitmap, bitmap);
            Assert.False(_bitmapSelected);
            if (failure == Failure.DeleteObjectThrows) throw Error;
            return failure is not (Failure.DeleteObject or Failure.AllCleanup);
        }

        public bool ReleaseDC(IntPtr window, IntPtr context)
        {
            Calls.Add(nameof(ReleaseDC));
            Assert.Equal((Window, Source), (window, context));
            if (failure == Failure.ReleaseDCThrows) throw Error;
            return failure is not (Failure.ReleaseDC or Failure.AllCleanup);
        }

        public void ReportCleanupFailure(string operation, IntPtr handle)
        {
            Calls.Add("ReportFailure:" + operation);
            Assert.NotEqual(IntPtr.Zero, handle);
        }
    }
}
