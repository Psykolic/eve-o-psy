using System;
using System.IO;
using System;

namespace EveO.Core.Tests.Diagnostics;

using EveOPreview.Diagnostics;

/// <summary>
/// O retrato é gravado no máximo uma vez por intervalo e nunca lança —
/// métrica não pode derrubar o aplicativo.
/// </summary>
public sealed class MetricsSnapshotWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "eveo-metrics-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void EscreveQuandoVenceOIntervalo()
    {
        string arquivo = Path.Combine(_dir, "metrics.log");
        var writer = new MetricsSnapshotWriter(arquivo, TimeSpan.FromSeconds(10));
        var agora = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(writer.WriteIfDue(agora), "primeira escrita deveria acontecer");
        Assert.False(writer.WriteIfDue(agora.AddSeconds(5)), "antes do intervalo não escreve");
        Assert.True(writer.WriteIfDue(agora.AddSeconds(11)), "depois do intervalo escreve");

        string[] linhas = File.ReadAllLines(arquivo);
        Assert.Equal(2, linhas.Length);
        Assert.Contains("refresh_ms=", linhas[0]);
    }

    [Fact]
    public void CaminhoInvalido_NaoLanca()
    {
        var writer = new MetricsSnapshotWriter("\0invalido", TimeSpan.FromSeconds(1));

        Assert.False(writer.WriteIfDue(DateTime.UtcNow));
    }

    [Fact]
    public void IntervaloNaoPositivo_EhRecusado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MetricsSnapshotWriter("x.log", TimeSpan.Zero));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }
}
