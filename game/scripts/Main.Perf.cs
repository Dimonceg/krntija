using System;
using System.Linq;
using Godot;

namespace Krntija.Game;

/// <summary>
/// --perf: куда уходит кадр. Кроме среднего FPS — время рендера на процессоре и на
/// видеокарте и рывки (<see cref="PerfProbe"/>). Разрешение задаёт движок:
/// `--resolution 1920x1080` перед `--`; окно по умолчанию 1152×648 сильно занижает
/// цену видеокарты. --perf-off=shadows,ssao,glow,fog,multimesh выключает по одному
/// виду нагрузки, чтобы увидеть его цену; --perf-spikes печатает долгие кадры.
/// --perf-car — машина стоит, камера в три четверти рядом (цена модели машины);
/// с --open=all открыты двери, капот и багажник.
/// </summary>
public partial class Main
{
    private PerfProbe? _perfProbe;
    private string[] _perfOff = Array.Empty<string>();
    private (int, int, int) _perfStreamState;

    /// <summary>Шаг цикла проверок (идёт по физике, 60 раз в секунду).</summary>
    private void PerfSample()
    {
        if (_perfProbe is null)
        {
            _perfProbe = new PerfProbe { SpikeMs = HasArg("--perf-spikes") ? 12 : 0 };
            AddChild(_perfProbe);
            _perfOff = (ArgValue("--perf-off=") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (_perfOff.Length > 0) GD.Print($"[perf] выключено: {string.Join(", ", _perfOff)}");
            if (HasArg("--perf-car")) SetupPerfCar();
        }

        if (HasArg("--perf-spikes") && _streamer is not null
            && (_streamer.CurrentChunk, _streamer.Shifts, _streamer.LoadedChunks) is var state && state != _perfStreamState)
        {
            GD.Print($"[perf] куски: текущий {state.Item1}, сдвигов {state.Item2}, загружено {state.Item3} на {Time.GetTicksMsec() / 1000.0:F2} с");
            _perfStreamState = state;
        }

        // Куски подгружаются на ходу: выключенное держим выключенным.
        if (_frames % 30 == 1) ApplyPerfOff();
    }

    /// <summary>Машина стоит на ручнике, камера в три четверти — кадр почти целиком машина.</summary>
    private void SetupPerfCar()
    {
        _car.Autopilot = false;
        _car.SetHandbrake(true);
        _car.Session.Clock.SetTotalMinutes(12 * 60);

        if (ArgValue("--open=") == "all" && Krntija.Core.CarLayout.For(_car.Session.Vehicle.Spec.Id) is { } layout)
            foreach (var slot in layout.Slots)
                if (slot.Opens) Krntija.Core.AssemblyRules.SetOpen(_car.Session.Vehicle, slot.Slot, true);

        ShotCarCamera();
        GD.Print($"[perf] машина крупно{(ArgValue("--open=") == "all" ? ", панели открыты" : string.Empty)}");
    }

    private void PerfReport()
    {
        _perfProbe?.Report();
        GD.Print($"[perf] проехано {_car.Session.Vehicle.OdometerKm * 1000:F0} м, шагов постройки кусков {_streamer?.StepsBuilt ?? 0}, текущий кусок {_streamer?.CurrentChunk ?? 0}");
        GD.Print($"[perf] треугольников {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame):F0}, "
                 + $"вызовов отрисовки {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):F0}, "
                 + $"текстуры {Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed) / (1024 * 1024):F1} МБ, "
                 + $"буферы {Performance.GetMonitor(Performance.Monitor.RenderBufferMemUsed) / (1024 * 1024):F1} МБ");
    }

    private void ApplyPerfOff()
    {
        foreach (string what in _perfOff)
        {
            switch (what)
            {
                case "shadows" when _sun is not null: _sun.ShadowEnabled = false; break;
                case "ssao" when _environment is not null: _environment.SsaoEnabled = false; break;
                case "glow" when _environment is not null: _environment.GlowEnabled = false; break;
                case "fog" when _environment is not null: _environment.FogEnabled = false; break;
                case "multimesh":
                    foreach (var node in FindChildren("*", nameof(MultiMeshInstance3D), true, false).OfType<MultiMeshInstance3D>())
                        node.Visible = false;
                    break;
            }
        }
    }
}
