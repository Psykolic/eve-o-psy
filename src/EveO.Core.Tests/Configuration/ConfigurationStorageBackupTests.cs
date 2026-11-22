using System;
using System.IO;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;

namespace EveO.Core.Tests.Configuration;

/// <summary>
/// A configuração do usuário é o ativo que ele acabou de construir. Salvar precisa ser
/// atômico (sem JSON pela metade) e um arquivo corrompido não pode ser destruído.
/// </summary>
public sealed class ConfigurationStorageBackupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "eveo-backup-" + Guid.NewGuid().ToString("N"));
    private readonly AppConfig _appConfig;

    public ConfigurationStorageBackupTests()
    {
        Directory.CreateDirectory(_dir);
        _appConfig = new AppConfig { ConfigFileName = Path.Combine(_dir, "EVE-O-Preview.json") };
    }

    private ConfigurationStorage Storage(ThumbnailConfiguration config)
        => new(_appConfig, config, new ConfigurationValidator());

    [Fact]
    public void Save_GuardaAVersaoAnteriorEmBak()
    {
        var config = new ThumbnailConfiguration();
        var storage = Storage(config);

        storage.Save();                          // primeira gravação: sem .bak
        Assert.False(File.Exists(_appConfig.ConfigFileName + ".bak"));

        config.ThumbnailRefreshPeriod = 900;
        storage.Save();                          // segunda: guarda a anterior

        Assert.True(File.Exists(_appConfig.ConfigFileName + ".bak"));
        Assert.Contains("500", File.ReadAllText(_appConfig.ConfigFileName + ".bak"));
        Assert.Contains("900", File.ReadAllText(_appConfig.ConfigFileName));
    }

    [Fact]
    public void Save_NaoDeixaArquivoTemporario()
    {
        var storage = Storage(new ThumbnailConfiguration());

        storage.Save();
        storage.Save();

        Assert.False(File.Exists(_appConfig.ConfigFileName + ".tmp"));
    }

    [Fact]
    public void JsonInvalido_GuardaCopiaCorrupta()
    {
        File.WriteAllText(_appConfig.ConfigFileName, "{ \"ThumbnailRefreshPeriod\": 500, ");

        var result = Storage(new ThumbnailConfiguration()).Load();

        Assert.False(result.IsValid);
        Assert.Contains("guardado em", result.Summary());

        string[] copias = Directory.GetFiles(_dir, "*.corrupt-*");
        Assert.Single(copias);
        Assert.Contains("500", File.ReadAllText(copias[0]));
    }

    [Fact]
    public void ConfigValida_NaoGeraCopiaCorrupta()
    {
        File.WriteAllText(_appConfig.ConfigFileName, "{ \"ThumbnailRefreshPeriod\": 700 }");

        var result = Storage(new ThumbnailConfiguration()).Load();

        Assert.True(result.IsValid);
        Assert.Empty(Directory.GetFiles(_dir, "*.corrupt-*"));
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
}
