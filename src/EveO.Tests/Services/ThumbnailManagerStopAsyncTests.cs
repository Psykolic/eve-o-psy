using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Threading;
using EveOPreview.Configuration;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using EveOPreview.View;
using MediatR;
using Moq;

namespace EveO.Tests.Services;

/// <summary>
/// Shutdown assíncrono do ThumbnailManager (issue #55 / auditoria TM-02): StopAsync
/// cancela o ciclo de vida, fecha os spotlights e aguarda o trabalho em voo dos handlers.
/// </summary>
public sealed class ThumbnailManagerStopAsyncTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed class TestHarness
    {
        public Mock<IMediator> Mediator { get; } = new(MockBehavior.Loose);
        public Mock<IThumbnailConfiguration> Configuration { get; } = new(MockBehavior.Loose);
        public Mock<IProcessMonitor> ProcessMonitor { get; } = new(MockBehavior.Loose);
        public Mock<IWindowManager> WindowManager { get; } = new(MockBehavior.Loose);
        public Mock<IThumbnailViewFactory> ViewFactory { get; } = new(MockBehavior.Loose);
        public ThumbnailManager Manager { get; }

        public TestHarness()
        {
            this.SetupConfiguration();
            this.WindowManager.Setup(w => w.GetForegroundWindowHandle()).Returns(IntPtr.Zero);

            this.Manager = new ThumbnailManager(
                this.Mediator.Object,
                this.Configuration.Object,
                this.ProcessMonitor.Object,
                this.WindowManager.Object,
                this.ViewFactory.Object);
        }

        private void SetupConfiguration()
        {
            this.Configuration.SetupGet(c => c.ThumbnailRefreshPeriod).Returns(500);
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

    private static void InvokePublishSpotlightRegionUpdated(ThumbnailManager manager, string title, Rectangle region)
    {
        var method = typeof(ThumbnailManager).GetMethod(
            "PublishSpotlightRegionUpdated",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new InvalidOperationException("ThumbnailManager.PublishSpotlightRegionUpdated method was not found (renamed?).");
        }

        method.Invoke(manager, new object[] { title, region });
    }

    private static void InvokeSpotlightViewRegionSelected(ThumbnailManager manager, IntPtr id, Rectangle region)
    {
        var method = typeof(ThumbnailManager).GetMethod(
            "SpotlightViewRegionSelected",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new InvalidOperationException("ThumbnailManager.SpotlightViewRegionSelected method was not found (renamed?).");
        }

        method.Invoke(manager, new object[] { id, region });
    }

    private static Dictionary<IntPtr, IThumbnailView> GetSpotlightViews(ThumbnailManager manager)
    {
        var field = typeof(ThumbnailManager).GetField("_spotlightViews", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException("ThumbnailManager._spotlightViews field was not found (renamed?).");
        }

        return (Dictionary<IntPtr, IThumbnailView>)field.GetValue(manager)!;
    }

    private static DispatcherTimer GetTimer(ThumbnailManager manager)
    {
        var field = typeof(ThumbnailManager).GetField("_thumbnailUpdateTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException("ThumbnailManager._thumbnailUpdateTimer field was not found (renamed?).");
        }

        return (DispatcherTimer)field.GetValue(manager)!;
    }

    [Fact]
    public async Task StopAsync_WaitsForInFlightHandlerBeforeReturning()
    {
        var harness = new TestHarness();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Ignora o token de propósito: simula um handler que ainda não cooperou com o
        // cancelamento. StopAsync deve esperar mesmo assim.
        harness.Mediator
            .Setup(m => m.Publish(It.IsAny<SpotlightRegionUpdated>(), It.IsAny<CancellationToken>()))
            .Returns(async (SpotlightRegionUpdated notification, CancellationToken token) =>
            {
                entered.TrySetResult();
                await release.Task;
            });

        harness.Manager.Start();

        InvokePublishSpotlightRegionUpdated(harness.Manager, "EVE - Alpha", Rectangle.Empty);
        await entered.Task.WaitAsync(Timeout);

        Task stop = harness.Manager.StopAsync(CancellationToken.None);
        Assert.False(stop.IsCompleted, "StopAsync retornou antes da operação em voo terminar.");

        release.TrySetResult();
        await stop.WaitAsync(Timeout);

        harness.Mediator.Verify(
            m => m.Publish(It.IsAny<SpotlightRegionUpdated>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_CancelledHandler_IsLoggedAsCancellationAndDoesNotFail()
    {
        var harness = new TestHarness();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        harness.Mediator
            .Setup(m => m.Publish(It.IsAny<SpotlightRegionUpdated>(), It.IsAny<CancellationToken>()))
            .Returns(async (SpotlightRegionUpdated notification, CancellationToken token) =>
            {
                entered.TrySetResult();
                await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, token);
            });

        var listener = new OperationTraceListener("PublishSpotlightRegionUpdated:");

        Trace.Listeners.Add(listener);
        try
        {
            harness.Manager.Start();

            InvokePublishSpotlightRegionUpdated(harness.Manager, "EVE - Alpha", Rectangle.Empty);
            await entered.Task.WaitAsync(Timeout);

            await harness.Manager.StopAsync(CancellationToken.None).WaitAsync(Timeout);
            Trace.Flush();
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        Assert.Contains(listener.Messages, message => message.Contains("cancelada", StringComparison.Ordinal));
        Assert.DoesNotContain(listener.Messages, message => message.Contains("exceção assíncrona suprimida", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StopAsync_ClosesSpotlightCreatedWhileDraining()
    {
        var harness = new TestHarness();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        harness.Mediator
            .Setup(m => m.Publish(It.IsAny<SpotlightRegionUpdated>(), It.IsAny<CancellationToken>()))
            .Returns(async (SpotlightRegionUpdated notification, CancellationToken token) =>
            {
                entered.TrySetResult();
                await release.Task;
            });

        harness.Manager.Start();
        InvokePublishSpotlightRegionUpdated(harness.Manager, "EVE - Alpha", Rectangle.Empty);
        await entered.Task.WaitAsync(Timeout);

        Task stop = harness.Manager.StopAsync(CancellationToken.None);

        // A primeira passada de fechamento já rodou; o manager está drenando. Um tick em voo
        // ainda pode criar um spotlight nesse intervalo.
        var id = new IntPtr(0x9002);
        var lateView = new Mock<IThumbnailView>(MockBehavior.Loose);
        lateView.SetupGet(v => v.Id).Returns(id);
        lateView.SetupGet(v => v.Title).Returns("EVE - Beta");
        GetSpotlightViews(harness.Manager)[id] = lateView.Object;

        release.TrySetResult();
        await stop.WaitAsync(Timeout);

        lateView.Verify(v => v.Close(), Times.Once);
        Assert.Empty(GetSpotlightViews(harness.Manager));
    }

    private sealed class OperationTraceListener(string operation) : TraceListener
    {
        public List<string> Messages { get; } = new List<string>();

        public override void Write(string? message)
        {
            if (message != null && message.Contains(operation, StringComparison.Ordinal))
            {
                this.Messages.Add(message);
            }
        }

        public override void WriteLine(string? message) => this.Write(message);
    }

    [Fact]
    public async Task StopAsync_ClosesSpotlightView_AndCancelledHandlerDoesNotTouchItAgain()
    {
        var harness = new TestHarness();
        var id = new IntPtr(0x9001);
        var view = new Mock<IThumbnailView>(MockBehavior.Loose);
        view.SetupGet(v => v.Id).Returns(id);
        view.SetupGet(v => v.Title).Returns("EVE - Alpha");

        Dictionary<IntPtr, IThumbnailView> spotlightViews = GetSpotlightViews(harness.Manager);
        spotlightViews[id] = view.Object;

        harness.Manager.Start();
        await harness.Manager.StopAsync(CancellationToken.None).WaitAsync(Timeout);

        view.Verify(v => v.Close(), Times.Once);
        Assert.Empty(spotlightViews);

        view.Invocations.Clear();

        // Token de vida já cancelado: o handler não pode alcançar a view fechada.
        InvokeSpotlightViewRegionSelected(harness.Manager, id, new Rectangle(0, 0, 32, 18));

        Assert.Empty(view.Invocations);
        harness.Mediator.Verify(
            m => m.Publish(It.IsAny<SpotlightRegionUpdated>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void StopAsync_WithoutInFlightWork_StopsTimerAndAllowsRestart()
    {
        var harness = new TestHarness();
        DispatcherTimer timer = GetTimer(harness.Manager);

        // O dicionário de views é confinado à thread que criou o manager (issue #59).
        // Aguardar StopAsync de forma síncrona mantém o ciclo start/stop nessa thread,
        // evitando a troca de thread das continuações do runner de testes.
        harness.Manager.Start();
        Assert.True(timer.IsEnabled);

        Assert.True(harness.Manager.StopAsync(CancellationToken.None).Wait(Timeout), "StopAsync não terminou no tempo esperado.");
        Assert.False(timer.IsEnabled);

        harness.Manager.Start();
        Assert.True(timer.IsEnabled);

        Assert.True(harness.Manager.StopAsync(CancellationToken.None).Wait(Timeout), "StopAsync não terminou no tempo esperado.");
        Assert.False(timer.IsEnabled);

        harness.Mediator.Verify(
            m => m.Send(It.IsAny<SaveConfiguration>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
