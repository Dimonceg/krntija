using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Подмена процедурного арта на модели. Если файла нет — возвращает null,
/// и вызывающий код строит заглушку. Так арт-пасс можно делать постепенно,
/// не ломая игру.
/// </summary>
public static class ModelLibrary
{
    private static readonly Dictionary<string, PackedScene?> Cache = new();

    public static PackedScene? TryGet(string resPath)
    {
        if (Cache.TryGetValue(resPath, out var cached)) return cached;

        PackedScene? scene = ResourceLoader.Exists(resPath)
            ? ResourceLoader.Load<PackedScene>(resPath)
            : null;

        Cache[resPath] = scene;
        return scene;
    }

    public static Node3D? Instantiate(string resPath)
    {
        var scene = TryGet(resPath);
        if (scene is null) return null;

        var node = scene.Instantiate<Node3D>();
        return node;
    }

    /// <summary>Кузов машины: assets/models/cars/&lt;spec_id&gt;.glb</summary>
    public static Node3D? CarBody(string specId) => Instantiate($"res://assets/models/cars/{specId}.glb");

    /// <summary>Проп у дороги: assets/models/props/&lt;feature&gt;.glb</summary>
    public static Node3D? Prop(string feature) => Instantiate($"res://assets/models/props/{feature}.glb");

    /// <summary>Сбросить кеш — пригодится при горячей перезагрузке ассетов.</summary>
    public static void Clear() => Cache.Clear();
}
