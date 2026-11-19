using System;
using System.Drawing;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interop;
using Xunit;

namespace EveO.Tests.Integration;

/// <summary>
/// Integration tests for StaticThumbnailGdi (GetDC/BitBlt/PrintWindow) with real windows.
/// These tests must run on Windows (Linux cannot exercise real GDI capture).
/// </summary>
[Collection("WindowsIntegration")]
public sealed class StaticThumbnailGdiIntegrationTests
{
    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public void GetStaticThumbnail_RealWindow_ReturnsValidBitmap()
    {
        Assert.True(OperatingSystem.IsWindows(), "This test requires Windows.");

        using var window = new Form
        {
            ClientSize = new Size(640, 480),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(100, 100),
            Text = "GDI Thumbnail Test Window",
            ShowInTaskbar = false
        };
        window.Show();
        Application.DoEvents();

        Assert.True(window.IsHandleCreated, "Window must have a native HWND.");

        var manager = new WindowManager(new ThumbnailConfiguration());
        var thumbnail = manager.GetStaticThumbnail(window.Handle);

        Assert.NotNull(thumbnail);

        var bitmap = (Bitmap)thumbnail;
        Assert.Equal(window.ClientSize.Width, bitmap.Width);
        Assert.Equal(window.ClientSize.Height, bitmap.Height);

        // Verify a pixel is not the default empty color
        Color pixel = bitmap.GetPixel(320, 240);
        Assert.NotEqual(Color.Empty.ToArgb(), pixel.ToArgb());
    }

    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public void GetStaticThumbnail_BlankSurface_ReturnsImage()
    {
        Assert.True(OperatingSystem.IsWindows(), "This test requires Windows.");

        using var window = new Form
        {
            ClientSize = new Size(640, 480),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(100, 100),
            Text = "Blank Surface Test",
            ShowInTaskbar = false
        };
        window.Show();
        Application.DoEvents();

        var manager = new WindowManager(new ThumbnailConfiguration());
        var thumbnail = manager.GetStaticThumbnail(window.Handle);

        // Should return a non-null image (even if blank, it's still a valid capture)
        Assert.NotNull(thumbnail);
    }

    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public void GetStaticThumbnail_InvalidHandle_ReturnsNull()
    {
        var manager = new WindowManager(new ThumbnailConfiguration());
        var thumbnail = manager.GetStaticThumbnail(IntPtr.Zero);
        Assert.Null(thumbnail);
    }
}