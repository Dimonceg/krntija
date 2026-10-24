using System;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Синтезированный звук двигателя: пила с гармониками, «выхлоп» на сбросе газа
/// и шум дороги. Без ассетов — важно для прототипа и для того, чтобы не тащить
/// в репозиторий чужие сэмплы.
/// </summary>
public partial class EngineAudio : AudioStreamPlayer3D
{
    private const int MixRate = 22050;

    private AudioStreamGeneratorPlayback? _playback;
    private double _phase;
    private double _popLeft;

    public double Rpm { get; set; } = 800;
    public double SpeedMps { get; set; }
    public double Load { get; set; }
    public bool Running { get; set; }
    public double Volume { get; set; } = 0.32;

    public override void _Ready()
    {
        Stream = new AudioStreamGenerator { MixRate = MixRate, BufferLength = 0.12f };
        UnitSize = 8f;
        MaxDistance = 60f;
        Play();
        _playback = GetStreamPlayback() as AudioStreamGeneratorPlayback;
    }

    public override void _ExitTree()
    {
        Stop();
        _playback = null;
        Stream = null;
    }

    public override void _Process(double delta)
    {
        if (_playback is null) return;

        int frames = Mathf.Min(_playback.GetFramesAvailable(), 4096);
        if (frames <= 0) return;

        // Частота вспышек четырёхцилиндрового мотора: rpm/60 * 2.
        double baseHz = Math.Clamp((float)(Rpm / 30.0), 20f, 400f);
        double load = Math.Clamp((float)Load, 0f, 1f);
        double gain = Running ? Volume * (0.5 + 0.5 * load) : 0.0;
        double roadNoise = Math.Clamp((float)(SpeedMps / 30.0), 0f, 1f) * 0.045;

        // Сброс газа на оборотах — короткий хлопок выхлопа.
        if (Running && load < 0.05 && Rpm > 2500 && GD.Randf() < 0.03f)
            _popLeft = 0.05;

        for (int i = 0; i < frames; i++)
        {
            double step = baseHz / MixRate;
            _phase += step;
            if (_phase >= 1) _phase -= 1;

            double saw1 = 2 * _phase - 1;
            double saw2 = 2 * (_phase * 2 % 1) - 1;
            double saw3 = 2 * (_phase * 3 % 1) - 1;

            // Чем больше газ, тем ярче тембр: гармоники подмешиваются по нагрузке.
            double sample = saw1 * 0.5
                            + saw2 * (0.18 + 0.22 * load)
                            + saw3 * (0.05 + 0.12 * load);

            // Хлопок выхлопа.
            if (_popLeft > 0)
            {
                double fade = _popLeft / 0.05;
                sample += (GD.Randf() * 2f - 1f) * 0.5 * fade;
                _popLeft -= 1.0 / MixRate;
            }

            sample = sample * gain + (GD.Randf() * 2f - 1f) * roadNoise;
            sample = Mathf.Clamp((float)sample, -1f, 1f);

            _playback.PushFrame(new Vector2((float)sample, (float)sample));
        }
    }
}
