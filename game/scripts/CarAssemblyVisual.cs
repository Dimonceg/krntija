using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Что установлено и что открыто — для показа машины из деталей (ADR 0003).
/// Правила (можно ли снять, доступ под капот) решает ядро; сцена только
/// спрашивает это состояние. Id гнёзд — <see cref="CarAssemblyVisual.Sockets"/>.
/// </summary>
public interface ICarAssemblyState
{
    bool IsInstalled(string socket);
    bool IsOpen(string socket);
}

/// <summary>
/// Простое состояние в сцене: всё установлено и закрыто, пока не сказано иное.
/// Им пользуются проверки и машина, пока ядро сборки (ADR 0003, A1) не подключено.
/// </summary>
public sealed class SceneAssemblyState : ICarAssemblyState
{
    public HashSet<string> Removed { get; } = new();
    public HashSet<string> Open { get; } = new();

    public bool IsInstalled(string socket) => !Removed.Contains(socket);
    public bool IsOpen(string socket) => Open.Contains(socket);
}

/// <summary>
/// Кузов из деталей по гнёздам (ADR 0003, п. 7–8). Лежит под корнем <c>Body</c>
/// (<see cref="CarUpgradeVisuals"/>), поэтому рессоры поднимают и детали.
/// Колёса — у <see cref="Car"/>: они участвуют в подвеске.
/// </summary>
/// <remarks>
/// Гнездо — узел <c>Socket_&lt;id&gt;</c>; у открывающихся ещё <c>Open_&lt;id&gt;</c>, и деталь
/// плавно идёт между двумя трансформами. Если в оболочке от арта узлов нет —
/// гнёзда, углы открывания и коробки-заглушки считаются по габаритам кузова,
/// чтобы механика рук работала до моделей.
/// </remarks>
public partial class CarAssemblyVisual : Node3D
{
    /// <summary>Гнёзда кузова (без колёс) — id из ADR 0003, п. 1.</summary>
    public static readonly string[] Sockets =
    {
        "engine", "radiator", "battery",
        "door_fl", "door_fr", "door_rl", "door_rr", "hood", "trunk",
    };

    /// <summary>Гнёзда колёс в порядке <c>Car.Anchors</c>: FL, FR, RL, RR.</summary>
    public static readonly string[] WheelSockets = { "wheel_fl", "wheel_fr", "wheel_rl", "wheel_rr" };

    /// <summary>Модель детали по умолчанию в гнезде: <c>parts/part_&lt;defId&gt;.glb</c>.</summary>
    private static readonly Dictionary<string, string> DefaultPart = new()
    {
        ["engine"] = "engine_1_6", ["radiator"] = "radiator_std", ["battery"] = "battery_std",
    };

    /// <summary>Скорость открывания: доля пути за секунду.</summary>
    private const float OpenSpeed = 2.5f;

    private sealed class Slot
    {
        public required Node3D Socket;
        public required Node3D Part;
        public Transform3D Closed;
        public Transform3D? Opened;
        public float Weight;
    }

    private readonly Dictionary<string, Slot> _slots = new();

    /// <summary>
    /// Без оболочки от арта виден цельный кузов: закрытая панель уже нарисована на нём,
    /// поэтому заглушку панели показываем только открытой.
    /// </summary>
    private bool _wholeBody;
    private ICarAssemblyState _state = new SceneAssemblyState();

    /// <summary>Есть ли у машины гнёзда — решает ядро (<see cref="Krntija.Core.CarLayout"/>).</summary>
    public static bool HasLayout(string specId) => Krntija.Core.CarLayout.For(specId) is not null;

    /// <summary>Откуда брать «установлено» и «открыто». Сменить можно в любой момент.</summary>
    public ICarAssemblyState State
    {
        get => _state;
        set
        {
            _state = value;
            Sync(immediate: true);
        }
    }

    /// <summary>Мировой трансформ гнезда в закрытом положении; null — такого гнезда нет.</summary>
    public Transform3D? SocketGlobal(string socket) =>
        _slots.TryGetValue(socket, out var slot) ? slot.Socket.GlobalTransform : null;

    /// <summary>Узел установленной детали: её обводит подсветка, по ней выбирается цель.</summary>
    public Node3D? PartNode(string socket) =>
        _slots.TryGetValue(socket, out var slot) && _state.IsInstalled(socket) ? slot.Part : null;

