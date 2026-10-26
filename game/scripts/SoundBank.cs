using System;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Звуки, синтезированные кодом: щелчок, хлопок двери, шаг, переливание,
/// циклы дождя, ветра, птиц, сверчков. Записи CC0 — <see cref="SoundLibrary"/>.
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

    /// <summary>
    /// Птицы днём (20 с, цикл): редкие трели нескольких «видов» — серии нот со
    /// скольжением частоты 2–6 кГц, ближние громче, дальние тише. События не
    /// пересекают конец цикла, поэтому стык не слышен.
    /// </summary>
    public static AudioStreamWav BirdsLoop()
    {
        const float seconds = 20f;
        var rng = new Random(31);
        var samples = new float[(int)(Rate * seconds)];

        float t = 0.4f;
        while (t < seconds - 2.5f)
        {
            float near = rng.NextSingle() < 0.25f ? 1f : 0.35f;
            float baseHz = 2200 + rng.NextSingle() * 2800;
            int notes = 2 + rng.Next(5);
            float noteLength = 0.05f + rng.NextSingle() * 0.08f;
            float sweep = (rng.NextSingle() - 0.4f) * 1600;

            for (int n = 0; n < notes; n++)
            {
                float start = t + n * noteLength * 1.6f;
                float hz = baseHz * (1 + 0.08f * MathF.Sin(n * 1.7f));
                AddNote(samples, start, noteLength, hz, sweep, 0.16f * near);
            }

            t += notes * noteLength * 1.6f + 0.3f + rng.NextSingle() * 2.2f;
        }

        return FromSamples(samples, loop: true);
    }

    /// <summary>
    /// Сверчки ночью (15 с, цикл): несколько сверчков на несущей 4–5 кГц, трель —
    /// три импульса по 15 мс, у каждого свой ритм; плюс тихий дальний «хор».
    /// </summary>
    public static AudioStreamWav CricketsLoop()
    {
        const float seconds = 15f;
        var rng = new Random(47);
        int count = (int)(Rate * seconds);
        var samples = new float[count];

        for (int cricket = 0; cricket < 4; cricket++)
        {
            float hz = 4200 + rng.NextSingle() * 900;
            float period = 0.45f + rng.NextSingle() * 0.4f;
            float level = cricket == 0 ? 0.12f : 0.05f + rng.NextSingle() * 0.04f;

            for (float chirp = rng.NextSingle() * period; chirp < seconds - 0.2f; chirp += period * (0.9f + 0.2f * rng.NextSingle()))
                for (int pulse = 0; pulse < 3; pulse++)
                    AddNote(samples, chirp + pulse * 0.03f, 0.015f, hz, 0, level);
        }

        // Дальний хор: узкий шум у 4,5 кГц, медленно дышит (период кратен циклу).
        float low = 0, band = 0;
        for (int i = 0; i < count; i++)
        {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            low += (noise - low) * 0.6f;
            band += (low - band) * 0.35f;
            float breath = 0.6f + 0.4f * MathF.Sin(Tau * 3 * i / count);
            samples[i] += (low - band) * 0.03f * breath;
        }

        return FromSamples(samples, loop: true);
    }

    /// <summary>
    /// Ветер в деревьях (12 с, цикл): шелест листвы — шум в средне-высокой полосе
    /// с порывами; частоты порывов кратны длине цикла, стык не слышен.
    /// </summary>
    public static AudioStreamWav WindTreesLoop()
    {
        const float seconds = 12f;
        var rng = new Random(59);
        int count = (int)(Rate * seconds);
        var samples = new float[count];
        float fast = 0, slow = 0;

        for (int i = 0; i < count; i++)
        {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            fast += (noise - fast) * 0.5f;
            slow += (fast - slow) * 0.08f;
            float phase = (float)i / count;
            float gust = 0.45f + 0.3f * MathF.Sin(Tau * 2 * phase) + 0.2f * MathF.Sin(Tau * 5 * phase + 1.3f)
                         + 0.05f * MathF.Sin(Tau * 11 * phase + 0.4f);
            samples[i] = (fast - slow) * 0.5f * gust;
        }

        return FromSamples(samples, loop: true);
    }

    /// <summary>Нота птицы или сверчка: синус со скольжением частоты и мягкой огибающей.</summary>
    private static void AddNote(float[] samples, float start, float length, float hz, float sweepHz, float level)
    {
        int from = (int)(start * Rate);
        int to = Math.Min(samples.Length, (int)((start + length) * Rate));
        float phase = 0;

        for (int i = Math.Max(0, from); i < to; i++)
        {
            float k = (i - from) / (float)(to - from);
            phase += Tau * (hz + sweepHz * k) / Rate;
            float envelope = MathF.Sin(MathF.PI * k);
            samples[i] += MathF.Sin(phase) * envelope * envelope * level;
        }
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
