using System.Collections.Generic;
using System.Linq;
using EveOPreview.Configuration;

namespace EveO.Core.Tests.Configuration;

/// <summary>
/// A lista do diálogo precisa começar pelos clientes do grupo, na ordem salva — senão
/// salvar reordena tudo alfabeticamente (defeito observado em uso).
/// </summary>
public sealed class CycleGroupEditModelTests
{
    private static CycleGroupEditModel Modelo(IEnumerable<string> selecionados)
    {
        var clientes = new List<CycleGroupClient>
        {
            new CycleGroupClient("EVE - Alfa", true),
            new CycleGroupClient("EVE - Beta", false),
            new CycleGroupClient("EVE - Charlie", true),
            new CycleGroupClient("EVE - Delta", false)
        };

        return new CycleGroupEditModel(1, "F14", "F13", clientes, selecionados, null, null);
    }

    [Fact]
    public void SelecionadosVemPrimeiroNaOrdemSalva()
    {
        var modelo = Modelo(new[] { "EVE - Delta", "EVE - Alfa" });

        var lista = modelo.BuildDisplayList();

        Assert.Equal("EVE - Delta", lista[0].Title);
        Assert.Equal("EVE - Alfa", lista[1].Title);
        Assert.Equal(2, lista.Count(c => c.IsOpen));
    }

    [Fact]
    public void DemaisVemDepoisSemRepetir()
    {
        var modelo = Modelo(new[] { "EVE - Beta" });

        var lista = modelo.BuildDisplayList();

        Assert.Equal("EVE - Beta", lista[0].Title);
        Assert.Equal(4, lista.Count);
        Assert.Equal(4, lista.Select(c => c.Title).Distinct().Count());
    }

    [Fact]
    public void ClienteDaOrdemQueNaoEstaNaLista_Aparece()
    {
        var modelo = Modelo(new[] { "EVE - Sumiu" });

        var lista = modelo.BuildDisplayList();

        Assert.Equal("EVE - Sumiu", lista[0].Title);
        Assert.False(lista[0].IsOpen);
        Assert.Equal(5, lista.Count);
    }

    [Fact]
    public void GrupoVazio_MantemAListaOriginal()
    {
        var modelo = Modelo(Enumerable.Empty<string>());

        var lista = modelo.BuildDisplayList();

        Assert.Equal(4, lista.Count);
        Assert.Equal("EVE - Alfa", lista[0].Title);
    }
}