    /// <summary>Насколько открыта деталь: 0 — закрыта, 1 — открыта. Для проверок.</summary>
    public float OpenWeight(string socket) => _slots.TryGetValue(socket, out var slot) ? slot.Weight : 0f;

    /// <param name="parent">Корень кузова (<c>Body</c>).</param>
    /// <param name="shell">Оболочка от арта с узлами гнёзд или null — тогда заглушки.</param>
    /// <param name="bounds">Габариты кузова в координатах машины: по ним — гнёзда-заглушки.</param>
    public static CarAssemblyVisual Build(Node3D parent, string specId, Node3D? shell, Aabb bounds)
    {
        var assembly = new CarAssemblyVisual { Name = "Assembly", _wholeBody = shell is null };
        parent.AddChild(assembly);

        var fallback = FallbackLayout(bounds);
        foreach (string id in Sockets)
        {
            var socket = shell?.FindChild($"Socket_{id}", true, false) as Node3D;
            var open = shell?.FindChild($"Open_{id}", true, false) as Node3D;
            var (closedLocal, openLocal, size) = fallback[id];

            if (socket is null)
            {
                socket = new Node3D { Name = $"Socket_{id}", Transform = closedLocal };
                assembly.AddChild(socket);
            }

            // Машина строится до входа в дерево: глобальных трансформов ещё нет.
            Transform3D closed = InBody(socket);
            Transform3D? opened = open is not null ? InBody(open) : openLocal;

            var part = PartModel(specId, id) ?? Placeholder(id, size);
            part.Name = $"Part_{id}";
            part.Transform = closed;
            assembly.AddChild(part);

            assembly._slots[id] = new Slot { Socket = socket, Part = part, Closed = closed, Opened = opened };
        }

        return assembly;
    }

    /// <summary>Трансформ узла в координатах корня кузова (там же, где <c>Assembly</c>).</summary>
    private static Transform3D InBody(Node3D node)
    {
        Transform3D t = node.Transform;
        for (var parent = node.GetParent() as Node3D; parent is not null and not CarUpgradeVisuals; parent = parent.GetParent() as Node3D)
            t = parent.Transform * t;
        return t;
    }

    public override void _Process(double delta)
    {
        foreach (var (id, slot) in _slots)
        {
            if (slot.Opened is not { } opened) continue;

            float target = _state.IsOpen(id) ? 1f : 0f;
            if (Mathf.IsEqualApprox(slot.Weight, target)) continue;

            slot.Weight = Mathf.MoveToward(slot.Weight, target, (float)delta * OpenSpeed);
            slot.Part.Transform = slot.Closed.InterpolateWith(opened, Mathf.SmoothStep(0f, 1f, slot.Weight));
            ShowPart(id, slot);
        }
    }

    private void ShowPart(string id, Slot slot)
    {
        bool visible = _state.IsInstalled(id) && !(_wholeBody && slot.Opened is not null && slot.Weight < 0.01f);
        if (slot.Part.Visible != visible) slot.Part.Visible = visible;
    }

    /// <summary>Показать установленное; дёшево, можно звать каждый кадр.</summary>
    public void Sync(bool immediate = false)
    {
        foreach (var (id, slot) in _slots)
        {
            if (immediate && slot.Opened is { } opened)
            {
                slot.Weight = _state.IsOpen(id) ? 1f : 0f;
                slot.Part.Transform = slot.Closed.InterpolateWith(opened, slot.Weight);
            }

            ShowPart(id, slot);
        }
    }

    /// <summary>Панель своей машины (<c>&lt;spec&gt;_&lt;id&gt;.glb</c>) или общая деталь (<c>parts/part_&lt;defId&gt;.glb</c>).</summary>
    private static Node3D? PartModel(string specId, string id) =>
        DefaultPart.TryGetValue(id, out string? defId)
            ? ModelLibrary.Instantiate($"res://assets/models/parts/part_{defId}.glb")
            : ModelLibrary.Instantiate($"res://assets/models/cars/{specId}_{id}.glb");

    private static StandardMaterial3D? _panelMaterial, _partMaterial;

