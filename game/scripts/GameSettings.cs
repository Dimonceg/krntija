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
    public static float MouseSensitivity { get; set; } = 1.0f;
    public static bool Fullscreen { get; set; }

    public static void Load()
    {
        var config = new ConfigFile();
        if (config.Load(Path) != Error.Ok) return;

        MasterVolume = Mathf.Clamp((float)config.GetValue("audio", "master", 0.8).AsDouble(), 0f, 1f);
        MouseSensitivity = Mathf.Clamp((float)config.GetValue("input", "sensitivity", 1.0).AsDouble(), 0.2f, 3f);
        Fullscreen = config.GetValue("video", "fullscreen", false).AsBool();
    }

    public static void Save()
    {
        var config = new ConfigFile();
        config.SetValue("audio", "master", MasterVolume);
        config.SetValue("input", "sensitivity", MouseSensitivity);
        config.SetValue("video", "fullscreen", Fullscreen);
        config.Save(Path);
    }

    public static void Apply()
    {
        AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(Mathf.Max(MasterVolume, 0.0001f)));
        AudioServer.SetBusMute(0, MasterVolume <= 0.001f);

        DisplayServer.WindowSetMode(Fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }
}
