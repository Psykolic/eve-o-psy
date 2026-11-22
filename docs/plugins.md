# Plugins

A partir da 8.3.5 (ou da build de `main` com o item 3.3), o EVE-O Preview carrega plugins de
uma pasta ao lado do executável.

## Onde

```
EVE-O-Preview.exe
Plugins\
  MeuPlugin.dll
  plugins.log      (criado pelo app quando há o que registrar)
```

A pasta **não é criada** pelo app. Sem `Plugins\`, nada acontece — nenhum custo, nenhum arquivo.

Cada plugin roda num `AssemblyLoadContext` coletável e isolado, mas enxerga o `EveO.Core` do
host: é assim que `IPlugin` é o **mesmo** tipo dos dois lados. Copiar `EveO.Core.dll` para dentro
da pasta de plugins é ignorado de propósito (criaria um segundo `IPlugin` e o cast falharia em
silêncio).

## Contrato

```csharp
public interface IPlugin
{
    string Name { get; }      // legível antes de Start — o host usa para a pasta de dados
    string Version { get; }
    void Start(IPluginContext context);
    void Stop();              // encerramento: soltar hook, timer, handle
}

public interface IPluginContext
{
    string DataDirectory { get; }  // <Plugins>\<Name> — caminho sugerido, quem cria é o plugin
    void Log(string message);      // vai para Plugins\plugins.log
}
```

Regras que o host garante:

| Situação | O que acontece |
|---|---|
| Pasta ausente | nada carrega, nada é escrito |
| DLL que não é .NET válida | linha `erro:` no log; os outros plugins carregam |
| Plugin lança em `Start` | linha `erro:` com o tipo e a mensagem; os outros carregam |
| Plugin lança em `Stop` | exceção engolida; o encerramento continua |
| `EveO.Core.dll` na pasta | ignorado |

Não há sandbox de permissões: um plugin roda no mesmo processo e com os mesmos direitos do app.
Instale plugin de fonte que você confia.

## Exemplo completo

`samples/EveO.Plugin.Sample` no repositório. O mínimo:

```csharp
using EveOPreview.Plugins;

public sealed class MeuPlugin : IPlugin
{
    private IPluginContext _context;

    public string Name => "Meu";
    public string Version => "0.1";

    public void Start(IPluginContext context)
    {
        _context = context;
        context.Log("iniciado");
    }

    public void Stop() => _context?.Log("parado");
}
```

Compile apontando para `EveO.Core.dll` da instalação e copie o `.dll` para `Plugins\`.

## O que ainda não existe

- UI de plugins (lista, habilitar/desabilitar, ver erros) — hoje o retrato é o `plugins.log`;
- sandbox, permissões e assinatura de plugin;
- API de verdade: o plugin recebe dados e um log, não o app. Expor thumbnails, configuração ou
  eventos é decisão futura, com demanda;
- versionamento do contrato (hoje é "compilou contra o `EveO.Core` desta versão, roda nesta");
- descarregamento **garantido**: o contexto é coletável e `Unload()` é chamado, mas o runtime só
  libera o assembly quando não sobra referência viva.

## Verificação

| Item | Onde |
|---|---|
| Carrega, inicia e para | `src/EveO.Core.Tests/Plugins/PluginLoaderTests.cs` |
| Log, pasta ausente e falha isolada | `src/EveO.Core.Tests/Plugins/PluginHostTests.cs` |
| Plugins de exemplo | `samples/EveO.Plugin.Sample`, `samples/EveO.Plugin.Broken` (fixture de erro) |