    /// <summary>Коробка вместо модели: смещена от петли так же, как будет смещена панель.</summary>
    private static Node3D Placeholder(string id, (Vector3 Size, Vector3 Offset) box)
    {
        bool panel = !DefaultPart.ContainsKey(id);
        var root = new Node3D();
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = box.Size },
            Position = box.Offset,
            MaterialOverride = panel
                ? _panelMaterial ??= new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.6f, 0.52f), Roughness = 0.8f }
                : _partMaterial ??= new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.26f, 0.27f), Metallic = 0.4f, Roughness = 0.6f },
        });
        return root;
    }

    /// <summary>
    /// Гнёзда без модели оболочки: петли и углы по габаритам кузова (перед −Z).
    /// Начало гнезда — петля, коробка смещена от неё; открытое — поворот вокруг петли.
    /// </summary>
    private static Dictionary<string, (Transform3D Closed, Transform3D? Open, (Vector3 Size, Vector3 Offset) Box)> FallbackLayout(Aabb b)
    {
        float halfW = b.Size.X * 0.5f * 0.86f; // без зеркал
        float front = b.Position.Z, back = b.End.Z, length = b.Size.Z;
        float bottom = b.Position.Y, height = b.Size.Y;
        float belt = bottom + height * 0.55f;   // линия капота и низа стёкол
        float doorY = bottom + height * 0.18f;  // низ двери

        float hoodLength = length * 0.27f, trunkLength = length * 0.2f, doorLength = length * 0.25f;
        float hoodHinge = front + hoodLength, trunkHinge = back - trunkLength;
        float frontDoorHinge = front + length * 0.36f, rearDoorHinge = frontDoorHinge + doorLength + 0.04f;

        Transform3D At(Vector3 p) => new(Basis.Identity, p);
        Transform3D Turn(Vector3 p, Vector3 axis, float degrees) => new(new Basis(axis, Mathf.DegToRad(degrees)), p);

        var doorBox = (new Vector3(0.05f, height * 0.5f, doorLength), new Vector3(0, height * 0.25f, doorLength * 0.5f));
        var layout = new Dictionary<string, (Transform3D, Transform3D?, (Vector3, Vector3))>
        {
            // Капот: петля у лобового стекла, передний край уходит вверх.
            ["hood"] = (At(new Vector3(0, belt, hoodHinge)), Turn(new Vector3(0, belt, hoodHinge), Vector3.Right, 60),
                (new Vector3(halfW * 1.7f, 0.04f, hoodLength), new Vector3(0, 0.02f, -hoodLength * 0.5f))),
            // Багажник: петля у заднего стекла, задний край вверх.
            ["trunk"] = (At(new Vector3(0, belt, trunkHinge)), Turn(new Vector3(0, belt, trunkHinge), Vector3.Right, -70),
                (new Vector3(halfW * 1.6f, 0.04f, trunkLength), new Vector3(0, 0.02f, trunkLength * 0.5f))),
            // Двери: петля спереди, задний край уходит наружу.
            ["door_fl"] = (At(new Vector3(-halfW, doorY, frontDoorHinge)), Turn(new Vector3(-halfW, doorY, frontDoorHinge), Vector3.Up, -70), doorBox),
            ["door_fr"] = (At(new Vector3(halfW, doorY, frontDoorHinge)), Turn(new Vector3(halfW, doorY, frontDoorHinge), Vector3.Up, 70), doorBox),
            ["door_rl"] = (At(new Vector3(-halfW, doorY, rearDoorHinge)), Turn(new Vector3(-halfW, doorY, rearDoorHinge), Vector3.Up, -70), doorBox),
            ["door_rr"] = (At(new Vector3(halfW, doorY, rearDoorHinge)), Turn(new Vector3(halfW, doorY, rearDoorHinge), Vector3.Up, 70), doorBox),
            // Моторный отсек: под капотом, точка крепления — низ детали.
            ["engine"] = (At(new Vector3(0, bottom + 0.2f, front + hoodLength * 0.55f)), null,
                (new Vector3(0.6f, 0.45f, 0.55f), new Vector3(0, 0.22f, 0))),
            ["radiator"] = (At(new Vector3(0, bottom + 0.22f, front + 0.16f)), null,
                (new Vector3(0.62f, 0.4f, 0.07f), new Vector3(0, 0.2f, 0))),
            ["battery"] = (At(new Vector3(-halfW * 0.6f, bottom + 0.35f, front + hoodLength * 0.75f)), null,
                (new Vector3(0.26f, 0.2f, 0.18f), new Vector3(0, 0.1f, 0))),
        };
        return layout;
    }

    internal static void ReleaseCaches()
    {
        StaticCaches.Dispose(ref _panelMaterial);
        StaticCaches.Dispose(ref _partMaterial);
    }
}
