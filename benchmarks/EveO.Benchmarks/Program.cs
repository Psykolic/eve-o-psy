using System;
using System.Collections.Generic;
using System.Drawing;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using EveOPreview;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Services;

namespace EveO.Benchmarks;

/// <summary>
/// Benchmark dos caminhos quentes que rodam no Linux (o gate e o CI não têm DWM/WPF).
/// O que depende de Win32/DWM precisa ser medido no PC do mantenedor — ver README.
/// </summary>
[MemoryDiagnoser]
public class ProcessSetDiffBenchmarks
{
    private ProcessSetDiff _diff;
    private List<ProcessSetEntry> _observed;

    [GlobalSetup]
    public void Setup()
    {
        _diff = new ProcessSetDiff();
        _observed = new List<ProcessSetEntry>(10);
        for (int i = 0; i < 10; i++)
        {
            _observed.Add(new ProcessSetEntry(new IntPtr(1000 + i), "EVE - Client " + i));
        }

        _diff.Update(_observed);
    }

    [Benchmark]
    public object UpdateComMesmoConjunto() => _diff.Update(_observed);

    [Benchmark]
    public object LerConhecidos() => _diff.Known;
}

/// <summary>
/// Configuração por cliente: chamada a cada thumbnail em cada ciclo de refresh.
/// </summary>
[MemoryDiagnoser]
public class ConfigurationLookupBenchmarks
{
    private ThumbnailConfiguration _configuration;

    [GlobalSetup]
    public void Setup()
    {
        _configuration = new ThumbnailConfiguration();
        _configuration.SetClientLayout("EVE - Client 1",
            new ClientLayout(0, 0, 320, 180, false));
    }

    [Benchmark]
    public Size TamanhoPorCliente_ComSobreposicao() =>
        _configuration.GetThumbnailSize("EVE - Client 1", "EVE - Client 1", new Size(320, 180));

    [Benchmark]
    public Size TamanhoPorCliente_SemSobreposicao() =>
        _configuration.GetThumbnailSize("EVE - Desconhecido", "EVE - Client 1", new Size(320, 180));

    [Benchmark]
    public bool ClienteDesabilitado() => _configuration.IsThumbnailDisabled("EVE - Client 1");
}

/// <summary>
/// Cálculos puros de geometria: spotlight e limites de tamanho.
/// </summary>
[MemoryDiagnoser]
public class GeometryBenchmarks
{
    private readonly Rectangle _spotlight = new Rectangle(100, 100, 800, 600);
    private readonly Size _minimum = new Size(160, 90);
    private readonly Size _maximum = new Size(3840, 2160);

    [Benchmark]
    public Rectangle RedimensionarSpotlight() =>
        SpotlightResizeCalculator.Calculate(_spotlight, SpotlightResizeEdge.BottomRight, 20, 20,
            16.0 / 9.0, false, _minimum);

    [Benchmark]
    public Rectangle MapearSelecao() =>
        SpotlightRegionCalculator.MapSelection(_spotlight, new Rectangle(0, 0, 1920, 1080), new Size(400, 300));

    [Benchmark]
    public Size LimitarTamanho() => ThumbnailSizePolicy.ClampToLimits(new Size(5000, 3000), _minimum, _maximum);
}

public static class Program
{
    // Exporters e job vêm da linha de comando (scripts/bench.sh), para o baseline
    // e a comparação usarem sempre o mesmo formato.
    public static void Main(string[] args) => BenchmarkSwitcher
        .FromAssembly(typeof(Program).Assembly)
        .Run(args);
}
