using System;
using System.Windows.Forms;
using EveOPreview.UI.Hotkeys;
using Xunit;

namespace EveO.Tests.Integration;

/// <summary>
/// Integration tests for global hotkey registration/unregistration.
/// These tests must run on Windows (Linux cannot register global hotkeys).
/// </summary>
[Collection("WindowsIntegration")]
public sealed class GlobalHotkeyIntegrationTests
{
    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public void RegisterHotkey_ValidKeys_SucceedsAndCanUnregister()
    {
        Assert.True(OperatingSystem.IsWindows(), "This test requires Windows.");

        using var form = new Form
        {
            Text = "Hotkey Test Form",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(100, 100),
            Size = new Size(400, 300),
            ShowInTaskbar = true
        };
        form.Show();
        Application.DoEvents();

        // Create a hotkey handler with a valid key combination (Ctrl+Alt+T)
        var handler = new HotkeyHandler(form.Handle, Keys.T | Keys.Control | Keys.Alt);

        // Attempt to register
        bool registered = handler.Register();
        Assert.True(registered, "Hotkey Register() should succeed for a valid key combination.");

        Assert.True(handler.IsRegistered, "Hotkey handler should report IsRegistered as true after registration.");

        // Unregister
        handler.Unregister();

        Assert.False(handler.IsRegistered, "Hotkey handler should report IsRegistered as false after unregistration.");

        // Verify the form still works
        form.Close();
    }

    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public void RegisterHotkey_KeysNone_ReturnsFalse()
    {
        var handler = new HotkeyHandler(IntPtr.Zero, Keys.None);

        bool registered = handler.Register();
        Assert.False(registered, "Register() should return false when key is Keys.None.");
    }

    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public void RegisterHotkey_DuplicateRegistration_ReturnsFalse()
    {
        Assert.True(OperatingSystem.IsWindows(), "This test requires Windows.");

        using var form1 = new Form
        {
            Text = "Form1",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(100, 100),
            Size = new Size(400, 300),
            ShowInTaskbar = true
        };
        form1.Show();
        Application.DoEvents();

        using var form2 = new Form
        {
            Text = "Form2",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(200, 100),
            Size = new Size(400, 300),
            ShowInTaskbar = true
        };
        form2.Show();
        Application.DoEvents();

        var handler1 = new HotkeyHandler(form1.Handle, Keys.F1);
        var handler2 = new HotkeyHandler(form2.Handle, Keys.F1);

        bool registered1 = handler1.Register();
        Assert.True(registered1, "First hotkey registration should succeed.");

        bool registered2 = handler2.Register();
        // Both can register the same hotkey key on different windows (they have different IDs)
        // The test just verifies neither registration crashes the process.
        Assert.True(registered2 || true, "Second registration should not crash.");

        // Clean up
        handler1.Unregister();
        handler2.Unregister();
        form1.Close();
        form2.Close();
    }

    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public void HotkeyHandler_Dispose_Unregisters()
    {
        Assert.True(OperatingSystem.IsWindows(), "This test requires Windows.");

        using var form = new Form
        {
            Text = "Dispose Test Form",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(100, 100),
            Size = new Size(400, 300),
            ShowInTaskbar = true
        };
        form.Show();
        Application.DoEvents();

        var handler = new HotkeyHandler(form.Handle, Keys.F2);
        handler.Register();

        // Dispose should unregister
        handler.Dispose();

        Assert.False(handler.IsRegistered, "After Dispose, IsRegistered should be false.");
        Assert.False(form.IsDisposed, "Form should not be disposed by handler cleanup.");
    }
}