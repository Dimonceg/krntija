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
