using EveOPreview.Services;

namespace EveO.Tests.Services;

/// <summary>
/// Rastreamento de operações assíncronas (issue #55): o dreno do shutdown precisa
/// aguardar tanto o trabalho já registrado quanto o que ainda for registrado enquanto
/// o próprio dreno espera.
/// </summary>
public sealed class AsyncOperationTrackerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task DrainAsync_WithoutOperations_Completes()
    {
        var tracker = new AsyncOperationTracker();

        await tracker.DrainAsync(CancellationToken.None).WaitAsync(Timeout);

        Assert.Equal(0, tracker.PendingCount);
    }

    [Fact]
    public async Task DrainAsync_WaitsForOperationRegisteredWhileDraining()
    {
        var tracker = new AsyncOperationTracker();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        tracker.Run(async () =>
        {
            firstEntered.TrySetResult();
            await releaseFirst.Task;
        });

        await firstEntered.Task.WaitAsync(Timeout);

        Task drain = tracker.DrainAsync(CancellationToken.None);

        // Registrada depois do início do dreno: ainda assim precisa ser aguardada.
        tracker.Run(async () =>
        {
            secondEntered.TrySetResult();
            await releaseSecond.Task;
        });

        await secondEntered.Task.WaitAsync(Timeout);

        releaseFirst.TrySetResult();
        await Task.Delay(50);

        Assert.False(drain.IsCompleted, "O dreno retornou com uma operação ainda em voo.");

        releaseSecond.TrySetResult();
        await drain.WaitAsync(Timeout);

        Assert.Equal(0, tracker.PendingCount);
    }

    [Fact]
    public async Task Run_OperationThatThrows_DoesNotEscapeOrLeakTracking()
    {
        var tracker = new AsyncOperationTracker();

        tracker.Run(() => throw new InvalidOperationException("boom"));

        await tracker.DrainAsync(CancellationToken.None).WaitAsync(Timeout);

        Assert.Equal(0, tracker.PendingCount);
    }
}
