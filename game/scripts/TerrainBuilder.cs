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
    private static readonly float[] HalfOffsets = { 0f, 3.6f, 4.7f, 6.6f, 9.4f, 12f, 18f, 26f, 36f, 50f, 70f, 95f, 120f, 140f };

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
        if (t <= 0f) return WorldArtGround.HeightAt(roadY, world);

        float hills = 10f * (0.6f * Mathf.Sin(world.X * 0.011f) + 0.4f * Mathf.Sin(world.Z * 0.013f + 1.7f));
        float rise = t * (4f + hills * 0.9f);
        float bumps = Mathf.Sin(world.X * 0.09f) * Mathf.Cos(world.Z * 0.08f) * 0.35f * t;

        return WorldArtGround.HeightAt(roadY + rise + bumps, world);
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

        var normals = new Vector3[rows.Count][];
        for (int i = 0; i < rows.Count; i++)
        {
            normals[i] = new Vector3[columns];
            for (int j = 0; j < columns; j++)
            {
                var across = rows[i][Mathf.Min(j + 1, columns - 1)] - rows[i][Mathf.Max(j - 1, 0)];
                var along = rows[Mathf.Min(i + 1, rows.Count - 1)][j] - rows[Mathf.Max(i - 1, 0)][j];
                normals[i][j] = across.Cross(along).Normalized();
            }
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        var faces = new List<Vector3>((rows.Count - 1) * (columns - 1) * 6);

        // Цвет вершины: у дороги гравий, дальше трава — обочина без отдельной ленты.
        var rowColors = new Color[rows.Count][];
        for (int i = 0; i < rows.Count; i++)
        {
            rowColors[i] = new Color[columns];
            for (int j = 0; j < columns; j++)
                rowColors[i][j] = TerrainColor(Offsets[j], rows[i][j]);
        }

        for (int i = 0; i < rows.Count - 1; i++)
        {
            for (int j = 0; j < columns - 1; j++)
            {
                Vector3 a = rows[i][j];
                Vector3 b = rows[i][j + 1];
                Vector3 c = rows[i + 1][j];
                Vector3 d = rows[i + 1][j + 1];

                AddTriangle(st, faces, a, b, c, normals[i][j], normals[i][j+1], normals[i+1][j], rowColors[i][j], rowColors[i][j + 1], rowColors[i + 1][j], Offsets[j], Offsets[j+1], Offsets[j]);
                AddTriangle(st, faces, b, d, c, normals[i][j+1], normals[i+1][j+1], normals[i+1][j], rowColors[i][j + 1], rowColors[i + 1][j + 1], rowColors[i + 1][j], Offsets[j+1], Offsets[j+1], Offsets[j]);
            }
        }

        parent.AddChild(new MeshInstance3D
        {
            Name = "LandscapeGround", Mesh = st.Commit(),
            MaterialOverride = LandscapeArt.GroundMaterial(),
        });

        var body = new StaticBody3D { Name = "LandscapeGroundCollision" };
        body.AddChild(new CollisionShape3D
        {
            // Двусторонняя коллизия: иначе, заехав в склон, машина проваливается.
            Shape = new ConcavePolygonShape3D { Data = faces.ToArray(), BackfaceCollision = true },
        });
        parent.AddChild(body);
    }

    private static readonly Color GrassColor = new(0.29f, 0.37f, 0.16f);
    private static readonly Color GravelColor = new(0.42f, 0.36f, 0.28f);
    private static readonly Color WheatColor = new(0.70f, 0.61f, 0.33f);
    private static readonly Color PlowedColor = new(0.40f, 0.31f, 0.23f);

    /// <summary>Цвет земли: гравий у асфальта, трава, крупные пятна полей.</summary>
    private static Color TerrainColor(float lateral, Vector3 world)
    {
        float distance = Mathf.Abs(lateral);

        if (distance <= 6.6f) return GravelColor;

        Color baseColor = distance >= 9.4f
            ? GrassColor
            : GravelColor.Lerp(GrassColor, (distance - 6.6f) / 2.8f);

        if (distance < 14f) return baseColor;

        // Поля: пятна пшеницы и пашни, привязанные к мировым координатам.
        float field = Mathf.Sin(world.X * 0.010f + 1.7f) * Mathf.Cos(world.Z * 0.008f + 0.4f);

        float wheat = Mathf.Clamp((field - 0.32f) / 0.34f, 0f, 1f);
        if (wheat > 0f) return baseColor.Lerp(WheatColor, wheat);

        float plowed = Mathf.Clamp((-field - 0.36f) / 0.30f, 0f, 1f);
        if (plowed > 0f) return baseColor.Lerp(PlowedColor, plowed);

        return baseColor;
    }

    private static void AddTriangle(SurfaceTool st, List<Vector3> faces, Vector3 a, Vector3 b, Vector3 c,
        Vector3 na, Vector3 nb, Vector3 nc, Color ca, Color cb, Color cc, float ua, float ub, float uc)
    {
        var vertices = new[] { a, b, c };
        var colors = new[] { ca, cb, cc };
        var normals = new[] { na, nb, nc };
        var lateral = new[] { ua, ub, uc };

        // Godot front faces use clockwise winding; keep the supplied upward normal.
        foreach (int i in new[] { 0, 2, 1 })
        {
            st.SetNormal(normals[i]);
            st.SetColor(colors[i]);
            st.SetUV(new Vector2(lateral[i], 0));
            st.AddVertex(vertices[i]);
            faces.Add(vertices[i]);
        }
    }
}
