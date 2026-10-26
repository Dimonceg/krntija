using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Krntija.Core;
using Krntija.Core.Economy;
using Krntija.Core.Items;
using Krntija.Core.Parts;
using Krntija.Core.Vehicle;
using Krntija.Core.Workshop;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>Сборка мира: небо, дорога, точки интереса, карта, тупики, дом игрока.</summary>
public partial class Main
{
    private readonly List<(Vector3 Position, Door Barrier, Node3D Wall)> _exitGates = new();
    private Node3D? _collapseNode;
    private Node3D? _collapseBody;
    private Vector3? _housePosition;
    private float _houseGroundY;

    /// <summary>Узел дома игрока: в старом мире — ребёнок Main, в мире кусками — места c0/p0.</summary>
    private Node3D? _houseNode;

    private void BuildWorld()
    {
        var skyMaterial = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.35f, 0.45f, 0.6f),
            SkyHorizonColor = new Color(0.72f, 0.68f, 0.58f),
            GroundHorizonColor = new Color(0.45f, 0.42f, 0.33f),
        };

        _skyMaterial = skyMaterial;

        _environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = skyMaterial },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.42f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,

            FogEnabled = true,
            FogLightColor = new Color(0.7f, 0.68f, 0.6f),
            FogDensity = 0.0022f,
            FogAerialPerspective = 0.35f,
            FogSkyAffect = 0.2f,

            // Объём: SSAO подчёркивает стыки, тонмаппинг убирает «пластик».
            SsaoEnabled = true,
            SsaoRadius = 1.6f,
            SsaoIntensity = 1.6f,
            SsaoPower = 1.4f,

            // Свечение выключено (2026-10-26): при 1920×1080 стоило 0,6 мс видеокарты
            // (12 % кадра), а на снимках днём и вечером разницы нет — светящегося в мире
            // пока нет. Понадобится для ламп или артефактов — включать с уровнями под них.
            GlowEnabled = false,

            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapExposure = 0.85f,
            TonemapWhite = 1.0f,
        };
        AddChild(new WorldEnvironment { Environment = _environment });

        _sun = new DirectionalLight3D
        {
            LightEnergy = 1.25f,
            LightColor = new Color(1.0f, 0.95f, 0.86f),
            ShadowEnabled = true,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
            DirectionalShadowMaxDistance = 140f,
            ShadowBias = 0.04f,
            ShadowNormalBias = 0.8f,
            ShadowBlur = 1.2f,
        };
        _sun.RotationDegrees = new Vector3(-48, -35, 0);
        AddChild(_sun);

        // Старого мира больше нет: игра всегда строится кусками.
        BuildChunkWorld();
    }

    private void BuildPlayerHouse()
    {
        if (_road is not { Centerline.Count: > 20 }) return;

        int index = 10;
        Vector3 anchor = _road.Centerline[index];
        var right = new Vector3(Mathf.Cos(_road.Headings[index]), 0, Mathf.Sin(_road.Headings[index]));
        Vector3 spot = anchor - right * 15f;
        spot.Y = TerrainBuilder.SurfaceHeight(anchor.Y, -15f, spot);

        // Фундамент: пол приподнят над газоном, чтобы рельеф не пробивал его.
        spot.Y += 0.12f;
        _houseGroundY = spot.Y;

        var doors = PlayerHouse.Build(this, spot, -_road.Headings[index] + Mathf.Pi * 0.5f);
        _housePosition = spot;
        _houseNode = GetNodeOrNull<Node3D>("PlayerHouse");
        foreach (var (position, door) in doors)
        {
            // Подсказка ищется от центра полотна, а не от петли: у петли радиус мал.
            Vector3 center = position + door.GlobalBasis.X.Normalized() * (door.Width * 0.5f);
            _interactions.Add(new InteractionPoint(center, InteractionKind.Door, 3.2f, Door: door));
        }

        GD.Print($"[house] дом игрока: {doors.Count} дверей, {spot}");

        // База: сундук на кухне, верстак в гараже, кровать на чердаке.
        if (GetNodeOrNull<Node3D>("PlayerHouse") is { } home)
        {
            _interactions.Add(new InteractionPoint(home.ToGlobal(new Vector3(-4.3f, 0.7f, 2.0f)),
                InteractionKind.Chest, 3f, Visual: PlayerHouse.UseFurniture(home, "chest")));
            _interactions.Add(new InteractionPoint(home.ToGlobal(new Vector3(4.3f, 0.7f, -0.6f)),
                InteractionKind.Workbench, 3f, Visual: PlayerHouse.UseFurniture(home, "workbench")));
            _interactions.Add(new InteractionPoint(home.ToGlobal(new Vector3(-3.6f, 3.0f, -1.8f)),
                InteractionKind.SleepHome, 3f, Visual: PlayerHouse.UseFurniture(home, "bed")));
        }
    }

    /// <summary>Дорожный указатель с названием места: «Село», «Рушевине» и т.п.</summary>
    private void AddFeatureSign(Vector3 roadPoint, float heading, string title)
    {
        var right = new Vector3(Mathf.Cos(heading), 0, Mathf.Sin(heading));
        Vector3 spot = roadPoint + right * (RoadMeshBuilder.Width * 0.5f + 2.6f);
        spot.Y = TerrainBuilder.SurfaceHeight(roadPoint.Y, 6f, spot);

        var root = new Node3D { Position = spot, Rotation = new Vector3(0, -heading, 0) };
        AddChild(root);

        var post = new Color(0.55f, 0.55f, 0.52f);
        var board = new Color(0.86f, 0.83f, 0.70f);

        Box(root, new Vector3(0.09f, 2.4f, 0.09f), new Vector3(-0.75f, 1.2f, 0), post);
        Box(root, new Vector3(0.09f, 2.4f, 0.09f), new Vector3(0.75f, 1.2f, 0), post);
        Box(root, new Vector3(1.9f, 0.62f, 0.07f), new Vector3(0, 2.25f, 0), board);

        root.AddChild(new Label3D
        {
            Text = title.ToUpperInvariant(),
            FontSize = 56,
            PixelSize = 0.0042f,
            Position = new Vector3(0, 2.25f, 0.06f),
            Modulate = new Color(0.16f, 0.15f, 0.14f),
        });
    }

    private static MeshInstance3D Box(Node3D parent, Vector3 size, Vector3 offset, Color color)
    {
        var box = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = offset,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f },
        };
        parent.AddChild(box);
        return box;
    }
}
