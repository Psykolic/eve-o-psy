using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interop;
using Moq;

namespace EveO.Tests.Windows;

/// <summary>
/// Item 1.3: o handle do DWM vive em <see cref="SafeDwmThumbnailHandle"/>.
/// Antes era um IntPtr solto, liberado só se o chamador lembrasse — e sem finalizer.
/// Estes testes não precisam de DWM: com composição desligada nada é registrado.
/// </summary>
public sealed class DwmThumbnailLifecycleTests
{
    private static DwmThumbnail Criar(bool composicao)
    {
        var windowManager = new Mock<IWindowManager>();
        windowManager.SetupGet(w => w.IsCompositionEnabled).Returns(composicao);
        return new DwmThumbnail(windowManager.Object);
    }

    [Fact]
    public void SafeHandle_ZeroEhInvalido()
    {
        using var handle = new SafeDwmThumbnailHandle(IntPtr.Zero);

        Assert.True(handle.IsInvalid);
    }

    [Fact]
    public void SemComposicao_NaoRegistraNemLanca()
    {
        var thumbnail = Criar(composicao: false);

        thumbnail.Register(new IntPtr(1), new IntPtr(2));
        thumbnail.Move(0, 0, 100, 100);
        thumbnail.Update();
        thumbnail.Unregister();
    }

    [Fact]
    public void Unregister_EhIdempotente()
    {
        var thumbnail = Criar(composicao: false);

        thumbnail.Unregister();
        thumbnail.Unregister();
        thumbnail.Dispose();
        thumbnail.Dispose();
    }

    [Fact]
    public void Dispose_NaoLancaSemComposicao()
    {
        var thumbnail = Criar(composicao: false);

        thumbnail.Register(new IntPtr(1), new IntPtr(2));
        thumbnail.Dispose();

        // Depois do Dispose o objeto continua utilizável para Update (no-op).
        thumbnail.Update();
    }
}
