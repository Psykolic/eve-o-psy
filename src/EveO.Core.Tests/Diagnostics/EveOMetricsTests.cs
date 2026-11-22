using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EveOPreview.Diagnostics;

using System;

namespace EveO.Core.Tests.Diagnostics;

/// <summary>
/// Item 1.5: métricas publicadas em System.Diagnostics.Metrics e um retrato local.
/// Roda no Linux porque a instrumentação mora no Core.
/// </summary>
[Collection("eveo-metrics")]
public sealed class EveOMetricsTests
{
    [Fact]
    public void RecordRefresh_AtualizaORetrato()
    {
        EveOMetrics.RecordRefresh(12.5, 7);

        var snapshot = EveOMetrics.Snapshot();

        Assert.Equal(12.5, snapshot.RefreshMs, 3);
        Assert.Equal(7, snapshot.ActiveThumbnails);
        Assert.True(snapshot.RefreshCount >= 1);
    }

    [Fact]
    public void SemProvedores_ReportaIndisponivel()
    {
        EveOMetrics.GdiHandleProvider = null;
        EveOMetrics.DwmHandleProvider = null;
        EveOMetrics.CpuUsageProvider = null;

        var snapshot = EveOMetrics.Snapshot();

        Assert.Equal(EveOMetrics.Unavailable, snapshot.GdiHandles);
        Assert.Equal(EveOMetrics.Unavailable, snapshot.DwmHandles);
        Assert.Equal(EveOMetrics.Unavailable, snapshot.CpuPercent);
    }

    [Fact]
    public void ProvedorQueLanca_NaoDerruba()
    {
        EveOMetrics.GdiHandleProvider = () => throw new InvalidOperationException("boom");

        var snapshot = EveOMetrics.Snapshot();

        Assert.Equal(EveOMetrics.Unavailable, snapshot.GdiHandles);
        EveOMetrics.GdiHandleProvider = null;
    }

    [Fact]
    public void LinhaDoRetrato_TemAsChavesEsperadas()
    {
        var line = EveOMetrics.Snapshot().ToLine();

        foreach (string chave in new[] { "ts=", "refresh_ms=", "refreshes=", "thumbnails=", "gdi=", "dwm=", "cpu_pct=" })
        {
            Assert.Contains(chave, line);
        }
    }

    [Fact]
    public void MeterListener_RecebeRefreshEInstrumentosObservaveis()
    {
        var duracoes = new List<double>();
        var thumbnails = new List<double>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == EveOMetrics.MeterName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<double>((instrumento, valor, _, _) =>
        {
            if (instrumento.Name == "thumbnail.refresh.duration")
            {
                duracoes.Add(valor);
            }
        });
        listener.SetMeasurementEventCallback<int>((instrumento, valor, _, _) =>
        {
            if (instrumento.Name == "thumbnail.count.active")
            {
                thumbnails.Add(valor);
            }
        });

        listener.Start();
        EveOMetrics.RecordRefresh(3.0, 6);
        listener.RecordObservableInstruments();

        Assert.Contains(duracoes, d => Math.Abs(d - 3.0) < 0.001);
        Assert.Contains(thumbnails, t => Math.Abs(t - 6) < 0.001);
    }
}
