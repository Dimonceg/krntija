using System;
using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Сливает неподвижные меши в один меш на пару «ячейка × материал».
/// Генераторы мира собирают постройки из сотен ящиков, у каждого свой
/// материал; без слияния это ~1000 вызовов отрисовки на 2,5 км дороги.
/// </summary>
/// <remarks>
/// Не трогает: узлы со скриптом и всё под ними (двери, лут, шлагбаумы,
/// машина, жители), узлы из группы <see cref="SkipGroup"/> (то, что код
/// потом удаляет), меши со скелетом и невидимые. Одинаковые по значению
/// материалы без текстур объединяются; материалы из <c>keepInstance</c>
/// (меняются во время игры, как мокрая дорога) — только сами с собой.
/// </remarks>
public static class StaticBatcher
{
    /// <summary>Группа для узлов, которые нельзя сливать: их удаляют или двигают.</summary>
    public const string SkipGroup = "no_batch";

    private sealed class Batch
    {
        public required Material Material;
        public required GeometryInstance3D.ShadowCastingSetting Shadow;
        public required uint Layers;
        public readonly SurfaceTool Tool = new();
        public bool Started;
    }

    /// <summary>Слить меши под <paramref name="root"/>. Возвращает, сколько мешей убрано.</summary>
    public static int Merge(Node3D root, float cellSize, ISet<Material>? keepInstance = null)
    {
        var batches = new Dictionary<string, Batch>();
        var shared = new Dictionary<string, Material>();
        var merged = new List<MeshInstance3D>();
        Transform3D toRoot = root.GlobalTransform.AffineInverse();

        foreach (var mesh in Candidates(root))
        {
            var source = mesh.Mesh;
            Transform3D local = toRoot * mesh.GlobalTransform;
            Vector3 center = local * source.GetAabb().GetCenter();
            var cell = (Mathf.FloorToInt(center.X / cellSize), Mathf.FloorToInt(center.Z / cellSize));

            bool usable = true;
            var surfaces = new List<(int Index, Material Material)>();
            for (int i = 0; i < source.GetSurfaceCount(); i++)
            {
                var material = mesh.MaterialOverride ?? mesh.GetSurfaceOverrideMaterial(i) ?? source.SurfaceGetMaterial(i);
                // Примитивы (BoxMesh и т. п.) всегда из треугольников; у ArrayMesh проверяем.
                bool triangles = source is not ArrayMesh array
                                 || array.SurfaceGetPrimitiveType(i) == Mesh.PrimitiveType.Triangles;
                if (material is null || !triangles)
                {
                    usable = false;
                    break;
                }

                surfaces.Add((i, material));
            }

            if (!usable || surfaces.Count == 0) continue;

            foreach (var (index, material) in surfaces)
            {
                var (materialKey, canonical) = Canonical(material, keepInstance, shared);
                string key = $"{cell.Item1}:{cell.Item2}|{materialKey}|{(int)mesh.CastShadow}|{mesh.Layers}";

                if (!batches.TryGetValue(key, out var batch))
                {
                    batch = new Batch { Material = canonical, Shadow = mesh.CastShadow, Layers = mesh.Layers };
                    batches[key] = batch;
                }

                if (!batch.Started)
                {
                    batch.Tool.Begin(Mesh.PrimitiveType.Triangles);
                    batch.Started = true;
                }

                batch.Tool.AppendFrom(source, index, local);
            }

            merged.Add(mesh);
        }

        if (merged.Count == 0) return 0;

        var container = new Node3D { Name = "StaticBatch" };
        root.AddChild(container);

        foreach (var batch in batches.Values)
        {
            var arrayMesh = batch.Tool.Commit();
            for (int i = 0; i < arrayMesh.GetSurfaceCount(); i++)
                arrayMesh.SurfaceSetMaterial(i, batch.Material);

            container.AddChild(new MeshInstance3D
            {
                Mesh = arrayMesh,
                CastShadow = batch.Shadow,
                Layers = batch.Layers,
            });
        }

        // Узел с детьми (часто внутри лежит коллизия) оставляем, только без меша.
        foreach (var mesh in merged)
        {
            if (mesh.GetChildCount() == 0) mesh.QueueFree();
            else mesh.Mesh = null;
        }

        return merged.Count - batches.Count;
    }

    private static IEnumerable<MeshInstance3D> Candidates(Node root)
    {
        var stack = new Stack<Node>();
        foreach (var child in root.GetChildren()) stack.Push(child);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            // Узел со скриптом — это наш C#-класс. Не через GetScript(): тот создаёт Variant
            // на каждый узел, и их финализаторы роняли Godot при выходе.
            if (node.GetType().Assembly == typeof(StaticBatcher).Assembly || node.IsInGroup(SkipGroup)) continue;
            if (node is Node3D { Visible: false }) continue;

            if (node is MeshInstance3D { Mesh: not null } mesh && mesh.Skeleton.IsEmpty)
                yield return mesh;

            foreach (var child in node.GetChildren()) stack.Push(child);
        }
    }

    /// <summary>Ключ материала и экземпляр, который получит слитый меш.</summary>
    private static (string Key, Material Material) Canonical(Material material, ISet<Material>? keepInstance,
        Dictionary<string, Material> shared)
    {
        if (keepInstance?.Contains(material) == true || material is not StandardMaterial3D standard)
            return ($"i{material.GetInstanceId()}", material);

        string key = string.Join(";",
            standard.AlbedoColor.ToHtml(), standard.AlbedoTexture?.GetRid().Id ?? 0,
            standard.Roughness.ToString("F3"), standard.Metallic.ToString("F3"),
            (int)standard.Transparency, (int)standard.CullMode, (int)standard.ShadingMode,
            standard.VertexColorUseAsAlbedo, standard.EmissionEnabled, standard.Emission.ToHtml(),
            standard.EmissionEnergyMultiplier.ToString("F3"),
            standard.NormalTexture?.GetRid().Id ?? 0, standard.RoughnessTexture?.GetRid().Id ?? 0,
            standard.Uv1Scale, standard.Uv1Offset, standard.Uv1Triplanar, (int)standard.TextureFilter);

        if (!shared.TryGetValue(key, out var canonical))
            shared[key] = canonical = standard;

        return ($"v{key}", canonical);
    }
}
