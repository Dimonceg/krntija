using System;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Короткие звуки, синтезированные кодом: щелчок, хлопок двери, шаг,
/// переливание из канистры, шум дождя. Ни одного файла в репозитории.
/// </summary>
public static class SoundBank
{
    private const int Rate = 22050;

    public static AudioStreamWav Click() => Build(0.06f, (t, rng) =>
        MathF.Sin(Tau * 1500 * t) * MathF.Exp(-t * 55) * 0.35f);

    public static AudioStreamWav Door() => Build(0.35f, (t, rng) =>
    {
        float thump = MathF.Sin(Tau * 68 * t) * MathF.Exp(-t * 14) * 0.6f;
        float clank = (rng.NextSingle() * 2 - 1) * MathF.Exp(-t * 45) * 0.35f;
        float later = t > 0.12f ? (rng.NextSingle() * 2 - 1) * MathF.Exp(-(t - 0.12f) * 30) * 0.2f : 0;
        return thump + clank + later;
    });

    public static AudioStreamWav Step() => Build(0.13f, (t, rng) =>
    {
        float noise = (rng.NextSingle() * 2 - 1) * MathF.Exp(-t * 32) * 0.22f;
        float low = MathF.Sin(Tau * 110 * t) * MathF.Exp(-t * 26) * 0.12f;
        return noise + low;
    });

    public static AudioStreamWav Pour() => Build(1.1f, (t, rng) =>
    {
        float bubble = MathF.Sin(Tau * (180 + 40 * MathF.Sin(t * 9)) * t) * 0.08f;
        float hiss = (rng.NextSingle() * 2 - 1) * 0.12f;
        float env = MathF.Min(1f, t * 6f) * MathF.Exp(-t * 0.6f);
        return (bubble + hiss) * env;
    });

    public static AudioStreamWav WindLoop()
    {
        var rng = new Random(23);
        int count = Rate * 3;
        var samples = new float[count];
        float previous = 0;

        for (int i = 0; i < count; i++)
        {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            previous = previous * 0.94f + noise * 0.06f; // сильнее фильтр — глуше ветер
            float fade = MathF.Min(1f, MathF.Min(i, count - i) / (Rate * 0.2f));
            float gust = 0.6f + 0.4f * MathF.Sin(i / (float)Rate * 0.7f);
            samples[i] = previous * 1.6f * fade * gust;
        }

        return FromSamples(samples, loop: true);
    }

    public static AudioStreamWav RainLoop()
    {
        var rng = new Random(7);
        int count = Rate * 2;
        var samples = new float[count];
        float previous = 0;

        for (int i = 0; i < count; i++)
        {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            previous = previous * 0.72f + noise * 0.28f; // мягкий фильтр
            float fade = MathF.Min(1f, MathF.Min(i, count - i) / (Rate * 0.05f));
            samples[i] = previous * 0.55f * fade;
        }

        return FromSamples(samples, loop: true);
    }

    private const float Tau = MathF.PI * 2;

    private static AudioStreamWav Build(float seconds, Func<float, Random, float> generator)
    {
        var rng = new Random(1234);
        int count = (int)(Rate * seconds);
        var samples = new float[count];

        for (int i = 0; i < count; i++)
            samples[i] = generator(i / (float)Rate, rng);

        return FromSamples(samples, loop: false);
    }

    private static AudioStreamWav FromSamples(float[] samples, bool loop)
    {
        var bytes = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)(Math.Clamp(samples[i], -1f, 1f) * 30000);
            bytes[i * 2] = (byte)(value & 0xFF);
            bytes[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new AudioStreamWav
        {
            Data = bytes,
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Rate,
            Stereo = false,
            LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
            LoopBegin = 0,
            LoopEnd = samples.Length,
        };
    }
}
