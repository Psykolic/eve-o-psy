using System.Collections.Generic;
using System.Linq;
using EveOPreview.Diagnostics;

namespace EveO.Core.Tests.Diagnostics;

/// <summary>
/// O refresh por evento (mudança de janela) precisa ser contado separado do tick de
/// reconciliação: com clientes em uso, é ele que faz o trabalho — e a métrica antiga só
/// enxergava a reconciliação.
/// </summary>
[Collection("eveo-metrics")]
public sealed class EveOMetricsEventoTests
{
    [Fact]
    public void RecordRefresh_SeparaReconciliacaoDeEvento()
    {
        var antes = EveOMetrics.Snapshot();

        EveOMetrics.RecordRefresh(5.0, 7, evento: true);
        EveOMetrics.RecordRefresh(6.0, 7, evento: true);
        EveOMetrics.RecordRefresh(7.0, 7);

        var depois = EveOMetrics.Snapshot();

        Assert.Equal(antes.EventRefreshes + 2, depois.EventRefreshes);
        Assert.Equal(antes.TimerRefreshes + 1, depois.TimerRefreshes);
        Assert.Equal(antes.RefreshCount + 3, depois.RefreshCount);
    }

    [Fact]
    public void LinhaDoRetrato_TrazOsDoisContadores()
    {
        EveOMetrics.RecordRefresh(1.0, 3, evento: true);

        string linha = EveOMetrics.Snapshot().ToLine();

        Assert.Contains("reconciliacao=", linha);
        Assert.Contains("eventos=", linha);
    }

    [Fact]
    public void Listener_RecebeTagDeMotivo()
    {
        var motivos = new List<string>();

        using var listener = new System.Diagnostics.Metrics.MeterListener
        {
            InstrumentPublished = (instrumento, l) =>
            {
                if (instrumento.Meter.Name == EveOMetrics.MeterName)
                {
                    l.EnableMeasurementEvents(instrumento);
                }
            },
        };

        listener.SetMeasurementEventCallback<double>((instrumento, valor, tags, _) =>
        {
            if (instrumento.Name == "thumbnail.refresh.duration")
            {
                foreach (var tag in tags)
                {
                    motivos.Add(tag.Key + "=" + tag.Value);
                }
            }
        });

        listener.Start();
        EveOMetrics.RecordRefresh(2.0, 5, evento: true);

        Assert.Contains("motivo=evento", motivos);
    }
}
