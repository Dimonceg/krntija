using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Холмистая земля вокруг дороги. Полоса следует за трассой, поэтому
/// не важно, куда она свернула: обочина всегда на месте.
/// </summary>
public static class TerrainBuilder
{
    public const float HalfWidth = 140f;

    /// <summary>До этого расстояния от оси земля ровная — обочина и площадки у POI.</summary>
    private const float FlatShoulder = 18f;

    private const int RowStep = 2; // каждые 8 м вдоль дороги

    /// <summary>Колонки сгущаются у дороги и разрежаются к краю полосы.</summary>
    private static readonly float[] HalfOffsets = { 0f, 6f, 12f, 18f, 26f, 36f, 50f, 70f, 95f, 120f, 140f };

    private static readonly float[] Offsets = BuildOffsets();

    private static float[] BuildOffsets()
    {
        var list = new List<float>();
        for (int i = HalfOffsets.Length - 1; i >= 0; i--)
        {
            if (HalfOffsets[i] > 0) list.Add(-HalfOffsets[i]);
        }

        for (int i = 0; i < HalfOffsets.Length; i++)
            list.Add(HalfOffsets[i]);

        return list.ToArray();
    }

    /// <summary>Высота земли на заданном удалении от оси дороги.</summary>
    public static float SurfaceHeight(float roadY, float lateral, Vector3 world)
    {
        float t = Mathf.Clamp((Mathf.Abs(lateral) - FlatShoulder) / (HalfWidth - FlatShoulder), 0f, 1f);
        if (t <= 0f) return roadY;

        float hills = 10f * (0.6f * Mathf.Sin(world.X * 0.011f) + 0.4f * Mathf.Sin(world.Z * 0.013f + 1.7f));
        float rise = t * (4f + hills * 0.9f);
        float bumps = Mathf.Sin(world.X * 0.09f) * Mathf.Cos(world.Z * 0.08f) * 0.35f * t;

        return roadY + rise + bumps;
    }

    public static void Build(List<Vector3> centerline, List<float> headings, Node3D parent)
    {
        if (centerline.Count < 2) return;

        int columns = Offsets.Length;
        var rows = new List<Vector3[]>();

        for (int i = 0; i < centerline.Count; i += RowStep)
        {
            var row = new Vector3[columns];
            var right = new Vector3(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));

            for (int j = 0; j < columns; j++)
            {
                float lateral = Offsets[j];
                Vector3 point = centerline[i] + right * lateral;
                point.Y = SurfaceHeight(centerline[i].Y, lateral, point);
                row[j] = point;
            }

            rows.Add(row);
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        var faces = new List<Vector3>((rows.Count - 1) * (columns - 1) * 6);

        for (int i = 0; i < rows.Count - 1; i++)
        {
            for (int j = 0; j < columns - 1; j++)
            {
                Vector3 a = rows[i][j];
                Vector3 b = rows[i][j + 1];
                Vector3 c = rows[i + 1][j];
                Vector3 d = rows[i + 1][j + 1];

                Vector3 normal = (c - a).Cross(b - a).Normalized();

                AddTriangle(st, faces, a, b, c, normal);
                AddTriangle(st, faces, b, d, c, normal);
            }
        }

        parent.AddChild(new MeshInstance3D
        {
            Mesh = st.Commit(),
            MaterialOverride = new StandardMaterial3D
            {
                // Трава с пятнами: тёмная и выгоревшая, трипланарно по миру.
                AlbedoTexture = ProcTexture.Noise(0.07f, new Color(0.26f, 0.32f, 0.19f), new Color(0.42f, 0.45f, 0.27f)),
                Uv1Triplanar = true,
                Uv1Scale = new Vector3(0.05f, 0.05f, 0.05f),
                Roughness = 1f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        });

        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D
        {
            // Двусторонняя коллизия: иначе, заехав в склон, машина проваливается.
            Shape = new ConcavePolygonShape3D { Data = faces.ToArray(), BackfaceCollision = true },
        });
        parent.AddChild(body);
    }

    private static void AddTriangle(SurfaceTool st, List<Vector3> faces, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
    {
        foreach (var vertex in new[] { a, b, c })
        {
            st.SetNormal(normal);
            st.AddVertex(vertex);
            faces.Add(vertex);
        }
    }
}
