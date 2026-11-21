using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;

namespace EveO.Core.Tests.Configuration;

/// <summary>
/// Item 1.4 do roadmap: config inválida é recusada com motivo claro, não silenciada.
/// A validação roda no Core justamente para ser coberta pelo gate Linux.
/// </summary>
public sealed class ConfigurationValidatorTests
{
    private readonly ConfigurationValidator _validator = new();

    [Fact]
    public void ConfiguracaoPadrao_EValida()
    {
        var result = _validator.Validate(new ThumbnailConfiguration());

        Assert.True(result.IsValid, result.Summary());
    }

    [Theory]
    [InlineData(5000)]
    [InlineData(1)]
    public void PeriodoDeRefresh_ForaDaFaixa_EhReportado(int period)
    {
        var config = new ThumbnailConfiguration { ThumbnailRefreshPeriod = period };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems, p => p.Key == nameof(config.ThumbnailRefreshPeriod));
    }

    [Fact]
    public void ZoomForaDaFaixa_EhReportado()
    {
        var config = new ThumbnailConfiguration { ThumbnailZoomFactor = 50 };

        var result = _validator.Validate(config);

        Assert.Contains(result.Problems, p => p.Key == nameof(config.ThumbnailZoomFactor));
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(2.0)]
    public void OpacidadeForaDaFaixa_EhReportada(double opacity)
    {
        var config = new ThumbnailConfiguration { ThumbnailOpacity = opacity };

        var result = _validator.Validate(config);

        Assert.Contains(result.Problems, p => p.Key == nameof(config.ThumbnailOpacity));
    }

    [Fact]
    public void TamanhoDaThumbnail_AbaixoDoMinimo_EhReportado()
    {
        var config = new ThumbnailConfiguration
        {
            ThumbnailMinimumSize = new System.Drawing.Size(32, 18),
            ThumbnailSize = new System.Drawing.Size(16, 9)
        };

        var result = _validator.Validate(config);

        Assert.Contains(result.Problems, p => p.Key == nameof(config.ThumbnailSize));
    }

    [Fact]
    public void SpotlightForaDaFaixa_EhReportado()
    {
        var config = new ThumbnailConfiguration { SpotlightSize = new System.Drawing.Size(10, 10) };

        var result = _validator.Validate(config);

        Assert.Contains(result.Problems, p => p.Key == nameof(config.SpotlightSize));
    }

    [Fact]
    public void Resumo_NomeiaChaveEValor()
    {
        var config = new ThumbnailConfiguration { ThumbnailZoomFactor = 50 };

        var summary = _validator.Validate(config).Summary();

        Assert.Contains("ThumbnailZoomFactor", summary);
        Assert.Contains("50", summary);
    }
}
