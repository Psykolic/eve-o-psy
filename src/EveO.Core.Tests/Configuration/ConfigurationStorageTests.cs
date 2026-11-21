using System;
using System.IO;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;

namespace EveO.Core.Tests.Configuration;

/// <summary>
/// O carregamento nunca lança: arquivo corrompido mantém os padrões, valor fora da
/// faixa é reportado e depois corrigido.
/// </summary>
public sealed class ConfigurationStorageTests : IDisposable
{
    private readonly string _dir;
    private readonly StubAppConfig _appConfig;

    public ConfigurationStorageTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "eveo-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _appConfig = new StubAppConfig { ConfigFileName = Path.Combine(_dir, "EVE-O-Preview.json") };
    }

    private ConfigurationStorage CreateStorage(ThumbnailConfiguration config)
        => new(_appConfig, config, new ConfigurationValidator());

    [Fact]
    public void SemArquivo_UsaPadroesESemProblemas()
    {
        var config = new ThumbnailConfiguration();

        var result = CreateStorage(config).Load();

        Assert.True(result.IsValid, result.Summary());
        Assert.Equal(new ThumbnailConfiguration().ThumbnailRefreshPeriod, config.ThumbnailRefreshPeriod);
    }

    [Fact]
    public void JsonQuebrado_NaoDerrubaEMantemPadroes()
    {
        var config = new ThumbnailConfiguration();
        int original = config.ThumbnailRefreshPeriod;
        File.WriteAllText(_appConfig.ConfigFileName, "{ \"ThumbnailRefreshPeriod\": 500, ");

        var result = CreateStorage(config).Load();

        Assert.False(result.IsValid);
        Assert.Equal(original, config.ThumbnailRefreshPeriod);
    }

    [Fact]
    public void ValorForaDaFaixa_EhReportadoECorrigido()
    {
        var config = new ThumbnailConfiguration();
        File.WriteAllText(_appConfig.ConfigFileName, "{ \"ThumbnailRefreshPeriod\": 99999 }");

        var result = CreateStorage(config).Load();

        Assert.Contains(result.Problems, p => p.Key == nameof(config.ThumbnailRefreshPeriod));
        Assert.Equal(ConfigurationLimits.RefreshPeriodMax, config.ThumbnailRefreshPeriod);
    }

    [Fact]
    public void ConfigValida_EhAplicadaSemProblemas()
    {
        var config = new ThumbnailConfiguration();
        File.WriteAllText(_appConfig.ConfigFileName, "{ \"ThumbnailRefreshPeriod\": 700 }");

        var result = CreateStorage(config).Load();

        Assert.True(result.IsValid, result.Summary());
        Assert.Equal(700, config.ThumbnailRefreshPeriod);
    }

    [Fact]
    public void TipoInesperado_MantemValorAnterior()
    {
        var config = new ThumbnailConfiguration { ThumbnailRefreshPeriod = 700 };
        File.WriteAllText(_appConfig.ConfigFileName, "{ \"ThumbnailRefreshPeriod\": \"muito rápido\" }");

        var result = CreateStorage(config).Load();

        Assert.False(result.IsValid);
        Assert.Equal(700, config.ThumbnailRefreshPeriod);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class StubAppConfig : IAppConfig
    {
        public string ConfigFileName { get; set; } = string.Empty;
    }
}
