using System;
using System.IO;

namespace EveO.Core.Tests.Plugins;

/// <summary>
/// Plugins de exemplo compilados, entregues ao teste como **arquivo** em
/// <c>plugins-sample/</c> (copiados pelo alvo <c>CopiaPluginsDeExemplo</c> do csproj).
/// </summary>
internal static class Fixtures
{
	public static string CaminhoDoExemplo =>
		Path.Combine(AppContext.BaseDirectory, "plugins-sample", "sample", "EveO.Plugin.Sample.dll");

	public static string CaminhoDoQuebrado =>
		Path.Combine(AppContext.BaseDirectory, "plugins-sample", "broken", "EveO.Plugin.Broken.dll");
}
