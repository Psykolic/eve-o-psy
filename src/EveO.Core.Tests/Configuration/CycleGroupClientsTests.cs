using System.Linq;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;

namespace EveO.Core.Tests.Configuration;

/// <summary>
/// Lista oferecida no editor de grupos: união de config + clientes abertos, com os
/// abertos primeiro e marcados.
/// </summary>
public sealed class CycleGroupClientsTests
{
    [Fact]
    public void UneConfigEAbertos_SemRepetir()
    {
        var lista = CycleGroupClients.Build(
            new[] { "EVE - A", "EVE - B" },
            new[] { "EVE - B", "EVE - C" });

        Assert.Equal(3, lista.Count);
        Assert.Equal(new[] { "EVE - A", "EVE - B", "EVE - C" }, lista.Select(c => c.Title).OrderBy(t => t).ToArray());
    }

    [Fact]
    public void AbertosVemPrimeiroEMarcados()
    {
        var lista = CycleGroupClients.Build(
            new[] { "EVE - Zulu" },
            new[] { "EVE - Alfa" });

        Assert.Equal("EVE - Alfa", lista[0].Title);
        Assert.True(lista[0].IsOpen);
        Assert.False(lista[1].IsOpen);
        Assert.Contains("●", lista[0].ToString());
        Assert.DoesNotContain("●", lista[1].ToString());
    }

    [Fact]
    public void IgnoraNomesVazios()
    {
        var lista = CycleGroupClients.Build(new[] { "", "  ", "EVE - A" }, new[] { "", null });

        Assert.Single(lista);
    }

    [Fact]
    public void ListaVazia_NaoQuebra()
    {
        Assert.Empty(CycleGroupClients.Build(null, null));
    }

    [Fact]
    public void ConfigConheceClientesDosGruposEFlatLayout()
    {
        var config = new ThumbnailConfiguration();
        CycleGroupEditor.SetGroup(config, 1, new[] { "F14" }, new[] { "F13" }, new[] { "EVE - Grupo" });
        config.SetClientLayout("EVE - Layout", new ClientLayout(0, 0, 100, 100, false));

        var conhecidos = config.GetKnownClients();

        Assert.Contains("EVE - Grupo", conhecidos);
        Assert.Contains("EVE - Layout", conhecidos);
    }
}
