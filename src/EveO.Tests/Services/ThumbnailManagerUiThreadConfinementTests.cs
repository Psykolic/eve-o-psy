// Confinamento do dicionário de views à thread de UI (issue #59).
//
// A auditoria docs/audits/thumbnail-manager-async-lifecycle.md registrava o risco P1 de
// disputa entre o tick do DispatcherTimer e a continuação de ThumbnailActivated. O código
// já confinava os dois caminhos à thread de UI; estes testes travam essa premissa para que
// um futuro caminho concorrente falhe de forma explícita em vez de corromper o dicionário.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Refresh;
using EveOPreview.Configuration;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using EveOPreview.View;
using MediatR;

namespace EveO.Tests.Services;

public sealed class ThumbnailManagerUiThreadConfinementTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Process monitor determinístico: entrega a lista configurada na primeira chamada e
    /// listas vazias depois, evitando dependência do snapshot real de processos do Windows.
    /// </summary>
    private sealed class StubProcessMonitor : IProcessMonitor
    {
        private bool _consumed;

        public ICollection<IProcessInfo> Added { get; } = new List<IProcessInfo>();

        public IProcessInfo GetMainProcess() => null!;

        public void GetUpdatedProcesses(
            out ICollection<IProcessInfo> addedProcesses,
            out ICollection<IProcessInfo> updatedProcesses,
            out ICollection<IProcessInfo> removedProcesses)
        {
            addedProcesses = this._consumed ? new List<IProcessInfo>() : this.Added;
            updatedProcesses = new List<IProcessInfo>();
            removedProcesses = new List<IProcessInfo>();
            this._consumed = true;
        }
    }

    /// <summary>
    /// SynchronizationContext que apenas enfileira as continuações, permitindo executar a
    /// continuação de ThumbnailActivated de forma determinística na thread do teste.
    /// </summary>
    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.BlockingCollection<KeyValuePair<SendOrPostCallback, object?>> _queue =
            new System.Collections.Concurrent.BlockingCollection<KeyValuePair<SendOrPostCallback, object?>>();

        public override void Post(SendOrPostCallback d, object? state)
        {
            this._queue.Add(new KeyValuePair<SendOrPostCallback, object?>(d, state));
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            d(state);
        }

        public bool TryDequeue(out KeyValuePair<SendOrPostCallback, object?> item)
        {
            return this._queue.TryTake(out item, Timeout);
        }
    }

    private sealed class TestHarness
    {
        public Mock<IMediator> Mediator { get; } = new(MockBehavior.Loose);
        public Mock<IThumbnailConfiguration> Configuration { get; } = new(MockBehavior.Loose);
        public StubProcessMonitor ProcessMonitor { get; } = new();
        public Mock<IWindowManager> WindowManager { get; } = new(MockBehavior.Loose);
        public Mock<IThumbnailViewFactory> ViewFactory { get; } = new(MockBehavior.Loose);
        public ThumbnailManager Manager { get; }

        public TestHarness()
        {
            this.SetupConfiguration();

            // Um foreground handle zero faz RefreshThumbnails() retornar antes de qualquer
            // interação real com janelas.
            this.WindowManager.Setup(w => w.GetForegroundWindowHandle()).Returns(IntPtr.Zero);

            this.Mediator
                .Setup(m => m.Publish(It.IsAny<ThumbnailListUpdated>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            this.Mediator
                .Setup(m => m.Send(It.IsAny<SaveConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Unit.Value);

            this.Manager = new ThumbnailManager(
                this.Mediator.Object,
                this.Configuration.Object,
                this.ProcessMonitor,
                this.WindowManager.Object,
                this.ViewFactory.Object,
                Mock.Of<IClientChangeSignal>(),
                Mock.Of<IWindowEventSource>());
        }

        private void SetupConfiguration()
        {
            this.Configuration.SetupGet(c => c.ThumbnailRefreshPeriod).Returns(500);
            this.Configuration.SetupGet(c => c.PerClientThumbnailSize).Returns(new Dictionary<string, Size>());
            this.Configuration.SetupGet(c => c.CycleGroup1ForwardHotkeys).Returns(new List<string>());
            this.Configuration.SetupGet(c => c.CycleGroup1BackwardHotkeys).Returns(new List<string>());
            this.Configuration.SetupGet(c => c.CycleGroup2ForwardHotkeys).Returns(new List<string>());
            this.Configuration.SetupGet(c => c.CycleGroup2BackwardHotkeys).Returns(new List<string>());
            this.Configuration.SetupGet(c => c.CycleGroup3ForwardHotkeys).Returns(new List<string>());
            this.Configuration.SetupGet(c => c.CycleGroup3BackwardHotkeys).Returns(new List<string>());
            this.Configuration.SetupGet(c => c.CycleGroup4ForwardHotkeys).Returns(new List<string>());
            this.Configuration.SetupGet(c => c.CycleGroup4BackwardHotkeys).Returns(new List<string>());
            this.Configuration.SetupGet(c => c.CycleGroup5ForwardHotkeys).Returns(new List<string>());
            this.Configuration.SetupGet(c => c.CycleGroup5BackwardHotkeys).Returns(new List<string>());
            this.Configuration.SetupGet(c => c.MinimizeAllClientsHotkeys).Returns(new List<string>());
            this.Configuration.SetupGet(c => c.ToggleSpotlightHotkeys).Returns(new List<string>());
        }
    }

    [Fact]
    public void EnsureUiThread_OnOwningThread_DoesNotThrow()
    {
        int owningThreadId = Environment.CurrentManagedThreadId;

        ThumbnailManager.EnsureUiThread(owningThreadId, nameof(EnsureUiThread_OnOwningThread_DoesNotThrow));
    }

    [Fact]
    public void EnsureUiThread_FromAnotherThread_Throws()
    {
        int owningThreadId = Environment.CurrentManagedThreadId;

        Exception? error = CaptureFromBackgroundThread(
            () => ThumbnailManager.EnsureUiThread(owningThreadId, "OperacaoDeTeste"));

        var invalidOperation = Assert.IsType<InvalidOperationException>(error);
        Assert.Contains("OperacaoDeTeste", invalidOperation.Message);
        Assert.Contains(owningThreadId.ToString(), invalidOperation.Message);
    }

    [Fact]
    public void GetClientByTitle_FromAnotherThread_Throws()
    {
        var harness = new TestHarness();

        Exception? error = CaptureFromBackgroundThread(() => harness.Manager.GetClientByTitle("EVE - Alpha"));

        Assert.IsType<InvalidOperationException>(error);
        Assert.Contains("GetClientByTitle", error!.Message);
    }

    [Fact]
    public void GetClientByPointer_FromAnotherThread_Throws()
    {
        var harness = new TestHarness();

        Exception? error = CaptureFromBackgroundThread(() => harness.Manager.GetClientByPointer(new IntPtr(0x59)));

        Assert.IsType<InvalidOperationException>(error);
        Assert.Contains("GetClientByPointer", error!.Message);
    }

    [Fact]
    public void GetActiveClient_FromAnotherThread_Throws()
    {
        var harness = new TestHarness();

        Exception? error = CaptureFromBackgroundThread(() => harness.Manager.GetActiveClient());

        Assert.IsType<InvalidOperationException>(error);
        Assert.Contains("GetActiveClient", error!.Message);
    }

    [Fact]
    public void CycleNextClient_FromAnotherThread_Throws()
    {
        var harness = new TestHarness();

        Exception? error = CaptureFromBackgroundThread(
            () => harness.Manager.CycleNextClient(true, new Dictionary<string, int>()));

        Assert.IsType<InvalidOperationException>(error);
        Assert.Contains("CycleNextClient", error!.Message);
    }

    [Fact]
    public void MinimizeAllClients_FromAnotherThread_Throws()
    {
        var harness = new TestHarness();

        Exception? error = CaptureFromBackgroundThread(() => harness.Manager.MinimizeAllClients());

        Assert.IsType<InvalidOperationException>(error);
        Assert.Contains("MinimizeAllClients", error!.Message);
    }

    [Fact]
    public void Start_FromAnotherThread_Throws()
    {
        var harness = new TestHarness();

        Exception? error = CaptureFromBackgroundThread(() => harness.Manager.Start());

        Assert.IsType<InvalidOperationException>(error);
        Assert.Contains("Start", error!.Message);
    }

    [Fact]
    public void TimerTickFollowedByActivation_KeepsThumbnailViewsConsistent()
    {
        var harness = new TestHarness();
        var firstLoginId = new IntPtr(0x6A01);
        var secondLoginId = new IntPtr(0x6A02);
        var firstView = CreateView(firstLoginId, "EVE");
        var secondView = CreateView(secondLoginId, "EVE");

        harness.ProcessMonitor.Added.Add(CreateProcess(firstLoginId, "EVE"));
        harness.ProcessMonitor.Added.Add(CreateProcess(secondLoginId, "EVE"));
        harness.ViewFactory
            .Setup(f => f.Create(It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<Size>()))
            .Returns((IntPtr id, string title, Size size) => id == firstLoginId ? firstView.Object : secondView.Object);

        InvokeTimerTick(harness.Manager);

        Assert.Equal(2, GetThumbnailViews(harness.Manager).Count);
        Assert.Same(firstView.Object, harness.Manager.GetClientByPointer(firstLoginId));

        using var activationEntered = new ManualResetEventSlim(false);
        using var activationRelease = new ManualResetEventSlim(false);
#if LINUX
        harness.WindowManager
            .Setup(w => w.ActivateWindow(It.IsAny<IntPtr>(), It.IsAny<string>()))
#else
        harness.WindowManager
            .Setup(w => w.ActivateWindow(It.IsAny<IntPtr>(), It.IsAny<AnimationStyle>()))
#endif
            .Callback(() =>
            {
                activationEntered.Set();
                activationRelease.Wait(Timeout);
            });

        RunWithQueuedContext(context =>
        {
            InvokeThumbnailActivated(harness.Manager, firstLoginId);

            Assert.True(activationEntered.Wait(Timeout), "A ativação em segundo plano não iniciou.");

            // Cenário da issue: um tick do timer enquanto a ativação ainda está em voo.
            // Os dois caminhos são serializados pela thread de UI; o dicionário deve
            // permanecer íntegro e o estado ativo não pode mudar antes da continuação.
            InvokeTimerTick(harness.Manager);

            Assert.Equal(2, GetThumbnailViews(harness.Manager).Count);
            Assert.Null(harness.Manager.GetActiveClient());

            activationRelease.Set();

            Assert.True(context.TryDequeue(out var continuation), "A continuação de ativação não foi enfileirada.");
            continuation.Key(continuation.Value);
        });

        // O tick não pode ter deixado o dicionário em estado inconsistente e a ativação não
        // pode ter removido nem duplicado entradas.
        Assert.Equal(2, GetThumbnailViews(harness.Manager).Count);
        Assert.Same(firstView.Object, harness.Manager.GetClientByPointer(firstLoginId));
        Assert.Same(secondView.Object, harness.Manager.GetClientByPointer(secondLoginId));

        // A ativação torna o cliente clicado o ativo, sem perder as views.
        Assert.Same(firstView.Object, harness.Manager.GetActiveClient());
        // Dois ticks e um RefreshThumbnails na continuação da ativação.
        harness.WindowManager.Verify(w => w.GetForegroundWindowHandle(), Times.Exactly(3));
    }

    private static Mock<IThumbnailView> CreateView(IntPtr id, string title)
    {
        var view = new Mock<IThumbnailView>(MockBehavior.Loose);
        view.SetupAllProperties();
        view.Object.Id = id;
        view.Object.Title = title;
        view.SetupGet(v => v.IsActive).Returns(true);
        view.SetupGet(v => v.IsDisposed).Returns(false);
        return view;
    }

    private static IProcessInfo CreateProcess(IntPtr handle, string title)
    {
        var process = new Mock<IProcessInfo>(MockBehavior.Loose);
        process.SetupGet(p => p.Handle).Returns(handle);
        process.SetupGet(p => p.Title).Returns(title);
        return process.Object;
    }

    private static void InvokeTimerTick(ThumbnailManager manager)
    {
        var method = typeof(ThumbnailManager).GetMethod(
            "ThumbnailUpdateTimerTick",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new InvalidOperationException("ThumbnailManager.ThumbnailUpdateTimerTick method was not found (renamed?).");
        }

        method.Invoke(manager, new object?[] { null, EventArgs.Empty });
    }

    private static void InvokeThumbnailActivated(ThumbnailManager manager, IntPtr id)
    {
        var method = typeof(ThumbnailManager).GetMethod(
            "ThumbnailActivated",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new InvalidOperationException("ThumbnailManager.ThumbnailActivated method was not found (renamed?).");
        }

        method.Invoke(manager, new object[] { id });
    }

    private static Dictionary<IntPtr, IThumbnailView> GetThumbnailViews(ThumbnailManager manager)
    {
        var field = typeof(ThumbnailManager).GetField(
            "_thumbnailViews",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException("ThumbnailManager._thumbnailViews field was not found (renamed?).");
        }

        return (Dictionary<IntPtr, IThumbnailView>)field.GetValue(manager)!;
    }

    private static void RunWithQueuedContext(Action<QueuedSynchronizationContext> action)
    {
        SynchronizationContext? previousContext = SynchronizationContext.Current;
        var context = new QueuedSynchronizationContext();

        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            action(context);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    private static Exception? CaptureFromBackgroundThread(Action action)
    {
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                captured = error;
            }
        })
        {
            IsBackground = true
        };

        thread.Start();
        Assert.True(thread.Join(Timeout), "A thread de teste não terminou.");

        return captured;
    }
}
