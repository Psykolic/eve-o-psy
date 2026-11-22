using System;
using System.IO;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;

namespace EveO.Core.Tests.Configuration;

/// <summary>
/// O botão "abrir arquivo" do app lia <c>IAppConfig.ConfigFileName</c>, que é sempre nulo —
/// então não abria nada. Quem sabe o caminho real é o storage.
/// </summary>
public sealed class ConfigurationFilePathTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "eveo-path-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void SemCaminhoExplicito_ResolveORelativo()
    {
        var appConfig = new AppConfig { ConfigFileName = null };
        var storage = new ConfigurationStorage(appConfig, new ThumbnailConfiguration(), new ConfigurationValidator());

        string caminho = storage.GetConfigurationFilePath();

        Assert.True(Path.IsPathRooted(caminho));
        Assert.EndsWith("EVE-O-Preview.json", caminho, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComCaminhoExplicito_DevolveOEsse()
    {
        Directory.CreateDirectory(_dir);
        string esperado = Path.Combine(_dir, "minha-config.json");
        var appConfig = new AppConfig { ConfigFileName = esperado };
        var storage = new ConfigurationStorage(appConfig, new ThumbnailConfiguration(), new ConfigurationValidator());

        Assert.Equal(Path.GetFullPath(esperado), storage.GetConfigurationFilePath());
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
