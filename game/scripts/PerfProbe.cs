using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Замер настоящих кадров для --perf: цикл проверок в Main идёт по шагам физики
/// (60 в секунду), а кадры отрисовки считаются здесь, в _Process.
/// </summary>
public partial class PerfProbe : Node
{
    /// <summary>Первые кадры — подгрузка и компиляция шейдеров, их не считаем.</summary>
    public double WarmupSeconds { get; init; } = 1.0;

    /// <summary>Печатать каждый кадр дольше этого, мс; 0 — не печатать.</summary>
    public double SpikeMs { get; init; }

    private readonly List<double> _frameMs = new();
    private double _renderCpuMs, _gpuMs, _elapsed;

    public override void _Ready() =>
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed < WarmupSeconds) return;

        double ms = delta * 1000.0;
        _frameMs.Add(ms);
        Rid viewport = GetViewport().GetViewportRid();
        _renderCpuMs += RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport);
        _gpuMs += RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport);

        if (SpikeMs > 0 && ms > SpikeMs) GD.Print($"[perf] рывок: {ms:F1} мс на {Time.GetTicksMsec() / 1000.0:F2} с");
    }

    public void Report()
    {
        if (_frameMs.Count == 0) return;

        int n = _frameMs.Count;
        double total = 0;
        foreach (double ms in _frameMs) total += ms;

        var sorted = new List<double>(_frameMs);
        sorted.Sort();
        Vector2 size = GetViewport().GetVisibleRect().Size;

        GD.Print($"[perf] кадров отрисовки {n} при {size.X:F0}×{size.Y:F0}: в среднем {total / n:F2} мс, "
                 + $"рендер на процессоре {_renderCpuMs / n:F2} мс, видеокарта {_gpuMs / n:F2} мс");
        GD.Print($"[perf] рывки: 99 % кадров не дольше {sorted[(int)(n * 0.99)]:F2} мс, худший {sorted[^1]:F2} мс, "
                 + $"дольше 16,7 мс: {sorted.FindAll(ms => ms > 16.7).Count}");
    }
}
