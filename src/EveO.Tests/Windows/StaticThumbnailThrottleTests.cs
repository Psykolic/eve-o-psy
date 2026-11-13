using EveOPreview.View;

namespace EveO.Tests.Windows;

/// <summary>
/// Throttle do cliente em segundo plano na captura estática (issue #50): cada captura
/// custa 30-60 ms por causa do retry com PrintWindow, então cliente que não está em
/// primeiro plano é amostrado a cada 2º refresh forçado.
/// </summary>
public sealed class StaticThumbnailThrottleTests
{
    [Fact]
    public void NextBackgroundCapture_AlternatesCaptureAndSkip()
    {
        var tick = 0;

        Assert.True(StaticThumbnailView.NextBackgroundCapture(ref tick));
        Assert.False(StaticThumbnailView.NextBackgroundCapture(ref tick));
        Assert.True(StaticThumbnailView.NextBackgroundCapture(ref tick));
        Assert.False(StaticThumbnailView.NextBackgroundCapture(ref tick));

        Assert.Equal(4, tick);
    }

    [Fact]
    public void NextBackgroundCapture_CapturesFirstTickSoNewThumbnailIsNotBlank()
    {
        var tick = 0;

        Assert.True(StaticThumbnailView.NextBackgroundCapture(ref tick));
    }

    [Fact]
    public void NextBackgroundCapture_HalvesCaptureRateOverManyTicks()
    {
        var tick = 0;
        var captures = 0;

        for (var i = 0; i < 100; i++)
        {
            if (StaticThumbnailView.NextBackgroundCapture(ref tick))
            {
                captures++;
            }
        }

        Assert.Equal(50, captures);
    }

    [Fact]
    public void BackgroundCaptureInterval_IsTwoForcedRefreshes()
    {
        Assert.Equal(2, StaticThumbnailView.BackgroundCaptureInterval);
    }
}
