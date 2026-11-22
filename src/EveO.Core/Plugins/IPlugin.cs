using System;
using System.Collections.Generic;
using System.Runtime.Loader;

namespace EveOPreview.Plugins
{
	/// <summary>
	/// Contrato de plugin. Deliberadamente pequeno: enquanto não houver demanda concreta,
	/// o plugin recebe dados e um log — não o app inteiro.
	///
	/// <see cref="Name"/> e <see cref="Version"/> precisam ser legíveis **antes** de
	/// <see cref="Start"/>, porque o host usa o nome para montar o diretório de dados.
	/// </summary>
	public interface IPlugin
	{
		string Name { get; }

		string Version { get; }

		void Start(IPluginContext context);

		/// <summary>
		/// Chamado no encerramento do app (e no descarregamento). Um plugin que instalou
		/// hook, timer ou handle deve soltar tudo aqui.
		/// </summary>
		void Stop();
	}

	/// <summary>O que o host oferece ao plugin.</summary>
	public interface IPluginContext
	{
		/// <summary>
		/// Pasta para dados próprios do plugin. O caminho é sugerido; quem cria é o
		/// plugin, quando precisar.
		/// </summary>
		string DataDirectory { get; }

		/// <summary>Registra uma linha no log de plugins do app.</summary>
		void Log(string message);
	}

	/// <summary>Plugin que carregou, com a origem (diagnóstico e log).</summary>
	public sealed class LoadedPlugin
	{
		public LoadedPlugin(string name, string version, string assemblyPath, IPlugin instance)
		{
			this.Name = name;
			this.Version = version;
			this.AssemblyPath = assemblyPath;
			this.Instance = instance;
		}

		public string Name { get; }

		public string Version { get; }

		public string AssemblyPath { get; }

		public IPlugin Instance { get; }
	}

	/// <summary>
	/// Resultado do carregamento: o que entrou e o que falhou, sem lançar exceção.
	/// Um plugin quebrado nunca impede os outros.
	/// </summary>
	public sealed class PluginLoadResult
	{
		private readonly List<AssemblyLoadContext> _contexts;
		private IReadOnlyList<LoadedPlugin> _plugins;
		private bool _unloaded;

		internal PluginLoadResult(
			IReadOnlyList<LoadedPlugin> plugins,
			IReadOnlyList<string> errors,
			List<AssemblyLoadContext> contexts)
		{
			this._plugins = plugins;
			this.Errors = errors;
			this._contexts = contexts;
		}

		public IReadOnlyList<LoadedPlugin> Plugins => this._plugins;

		public IReadOnlyList<string> Errors { get; }

		/// <summary>
		/// Para os plugins (na ordem inversa do carregamento) e descarrega os contextos.
		/// Idempotente: chamar duas vezes não faz nada na segunda.
		///
		/// Descarregar é melhor esforço — o runtime só libera o assembly quando não sobra
		/// nenhuma referência viva a ele, e o host não tem como garantir isso.
		/// </summary>
		public void Unload()
		{
			if (this._unloaded)
			{
				return;
			}

			this._unloaded = true;

			for (int i = this._plugins.Count - 1; i >= 0; i--)
			{
				try
				{
					this._plugins[i].Instance.Stop();
				}
				catch (Exception)
				{
					// Plugin que falha ao parar não pode derrubar o encerramento do app.
				}
			}

			this._plugins = Array.Empty<LoadedPlugin>();

			foreach (AssemblyLoadContext context in this._contexts)
			{
				try
				{
					context.Unload();
				}
				catch (InvalidOperationException)
				{
					// Contexto já descarregado.
				}
			}

			this._contexts.Clear();
		}
	}
}
