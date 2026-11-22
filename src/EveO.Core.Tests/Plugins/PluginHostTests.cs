using System;
using System.IO;
using System.Linq;
using EveOPreview.Plugins;

namespace EveO.Core.Tests.Plugins;

/// <summary>
/// O host: pasta opcional, log do que aconteceu e ciclo de vida. Nenhuma falha de plugin
/// pode chegar ao app.
/// </summary>
public sealed class PluginHostTests : IDisposable
{
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "eveo-host-" + Guid.NewGuid().ToString("N"));

	public PluginHostTests() => Directory.CreateDirectory(this._dir);

	[Fact]
	public void PastaAusente_NaoCriaNadaNemRegistra()
	{
		string inexistente = Path.Combine(this._dir, "Plugins");

		using (PluginHost host = PluginHost.Start(inexistente))
		{
			Assert.Empty(host.Plugins);
			Assert.Empty(host.Errors);
			Assert.Null(host.LogPath);
		}

		Assert.False(Directory.Exists(inexistente));
	}

	[Fact]
	public void CarregaPluginERegistraNoLog()
	{
		File.Copy(Fixtures.CaminhoDoExemplo, Path.Combine(this._dir, "EveO.Plugin.Sample.dll"));

		using (PluginHost host = PluginHost.Start(this._dir))
		{
			LoadedPlugin plugin = Assert.Single(host.Plugins);
			Assert.Equal("Sample", plugin.Name);
			Assert.Empty(host.Errors);
			Assert.Equal(Path.Combine(this._dir, PluginHost.LogFileName), host.LogPath);

			string log = File.ReadAllText(host.LogPath);
			Assert.Contains("Sample 0.1 carregado", log);
			Assert.Contains("[Sample] sample plugin iniciado", log);
		}
	}

	[Fact]
	public void Dispose_ParaOsPluginsERegistraEncerramento()
	{
		File.Copy(Fixtures.CaminhoDoExemplo, Path.Combine(this._dir, "EveO.Plugin.Sample.dll"));

		PluginHost host = PluginHost.Start(this._dir);
		string logPath = host.LogPath;
		host.Dispose();

		Assert.Empty(host.Plugins);

		string log = File.ReadAllText(logPath);
		Assert.Contains("[Sample] sample plugin parado", log);
		Assert.Contains("plugins encerrados", log);

		// Idempotente.
		host.Dispose();
		Assert.Single(File.ReadAllLines(logPath).Where(l => l.Contains("plugins encerrados")));
	}

	[Fact]
	public void PluginQuebrado_FicaNoLogSemDerrubarOsOutros()
	{
		File.Copy(Fixtures.CaminhoDoExemplo, Path.Combine(this._dir, "EveO.Plugin.Sample.dll"));
		File.Copy(Fixtures.CaminhoDoQuebrado, Path.Combine(this._dir, "EveO.Plugin.Broken.dll"));

		using (PluginHost host = PluginHost.Start(this._dir))
		{
			Assert.Single(host.Plugins);
			Assert.Single(host.Errors);

			string log = File.ReadAllText(host.LogPath);
			Assert.Contains("erro:", log);
			Assert.Contains("falha proposital do plugin de teste", log);
		}
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(this._dir, true);
		}
		catch (IOException)
		{
		}
	}
}
