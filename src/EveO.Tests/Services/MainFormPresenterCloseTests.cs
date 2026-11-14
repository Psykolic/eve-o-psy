using System.Reflection;
using EveOPreview;
using EveOPreview.Configuration;
using EveOPreview.Mediator.Messages;
using EveOPreview.Presenters;
using EveOPreview.View;
using MediatR;
using Moq;

namespace EveO.Tests.Services;

/// <summary>
/// Fechamento diferido (issue #55): o presenter não pode bloquear a thread de UI com
/// Wait/Result enquanto o StopAsync aguarda trabalho em voo. A primeira passagem nega
/// o fechamento; a segunda conclui, já com tudo encerrado e a configuração salva.
/// </summary>
public sealed class MainFormPresenterCloseTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Close_DefersShutdownInsteadOfBlockingUiThread()
    {
        var view = new Mock<IMainFormView>(MockBehavior.Loose);
        view.SetupProperty(v => v.MinimizeToTray, false);

        var stopCompletion = new TaskCompletionSource<Unit>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mediator = new Mock<IMediator>(MockBehavior.Loose);
        mediator
            .Setup(m => m.Send(It.IsAny<StopService>(), It.IsAny<CancellationToken>()))
            .Returns(stopCompletion.Task);

        bool saveCompleted = false;
        var storage = new Mock<IConfigurationStorage>(MockBehavior.Loose);
        storage.Setup(s => s.Save()).Callback(() => saveCompleted = true);

        MainFormPresenter presenter = null!;
        var secondPass = new TaskCompletionSource<ViewCloseRequest>(TaskCreationOptions.RunContinuationsAsynchronously);

        view.Setup(v => v.Close()).Callback(() =>
        {
            // O form real dispararia FormClosing novamente; simulamos a segunda passagem.
            var request = new ViewCloseRequest();
            InvokeClose(presenter, request);
            secondPass.TrySetResult(request);
        });

        presenter = new MainFormPresenter(
            new Mock<IApplicationController>(MockBehavior.Loose).Object,
            view.Object,
            mediator.Object,
            new Mock<IThumbnailConfiguration>(MockBehavior.Loose).Object,
            storage.Object);

        var firstPass = new ViewCloseRequest();
        InvokeClose(presenter, firstPass);

        // A chamada retorna imediatamente, sem Wait/Result: o shutdown fica pendente.
        Assert.False(firstPass.Allow);
        Assert.False(saveCompleted);
        Assert.False(secondPass.Task.IsCompleted);
        view.Verify(v => v.Close(), Times.Never);

        stopCompletion.TrySetResult(Unit.Value);

        ViewCloseRequest allowed = await secondPass.Task.WaitAsync(Timeout);
        Assert.True(allowed.Allow);
        Assert.True(saveCompleted);
        view.Verify(v => v.Close(), Times.Once);
    }

    private static void InvokeClose(MainFormPresenter presenter, ViewCloseRequest request)
    {
        var method = typeof(MainFormPresenter).GetMethod(
            "Close",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new InvalidOperationException("MainFormPresenter.Close method was not found (renamed?).");
        }

        method.Invoke(presenter, new object[] { request });
    }
}
