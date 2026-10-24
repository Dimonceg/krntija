using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>Что построили: точки интереса и центральная линия дороги.</summary>
public sealed class RoadBuildResult
{
    public List<RoadMeshBuilder.PointOfInterest> Points { get; } = new();
    public List<Vector3> Centerline { get; } = new();
    public List<float> Headings { get; } = new();
}

/// <summary>
/// Строит дорогу по плану из ядра: ленту с поворотами, коллизию и столбики.
/// Никаких ассетов — только геометрия, чтобы можно было ездить уже сейчас.
/// </summary>
public static class RoadMeshBuilder
{
    public const float Width = 9f;
    private const float StepMeters = 4f;

    public sealed record PointOfInterest(RoadSegment Segment, Vector3 Position, float Heading);

    public static RoadBuildResult Build(RoadPlan plan, Node3D parent)
    {
        var centers = new List<Vector3>();
        var headingsAll = new List<float>();

        double distance = 0;
        float theta = 0;
        var position = Vector3.Zero;

        foreach (var segment in plan.Segments)
        {
            double segmentEnd = segment.StartM + segment.LengthM;
            while (distance < segmentEnd - 0.001)
            {
                position.Y = (float)RoadProfile.HeightAt(distance);
                centers.Add(position);
                headingsAll.Add(theta);

                theta += (float)(RoadPlanner.RadiansPerMeter(segment.Curvature) * StepMeters);
                position += new Vector3(Mathf.Sin(theta), 0, -Mathf.Cos(theta)) * StepMeters;
                distance += StepMeters;
            }
        }

        var result = new RoadBuildResult();
        if (centers.Count < 2) return result;

        result.Centerline.AddRange(centers);
        result.Headings.AddRange(headingsAll);

        var mesh = BuildRibbon(centers, headingsAll);
        var body = new StaticBody3D();
        body.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.13f, 0.13f, 0.14f),
                Roughness = 1f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        });
        body.AddChild(BuildCollision(centers, headingsAll));
        parent.AddChild(body);

        AddCenterLine(centers, headingsAll, parent);

        AddRoadsidePosts(centers, headingsAll, parent);

        // Точки интереса ставим у начала своего сегмента.
        foreach (var segment in plan.Features)
        {
            int index = Mathf.Clamp((int)Math.Round(segment.StartM / StepMeters), 0, centers.Count - 1);
            result.Points.Add(new PointOfInterest(segment, centers[index], headingsAll[index]));
        }

        return result;
    }

    /// <summary>Прерывистая осевая: каждый второй четырёхметровый шаг.</summary>
    private static void AddCenterLine(List<Vector3> points, List<float> headings, Node3D parent)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < points.Count - 1; i += 2)
        {
            Vector3 ra = new(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));
            Vector3 rb = new(Mathf.Cos(headings[i + 1]), 0, Mathf.Sin(headings[i + 1]));

            float ay = points[i].Y + 0.05f;
            float by = points[i + 1].Y + 0.05f;

            Vector3 a1 = points[i] - ra * 0.09f;
            Vector3 a2 = points[i] + ra * 0.09f;
            Vector3 b1 = points[i + 1] - rb * 0.09f;
            Vector3 b2 = points[i + 1] + rb * 0.09f;

            AddDashedVertex(st, a1, ay);
            AddDashedVertex(st, b1, by);
            AddDashedVertex(st, a2, ay);
            AddDashedVertex(st, a2, ay);
            AddDashedVertex(st, b1, by);
            AddDashedVertex(st, b2, by);
        }

        parent.AddChild(new MeshInstance3D
        {
            Mesh = st.Commit(),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.82f, 0.8f, 0.72f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        });
    }

    private static void AddDashedVertex(SurfaceTool st, Vector3 vertex, float y)
    {
        st.SetNormal(Vector3.Up);
        st.AddVertex(new Vector3(vertex.X, y, vertex.Z));
    }

    private static ArrayMesh BuildRibbon(List<Vector3> points, List<float> headings)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 leftA = Edge(points[i], headings[i], -1);
            Vector3 rightA = Edge(points[i], headings[i], +1);
            Vector3 leftB = Edge(points[i + 1], headings[i + 1], -1);
            Vector3 rightB = Edge(points[i + 1], headings[i + 1], +1);

            AddTriangle(st, leftA, leftB, rightA);
            AddTriangle(st, rightA, leftB, rightB);
        }

        return st.Commit();
    }

    private static void AddTriangle(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c)
    {
        foreach (var vertex in new[] { a, b, c })
        {
            st.SetNormal(Vector3.Up);
            st.AddVertex(vertex);
        }
    }

    private static Vector3 Edge(Vector3 point, float heading, int side)
    {
        var right = new Vector3(Mathf.Cos(heading), 0, Mathf.Sin(heading));
        var vertex = point + right * (Width * 0.5f * side);
        vertex.Y = point.Y + 0.04f;
        return vertex;
    }

    private static CollisionShape3D BuildCollision(List<Vector3> points, List<float> headings)
    {
        var faces = new List<Vector3>((points.Count - 1) * 6);

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 leftA = Edge(points[i], headings[i], -1);
            Vector3 rightA = Edge(points[i], headings[i], +1);
            Vector3 leftB = Edge(points[i + 1], headings[i + 1], -1);
            Vector3 rightB = Edge(points[i + 1], headings[i + 1], +1);

            faces.AddRange(new[] { leftA, leftB, rightA });
            faces.AddRange(new[] { rightA, leftB, rightB });
        }

        return new CollisionShape3D
        {
            Shape = new ConcavePolygonShape3D { Data = faces.ToArray(), BackfaceCollision = true },
        };
    }

    private static void AddRoadsidePosts(List<Vector3> points, List<float> headings, Node3D parent)
    {
        var material = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.83f, 0.76f), Roughness = 0.9f };

        for (int i = 8; i < points.Count; i += 12)
        {
            Vector3 right = new(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));
            float side = (i / 12) % 2 == 0 ? 1 : -1;
            var post = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.16f, 0.9f, 0.16f) },
                Position = points[i] + right * (Width * 0.5f + 0.6f) * side + Vector3.Up * 0.45f,
                MaterialOverride = material,
            };
            parent.AddChild(post);
        }
    }
}
