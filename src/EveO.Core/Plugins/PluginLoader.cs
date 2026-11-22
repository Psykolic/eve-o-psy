using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace EveOPreview.Plugins
{
	/// <summary>
	/// Carrega plugins de uma pasta, cada dll no próprio <see cref="AssemblyLoadContext"/>
	/// (coletável).
	///
	/// O ponto crítico: o plugin referencia <c>EveO.Core</c> e precisa enxergar **o mesmo**
	/// <see cref="IPlugin"/> que o host — se o contexto isolado carregar a própria cópia,
	/// os tipos não batem e o cast falha em silêncio. Por isso as assemblies que o host já
	/// tem carregadas são resolvidas no contexto padrão.
	/// </summary>
	public static class PluginLoader
	{
		/// <param name="contextFactory">
		/// Cria o contexto de cada plugin. Recebe o nome que o plugin declarou, antes de
		/// <see cref="IPlugin.Start"/>.
		/// </param>
		public static PluginLoadResult LoadFrom(string directory, Func<string, IPluginContext> contextFactory)
		{
			var plugins = new List<LoadedPlugin>();
			var errors = new List<string>();
			var contexts = new List<AssemblyLoadContext>();

			if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
			{
				return new PluginLoadResult(plugins, errors, contexts);
			}

			if (contextFactory == null)
			{
				contextFactory = _ => null;
			}

			foreach (string arquivo in Directory.GetFiles(directory, "*.dll").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
			{
				// Copiar EveO.Core.dll para a pasta do plugin é um erro comum e criaria um
				// segundo IPlugin. Arquivos que o host já carregou são ignorados.
				if (IsHostAssembly(arquivo))
				{
					continue;
				}

				AssemblyLoadContext contexto = null;
				try
				{
					contexto = new PluginLoadContext(arquivo);
					Assembly assembly = contexto.LoadFromAssemblyPath(arquivo);
					bool carregouAlgo = false;

					foreach (Type tipo in PluginTypes(assembly))
					{
						try
						{
							if (!(Activator.CreateInstance(tipo) is IPlugin plugin))
							{
								continue;
							}

							plugin.Start(contextFactory(plugin.Name));
							plugins.Add(new LoadedPlugin(plugin.Name, plugin.Version, arquivo, plugin));
							carregouAlgo = true;
						}
						catch (Exception excecao)
						{
							errors.Add(Path.GetFileName(arquivo) + " (" + tipo.Name + "): "
								+ excecao.GetType().Name + " — " + excecao.Message);
						}
					}

					if (carregouAlgo)
					{
						contexts.Add(contexto);
					}
					else
					{
						// Pasta de plugin pode ter dependências sem IPlugin: não é erro.
						contexto.Unload();
					}
				}
				catch (Exception excecao)
				{
					errors.Add(Path.GetFileName(arquivo) + ": " + excecao.GetType().Name + " — " + excecao.Message);
					try
					{
						contexto?.Unload();
					}
					catch (InvalidOperationException)
					{
					}
				}
			}

			return new PluginLoadResult(plugins, errors, contexts);
		}

		/// <summary>
		/// Tipos do assembly que implementam <see cref="IPlugin"/>. Assembly que não é
		/// .NET válido lança — quem chama trata.
		/// </summary>
		private static IEnumerable<Type> PluginTypes(Assembly assembly)
		{
			Type[] tipos;
			try
			{
				tipos = assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException excecao)
			{
				// Dependência ausente: aproveita os tipos que carregaram.
				tipos = excecao.Types ?? Array.Empty<Type>();
			}

			return tipos.Where(t => t != null && t.IsClass && !t.IsAbstract && typeof(IPlugin).IsAssignableFrom(t));
		}

		/// <summary>
		/// Assembly do host é aquele que o contexto padrão já tem carregado. Comparar por
		/// prefixo de nome era errado: um plugin chamado <c>EveO.AlgumaCoisa</c> seria
		/// descartado.
		/// </summary>
		private static bool IsHostAssembly(string caminho)
		{
			return IsLoadedInHost(Path.GetFileNameWithoutExtension(caminho));
		}

		private static bool IsLoadedInHost(string nome)
		{
			if (string.IsNullOrEmpty(nome))
			{
				return false;
			}

			foreach (Assembly carregada in AssemblyLoadContext.Default.Assemblies)
			{
				if (string.Equals(carregada.GetName().Name, nome, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Contexto isolado e coletável. Devolver <c>null</c> em <see cref="Load"/> manda o
		/// runtime procurar no contexto padrão — é assim que o plugin vê o <c>EveO.Core</c>
		/// do host em vez de carregar o seu.
		/// </summary>
		private sealed class PluginLoadContext : AssemblyLoadContext
		{
			private readonly AssemblyDependencyResolver _resolver;

			public PluginLoadContext(string pluginPath)
				: base("plugin:" + Path.GetFileName(pluginPath), isCollectible: true)
			{
				this._resolver = new AssemblyDependencyResolver(pluginPath);
			}

			protected override Assembly Load(AssemblyName assemblyName)
			{
				if (IsLoadedInHost(assemblyName.Name))
				{
					return null;
				}

				string caminho = this._resolver.ResolveAssemblyToPath(assemblyName);
				return caminho == null ? null : this.LoadFromAssemblyPath(caminho);
			}
		}
	}
}
