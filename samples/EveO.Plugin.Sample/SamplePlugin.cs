namespace EveOPreview.Plugin.Sample;

using EveOPreview.Plugins;

/// <summary>
/// Plugin de exemplo: existe para provar que um assembly separado consegue implementar
/// <see cref="IPlugin"/>, ser iniciado e parado pelo host. Não faz mais nada de propósito.
/// </summary>
public sealed class SamplePlugin : IPlugin
{
    private IPluginContext _context;

    public string Name => "Sample";

    public string Version => "0.1";

    public void Start(IPluginContext context)
    {
        this._context = context;
        context?.Log("sample plugin iniciado em " + (context?.DataDirectory ?? "?"));
    }

    public void Stop()
    {
        this._context?.Log("sample plugin parado");
    }
}
