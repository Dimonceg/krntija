using System.Collections.Generic;
using Godot;
using Krntija.Core;
using Krntija.Core.Parts;

namespace Krntija.Game;

/// <summary>
/// Старт в гараже дома (ADR 0003, п. 5): машина у ворот гаража, снятые детали
/// лежат рядом, в HUD — задача «Собери машину», пока все гнёзда не заняты.
/// </summary>
/// <remarks>
/// Где стоят машина и детали, решает ядро (<see cref="StartScenario"/>); сцена
/// только поворачивает смещения на угол дома и ставит тела. Якоря
/// <c>GarageCarAnchor</c> и <c>PartSpot_*</c> из модели дома появятся у арт-модели
/// (B3), до тех пор берём смещения от ворот гаража процедурного дома.
/// </remarks>
public partial class Main
{
    /// <summary>
    /// Машина перед воротами гаража (проём в фасаде x 2.5…5.5, пол на y = 0),
    /// носом от дома. В нынешнем гараже (3,3 × 4 м) вокруг машины не пройти, а
    /// к моторному отсеку нужно подходить спереди — арт B3 расширит гараж, и
    /// якорь <c>GarageCarAnchor</c> поставит машину внутрь.
    /// </summary>
    private static readonly Vector3 GarageCarLocal = new(4.0f, 0f, -6.4f);

    private const string AssemblyObjective = "Собери машину";

    private Node3D? _loosePartsRoot;
    private readonly List<RigidBody3D> _loosePartBodies = new();

    /// <summary>
    /// Поставить машину к гаражу и разложить снятые детали. False — у машины нет
    /// раскладки, старт обычный (на дороге, машина собрана).
    /// </summary>
    private bool PlaceCarAtGarage()
    {
        var session = _car.Session;
        var plan = StartScenario.For(session.Vehicle.Spec.Id);
        if (plan is null) return false;

        Vector3 carScene = _car.GlobalPosition;
        float yaw = _car.Rotation.Y;

        if (HouseNode() is { } house)
        {
            carScene = house.ToGlobal(GarageCarLocal);
            yaw = house.GlobalRotation.Y;

            _car.GlobalPosition = carScene + Vector3.Up * 0.35f;
            _car.Rotation = new Vector3(0, yaw, 0);
            _car.LinearVelocity = Vector3.Zero;
            _car.AngularVelocity = Vector3.Zero;

            // Дорога рядом не нужна: автопилот проверок пусть едет от машины.
            SteerAlongChunkRoad();
        }

        var (worldX, worldZ) = WorldOrigin.ToWorld(carScene);
        StartScenario.Apply(session, worldX, carScene.Y, worldZ, yaw);

        SpawnLooseParts();
        GD.Print($"[garage] {plan.EmptySlots.Count} деталей снято, {session.LooseParts.Count} лежат у гаража");
        return true;
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

        // Деталь не должна оказаться в земле: старт в гараже задаёт высоту от пола
        // дома, а двор может быть выше. Ставим на первую твёрдую поверхность.
        Vector3 scene = WorldOrigin.ToScene(loose.X, loose.Y, loose.Z);
        Vector3 size = LoosePartLook.Size(loose.Definition?.Slot ?? PartSlot.Body);
        float ground = GroundY(scene + Vector3.Up * 2f);
        float y = Mathf.Max(scene.Y, ground + size.Y * 0.5f + 0.05f);

        loose.Y = y;
        body.GlobalPosition = new Vector3(scene.X, y, scene.Z);
        body.GlobalRotation = new Vector3(0, (float)loose.Yaw, 0);
        _loosePartBodies.Add(body);
        return body;
    }

    private static RigidBody3D BuildLoosePartBody(LoosePart loose)
    {
        var slot = loose.Definition?.Slot ?? PartSlot.Body;

        var body = new RigidBody3D
        {
            Name = $"Part_{loose.Id.Replace(':', '_')}",
            Mass = Mathf.Max(1f, (float)(loose.Definition?.MassKg ?? 10)),
            Sleeping = true,
            ContinuousCd = true,
        };
        body.SetMeta("part_id", loose.Id);

        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = LoosePartLook.Size(slot) } });
        body.AddChild(LoosePartVisual(loose));
        return body;
    }

    /// <summary>Видимая деталь: модель арта (ADR 0003, п. 8), иначе коробка-заглушка.</summary>
    private static Node3D LoosePartVisual(LoosePart loose)
    {
        var slot = loose.Definition?.Slot ?? PartSlot.Body;

        var model = loose.Definition is { } def
            ? ModelLibrary.Instantiate($"res://assets/models/parts/part_{def.Id}.glb")
            : null;

        if (model is not null) return model;

        return new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = LoosePartLook.Size(slot) },
            MaterialOverride = LoosePartLook.Material(slot),
        };
    }

    /// <summary>Тот же вид детали, но полупрозрачный — магнит показывает, куда она встанет.</summary>
    private static Node3D GhostVisual(LoosePart loose)
    {
        var ghost = LoosePartVisual(loose);
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
