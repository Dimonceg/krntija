using System;
using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

/// <summary>Поверхность под ногами: от неё зависит звук шага.</summary>
public enum SurfaceKind
{
    Concrete,
    Grass,
    Wood,
    Gravel,
    Carpet,
}

/// <summary>
/// Все id звуков и их группы (папки в <c>game/assets/audio</c>) — одно место
/// (docs/AUDIO.md). Новый звук — строка здесь и файлы <c>&lt;id&gt;_&lt;n&gt;.ogg</c>.
/// </summary>
public static class SoundIds
{
    public static readonly IReadOnlyDictionary<string, string> Groups = Build();

    // Циклы природы и погоды — синтез (SoundBank), файлов нет.
    public const string Birds = "birds";
    public const string Crickets = "crickets";
    public const string WindTrees = "wind_trees";

    public static string Step(SurfaceKind surface) => "step_" + surface.ToString().ToLowerInvariant();

    private static Dictionary<string, string> Build()
    {
        var groups = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string material in new[] { "metal", "panel", "wood", "glass" })
            foreach (string weight in new[] { "heavy", "medium", "light" })
                groups[$"{material}_{weight}"] = "impact";

        foreach (string id in new[] { "soft_medium", "soft_heavy", "tin", "generic_light", "plank" })
            groups[id] = "impact";

        foreach (SurfaceKind surface in Enum.GetValues<SurfaceKind>())
            groups[Step(surface)] = "steps";

        foreach (string id in new[] { "door_open", "door_close", "creak", "coins", "cloth", "pot",
                     "metal_click", "metal_latch", "drop_soft", "belt" })
            groups[id] = "foley";

        return groups;
    }
}

/// <summary>
/// Загрузка звуков по id: все варианты <c>&lt;группа&gt;/&lt;id&gt;_&lt;n&gt;.ogg</c>. Нет файла —
/// синтезированная замена из <see cref="SoundBank"/> и одна строка в лог: игра не молчит.
/// </summary>
public static class SoundLibrary
{
    private static readonly Dictionary<string, AudioStream[]> Cache = new();
    private static readonly HashSet<string> Missing = new();

    /// <summary>Варианты звука; всегда хотя бы один.</summary>
    public static AudioStream[] Variants(string id)
    {
        if (Cache.TryGetValue(id, out var cached)) return cached;

        var list = new List<AudioStream>();
        if (SoundIds.Groups.TryGetValue(id, out string? group))
        {
            for (int n = 0; ; n++)
            {
                string path = $"res://assets/audio/{group}/{id}_{n}.ogg";
                if (!ResourceLoader.Exists(path)) break;
                if (ResourceLoader.Load<AudioStream>(path) is { } stream) list.Add(stream);
            }
        }

        if (list.Count == 0)
        {
            if (Missing.Add(id)) GD.Print($"[audio] нет звука {id}: синтез");
            list.Add(Fallback(id));
        }

        return Cache[id] = list.ToArray();
    }

    /// <summary>Есть ли у id записанные файлы (а не замена) — для проверок.</summary>
    public static bool HasFiles(string id) =>
        SoundIds.Groups.TryGetValue(id, out string? group)
        && ResourceLoader.Exists($"res://assets/audio/{group}/{id}_0.ogg");

    private static AudioStream Fallback(string id) => id switch
    {
        _ when id.StartsWith("step_", StringComparison.Ordinal) => SoundBank.Step(),
        _ when id.StartsWith("door", StringComparison.Ordinal) => SoundBank.Door(),
        "pour" => SoundBank.Pour(),
        _ => SoundBank.Click(),
    };

    internal static void ReleaseCaches()
    {
        foreach (var variants in Cache.Values)
            foreach (var stream in variants) stream.Dispose();
        Cache.Clear();
        Missing.Clear();
    }
}
