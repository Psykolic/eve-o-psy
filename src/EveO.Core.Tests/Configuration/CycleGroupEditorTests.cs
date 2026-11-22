using System;
using System.Collections.Generic;
using System.Linq;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;

namespace EveO.Core.Tests.Configuration;

/// <summary>
/// Edição de grupos: define, limpa e avisa atalho repetido. É a base do editor da tela —
/// enquanto isso não existir, configurar grupo depende de editar o JSON na mão.
/// </summary>
public sealed class CycleGroupEditorTests
{
    [Fact]
    public void SetGroup_AplicaAtalhosEClientesNaOrdem()
    {
        var config = new ThumbnailConfiguration();

        CycleGroupEditor.SetGroup(config, 3,
            new[] { " Shift+F6 " },
            new[] { "Shift+F5" },
            new[] { "EVE - B", "EVE - A" });

        Assert.Equal(new[] { "Shift+F6" }, config.CycleGroup3ForwardHotkeys.ToArray());
        Assert.Equal(new[] { "Shift+F5" }, config.CycleGroup3BackwardHotkeys.ToArray());
        Assert.Equal(1, config.CycleGroup3ClientsOrder["EVE - B"]);
        Assert.Equal(2, config.CycleGroup3ClientsOrder["EVE - A"]);
    }

    [Fact]
    public void SetGroup_IgnoraAtalhoVazioERepetido()
    {
        var config = new ThumbnailConfiguration();

        CycleGroupEditor.SetGroup(config, 1,
            new[] { "F14", "", "  ", "f14" },
            Array.Empty<string>(),
            Array.Empty<string>());

        Assert.Equal(new[] { "F14" }, config.CycleGroup1ForwardHotkeys.ToArray());
        Assert.Empty(config.CycleGroup1BackwardHotkeys);
    }

    [Fact]
    public void ClearGroup_EsvaziaOGrupo()
    {
        var config = new ThumbnailConfiguration
        {
            CycleGroup2ForwardHotkeys = new List<string> { "F16" },
            CycleGroup2ClientsOrder = new Dictionary<string, int> { ["EVE - X"] = 1 }
        };

        CycleGroupEditor.ClearGroup(config, 2);

        Assert.Empty(config.CycleGroup2ForwardHotkeys);
        Assert.Empty(config.CycleGroup2ClientsOrder);
    }

    [Fact]
    public void ConflitoEntreGrupos_EhDetectado()
    {
        var config = new ThumbnailConfiguration();
        CycleGroupEditor.SetGroup(config, 1, new[] { "F14" }, new[] { "F13" }, new[] { "EVE - A" });

        var avisos = CycleGroupEditor.FindHotkeyConflicts(config, 2, new[] { "F14" }, new[] { "F12" });

        Assert.Single(avisos);
        Assert.Contains("F14", avisos[0]);
        Assert.Contains("grupo 1", avisos[0]);
    }

    [Fact]
    public void AtalhoRepetidoNoMesmoGrupo_EhDetectado()
    {
        var config = new ThumbnailConfiguration();

        var avisos = CycleGroupEditor.FindHotkeyConflicts(config, 1, new[] { "F9" }, new[] { "F9" });

        Assert.Single(avisos);
        Assert.Contains("repetido neste mesmo grupo", avisos[0]);
    }

    [Fact]
    public void SemConflito_NaoAvisa()
    {
        var config = new ThumbnailConfiguration();
        CycleGroupEditor.SetGroup(config, 1, new[] { "F14" }, new[] { "F13" }, new[] { "EVE - A" });

        Assert.Empty(CycleGroupEditor.FindHotkeyConflicts(config, 2, new[] { "Shift+F6" }, new[] { "Shift+F5" }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void GrupoForaDaFaixa_EhRecusado(int grupo)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CycleGroupEditor.SetGroup(new ThumbnailConfiguration(), grupo,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));
    }
}
