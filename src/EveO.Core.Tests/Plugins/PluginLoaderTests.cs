using System;
using System.Collections.Generic;
using System.IO;
using EveOPreview.Plugins;

namespace EveO.Core.Tests.Plugins;

/// <summary>
/// Spike do carregamento de plugin: assembly separado, contexto isolado e o contrato
/// <see cref="IPlugin"/> compartilhado com o host (o ponto que quebra se o contexto
/// carregar a própria cópia de EveO.Core).
///
/// Os plugins de exemplo **não** são referenciados pelo assembly de teste — existem só
/// como arquivo em <c>plugins-sample/</c>, igual a um plugin instalado. Referenciá-los
/// faria o host já tê-los carregado e o loader (corretamente) os pularia.
/// </summary>
public sealed class PluginLoaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "eveo-plugins-" + Guid.NewGuid().ToString("N"));
    private readonly RecordingContext _contexto = new RecordingContext();

    public PluginLoaderTests() => Directory.CreateDirectory(_dir);

    private PluginLoadResult Load() => PluginLoader.LoadFrom(_dir, _ => this._contexto);

    [Fact]
    public void CarregaPluginReal_EmContextoIsolado()
    {
        string destino = Path.Combine(_dir, "EveO.Plugin.Sample.dll");
        File.Copy(Fixtures.CaminhoDoExemplo, destino);

        PluginLoadResult resultado = this.Load();

        Assert.Empty(resultado.Errors);
        LoadedPlugin plugin = Assert.Single(resultado.Plugins);
        Assert.Equal("Sample", plugin.Name);
        Assert.Equal("0.1", plugin.Version);
        Assert.Equal(destino, plugin.AssemblyPath);
        Assert.Contains(this._contexto.Mensagens, m => m.Contains("sample plugin iniciado"));
    }

    [Fact]
    public void DllInvalida_ViraErroSemDerrubar()
    {
        File.WriteAllText(Path.Combine(_dir, "quebrado.dll"), "isto nao e um assembly");

        PluginLoadResult resultado = this.Load();

        Assert.Empty(resultado.Plugins);
        Assert.Single(resultado.Errors);
        Assert.Contains("quebrado.dll", resultado.Errors[0]);
    }

    [Fact]
    public void CopiaDoHostNaPastaDoPlugin_EhIgnorada()
    {
        // Um EveO.Core.dll largado na pasta de plugins: carregá-lo criaria um segundo
        // IPlugin e o cast falharia em silêncio.
        File.Copy(typeof(PluginLoadResult).Assembly.Location, Path.Combine(_dir, "EveO.Core.dll"));

        PluginLoadResult resultado = this.Load();

        Assert.Empty(resultado.Plugins);
        Assert.Empty(resultado.Errors);
    }

    [Fact]
    public void PastaInexistente_NaoQuebra()
    {
        PluginLoadResult resultado = PluginLoader.LoadFrom(Path.Combine(_dir, "nao-existe"), _ => this._contexto);

        Assert.Empty(resultado.Plugins);
        Assert.Empty(resultado.Errors);
    }

    [Fact]
    public void PluginQueFalhaNoStart_ViraErroESobraEspacoParaOsOutros()
    {
        File.Copy(Fixtures.CaminhoDoExemplo, Path.Combine(_dir, "EveO.Plugin.Sample.dll"));
        File.Copy(Fixtures.CaminhoDoQuebrado, Path.Combine(_dir, "EveO.Plugin.Broken.dll"));

        PluginLoadResult resultado = this.Load();

        LoadedPlugin plugin = Assert.Single(resultado.Plugins);
        Assert.Equal("Sample", plugin.Name);
        Assert.Single(resultado.Errors);
        Assert.Contains("falha proposital do plugin de teste", resultado.Errors[0]);
    }

    [Fact]
    public void ErroEmUm_NaoImpedeOsOutros()
    {
        File.Copy(Fixtures.CaminhoDoExemplo, Path.Combine(_dir, "EveO.Plugin.Sample.dll"));
        File.WriteAllText(Path.Combine(_dir, "outro-quebrado.dll"), "lixo");

        PluginLoadResult resultado = this.Load();

        Assert.Single(resultado.Plugins);
        Assert.Single(resultado.Errors);
        Assert.Contains("outro-quebrado.dll", resultado.Errors[0]);
    }

    [Fact]
    public void Unload_ParaOsPluginsEValeSoUmaVez()
    {
        File.Copy(Fixtures.CaminhoDoExemplo, Path.Combine(_dir, "EveO.Plugin.Sample.dll"));
        PluginLoadResult resultado = this.Load();

        resultado.Unload();

        Assert.Empty(resultado.Plugins);
        Assert.Contains(this._contexto.Mensagens, m => m.Contains("sample plugin parado"));

        // Segunda chamada não pode parar de novo (nem explodir).
        int paradas = this._contexto.Mensagens.FindAll(m => m.Contains("sample plugin parado")).Count;
        resultado.Unload();
        Assert.Equal(paradas, this._contexto.Mensagens.FindAll(m => m.Contains("sample plugin parado")).Count);
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

    internal sealed class RecordingContext : IPluginContext
    {
        public List<string> Mensagens { get; } = new();

        public string DataDirectory => Path.GetTempPath();

        public void Log(string message) => this.Mensagens.Add(message);
    }
}
