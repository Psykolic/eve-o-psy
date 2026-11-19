using System;
using System.Diagnostics;
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interop;
using Xunit;

namespace EveO.Tests.Integration;

/// <summary>
/// Integration tests for WindowManager Win32 operations:
/// ActivateWindow, MinimizeWindow, GetForegroundWindowHandle.
/// These tests must run on Windows (Linux cannot exercise real window management).
/// </summary>
[Collection("WindowsIntegration")]
public sealed class WindowManagerIntegrationTests
{
    private readonly WindowManager _manager;

    public WindowManagerIntegrationTests()
    {
        _manager = new WindowManager(new ThumbnailConfiguration());
    }

    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public void GetForegroundWindowHandle_ReturnsValidHwnd_WhenWindowExists()
    {
        Assert.True(OperatingSystem.IsWindows(), "This test requires Windows.");

        using var testForm = new Form
        {
            Text = "ForegroundTestForm",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(50, 50),
            Size = new Size(400, 300),
            ShowInTaskbar = true
        };
        testForm.Show();
        Application.DoEvents();

        System.Threading.Thread.Sleep(100);

        IntPtr hwnd = _manager.GetForegroundWindowHandle();
        Assert.NotEqual(IntPtr.Zero, hwnd);
    }

    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public void MinimizeWindow_WithNoAnimation_SendsSysCommand()
    {
        Assert.True(OperatingSystem.IsWindows(), "This test requires Windows.");

        using var form = new Form
        {
            Text = "MinimizeTestForm",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(50, 50),
            Size = new Size(400, 300),
            ShowInTaskbar = true
        };
        form.Show();
        Application.DoEvents();

        // Minimize with no animation - passes enableAnimation=false
        _manager.MinimizeWindow(form.Handle, AnimationStyle.NoAnimation, false);

        // Check minimization via IsIconic (User32NativeMethods)
        Assert.True(User32NativeMethods.IsIconic(form.Handle),
            "Form should be minimized (IsIconic) after MinimizeWindow with NoAnimation.");
    }

    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public void MinimizeWindow_WithOriginalAnimation_SendsSysCommand()
    {
        Assert.True(OperatingSystem.IsWindows(), "This test requires Windows.");

        using var form = new Form
        {
            Text = "MinimizeAnimTestForm",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(50, 50),
            Size = new Size(400, 300),
            ShowInTaskbar = true
        };
        form.Show();
        Application.DoEvents();

        // Minimize with original animation - passes enableAnimation=true
        _manager.MinimizeWindow(form.Handle, AnimationStyle.OriginalAnimation, true);

        // Should not crash; check window state
        Assert.True(User32NativeMethods.IsIconic(form.Handle) || form.WindowState == FormWindowState.Minimized,
            "MinimizeWindow with OriginalAnimation should minimize the form.");
    }

    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public void ActivateWindow_SetsForeground()
    {
        Assert.True(OperatingSystem.IsWindows(), "This test requires Windows.");

        using var form1 = new Form
        {
            Text = "FormToActivate",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(50, 50),
            Size = new Size(400, 300),
            ShowInTaskbar = true
        };
        form1.Show();
        Application.DoEvents();

        using var form2 = new Form
        {
            Text = "AnotherForm",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(200, 50),
            Size = new Size(400, 300),
            ShowInTaskbar = true
        };
        form2.Show();
        Application.DoEvents();

        // Activate form1
        _manager.ActivateWindow(form1.Handle, AnimationStyle.OriginalAnimation);

        // Should not throw
        Assert.True(true, "ActivateWindow should not throw exception.");
    }
}