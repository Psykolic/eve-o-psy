using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;

namespace EveO.Core.Tests.Configuration;

/// <summary>
/// Importação do formato do aplicativo anterior (ConfigVersion 3 com "CycleGroups").
/// Era o que deixava o usuário sem hotkeys: os grupos simplesmente não eram lidos.
/// </summary>
public sealed class UpstreamConfigurationImportTests : IDisposable
{
    private readonly string _dir;
    private readonly AppConfig _appConfig;

    public UpstreamConfigurationImportTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "eveo-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _appConfig = new AppConfig { ConfigFileName = Path.Combine(_dir, "EVE-O-Preview.json") };
    }

    private (ThumbnailConfiguration config, ConfigurationValidationResult result) Carregar(string json)
    {
        File.WriteAllText(_appConfig.ConfigFileName, json);
        var config = new ThumbnailConfiguration();
        var storage = new ConfigurationStorage(_appConfig, config, new ConfigurationValidator());
        return (config, storage.Load());
    }

    [Fact]
    public void ConfigDoAppAnterior_ImportaGruposNaOrdemCerta()
    {
        var (config, result) = Carregar(@"{
  ""ConfigVersion"": 3,
  ""CycleGroups"": [
    {
      ""Description"": ""New Cycle Group"",
      ""ForwardHotkeys"": [ ""Shift+F6"" ],
      ""BackwardHotkeys"": [ ""Shift+F5"" ],
      ""ClientsOrder"": {
        ""2"": ""EVE - Bozo Nolen"",
        ""3"": ""EVE - Naslah Scariots"",
        ""4"": ""EVE - Mika Nollen""
      }
    }
  ],
  ""MinimizeToTray"": true
}");

        Assert.True(result.IsValid, result.Summary());
        Assert.Equal(new[] { "Shift+F6" }, config.CycleGroup1ForwardHotkeys.ToArray());
        Assert.Equal(new[] { "Shift+F5" }, config.CycleGroup1BackwardHotkeys.ToArray());

        // posição relativa preservada e normalizada para 1..n
        Assert.Equal(1, config.CycleGroup1ClientsOrder["EVE - Bozo Nolen"]);
        Assert.Equal(2, config.CycleGroup1ClientsOrder["EVE - Naslah Scariots"]);
        Assert.Equal(3, config.CycleGroup1ClientsOrder["EVE - Mika Nollen"]);
    }

    [Fact]
    public void Importacao_RegistraAviso()
    {
        var (_, result) = Carregar(@"{
  ""ConfigVersion"": 3,
  ""CycleGroups"": [ { ""ForwardHotkeys"": [ ""F6"" ], ""ClientsOrder"": { ""1"": ""EVE - Um"" } } ]
}");

        Assert.True(result.HasNotices);
        Assert.Contains("importado", result.Summary(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GruposVazios_NaoViramAviso()
    {
        var (_, result) = Carregar(@"{
  ""ConfigVersion"": 3,
  ""CycleGroups"": [ { ""ForwardHotkeys"": [], ""BackwardHotkeys"": [], ""ClientsOrder"": {} } ]
}");

        Assert.False(result.HasNotices);
    }

    [Fact]
    public void ConfigDoFormatoAtual_NaoTemAvisoDeImportacao()
    {
        var (config, result) = Carregar(@"{
  ""ConfigVersion"": 2,
  ""CycleGroup1ForwardHotkeys"": [ ""F14"" ],
  ""CycleGroup1ClientsOrder"": { ""EVE - Um"": 1 }
}");

        Assert.False(result.HasNotices);
        Assert.Equal(new[] { "F14" }, config.CycleGroup1ForwardHotkeys.ToArray());
    }

    [Fact]
    public void CompatibilityMode_ViraWineCompatibilityMode()
    {
        var (config, _) = Carregar(@"{
  ""ConfigVersion"": 3,
  ""CompatibilityMode"": true,
  ""CycleGroups"": [ { ""ForwardHotkeys"": [ ""F6"" ], ""ClientsOrder"": { ""1"": ""EVE - Um"" } } ]
}");

        Assert.True(config.EnableWineCompatibilityMode);
    }

    [Fact]
    public void MaisDeCincoGrupos_ImportaOsCincoPrimeiros()
    {
        var grupos = string.Join(",", Enumerable.Range(0, 7).Select(i =>
            "{ \"ForwardHotkeys\": [ \"F1\" ], \"ClientsOrder\": { \"1\": \"EVE - " + i + "\" } }"));

        var (config, _) = Carregar("{ \"ConfigVersion\": 3, \"CycleGroups\": [" + grupos + "] }");

        Assert.Single(config.CycleGroup5ClientsOrder);
        Assert.Equal("EVE - 4", config.CycleGroup5ClientsOrder.Keys.Single());
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
