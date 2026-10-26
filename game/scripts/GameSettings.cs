using Godot;

namespace Krntija.Game;

/// <summary>
/// Настройки игры: громкость, чувствительность мыши, полный экран.
/// Хранятся в user://settings.cfg, чтобы не терялись между запусками.
/// </summary>
public static class GameSettings
{
    private const string Path = "user://settings.cfg";

    public static float MasterVolume { get; set; } = 0.8f;

    /// <summary>Громкость шин (docs/AUDIO.md): двигатель, мир, действия, интерфейс; 0…1.</summary>
    public static readonly string[] Buses = { "Engine", "World", "Sfx", "Ui" };

    public static float[] BusVolumes { get; } = { 1f, 1f, 1f, 1f };
    public static float MouseSensitivity { get; set; } = 1.0f;
    public static bool Fullscreen { get; set; }

    public static void Load()
    {
        var config = new ConfigFile();
        if (config.Load(Path) != Error.Ok) return;

        MasterVolume = Mathf.Clamp((float)config.GetValue("audio", "master", 0.8).AsDouble(), 0f, 1f);
        for (int i = 0; i < Buses.Length; i++)
            BusVolumes[i] = Mathf.Clamp((float)config.GetValue("audio", Buses[i].ToLowerInvariant(), 1.0).AsDouble(), 0f, 1f);
        MouseSensitivity = Mathf.Clamp((float)config.GetValue("input", "sensitivity", 1.0).AsDouble(), 0.2f, 3f);
        Fullscreen = config.GetValue("video", "fullscreen", false).AsBool();
    }

    public static void Save()
    {
        var config = new ConfigFile();
        config.SetValue("audio", "master", MasterVolume);
        for (int i = 0; i < Buses.Length; i++) config.SetValue("audio", Buses[i].ToLowerInvariant(), BusVolumes[i]);
        config.SetValue("input", "sensitivity", MouseSensitivity);
        config.SetValue("video", "fullscreen", Fullscreen);
        config.Save(Path);
    }

    private static void SetBus(int bus, float volume)
    {
        AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Mathf.Max(volume, 0.0001f)));
        AudioServer.SetBusMute(bus, volume <= 0.001f);
    }

    public static void Apply()
    {
        SetBus(0, MasterVolume);
        for (int i = 0; i < Buses.Length; i++)
            if (AudioServer.GetBusIndex(Buses[i]) is var bus and >= 0) SetBus(bus, BusVolumes[i]);

        DisplayServer.WindowSetMode(Fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }
}
