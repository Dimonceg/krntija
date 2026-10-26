using System.Collections.Generic;
using Godot;
using Krntija.Core;
using Krntija.Core.Parts;

namespace Krntija.Game;

/// <summary>
/// Старт в гараже дома (ADR 0003, п. 5): машина в сборочном боксе, снятые детали
/// лежат на своих местах, в HUD — задача «Собери машину», пока все гнёзда не заняты.
/// </summary>
/// <remarks>
/// Где стоят машина и детали, решает ядро (<see cref="StartScenario"/>: какая
/// деталь на каком месте); сцена находит якоря <c>GarageCarAnchor</c> и
/// <c>PartSpot_0…7</c> в модели дома и отдаёт ядру их позы. Якорей нет — работаем
/// по запасным смещениям от старого гаража.
/// </remarks>
public partial class Main
{
    private const string CarAnchorName = "GarageCarAnchor";
    private const string PartSpotPrefix = "PartSpot_";

    /// <summary>
    /// Запасной путь без якорей: машина перед воротами старого гаража, носом от дома.
    /// </summary>
    private static readonly Vector3 GarageCarLocal = new(4.0f, 0f, -6.4f);

    private const string AssemblyObjective = "Собери машину";

    private Node3D? _loosePartsRoot;
    private readonly List<RigidBody3D> _loosePartBodies = new();

    /// <summary>
    /// Поставить машину в бокс и разложить снятые детали. False — у машины нет
    /// раскладки, старт обычный (на дороге, машина собрана).
    /// </summary>
    private bool PlaceCarAtGarage()
    {
        var session = _car.Session;
        var plan = StartScenario.For(session.Vehicle.Spec.Id);
        if (plan is null) return false;

        var house = HouseNode();
        Node3D? carAnchor = house?.FindChild(CarAnchorName, true, false) as Node3D;
        List<SpotPose>? spots = null;

        Vector3 carScene = _car.GlobalPosition;
        float yaw = _car.Rotation.Y;

        if (carAnchor is not null && house is not null)
        {
            // Якорь задаёт пол бокса, перед машины (−Z) смотрит на ворота.
            carScene = carAnchor.GlobalPosition;
            yaw = carAnchor.GlobalRotation.Y;

            // Шасси над полом: колёса должны лечь на пол, а не провалиться в него.
            float lift = (float)session.Vehicle.Spec.WheelRadiusM + 0.02f;

            _car.GlobalPosition = carScene + Vector3.Up * lift;
            _car.Rotation = new Vector3(0, yaw, 0);
            _car.LinearVelocity = Vector3.Zero;
            _car.AngularVelocity = Vector3.Zero;

            // Дорога рядом не нужна: автопилот проверок пусть едет от машины.
            SteerAlongChunkRoad();

            spots = SpotPoses(house);
        }
        else if (house is not null)
        {
            // Якорей нет (дом старый или арт не подвезли) — прежний путь на смещениях.
            carScene = house.ToGlobal(GarageCarLocal);
            yaw = house.GlobalRotation.Y;

            _car.GlobalPosition = carScene + Vector3.Up * 0.35f;
            _car.Rotation = new Vector3(0, yaw, 0);
            _car.LinearVelocity = Vector3.Zero;
            _car.AngularVelocity = Vector3.Zero;

            SteerAlongChunkRoad();
        }

        var (worldX, worldZ) = WorldOrigin.ToWorld(carScene);
        StartScenario.Apply(session, worldX, carScene.Y, worldZ, yaw, spots);

        SpawnLooseParts();
        GD.Print($"[garage] якорь машины: {(carAnchor is not null ? "есть" : "нет")}, "
                 + $"мест деталей: {spots?.Count ?? 0}; снято {plan.EmptySlots.Count}, "
                 + $"в мире {session.LooseParts.Count}");
        return true;
    }

    /// <summary>Позы якорей деталей <c>PartSpot_N</c> в мировых координатах.</summary>
    private static List<SpotPose> SpotPoses(Node3D house)
    {
        var spots = new List<SpotPose>(8);

        for (int i = 0; i < 8; i++)
        {
            if (house.FindChild($"{PartSpotPrefix}{i}", true, false) is not Node3D node) continue;

            var (x, z) = WorldOrigin.ToWorld(node.GlobalPosition);
            spots.Add(new SpotPose(i, x, node.GlobalPosition.Y, z, node.GlobalRotation.Y));
        }

        return spots;
    }

    /// <summary>Дом игрока: держим узел из мира кусками, иначе ищем по имени.</summary>
    private Node3D? HouseNode()
    {
        if (_houseNode is not null && IsInstanceValid(_houseNode)) return _houseNode;

        return FindChild("PlayerHouse", true, false) as Node3D;
    }

