using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core.WorldGen;

namespace Krntija.Game;

/// <summary>
/// Дворовый декор у мест (арт-модель, docs/YARD_ART.md): бочки, шины, поддоны,
/// лом, скамейки, знаки. Где стоит — решает ядро (<see cref="WorldGenerator.Decor"/>),
/// здесь — по одному MultiMesh на модель в куске. Без коллизии и состояния.
/// </summary>
public static class YardDecor
{
    private static Dictionary<string, Mesh>? _meshes;

    /// <summary>Загрузить модели заранее: первая загрузка GLB посреди поездки — рывок.</summary>
    public static void Preload() => Meshes();

    private static Dictionary<string, Mesh> Meshes()
    {
        if (_meshes is not null) return _meshes;

        _meshes = new Dictionary<string, Mesh>();
        foreach (string id in WorldGenerator.DecorModels)
        {
            var root = ModelLibrary.Prop(id);
            if (root is null) continue;
            if (WorldVegetation.FindMesh(root) is { } mesh) _meshes[id] = mesh;
            root.Free();
        }

        return _meshes;
    }

    /// <param name="ground">Высота земли куска (с площадками мест).</param>
    /// <param name="local">Мировые (x, y, z) → координаты корня куска.</param>
    public static int Build(Node3D root, RoadCurve curve, ulong seed, ChunkPlan plan,
        Func<double, double, float> ground, Func<double, double, double, Vector3> local)
    {
        var meshes = Meshes();
        var groups = new Dictionary<string, List<Transform3D>>();

        foreach (var place in plan.Places)
        {
            foreach (var item in WorldGenerator.Decor(seed, place, curve))
            {
                if (!meshes.ContainsKey(item.ModelId)) continue;
                var (x, z) = WorldGenerator.PlaceToWorld(place, curve, item.OffsetX, item.OffsetZ);
                var basis = new Basis(Vector3.Up, -(float)(place.Yaw + item.Yaw));
                if (!groups.TryGetValue(item.ModelId, out var list)) groups[item.ModelId] = list = new List<Transform3D>();
                list.Add(new Transform3D(basis, local(x, ground(x, z), z)));
            }
        }

        int count = 0;
        foreach (var (id, transforms) in groups)
        {
            var mesh = meshes[id];
            var multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = mesh,
                InstanceCount = transforms.Count,
            };

            Aabb bounds = default;
            for (int i = 0; i < transforms.Count; i++)
            {
                multimesh.SetInstanceTransform(i, transforms[i]);
                var aabb = transforms[i] * mesh.GetAabb();
                bounds = i == 0 ? aabb : bounds.Merge(aabb);
            }

            root.AddChild(new MultiMeshInstance3D
            {
                Name = $"Decor_{id}",
                Multimesh = multimesh,
                CustomAabb = bounds.Grow(0.5f),
                VisibilityRangeEnd = 180,
                VisibilityRangeEndMargin = 20,
            });
            count += transforms.Count;
        }

        return count;
    }

    /// <summary>Отпустить меши перед выходом (StaticCaches).</summary>
    internal static void ReleaseCaches()
    {
        if (_meshes is null) return;
        foreach (var mesh in _meshes.Values) mesh.Dispose();
        _meshes = null;
    }
}
