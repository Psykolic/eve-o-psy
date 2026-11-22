using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace EveOPreview.Plugins
{
	/// <summary>
	/// Carrega os plugins instalados e mantém o registro do que aconteceu em
	/// <c>Plugins\plugins.log</c>.
	///
	/// Regras que valem aqui:
	/// <list type="bullet">
	/// <item>pasta ausente não é erro nem é criada — quem não usa plugins não vê nada;</item>
	/// <item>nenhuma falha de plugin impede o app de abrir;</item>
	/// <item>o log só é escrito quando há o que registrar.</item>
	/// </list>
	/// </summary>
	public sealed class PluginHost : IDisposable
	{
		public const string DefaultFolderName = "Plugins";

		public const string LogFileName = "plugins.log";

		private readonly object _sync = new object();
		private readonly string _directory;
		private readonly string _logPath;
		private bool _disposed;

		private PluginHost(string directory)
		{
			this._directory = directory;
			this._logPath = string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)
				? null
				: Path.Combine(directory, LogFileName);
		}

		/// <summary>Pasta de plugins ao lado do executável, junto da configuração.</summary>
		public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, DefaultFolderName);

		public IReadOnlyList<LoadedPlugin> Plugins => this._result?.Plugins ?? Array.Empty<LoadedPlugin>();

		public IReadOnlyList<string> Errors => this._result?.Errors ?? Array.Empty<string>();

		public string PluginDirectory => this._directory;

		/// <summary>Caminho do log, ou <c>null</c> quando não há pasta de plugins.</summary>
		public string LogPath => this._logPath;

		private PluginLoadResult _result;

		/// <summary>
		/// Carrega o que estiver na pasta. Nunca lança: qualquer problema vira linha de log
		/// e a lista de erros.
		/// </summary>
		public static PluginHost Start(string directory)
		{
			var host = new PluginHost(directory);

			try
			{
				host.Load();
			}
			catch (Exception excecao)
			{
				// Rede de segurança: carregar plugin é opcional, abrir o app não é.
				host.Append("falha ao carregar plugins: " + excecao.GetType().Name + " — " + excecao.Message);
			}

			return host;
		}

		private void Load()
		{
			this._result = PluginLoader.LoadFrom(this._directory, this.CreateContext);

			foreach (LoadedPlugin plugin in this._result.Plugins)
			{
				this.Append(plugin.Name + " " + plugin.Version + " carregado de " + plugin.AssemblyPath);
			}

			foreach (string erro in this._result.Errors)
			{
				this.Append("erro: " + erro);
			}
		}

		private IPluginContext CreateContext(string pluginName)
		{
			string nome = string.IsNullOrWhiteSpace(pluginName) ? "plugin" : pluginName;
			return new HostContext(this, nome, Path.Combine(this._directory, nome));
		}

		private void Append(string message)
		{
			if (this._logPath == null)
			{
				return;
			}

			string linha = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
				+ " " + message + Environment.NewLine;

			lock (this._sync)
			{
				try
				{
					File.AppendAllText(this._logPath, linha);
				}
				catch (IOException)
				{
					// Disco cheio, arquivo travado por antivírus: perder log não é motivo
					// para atrapalhar o plugin.
				}
				catch (UnauthorizedAccessException)
				{
				}
			}
		}

		public void Dispose()
		{
			if (this._disposed)
			{
				return;
			}

			this._disposed = true;

			try
			{
				this._result?.Unload();
			}
			finally
			{
				this.Append("plugins encerrados");
			}
		}

		/// <summary>Contexto dado a um plugin: pasta própria e log prefixado pelo nome.</summary>
		private sealed class HostContext : IPluginContext
		{
			private readonly PluginHost _host;

			public HostContext(PluginHost host, string name, string dataDirectory)
			{
				this._host = host;
				this.Name = name;
				this.DataDirectory = dataDirectory;
			}

			public string Name { get; }

			public string DataDirectory { get; }

			public void Log(string message)
			{
				this._host.Append("[" + this.Name + "] " + message);
			}
		}
	}
}
