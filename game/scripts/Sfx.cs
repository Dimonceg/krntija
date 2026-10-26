using System;
using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Проигрывание звуков (docs/AUDIO.md): один узел на игру. Короткие звуки — из
/// пула голосов по id (<see cref="SoundLibrary"/>, случайный вариант и высота),
/// фон природы — циклы с громкостью от 0 до 1. Работает и на паузе (меню).
/// </summary>
public partial class Sfx : Node
{
    public static Sfx? Instance { get; private set; }

    /// <summary>Сколько коротких 3D-звуков звучит одновременно.</summary>
    public const int MaxVoices = 24;

    /// <summary>Дальше этого от слушателя короткий звук не играется, метры.</summary>
    public const float MaxDistance = 40f;

    private readonly List<AudioStreamPlayer3D> _voices = new();
    private readonly List<AudioStreamPlayer> _flat = new();
    private readonly Dictionary<string, AudioStreamPlayer> _ambience = new();
    private readonly Dictionary<string, int> _played = new(StringComparer.Ordinal);
    private readonly Random _random = new(20261026);
    private int _nextVoice, _nextFlat;

    private AudioStreamPlayer _ui = null!;
    private AudioStreamPlayer _rain = null!;
    private AudioStreamPlayer _wind = null!;
    private AudioStreamPlayer3D _pour = null!;
    private AudioStreamPlayer3D _door = null!;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        for (int i = 0; i < MaxVoices; i++)
        {
            var voice = new AudioStreamPlayer3D
            {
                Name = $"Voice{i}", Bus = "Sfx", MaxDistance = MaxDistance, UnitSize = 4f,
            };
            AddChild(voice);
            _voices.Add(voice);
        }

        for (int i = 0; i < 4; i++)
        {
            var flat = new AudioStreamPlayer { Name = $"Flat{i}", Bus = "Sfx" };
            AddChild(flat);
            _flat.Add(flat);
        }

        _ui = Create2D(SoundBank.Click(), "Ui", "Ui");
        _pour = Create3D(SoundBank.Pour(), "Pour");
        _door = Create3D(SoundBank.Door(), "Door");