    /// <summary>Тела деталей в мире: по одному на <c>LooseParts</c> (ADR 0003, п. 3).</summary>
    private void SpawnLooseParts()
    {
        ClearHands();

        _loosePartsRoot?.QueueFree();
        _loosePartBodies.Clear();

        _loosePartsRoot = new Node3D { Name = "LooseParts" };
        AddChild(_loosePartsRoot);

        foreach (var loose in _car.Session.LooseParts.Items)
            AddLoosePartBody(loose);
    }

    /// <summary>Тело одной детали по её мировым координатам: и для сейва, и когда игрок её бросил.</summary>
    private RigidBody3D AddLoosePartBody(LoosePart loose)
    {
        if (_loosePartsRoot is null || !IsInstanceValid(_loosePartsRoot))
        {
            _loosePartsRoot = new Node3D { Name = "LooseParts" };
            AddChild(_loosePartsRoot);
        }

        var body = BuildLoosePartBody(loose);
        _loosePartsRoot.AddChild(body);

        // Деталь не должна оказаться в земле: высота из плана может быть ниже двора.
        Vector3 scene = WorldOrigin.ToScene(loose.X, loose.Y, loose.Z);
        Vector3 size = LooseSize(loose);
        float ground = SurfaceUnder(scene, body);
        float y = Mathf.Max(scene.Y, ground + size.Y * 0.5f + 0.05f);

        loose.Y = y;
        body.GlobalPosition = new Vector3(scene.X, y, scene.Z);
        body.GlobalRotation = new Vector3(0, (float)loose.Yaw, 0);
        _loosePartBodies.Add(body);
        return body;
    }

    /// <summary>
    /// Твёрдая поверхность под точкой в пределах полутора метров: пол бокса, стол,
    /// земля. Так деталь не встаёт на крышу (она выше) и не проваливается в двор.
    /// </summary>
    private float SurfaceUnder(Vector3 point, RigidBody3D self)
    {
        var query = PhysicsRayQueryParameters3D.Create(point + Vector3.Up * 0.05f, point - Vector3.Up * 1.5f);
        var exclude = new Godot.Collections.Array<Rid> { _car.GetRid(), self.GetRid() };
        if (_player is not null) exclude.Add(_player.GetRid());
        query.Exclude = exclude;

        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0) return PhysicsHit.Position(hit).Y;

        // Под точкой пусто (деталь в земле двора) — берём первую поверхность сверху.
        return GroundY(point + Vector3.Up);
    }

    private static RigidBody3D BuildLoosePartBody(LoosePart loose)    {
        var slot = loose.Definition?.Slot ?? PartSlot.Body;

        var body = new RigidBody3D
        {
            Name = $"Part_{loose.Id.Replace(':', '_')}",
            Mass = Mathf.Max(1f, (float)(loose.Definition?.MassKg ?? 10)),
            Sleeping = true,
            ContinuousCd = true,
        };
        body.SetMeta("part_id", loose.Id);

        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = LooseSize(loose) } });
        body.AddChild(LoosePartVisual(loose));
        return body;
    }

    /// <summary>
    /// Модель детали от арта (ADR 0003, п. 8): общая деталь — <c>parts/part_&lt;defId&gt;.glb</c>,
    /// панель своей машины — <c>cars/&lt;spec&gt;_&lt;slot id&gt;.glb</c>. Null — модели нет.
    /// </summary>
    private static Node3D? PartModel(PartDefinition def) =>
        ModelLibrary.Instantiate($"res://assets/models/parts/part_{def.Id}.glb")
        ?? (def.SpecId is { } spec
            ? ModelLibrary.Instantiate($"res://assets/models/cars/{spec}_{PartSlotIds.Of(def.Slot)}.glb")
            : null);

    /// <summary>Габарит модели детали: центр от точки крепления и размер; по id детали.</summary>
    private static readonly Dictionary<string, Aabb?> PartBounds = new();

    private static Aabb? Bounds(LoosePart loose)
    {
        if (loose.Definition is not { } def) return null;
        if (PartBounds.TryGetValue(def.Id, out var cached)) return cached;

        Aabb? bounds = null;
        if (PartModel(def) is { } model)
        {
            bounds = PropFactory.Measure(model);
            model.Free();
        }

        PartBounds[def.Id] = bounds;
        return bounds;
    }

    /// <summary>
    /// Центр модели от точки крепления: у агрегата крепление внизу, у панели — в
    /// петле. На полу и в руках деталь держат за центр, иначе она «прыгает».
    /// </summary>
    private static Vector3 PartCenter(LoosePart loose) => Bounds(loose)?.GetCenter() ?? Vector3.Zero;

    /// <summary>Размер тела детали: по модели, а без неё — заглушка по слоту.</summary>
    private static Vector3 LooseSize(LoosePart loose) =>
        Bounds(loose)?.Size ?? LoosePartLook.Size(loose.Definition?.Slot ?? PartSlot.Body);

    /// <summary>
    /// Видимая деталь: модель арта, иначе коробка-заглушка. <paramref name="centered"/> —
    /// центр модели в начале узла (на полу, в руках); иначе начало — точка крепления
    /// (призрак у гнезда).
    /// </summary>
    private static Node3D LoosePartVisual(LoosePart loose, bool centered = true)
    {
        var slot = loose.Definition?.Slot ?? PartSlot.Body;

        if (loose.Definition is { } def && PartModel(def) is { } model)
        {
            if (!centered) return model;

            var root = new Node3D();
            model.Position = -PartCenter(loose);
            root.AddChild(model);
            return root;
        }

        return new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = LoosePartLook.Size(slot) },
            MaterialOverride = LoosePartLook.Material(slot),
        };
    }

    /// <summary>Тот же вид детали, но полупрозрачный — магнит показывает, куда она встанет.</summary>
    private static Node3D GhostVisual(LoosePart loose)
    {
        var ghost = LoosePartVisual(loose, centered: false);
        PaintGhost(ghost);
        return ghost;
    }

    private static void PaintGhost(Node node)
    {
        if (node is MeshInstance3D mesh) mesh.MaterialOverride = LoosePartLook.GhostMaterial();

        foreach (Node child in node.GetChildren())
            PaintGhost(child);
    }

    /// <summary>
    /// Задача сборки важнее дорожной: пока есть пустые гнёзда, HUD показывает
    /// «Собери машину» и сколько осталось. True — дорожную задачу не показывать.
    /// </summary>
    private bool UpdateAssemblyObjective()
    {
        int missing = StartScenario.MissingCount(_car.Session.Vehicle);

        if (missing == 0)
        {
            // Текст мог остаться от прошлого кадра или от сейва — убираем свой.
            if (_hud.Objective.StartsWith(AssemblyObjective))
            {
                _hud.Objective = string.Empty;
                _hud.Toast("Машина собрана");
            }

            return false;
        }

        string text = $"{AssemblyObjective} (осталось {missing})";
        if (_hud.Objective != text) _hud.Objective = text;

        return true;
    }
}

