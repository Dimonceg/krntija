using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>
/// Одевает обочины: деревья и столбы ЛЭП. Всё через MultiMesh, чтобы
/// сотни объектов не превращались в сотни узлов.
/// </summary>
public static class WorldScatter
{
    public static void ScatterTrees(List<Vector3> centerline, List<float> headings, Node3D parent, ulong seed)
    {
        var rng = new Pcg32(seed, sequence: 99);
        var spots = new List<(Vector3 Position, float Scale, float Yaw)>();

        for (int i = 4; i < centerline.Count; i += 2)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (!rng.Chance(0.55)) continue;

                float offset = (float)rng.Range(11, 30);
                var right = new Vector3(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));
                Vector3 tree = centerline[i] + right * offset * side;
                tree.Y = TerrainBuilder.SurfaceHeight(centerline[i].Y, offset * side, tree);

                spots.Add((
                    tree,
                    (float)rng.Range(0.75, 1.45),
                    (float)rng.Range(0, Mathf.Tau)));
            }
        }

        if (spots.Count == 0) return;

        var trunkMesh = new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.26f, Height = 2.4f, RadialSegments = 6 };
        var crownMesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 1.7f, Height = 3.6f, RadialSegments = 7 };

        var trunks = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = trunkMesh, InstanceCount = spots.Count };
        var crowns = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = crownMesh, InstanceCount = spots.Count };

        for (int i = 0; i < spots.Count; i++)
        {
            var (position, scale, yaw) = spots[i];
            var basis = new Basis(Vector3.Up, yaw).Scaled(new Vector3(scale, scale, scale));

            trunks.SetInstanceTransform(i, new Transform3D(basis, position + Vector3.Up * (1.2f * scale)));
            crowns.SetInstanceTransform(i, new Transform3D(basis, position + Vector3.Up * (3.2f * scale)));
        }

        parent.AddChild(new MultiMeshInstance3D
        {
            Multimesh = trunks,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.29f, 0.22f, 0.15f), Roughness = 1f },
        });

        parent.AddChild(new MultiMeshInstance3D
        {
            Multimesh = crowns,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.29f, 0.16f), Roughness = 1f },
        });
    }

    public static void ScatterPowerLine(List<Vector3> centerline, List<float> headings, Node3D parent, ulong seed)
    {
        var rng = new Pcg32(seed, sequence: 123);
        int side = rng.Chance(0.5) ? 1 : -1;

        var poles = new List<Vector3>();
        for (int i = 12; i < centerline.Count; i += 14)
        {
            var right = new Vector3(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));
            Vector3 pole = centerline[i] + right * (13f * side);
            pole.Y = TerrainBuilder.SurfaceHeight(centerline[i].Y, 13f * side, pole);
            poles.Add(pole);
        }

        if (poles.Count == 0) return;

        var poleMesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.2f, Height = 7f, RadialSegments = 5 };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = poleMesh, InstanceCount = poles.Count };

        for (int i = 0; i < poles.Count; i++)
            mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, poles[i] + Vector3.Up * 3.5f));

        parent.AddChild(new MultiMeshInstance3D
        {
            Multimesh = mm,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.31f, 0.26f), Roughness = 1f },
        });

        // Провода: тонкие цилиндры между соседними столбами.
        var wireMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.08f, 0.08f), Roughness = 1f };
        for (int i = 0; i < poles.Count - 1; i++)
        {
            for (int wire = -1; wire <= 1; wire += 2)
            {
                Vector3 from = poles[i] + Vector3.Up * 6.6f + Vector3.Right * (0.45f * wire);
                Vector3 to = poles[i + 1] + Vector3.Up * 6.6f + Vector3.Right * (0.45f * wire);
                AddWire(parent, from, to, wireMaterial);
            }
        }
    }

    /// <summary>Километровые столбики с цифрами: по ним видно, сколько проехал.</summary>
    public static void AddKilometerPosts(List<Vector3> centerline, List<float> headings, Node3D parent)
    {
        const int everyPoints = 50; // 50 * 4 м = 200 м
        var material = new StandardMaterial3D { AlbedoColor = new Color(0.88f, 0.87f, 0.82f), Roughness = 0.9f };

        for (int i = everyPoints; i < centerline.Count; i += everyPoints)
        {
            Vector3 right = new Vector3(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));
            Vector3 position = centerline[i] + right * (RoadMeshBuilder.Width * 0.5f + 1.1f);

            parent.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.18f, 1.1f, 0.18f) },
                Position = position + Vector3.Up * 0.55f,
                MaterialOverride = material,
            });

            parent.AddChild(new Label3D
            {
                Text = $"{i * 4 / 1000.0:F1}",
                FontSize = 72,
                PixelSize = 0.0032f,
                Position = position + Vector3.Up * 1.25f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = new Color(0.95f, 0.94f, 0.88f),
                OutlineSize = 6,
            });
        }
    }

    private static void AddWire(Node3D parent, Vector3 from, Vector3 to, Material material)
    {
        float length = from.DistanceTo(to);
        if (length < 0.1f) return;

        var wire = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = length, RadialSegments = 4 },
            MaterialOverride = material,
        };

        parent.AddChild(wire);

        Vector3 direction = (to - from).Normalized();
        Vector3 middle = (from + to) * 0.5f;

        // Цилиндр растёт вдоль +Y, поэтому поворачиваем его на направление провода.
        var basis = Basis.Identity;
        if (Math.Abs(direction.Dot(Vector3.Up)) < 0.999f)
        {
            Vector3 axis = Vector3.Up.Cross(direction).Normalized();
            float angle = Vector3.Up.AngleTo(direction);
            basis = new Basis(axis, angle);
        }

        wire.Transform = new Transform3D(basis, middle);
    }
}
