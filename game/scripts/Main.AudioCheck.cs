using System;
using System.Linq;
using Godot;

namespace Krntija.Game;

/// <summary>
/// --check-audio (docs/AUDIO.md, S1): шины на месте, у каждого id есть записи CC0,
/// циклы природы не молчат (и сохраняются в user://ambience_*.wav — послушать),
/// пул голосов не растёт сверх <see cref="Sfx.MaxVoices"/>.
/// </summary>
public partial class Main
{
    private bool _checkAudio;

    private async void RunAudioCheck()
    {
        _selfTestOk = true;

        foreach (string bus in GameSettings.Buses)
            Check(AudioServer.GetBusIndex(bus) > 0, $"шина {bus} есть");

        var withoutFiles = SoundIds.Groups.Keys.Where(id => !SoundLibrary.HasFiles(id)).ToArray();
        Check(withoutFiles.Length == 0, $"у всех {SoundIds.Groups.Count} id есть записи (нет: {string.Join(", ", withoutFiles)})");
        Check(SoundIds.Groups.Keys.All(id => SoundLibrary.Variants(id).Length > 0), "каждый id даёт хотя бы один звук");

        foreach (var (id, loop) in new[]
                 {
                     (SoundIds.Birds, SoundBank.BirdsLoop()), (SoundIds.Crickets, SoundBank.CricketsLoop()),
                     (SoundIds.WindTrees, SoundBank.WindTreesLoop()),
                 })
        {
            double rms = Rms(loop.Data);
            Check(rms > 0.003 && rms < 0.4, $"цикл {id} слышен и не перегружен (RMS {rms:F3})");

            // Синтез ушами не проверить: сохраняем, чтобы послушал человек.
            string path = ProjectSettings.GlobalizePath($"user://ambience_{id}.wav");
            loop.SaveToWav(path);
            loop.Dispose();
        }

        // Пул: тридцать звуков разом — голосов не больше MaxVoices, счётчик растёт.
        Vector3 here = _car.GlobalPosition;
        for (int i = 0; i < 30; i++) Sfx.Play("metal_light", here);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(Sfx.ActiveVoices <= Sfx.MaxVoices && Sfx.ActiveVoices > 0, $"голосов звучит {Sfx.ActiveVoices} из {Sfx.MaxVoices}");
        Check(Sfx.PlayCount("metal_light") == 30, $"счётчик звуков: {Sfx.PlayCount("metal_light")}");

        Sfx.Ambience(SoundIds.Crickets, 0.8f);
        Check(Sfx.AmbienceVolumeDb(SoundIds.Crickets) is > -40f, "цикл сверчков включается");
        Sfx.Ambience(SoundIds.Crickets, 0f);
        Check(Sfx.AmbienceVolumeDb(SoundIds.Crickets) is null, "и выключается");

        GD.Print(_selfTestOk ? "[audio] OK" : "[audio] FAIL");
        QuitSoon(_selfTestOk ? 0 : 1);
    }

    /// <summary>Средняя громкость 16-битных моно-данных, 0…1.</summary>
    private static double Rms(byte[] data)
    {
        double sum = 0;
        int count = data.Length / 2;
        for (int i = 0; i < count; i++)
        {
            double sample = BitConverter.ToInt16(data, i * 2) / 32768.0;
            sum += sample * sample;
        }

        return count == 0 ? 0 : Math.Sqrt(sum / count);
    }
}
