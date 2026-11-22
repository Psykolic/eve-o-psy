namespace EveOPreview.Plugin.Broken;

using System;
using EveOPreview.Plugins;

/// <summary>
/// Plugin que falha no <see cref="Start"/>. É fixture de teste: existe para provar que um
/// plugin quebrado vira uma linha de erro e **não** impede os outros nem o app.
/// </summary>
public sealed class BrokenPlugin : IPlugin
{
    public string Name => "Broken";

    public string Version => "0.1";

    public void Start(IPluginContext context)
    {
        throw new InvalidOperationException("falha proposital do plugin de teste");
    }

    public void Stop()
    {
    }
}