/// <summary>
/// Как выглядит деталь, лежащая в мире, пока у арта нет моделей
/// <c>parts/part_*.glb</c> (ADR 0003, п. 8). Размеры — только для глаза.
/// </summary>
public static class LoosePartLook
{
    private static readonly Dictionary<PartSlot, StandardMaterial3D> Materials = new();
    private static StandardMaterial3D? _ghost;

    public static Vector3 Size(PartSlot slot) => slot switch
    {
        PartSlot.Engine => new Vector3(0.75f, 0.55f, 0.60f),
        PartSlot.Radiator => new Vector3(0.65f, 0.50f, 0.12f),
        PartSlot.Battery => new Vector3(0.30f, 0.22f, 0.35f),
        PartSlot.Hood => new Vector3(1.40f, 0.08f, 1.15f),
        PartSlot.TrunkLid => new Vector3(1.30f, 0.08f, 0.95f),
        PartSlot.DoorFrontLeft or PartSlot.DoorFrontRight
            or PartSlot.DoorRearLeft or PartSlot.DoorRearRight => new Vector3(1.05f, 0.12f, 0.95f),
        PartSlot.WheelFrontLeft or PartSlot.WheelFrontRight
            or PartSlot.WheelRearLeft or PartSlot.WheelRearRight => new Vector3(0.58f, 0.58f, 0.25f),
        _ => new Vector3(0.4f, 0.3f, 0.4f),
    };

    public static StandardMaterial3D Material(PartSlot slot)
    {
        if (Materials.TryGetValue(slot, out var material)) return material;

        material = new StandardMaterial3D
        {
            AlbedoColor = slot switch
            {
                PartSlot.Engine => new Color(0.30f, 0.32f, 0.35f),
                PartSlot.Radiator => new Color(0.45f, 0.47f, 0.44f),
                PartSlot.Battery => new Color(0.22f, 0.26f, 0.35f),
                PartSlot.Hood or PartSlot.TrunkLid => new Color(0.55f, 0.58f, 0.52f),
                PartSlot.DoorFrontLeft or PartSlot.DoorFrontRight
                    or PartSlot.DoorRearLeft or PartSlot.DoorRearRight => new Color(0.50f, 0.53f, 0.48f),
                _ => new Color(0.42f, 0.42f, 0.40f),
            },
            Roughness = 0.85f,
        };
        Materials[slot] = material;
        return material;
    }

    /// <summary>Полупрозрачный вид для магнита: куда деталь встанет (ADR 0003, п. 6).</summary>
    public static StandardMaterial3D GhostMaterial()
    {
        _ghost ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.75f, 0.95f, 0.65f, 0.35f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        return _ghost;
    }

    /// <summary>Освободить материалы, пока движок жив (см. <see cref="StaticCaches"/>).</summary>
    public static void ReleaseCaches()
    {
        foreach (var material in Materials.Values) material.Dispose();
        Materials.Clear();

        _ghost?.Dispose();
        _ghost = null;
    }
}
