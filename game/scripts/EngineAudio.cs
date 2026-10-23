using System;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Синтезированный звук двигателя: пила + гармоники, частота от оборотов.
/// Без ассетов — важно для прототипа и для того, чтобы не тащить в репозиторий
/// чужие сэмплы.
/// </summary>
public partial class EngineAudio : AudioStreamPlayer3D
{
    private const int MixRate = 22050;

    private AudioStreamGeneratorPlayback? _playback;
    private double _phase;
    private double _phase2;

    public double Rpm { get; set; } = 800;
    public double SpeedMps { get; set; }
    public bool Running { get; set; }
    public double Volume { get; set; } = 0.35;

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
    {        if (_playback is null) return;

        int frames = Mathf.Min(_playback.GetFramesAvailable(), 4096);
        if (frames <= 0) return;

        // Частота вспышек четырёхцилиндрового мотора: rpm/60 * 2.
        double baseHz = Mathf.Clamp((float)(Rpm / 30.0), 20f, 400f);
        double gain = Running ? Volume : 0.0;
        double noise = Math.Clamp((float)(SpeedMps / 30.0), 0f, 1f) * 0.05;

        for (int i = 0; i < frames; i++)
        {
            double step = baseHz / MixRate;
            _phase += step;
            if (_phase >= 1) _phase -= 1;
            _phase2 += step * 0.5;
            if (_phase2 >= 1) _phase2 -= 1;

            // Пила + октава ниже = грубый старый мотор.
            double saw = 2 * _phase - 1;
            double sub = 2 * _phase2 - 1;
            double sample = (saw * 0.6 + sub * 0.4) * gain;

            // Шум дороги.
            sample += ((GD.Randf() * 2f - 1f) * noise);

            sample = Mathf.Clamp((float)sample, -1f, 1f);
            _playback.PushFrame(new Vector2((float)sample, (float)sample));
        }
    }
}
