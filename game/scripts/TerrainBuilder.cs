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
    private const float LateralStep = 20f;
    private const int RowStep = 2; // каждые 8 м вдоль дороги

    /// <summary>Высота земли на заданном удалении от оси дороги.</summary>
    public static float SurfaceHeight(float roadY, float lateral, Vector3 world)
    {
        float t = Mathf.Clamp(Mathf.Abs(lateral) / HalfWidth, 0f, 1f);

        float hills = 10f * (0.6f * Mathf.Sin(world.X * 0.011f) + 0.4f * Mathf.Sin(world.Z * 0.013f + 1.7f));
        float rise = t * (4f + hills * 0.9f);
        float bumps = Mathf.Sin(world.X * 0.09f) * Mathf.Cos(world.Z * 0.08f) * 0.35f * t;

        return roadY + rise + bumps;
    }

    public static void Build(List<Vector3> centerline, List<float> headings, Node3D parent)
    {
        if (centerline.Count < 2) return;

        int columns = (int)(HalfWidth * 2 / LateralStep) + 1;
        var rows = new List<Vector3[]>();

        for (int i = 0; i < centerline.Count; i += RowStep)
        {
            var row = new Vector3[columns];
            var right = new Vector3(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));

            for (int j = 0; j < columns; j++)
            {
                float lateral = -HalfWidth + j * LateralStep;
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
                AlbedoColor = new Color(0.27f, 0.31f, 0.2f),
                Roughness = 1f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        });

        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D { Shape = new ConcavePolygonShape3D { Data = faces.ToArray() } });
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
