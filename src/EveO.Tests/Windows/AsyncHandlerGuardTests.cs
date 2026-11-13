using System.Diagnostics;
using EveOPreview.Services;

namespace EveO.Tests.Windows;

/// <summary>
/// Guarda dos handlers async void (issue #53): exceção que escapa de um async void
/// derruba o processo, então todo handler passa por <see cref="ThumbnailManager.RunGuardedAsync"/>,
/// que registra e suprime.
/// </summary>
public sealed class AsyncHandlerGuardTests
{
    [Fact]
    public async Task RunGuardedAsync_ThrowingAction_DoesNotPropagate()
    {
        var operation = "guard-throws-" + Guid.NewGuid().ToString("N");

        await ThumbnailManager.RunGuardedAsync(operation, () => throw new InvalidOperationException("boom"));
    }

    [Fact]
    public async Task RunGuardedAsync_ThrowingAction_LogsOperationAndException()
    {
        var operation = "guard-logs-" + Guid.NewGuid().ToString("N");
        var listener = new CapturingListener(operation);

        Trace.Listeners.Add(listener);
        try
        {
            await ThumbnailManager.RunGuardedAsync(operation, () => throw new InvalidOperationException("boom-message"));
            Trace.Flush();
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        Assert.Contains(listener.Messages, m => m.Contains(operation, StringComparison.Ordinal));
        Assert.Contains(listener.Messages, m => m.Contains("boom-message", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunGuardedAsync_SuccessfulAction_RunsOnceAndDoesNotLog()
    {
        var operation = "guard-ok-" + Guid.NewGuid().ToString("N");
        var listener = new CapturingListener(operation);
        var runs = 0;

        Trace.Listeners.Add(listener);
        try
        {
            await ThumbnailManager.RunGuardedAsync(operation, () =>
            {
                runs++;
                return Task.CompletedTask;
            });
            Trace.Flush();
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        Assert.Equal(1, runs);
        Assert.Empty(listener.Messages);
    }

    private sealed class CapturingListener(string tag) : TraceListener
    {
        public List<string> Messages { get; } = [];

        public override void Write(string? message)
        {
            if (message != null && message.Contains(tag, StringComparison.Ordinal))
            {
                Messages.Add(message);
            }
        }

        public override void WriteLine(string? message) => this.Write(message);
    }
}
