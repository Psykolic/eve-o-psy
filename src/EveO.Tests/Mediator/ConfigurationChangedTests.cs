using EveOPreview.Configuration;
using EveOPreview.Mediator.Handlers.Configuration;
using EveOPreview.Mediator.Messages;
using EveOPreview.Mediator.Messages.Configuration;
using EveOPreview.Services;
using MediatR;
using Moq;

namespace EveO.Tests.Mediator;

/// <summary>
/// Item 2.6 do roadmap: a alteração de configuração é aplicada por notificação, não por
/// polling. O save publica <see cref="ConfigurationChangedNotification"/> e o handler
/// aplica o intervalo do refresh e o fator de zoom sem reiniciar o aplicativo.
/// </summary>
public sealed class ConfigurationChangedTests
{
    [Fact]
    public async Task ConfigurationChanged_AplicaTimerEZoom()
    {
        var manager = new Mock<IThumbnailManager>(MockBehavior.Strict);
        manager.Setup(m => m.RefreshTimerInterval());
        manager.Setup(m => m.RefreshZoomFactor());
        manager.Setup(m => m.RefreshCycleGroupHotkeys());

        var handler = new ConfigurationChangedHandler(manager.Object);
        await handler.Handle(new ConfigurationChangedNotification(), CancellationToken.None);

        manager.Verify(m => m.RefreshTimerInterval(), Times.Once);
        manager.Verify(m => m.RefreshZoomFactor(), Times.Once);
        manager.Verify(m => m.RefreshCycleGroupHotkeys(), Times.Once);
    }

    [Fact]
    public async Task SaveConfiguration_SalvaEPublica()
    {
        var storage = new Mock<IConfigurationStorage>(MockBehavior.Strict);
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        storage.Setup(s => s.Save());
        mediator
            .Setup(m => m.Publish(It.IsAny<ConfigurationChangedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new SaveConfigurationHandler(storage.Object, mediator.Object);
        await handler.Handle(new SaveConfiguration(), CancellationToken.None);

        storage.Verify(s => s.Save(), Times.Once);
        mediator.Verify(
            m => m.Publish(It.IsAny<ConfigurationChangedNotification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// A publicação é aguardada: exceção de hot-reload tem de aparecer no fluxo de
    /// salvamento, não como tarefa não observada (que derrubaria o processo).
    /// </summary>
    [Fact]
    public async Task SaveConfiguration_FalhaNoHotReload_Propaga()
    {
        var storage = new Mock<IConfigurationStorage>();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Publish(It.IsAny<ConfigurationChangedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new InvalidOperationException("hot-reload falhou")));

        var handler = new SaveConfigurationHandler(storage.Object, mediator.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new SaveConfiguration(), CancellationToken.None));
    }
}
