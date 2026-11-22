using System.Collections.Generic;
using System.Linq;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;

namespace EveO.Core.Tests.Configuration;

/// <summary>
/// Resumo dos grupos que a tela mostra. Antes disso, a única forma de saber o que estava
/// configurado era abrir o JSON — e quem não acha o arquivo não configura nada.
/// </summary>
public sealed class CycleGroupSummaryTests
{
    [Fact]
    public void MontaOsCincoGrupos()
    {
        var summaries = CycleGroupSummaryBuilder.Build(new ThumbnailConfiguration());

        Assert.Equal(5, summaries.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, summaries.Select(s => s.Number).ToArray());
    }

    [Fact]
    public void ClientesSaemNaOrdemDeCiclagem()
    {
        var config = new ThumbnailConfiguration
        {
            CycleGroup1ClientsOrder = new Dictionary<string, int>
            {
                ["EVE - C"] = 3,
                ["EVE - A"] = 1,
                ["EVE - B"] = 2
            }
        };

        var grupo = CycleGroupSummaryBuilder.Build(config)[0];

        Assert.Equal(new[] { "EVE - A", "EVE - B", "EVE - C" }, grupo.Clients.ToArray());
    }

    [Fact]
    public void AtalhosVazios_ViramTextoSemAtalho()
    {
        var config = new ThumbnailConfiguration
        {
            CycleGroup3ForwardHotkeys = new List<string> { "" },
            CycleGroup3BackwardHotkeys = new List<string> { "" },
            CycleGroup3ClientsOrder = new Dictionary<string, int>()
        };

        var grupo = CycleGroupSummaryBuilder.Build(config)[2];

        Assert.True(grupo.IsEmpty);
        Assert.Contains("(sem atalho)", grupo.HotkeyText);
    }

    [Fact]
    public void AtalhosEAvancarEVoltar_AparecemNoTexto()
    {
        var config = new ThumbnailConfiguration
        {
            CycleGroup1ForwardHotkeys = new List<string> { "Shift+F6" },
            CycleGroup1BackwardHotkeys = new List<string> { "Shift+F5" }
        };

        var grupo = CycleGroupSummaryBuilder.Build(config)[0];

        Assert.Contains("avançar: Shift+F6", grupo.HotkeyText);
        Assert.Contains("voltar: Shift+F5", grupo.HotkeyText);
        Assert.False(grupo.IsEmpty);
    }

    [Fact]
    public void ConfiguracaoNula_NaoQuebra()
    {
        Assert.Empty(CycleGroupSummaryBuilder.Build(null));
    }
}
