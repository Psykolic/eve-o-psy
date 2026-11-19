using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Services.Implementation;
using Xunit;
using Xunit.Abstractions;

namespace EveO.Tests.Windows;

/// <summary>
/// Exercises the real WindowManager.GetStaticThumbnail Win32/GDI code path against
/// a controlled WinForms window. No EVE client or Wine compatibility flag required.
/// </summary>
/// <remarks>
/// Runs in the same xUnit collection as the Windows integration tests (#124). Both create
/// real windows and this test measures process-wide GetGuiResources, so running them in
/// parallel makes one test's USER handles look like a leak in the other.
/// </remarks>
[Collection("WindowsIntegration")]
public sealed class GdiStressTests
{
    private const int CaptureCount = 1000;
    private const int WarmupCount = 30;
    private const int MaximumGdiGrowth = 16;
    private const int MaximumUserGrowth = 8;
    private readonly ITestOutputHelper _output;

    public GdiStressTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "WindowsGdiStress")]
    public void GetStaticThumbnail_ThousandCaptures_RefreshesPixelsWithoutPersistentHandleGrowth()
    {
        Assert.True(OperatingSystem.IsWindows(), "This test requires a Windows Win32 desktop.");

        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                RunStress();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        })
        {
            IsBackground = true,
            Name = "GDI stress STA"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromMinutes(3)), "GDI stress test exceeded 3 minutes.");
        if (exception != null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private void RunStress()
    {
        using var process = Process.GetCurrentProcess();
        using var window = new Form
        {
            ClientSize = new Size(640, 480),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(20, 20),
            ShowInTaskbar = false,
            Text = "EVE-O GDI stress fixture"
        };
        window.Show();
        Application.DoEvents();

        Assert.True(window.IsHandleCreated, "The test window must have a native HWND.");
        var manager = new WindowManager(new ThumbnailConfiguration());

        // Warm-up excludes one-time WinForms/font/GDI allocations from the leak delta.
        for (int i = 0; i < WarmupCount; i++)
        {
            PaintAndCapture(window, manager, i, verifyPixels: false);
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        uint startingGdi = ReadGuiResources(process, 0);
        uint startingUser = ReadGuiResources(process, 1);
        Assert.True(startingGdi > 0, "GetGuiResources(GDI) returned zero; no meaningful baseline.");
        Assert.True(startingUser > 0, "GetGuiResources(USER) returned zero; no meaningful baseline.");

        string? resultPath = Environment.GetEnvironmentVariable("EVE_O_GDI_METRICS_PATH");
        StreamWriter? metrics = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(resultPath))
            {
                string fullPath = Path.GetFullPath(resultPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                metrics = new StreamWriter(fullPath, append: false);
                metrics.WriteLine("iteration,gdi,user,gdi_delta,user_delta");
                metrics.WriteLine($"0,{startingGdi},{startingUser},0,0");
                metrics.Flush();
            }

            _output.WriteLine($"GDI baseline: {startingGdi}, USER baseline: {startingUser}");
            int? previousColor = null;

            for (int iteration = 1; iteration <= CaptureCount; iteration++)
            {
                int capturedColor = PaintAndCapture(window, manager, iteration, verifyPixels: true);
                if (previousColor.HasValue && previousColor.Value == capturedColor)
                {
                    Assert.Fail($"Snapshot did not update between captures {iteration - 1} and {iteration}.");
                }
                previousColor = capturedColor;

                if (iteration == 100 || iteration == 500 || iteration == CaptureCount)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    uint gdi = ReadGuiResources(process, 0);
                    uint user = ReadGuiResources(process, 1);
                    long gdiDelta = (long)gdi - startingGdi;
                    long userDelta = (long)user - startingUser;

                    _output.WriteLine($"Captures={iteration}; GDI={gdi} (delta {gdiDelta:+#;-#;0}), USER={user} (delta {userDelta:+#;-#;0})");
                    metrics?.WriteLine($"{iteration},{gdi},{user},{gdiDelta},{userDelta}");
                    metrics?.Flush();

                    Assert.True(gdiDelta <= MaximumGdiGrowth,
                        $"Possible GDI handle leak: +{gdiDelta} after {iteration} captures (allowed +{MaximumGdiGrowth}).");
                    Assert.True(userDelta <= MaximumUserGrowth,
                        $"Possible USER handle leak: +{userDelta} after {iteration} captures (allowed +{MaximumUserGrowth}).");
                }
            }
        }
        finally
        {
            metrics?.Dispose();
            window.Close();
        }
    }

    private static int PaintAndCapture(Form window, WindowManager manager, int iteration, bool verifyPixels)
    {
        Color expected = (iteration & 1) == 0 ? Color.FromArgb(20, 120, 220) : Color.FromArgb(220, 70, 30);

        // Win32/GDI drawing is intentional: BitBlt from a DWM/GPU game surface
        // may be unsupported even when native resource ownership is correct.
        using (Graphics graphics = window.CreateGraphics())
        {
            graphics.Clear(expected);
        }

        using Image? image = manager.GetStaticThumbnail(window.Handle);
        Assert.NotNull(image);
        Assert.Equal(window.ClientSize.Width, image.Width);
        Assert.Equal(window.ClientSize.Height, image.Height);
        Assert.IsType<Bitmap>(image);

        Color actual = ((Bitmap)image).GetPixel(320, 240);
        if (verifyPixels)
        {
            Assert.Equal(expected.ToArgb(), actual.ToArgb());
        }
        return actual.ToArgb();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetGuiResources(IntPtr processHandle, uint flags);

    private static uint ReadGuiResources(Process process, uint flags)
    {
        uint value = GetGuiResources(process.Handle, flags);
        if (value == 0)
        {
            throw new InvalidOperationException(
                $"GetGuiResources(flags={flags}) returned zero (Win32 error {Marshal.GetLastWin32Error()}).");
        }
        return value;
    }
}