        _rain = Create2D(SoundBank.RainLoop(), "Rain", "World");
        _rain.VolumeDb = -60f;
        _wind = Create2D(SoundBank.WindLoop(), "Wind", "World");
        _wind.VolumeDb = -60f;
        _wind.Play();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
        base._ExitTree();
    }

    // ------------------------------------------------------------ API (docs/AUDIO.md)

    /// <summary>Звук в точке мира: случайный вариант, высота ±6 %. Дальше 40 м — тишина.</summary>
    public static void Play(string id, Vector3 position, float volumeDb = 0f)
    {
        if (Instance is not { } sfx) return;
        sfx.Count(id);

        if (sfx.GetViewport()?.GetCamera3D() is { } camera
            && camera.GlobalPosition.DistanceTo(position) > MaxDistance) return;

        var voice = sfx.NextVoice();
        voice.Stream = sfx.Pick(id);
        voice.GlobalPosition = position;
        voice.VolumeDb = volumeDb;
        voice.PitchScale = sfx.Pitch();
        voice.Play();
    }

    /// <summary>Звук без места (деньги, рюкзак): слышен одинаково.</summary>
    public static void Play(string id, float volumeDb = 0f)
    {
        if (Instance is not { } sfx) return;
        sfx.Count(id);

        var flat = sfx._flat[sfx._nextFlat];
        sfx._nextFlat = (sfx._nextFlat + 1) % sfx._flat.Count;
        flat.Stream = sfx.Pick(id);
        flat.VolumeDb = volumeDb;
        flat.PitchScale = sfx.Pitch();
        flat.Play();
    }

    /// <summary>Шаг по поверхности.</summary>
    public static void Step(SurfaceKind surface, Vector3 position, float volumeDb = -4f) =>
        Play(SoundIds.Step(surface), position, volumeDb);

    /// <summary>Цикл природы (<see cref="SoundIds.Birds"/>, …): 0 — выключен, 1 — полная громкость.</summary>
    public static void Ambience(string id, float intensity)
    {
        if (Instance is not { } sfx) return;

        float level = Mathf.Clamp(intensity, 0f, 1f);
        if (!sfx._ambience.TryGetValue(id, out var player))
        {
            if (level < 0.02f) return;
            player = sfx.Create2D(AmbienceLoop(id), $"Ambience_{id}", "World");
            sfx._ambience[id] = player;
        }

        player.VolumeDb = level < 0.02f ? -60f : Mathf.LinearToDb(0.3f * level + 0.001f);
        if (level >= 0.02f && !player.Playing) player.Play();
        else if (level < 0.02f && player.Playing) player.Stop();
    }

    /// <summary>Сколько раз звучал id с начала игры — для проверок.</summary>
    public static int PlayCount(string id) => Instance?._played.GetValueOrDefault(id) ?? 0;

    /// <summary>Сколько коротких 3D-голосов звучит сейчас — для проверок.</summary>
    public static int ActiveVoices => Instance?._voices.FindAll(voice => voice.Playing).Count ?? 0;

    /// <summary>Играет ли цикл природы и с какой громкостью (дБ) — для проверок.</summary>
    public static float? AmbienceVolumeDb(string id) =>
        Instance is { } sfx && sfx._ambience.TryGetValue(id, out var player) && player.Playing ? player.VolumeDb : null;

    // ------------------------------------------------------------ прежние вызовы

    public static void PlayUi() => Instance?._ui.Play();

    public static void PlayDoor(Vector3 position)
    {
        if (Instance is null) return;
        Instance._door.GlobalPosition = position;
        Instance._door.Play();
    }

    /// <summary>Шаг без знания поверхности — бетон; поверхность подставляет S3.</summary>
    public static void PlayStep(Vector3 position) => Step(SurfaceKind.Concrete, position);

    public static void PlayPour(Vector3 position)
    {
        if (Instance is null) return;
        Instance._pour.GlobalPosition = position;
        Instance._pour.Play();
    }

    public static void SetWind(float intensity)
    {
        if (Instance is null) return;

        float clamped = Mathf.Clamp(intensity, 0f, 1f);
        Instance._wind.VolumeDb = clamped < 0.02f ? -60f : Mathf.LinearToDb(0.18f * clamped + 0.001f);
    }

    public static void SetRain(float intensity)
    {
        if (Instance is null) return;

        float clamped = Mathf.Clamp(intensity, 0f, 1f);
        Instance._rain.VolumeDb = clamped < 0.02f ? -60f : Mathf.LinearToDb(0.25f * clamped + 0.001f);
        if (clamped > 0.02f && !Instance._rain.Playing) Instance._rain.Play();
    }

    // ------------------------------------------------------------ внутреннее

    private static AudioStream AmbienceLoop(string id) => id switch
    {
        SoundIds.Birds => SoundBank.BirdsLoop(),
        SoundIds.Crickets => SoundBank.CricketsLoop(),
        SoundIds.WindTrees => SoundBank.WindTreesLoop(),
        _ => SoundBank.WindLoop(),
    };

    private void Count(string id) => _played[id] = _played.GetValueOrDefault(id) + 1;

    private AudioStream Pick(string id)
    {
        var variants = SoundLibrary.Variants(id);
        return variants[_random.Next(variants.Length)];
    }

    private float Pitch() => 0.94f + (float)_random.NextDouble() * 0.12f;

    /// <summary>Свободный голос, а если все заняты — самый давний (по кругу).</summary>
    private AudioStreamPlayer3D NextVoice()
    {
        for (int i = 0; i < _voices.Count; i++)
        {
            var voice = _voices[(_nextVoice + i) % _voices.Count];
            if (voice.Playing) continue;
            _nextVoice = (_nextVoice + i + 1) % _voices.Count;
            return voice;
        }

        var oldest = _voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % _voices.Count;
        return oldest;
    }

    private AudioStreamPlayer Create2D(AudioStream stream, string name, string bus)
    {
        var player = new AudioStreamPlayer { Stream = stream, Name = name, Bus = bus };
        AddChild(player);
        return player;
    }

    private AudioStreamPlayer3D Create3D(AudioStream stream, string name)
    {
        var player = new AudioStreamPlayer3D
        {
            Stream = stream, Name = name, Bus = "Sfx", MaxDistance = MaxDistance, UnitSize = 4f,
        };
        AddChild(player);
        return player;
    }
}
