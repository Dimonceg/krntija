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
    private MapResult _map = new();
    private List<RoadsideMap.Place> _mapPlaces = new();
    private readonly List<(Vector3 Position, Door Barrier, Node3D Wall)> _exitGates = new();
    private Node3D? _collapseNode;
    private Node3D? _collapseBody;
    private Vector3? _housePosition;
    private float _houseGroundY;

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

            GlowEnabled = true,
            GlowIntensity = 0.22f,
            GlowBloom = 0.05f,

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

        if (UseChunkWorld)
        {
            BuildChunkWorld();
            return;
        }

        BuildRoad();
    }

    /// <summary>Дорога из плана ядра: лента, коллизия, точки интереса и обочины.</summary>
    private void BuildRoad()
    {
        var plan = RoadPlanner.Generate(seed: 20261023, totalMeters: 2500);
        var road = RoadMeshBuilder.Build(plan, this);
        _road = road;
        WorldArtGround.Configure(road.Points);

        int poiIndex = 0;
        foreach (var poi in road.Points)
        {
            GD.Print($"[poi] {poi.Segment.Feature} at index {(int)Mathf.Round(poi.Segment.StartM / 4f)} ({poi.Segment.StartM:F0} м)");
            var right = new Vector3(Mathf.Cos(poi.Heading), 0, Mathf.Sin(poi.Heading));
            Vector3 spot = poi.Position - right * 16f; // левая обочина
            spot.Y = TerrainBuilder.SurfaceHeight(poi.Position.Y, -16f, spot);
            AddLandmark(poi.Segment.Feature, poi.Segment.Title, spot, poi.Heading, poi.Position, poiIndex++);
        }

        TerrainBuilder.Build(road.Centerline, road.Headings, this);
        WorldScatter.ScatterHills(road.Centerline, road.Headings, this, seed: 20261023);
        LandscapeArt.Grass(road.Centerline, road.Headings, this);
        WorldScatter.ScatterTrees(road.Centerline, road.Headings, this, seed: 20261023, landmarks: road.Points);
        WorldScatter.ScatterPowerLine(road.Centerline, road.Headings, this, seed: 20261023);
        WorldScatter.AddKilometerPosts(road.Centerline, road.Headings, this);
        WorldScatter.ScatterSigns(road.Centerline, road.Headings, this, seed: 20261023);

        AddRoadBoundaries(road);
        _mapPlaces = RoadsideMap.Build(road.Centerline, road.Headings, this, seed: 20261023);
        _map = BackcountryMap.Build(road.Centerline, road.Headings, this, seed: 20261023);
        MapLife.BuildForkSigns(this, road.Centerline, road.Headings, _map.Branches, _map);
        MapLife.BuildResidents(this, _map, new Pcg32(20261025, sequence: 5));
        _exitGates.AddRange(MapLife.BuildClosedExits(this, road.Centerline, road.Headings, _map,
            new Pcg32(20261026, sequence: 6)));
        RegisterMapUses(_map);
        SpawnMapEvents();

        var target = road.Points.Count > 0 ? road.Points[^1] : null;
        if (target is not null)
        {
            _objectivePosition = target.Position;
            _objectiveTitle = target.Segment.Title;
        }

        GD.Print($"[road] {plan.Segments.Count} сегментов, {plan.TotalMeters:F0} м, точек интереса: {road.Points.Count}");
    }

    private void AddLandmark(RoadFeature feature, string title, Vector3 position, float heading, Vector3 roadPoint, int poiIndex)
    {
        string featureId = feature.ToString().ToLowerInvariant();
        string? variant = PropFactory.PickVariant(PropVariants(featureId), poiIndex);

        // Небольшой разворот; высота площадки фиксирована, чтобы не затопить настил землёй.
        var rng = new Pcg32((ulong)(20261024 + poiIndex), sequence: 5);
        float yawJitter = (float)rng.Range(-0.18, 0.18);

        Node3D? model = variant is null
            ? null
            : PropFactory.Spawn(variant, position, heading, solid: true, this, yawJitter);
        if (model is not null) model.Name = $"Landmark_{feature}";

        switch (feature)
        {
            case RoadFeature.GasStation:
                if (model is null) AddGasStation(position, heading);
                else AddGasStationInteractions(model.Position, -model.Rotation.Y);
                break;

            case RoadFeature.Village:
                if (model is null) AddVillage(position, heading);
                else if (model.FindChild("SleepAnchor", true, false) is Node3D bed)
                    _interactions.Add(new InteractionPoint(bed.GlobalPosition, InteractionKind.Sleep, 4.5f));
                else AddMotel(model, new Vector3(-6f, 0, 9.5f));

                // Скупщик у села: берёт всё, платит хуже лавки.
                _interactions.Add(new InteractionPoint(position + Vector3.Up * 0.2f,
                    InteractionKind.SellAll, 5f));
                break;
            case RoadFeature.Ruins: if (model is null) AddRuins(position, heading); break;
            case RoadFeature.Farm: if (model is null) AddFarm(position, heading); break;
            case RoadFeature.Checkpoint: AddCheckpoint(position, heading, roadPoint, poiIndex, model is null); break;
        }

        if (title.Length > 0) AddFeatureSign(roadPoint, heading, title);
    }

    /// <summary>Какие файлы ищем для каждого типа места: base, _a, _b, _c.</summary>
    private static IReadOnlyList<string> PropVariants(string featureId) => new[]
    {
        featureId + "_a", featureId + "_b", featureId + "_c", featureId,
    };

    /// <summary>
    /// Слить неподвижную геометрию мира: меньше вызовов отрисовки. Мокрая дорога
    /// меняет свой материал во время игры, поэтому он сливается только сам с собой.
    /// </summary>
    private void BatchStaticGeometry()
    {
        // --no-batch: сравнить картинку и цифры без слияния.
        if (HasArg("--no-batch")) return;

        var dynamicMaterials = new HashSet<Material>();
        if (_road?.RoadMaterial is { } road) dynamicMaterials.Add(road);

        ulong started = Time.GetTicksMsec();
        int removed = StaticBatcher.Merge(this, cellSize: 64f, dynamicMaterials);
        GD.Print($"[batch] мешей убрано: {removed}, за {Time.GetTicksMsec() - started} мс");
    }

    /// <summary>Точки карты: лут, скупщик, колодец, собака.</summary>
    private void RegisterMapUses(MapResult map)
    {
        int loot = 0;

        foreach (var use in map.Uses)
        {
            switch (use.Kind)
            {
                case "loot":
                    AddLoot(WorldState.Loot("map", loot++), use.Position, use.ItemId ?? "cassette", use.Count);
                    break;
                case "sell":
                    _interactions.Add(new InteractionPoint(use.Position, InteractionKind.SellAll, 5f));
                    break;
                case "drink":
                    _interactions.Add(new InteractionPoint(use.Position, InteractionKind.Drink, 3.5f));
                    break;
                case "dog":
                    _interactions.Add(new InteractionPoint(use.Position, InteractionKind.Pet, 3f));
                    break;
                case "talk":
                    _interactions.Add(new InteractionPoint(use.Position, InteractionKind.Talk, 3f,
                        LootId: use.ItemId));
                    break;
                case "exit":
                    _interactions.Add(new InteractionPoint(use.Position, InteractionKind.RepairExit, 3.5f,
                        LootId: use.ItemId,
                        StateId: int.TryParse(use.ItemId, out int exit) ? WorldState.Exit(exit) : null));
                    break;
            }
        }

        foreach (var door in map.Doors)
            _interactions.Add(new InteractionPoint(door.GlobalPosition, InteractionKind.Door, 2.8f, Door: door));

        GD.Print($"[map] лута на карте: {loot}, дверей: {map.Doors.Count}, всего точек: {map.Uses.Count}");
    }

    /// <summary>События карты: завал на первой ветке и волки у сарая с собакой.</summary>
    private void SpawnMapEvents()
    {
        if (_map.Branches.Count > 0)
        {
            var (position, node, body) = MapLife.BuildCollapse(this, _map.Branches[0],
                new Pcg32(20261027, sequence: 9));
            _collapseNode = node;
            _collapseBody = body;
            node.AddToGroup(StaticBatcher.SkipGroup); // завал разбирают — его меши не сливаем

            _interactions.Add(new InteractionPoint(position + Vector3.Up * 0.4f,
                InteractionKind.ClearCollapse, 4f));
            GD.Print($"[map] завал на ветке: {position}");
        }

        foreach (var place in _map.Places)
        {
            if (place.Kind != "dog") continue;

            Vector3 center = place.Position + new Vector3(-2f, 0, 3f);
            _wolvesCenter = center;
            MapLife.BuildWolves(this, center, new Pcg32(20261028, sequence: 11));
            GD.Print($"[map] волки у сарая: {center}");
            break;
        }
    }

    /// <summary>
    /// Тупики на обоих концах дороги: бетонный отбойник во всю ширину обочины,
    /// чтобы нельзя было уехать или уйти туда, где мир ещё не построен.
    /// </summary>
    private void AddRoadBoundaries(RoadBuildResult road)
    {
        if (road.Centerline.Count < 8) return;

        AddBoundary(road, 1, -1f, "Начало");
        AddBoundary(road, road.Centerline.Count - 2, 1f, "Конец");
    }

    private void AddBoundary(RoadBuildResult road, int index, float side, string label)
    {
        Vector3 point = road.Centerline[index];
        float heading = road.Headings[index];

        // Касательная дороги: по соглашению heading она равна (sin, 0, −cos).
        var tangent = new Vector3(Mathf.Sin(heading), 0, -Mathf.Cos(heading));

        Vector3 position = point + tangent * (3f * side);
        position.Y = point.Y;

        GD.Print($"[road] тупик {label}: сдвиг {3f * side:F0} м вдоль дороги");

        var root = new Node3D { Name = $"Boundary_{label}", Position = position, Rotation = new Vector3(0, -heading, 0) };
        AddChild(root);

        var concrete = new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.6f, 0.56f), Roughness = 0.95f };
        var paint = new StandardMaterial3D { AlbedoColor = new Color(0.75f, 0.2f, 0.15f), Roughness = 0.8f };

        var wall = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(26f, 1.15f, 0.7f) },
            Position = new Vector3(0, 0.58f, 0),
            MaterialOverride = concrete,
        };
        root.AddChild(wall);

        var stripe = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(26f, 0.18f, 0.72f) },
            Position = new Vector3(0, 1.0f, 0),
            MaterialOverride = paint,
        };
        root.AddChild(stripe);

        var body = new StaticBody3D { Name = "Boundary" };
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(26f, 2.4f, 1.2f) },
            Position = new Vector3(0, 1.2f, 0),
        });
        root.AddChild(body);

        // Невидимая стена шире обочины: обойти по траве тоже нельзя.
        var wide = new StaticBody3D { Name = "BoundaryWide" };
        wide.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(70f, 4f, 1.5f) },
            Position = new Vector3(0, 2f, -0.9f * side),
        });
        root.AddChild(wide);

        // Столбики по краям — чтобы отбойник читался как настоящий.
        for (int i = -1; i <= 1; i += 2)
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.3f, 1.5f, 0.3f) },
                Position = new Vector3(i * 12.6f, 0.75f, 0),
                MaterialOverride = concrete,
            });
        }
    }

    /// <summary>Дом игрока у старта: слева от дороги, с дверями и чердаком.</summary>
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
                InteractionKind.Chest, 3f));
            _interactions.Add(new InteractionPoint(home.ToGlobal(new Vector3(4.3f, 0.7f, -0.6f)),
                InteractionKind.Workbench, 3f));
            _interactions.Add(new InteractionPoint(home.ToGlobal(new Vector3(-3.6f, 3.0f, -1.8f)),
                InteractionKind.SleepHome, 3f));
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
