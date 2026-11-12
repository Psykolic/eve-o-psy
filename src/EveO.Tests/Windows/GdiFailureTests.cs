using System.Drawing;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interop;

namespace EveO.Tests.Windows;

public sealed class GdiFailureTests
{
    private static readonly IntPtr Window = new(1);
    private static readonly IntPtr SourceDc = new(2);
    private static readonly IntPtr DestinationDc = new(3);
    private static readonly IntPtr BitmapHandle = new(4);
    private static readonly IntPtr OriginalBitmap = new(5);

    [Theory]
    [InlineData("GetDC", "GetDC")]
    [InlineData("GetClientRect", "GetDC GetClientRect ReleaseDC")]
    [InlineData("CreateCompatibleDC", "GetDC GetClientRect CreateCompatibleDC ReleaseDC")]
    [InlineData("CreateCompatibleBitmap", "GetDC GetClientRect CreateCompatibleDC CreateCompatibleBitmap DeleteDC ReleaseDC")]
    [InlineData("SelectZero", "GetDC GetClientRect CreateCompatibleDC CreateCompatibleBitmap Select DeleteDC DeleteObject ReleaseDC")]
    [InlineData("SelectInvalid", "GetDC GetClientRect CreateCompatibleDC CreateCompatibleBitmap Select DeleteDC DeleteObject ReleaseDC")]
    [InlineData("BitBlt", "GetDC GetClientRect CreateCompatibleDC CreateCompatibleBitmap Select BitBlt Restore DeleteDC DeleteObject ReleaseDC")]
    [InlineData("RestoreZero", "GetDC GetClientRect CreateCompatibleDC CreateCompatibleBitmap Select BitBlt Restore Restore DeleteDC DeleteObject ReleaseDC")]
    [InlineData("RestoreInvalid", "GetDC GetClientRect CreateCompatibleDC CreateCompatibleBitmap Select BitBlt Restore Restore DeleteDC DeleteObject ReleaseDC")]
    [InlineData("RestoreThrows", "GetDC GetClientRect CreateCompatibleDC CreateCompatibleBitmap Select BitBlt Restore Restore DeleteDC DeleteObject ReleaseDC")]
    [InlineData("FromHbitmap", "GetDC GetClientRect CreateCompatibleDC CreateCompatibleBitmap Select BitBlt Restore FromHbitmap DeleteDC DeleteObject ReleaseDC")]
    [InlineData("DeleteDCThrows", "GetDC GetClientRect CreateCompatibleDC CreateCompatibleBitmap Select BitBlt Restore FromHbitmap DeleteDC DeleteObject ReleaseDC")]
    [InlineData("DeleteObjectThrows", "GetDC GetClientRect CreateCompatibleDC CreateCompatibleBitmap Select BitBlt Restore FromHbitmap DeleteDC DeleteObject ReleaseDC")]
    [InlineData("ReleaseDCThrows", "GetDC GetClientRect CreateCompatibleDC CreateCompatibleBitmap Select BitBlt Restore FromHbitmap DeleteDC DeleteObject ReleaseDC")]
    [InlineData("CleanupReturnsFailure", "GetDC GetClientRect CreateCompatibleDC CreateCompatibleBitmap Select BitBlt Restore FromHbitmap DeleteDC DeleteObject ReleaseDC")]
    public void GetStaticThumbnail_AttemptsCleanupInOrder(string failure, string expectedCalls)
    {
        using var gdi = new FakeGdi(failure);

        if (failure.EndsWith("Throws", StringComparison.Ordinal) || failure == "FromHbitmap")
        {
            Assert.Same(gdi.Error, Assert.Throws<InvalidOperationException>(
                () => WindowManager.GetStaticThumbnail(Window, gdi)));

            // Assert disposal before FakeGdi.Dispose can clean up the image.
            if (failure is "DeleteDCThrows" or "DeleteObjectThrows" or "ReleaseDCThrows")
            {
                var abandoned = Assert.IsType<Bitmap>(gdi.Image);
                Assert.Throws<ArgumentException>(() => abandoned.GetPixel(0, 0));
            }
        }
        else
        {
            var image = WindowManager.GetStaticThumbnail(Window, gdi);
            if (failure == "CleanupReturnsFailure")
            {
                Assert.Same(gdi.Image, image);
            }
            else
            {
                Assert.Null(image);
            }
        }

        Assert.Equal(expectedCalls.Split(' '), gdi.Calls);
    }

