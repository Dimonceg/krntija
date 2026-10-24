using System;
using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Каркас для ассетов мира: загрузка пропа, нормализация масштаба,
/// привязка к земле и автоматическая коллизия. Художнику достаточно
/// положить GLB в assets/models/props — код сам вставит его в мир.
/// </summary>
public static class PropFactory
{
    public const string Folder = "res://assets/models/props/";

    private static readonly Dictionary<string, Aabb?> BoundsCache = new();

    /// <summary>Найти первый существующий вариант из списка (village_a, village_b, village...).</summary>
    public static string? PickVariant(IReadOnlyList<string> candidates, int seed)
    {
        var existing = new List<string>();
        foreach (string id in candidates)
        {
            if (ResourceLoader.Exists(Folder + id + ".glb")) existing.Add(id);
        }

        if (existing.Count == 0) return null;
        return existing[Math.Abs(seed) % existing.Count];
    }

    /// <summary>
    /// Поставить проп на землю. Возвращает узел или null, если модели нет.
    /// </summary>
    public static Node3D? Spawn(string id, Vector3 ground, float heading, bool solid, Node3D parent, float yawOffset = 0f)
    {
        var model = ModelLibrary.Instantiate(Folder + id + ".glb");
        if (model is null) return null;

        float scale = ScaleFix(model);
        if (!Mathf.IsEqualApprox(scale, 1f)) model.Scale = Vector3.One * scale;

        var bounds = Measure(model);
        model.Position = ground;
        model.Rotation = new Vector3(0, -heading + yawOffset, 0);
        parent.AddChild(model);

        if (solid) AttachCollision(model, bounds, scale);
        return model;
    }

    /// <summary>Габариты пропа в метрах без учёта трансформации узла.</summary>
    public static Aabb? Measure(Node3D root)
    {
        if (BoundsCache.TryGetValue(root.Name, out var cached) && cached.HasValue) return cached;

        bool any = false;
        var box = new Aabb();

        foreach (var mesh in FindMeshes(root))
        {
            if (mesh.Mesh is null) continue;
            var local = mesh.Mesh.GetAabb();
            var transformed = mesh.Transform * local;

            box = any ? box.Merge(transformed) : transformed;
            any = true;
        }

        Aabb? result = any ? box : null;
        BoundsCache[root.Name] = result;
        return result;
    }

    /// <summary>Модель пришла в сантиметрах? Приводим к метрам.</summary>
    private static float ScaleFix(Node3D root)
    {
        var bounds = Measure(root);
        if (bounds is null) return 1f;

        float longest = Mathf.Max(bounds.Value.Size.X, Mathf.Max(bounds.Value.Size.Y, bounds.Value.Size.Z));
        if (longest > 40f) return 0.01f;
        if (longest < 0.2f) return 10f;
        return 1f;
    }

    /// <summary>Прокси-коллайдер по габаритам: не требует тримеша от художника.</summary>
    private static void AttachCollision(Node3D root, Aabb? bounds, float scale)
    {
        if (bounds is null) return;

        var size = bounds.Value.Size * scale;
        var center = bounds.Value.GetCenter() * scale;

        // Совсем плоские пропы (знаки, кусты) не должны быть стеной.
        if (size.Y < 0.4f || size.X < 0.25f || size.Z < 0.25f) return;

        var body = new StaticBody3D { Name = "PropCollision", Position = center };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        root.AddChild(body);
    }

    private static IEnumerable<MeshInstance3D> FindMeshes(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is MeshInstance3D mesh) yield return mesh;
            foreach (var nested in FindMeshes(child)) yield return nested;
        }
    }

    /// <summary>Проверка ассетов для художника: размеры, центровка, масштаб.</summary>
    public static bool Check(string id)
    {
        string path = Folder + id + ".glb";

        if (!ResourceLoader.Exists(path))
        {
            GD.Print($"[props] {id,-14} — модели нет");
            return true;
        }

        var model = ModelLibrary.Instantiate(path);
        if (model is null)
        {
            GD.Print($"[props] {id,-14} — FAIL: не загрузилась");
            return false;
        }

        float scale = ScaleFix(model);
        var bounds = Measure(model);
        model.QueueFree();

        if (bounds is null)
        {
            GD.Print($"[props] {id,-14} — FAIL: нет мешей");
            return false;
        }

        var size = bounds.Value.Size * scale;
        var center = bounds.Value.GetCenter() * scale;

        bool sane = size.X < 60f && size.Y < 60f && size.Z < 60f
                    && size.X > 0.05f && size.Y > 0.05f && size.Z > 0.05f;

        string note = Mathf.IsEqualApprox(scale, 1f) ? "" : $" масштаб x{scale:0.##}";
        string ground = Mathf.Abs(bounds.Value.Position.Y * scale) > 0.3f
            ? $" НИЗ НЕ НА ЗЕМЛЕ ({bounds.Value.Position.Y * scale:F2})" : "";

        GD.Print($"[props] {id,-14} {size.X:F2} × {size.Y:F2} × {size.Z:F2} м"
                 + $", центр ({center.X:F2}; {center.Y:F2}; {center.Z:F2}){note}{ground}"
                 + (sane ? "" : " — FAIL: габариты вне разумных пределов"));

        return sane;
    }
}