    private sealed class FakeGdi(string failure) : IStaticThumbnailGdi, IDisposable
    {
        public readonly List<string> Calls = [];
        public readonly InvalidOperationException Error = new(failure);
        public Image? Image { get; private set; }

        public IntPtr GetDC(IntPtr window)
        {
            Assert.Equal(Window, window);
            Calls.Add("GetDC");
            return failure == "GetDC" ? IntPtr.Zero : SourceDc;
        }

        public bool GetClientRect(IntPtr window, out RECT rect)
        {
            Assert.Equal(Window, window);
            Calls.Add("GetClientRect");
            rect = new RECT(0, 0, 640, 480);
            return failure != "GetClientRect";
        }

        public IntPtr CreateCompatibleDC(IntPtr source)
        {
            Assert.Equal(SourceDc, source);
            Calls.Add("CreateCompatibleDC");
            return failure == "CreateCompatibleDC" ? IntPtr.Zero : DestinationDc;
        }

        public IntPtr CreateCompatibleBitmap(IntPtr source, int width, int height)
        {
            Assert.Equal(SourceDc, source);
            Assert.Equal((640, 480), (width, height));
            Calls.Add("CreateCompatibleBitmap");
            return failure == "CreateCompatibleBitmap" ? IntPtr.Zero : BitmapHandle;
        }

        public IntPtr SelectObject(IntPtr context, IntPtr bitmap)
        {
            Assert.Equal(DestinationDc, context);
            if (bitmap == BitmapHandle)
            {
                Calls.Add("Select");
                return failure switch
                {
                    "SelectZero" => IntPtr.Zero,
                    "SelectInvalid" => new IntPtr(-1),
                    _ => OriginalBitmap
                };
            }

            Assert.Equal(OriginalBitmap, bitmap);
            Calls.Add("Restore");
            return failure switch
            {
                "RestoreZero" => IntPtr.Zero,
                "RestoreInvalid" => new IntPtr(-1),
                "RestoreThrows" => throw Error,
                _ => BitmapHandle
            };
        }

        public bool BitBlt(IntPtr destination, int width, int height, IntPtr source)
        {
            Assert.Equal(DestinationDc, destination);
            Assert.Equal(SourceDc, source);
            Assert.Equal((640, 480), (width, height));
            Calls.Add("BitBlt");
            return failure != "BitBlt";
        }

        public Image FromHbitmap(IntPtr bitmap)
        {
            Assert.Equal(BitmapHandle, bitmap);
            Calls.Add("FromHbitmap");
            if (failure == "FromHbitmap")
            {
                throw Error;
            }

            return Image = new Bitmap(1, 1);
        }

        public bool DeleteDC(IntPtr context)
        {
            Assert.Equal(DestinationDc, context);
            return Cleanup("DeleteDC");
        }

        public bool DeleteObject(IntPtr bitmap)
        {
            Assert.Equal(BitmapHandle, bitmap);
            return Cleanup("DeleteObject");
        }

        public IntPtr ReleaseDC(IntPtr window, IntPtr context)
        {
            Assert.Equal(Window, window);
            Assert.Equal(SourceDc, context);
            return Cleanup("ReleaseDC") ? new IntPtr(1) : IntPtr.Zero;
        }

        private bool Cleanup(string operation)
        {
            Calls.Add(operation);
            if (failure == operation + "Throws")
            {
                throw Error;
            }

            return failure != "CleanupReturnsFailure";
        }

        public void Dispose() => Image?.Dispose();
    }
}
