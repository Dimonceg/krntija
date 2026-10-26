using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core;
using Krntija.Core.Interaction;
using Krntija.Core.Parts;
using Krntija.Core.Vehicle;

namespace Krntija.Game;

/// <summary>
/// «Установлено / открыто» для показа кузова — из ядра: детали в <c>Vehicle.Parts</c>,
/// открытые панели в <c>OpenSlots</c> (ADR 0003, п. 6). <see cref="Car"/> спрашивает
/// это каждый кадр, поэтому ставить и снимать детали можно без правок показа.
/// </summary>
public sealed class CoreAssemblyState : ICarAssemblyState
{
    private readonly VehicleState _vehicle;

    public CoreAssemblyState(VehicleState vehicle) => _vehicle = vehicle;

    public bool IsInstalled(string socket) =>
        PartSlotIds.TryParse(socket, out var slot) && _vehicle.Parts.ContainsKey(slot);

    public bool IsOpen(string socket) =>
        PartSlotIds.TryParse(socket, out var slot) && AssemblyRules.IsOpen(_vehicle, slot);
}

/// <summary>
/// Руки и магнит (ADR 0003, п. 6): взять деталь из мира (E), нести в метре от
/// камеры, положить (E без гнезда), примагнитить к гнезду и поставить (E),
/// снять установленную (R), открыть панель (E). Правила — в ядре
/// (<see cref="AssemblyRules"/>), здесь только ввод и показ.
/// </summary>
public partial class Main
{
    /// <summary>Деталь в руках: та же запись <c>LooseParts</c>, что и у лежащей на земле.</summary>
    private LoosePart? _held;

    private Node3D? _heldVisual;
    private Node3D? _ghost;
    private PartSlot? _snapSlot;

    private readonly List<(PartSlot Slot, double AngleDeg, double Distance)> _aim = new(16);
    private readonly List<(PartSlot Slot, double Distance)> _snapCandidates = new(16);

    public bool HasPartInHands => _held is not null;

    /// <summary>Насколько далеко от груди висит деталь в руках, метры (рука вытянута).</summary>
    private const float HeldReach = 1.15f;

    /// <summary>Множитель скорости пешком с деталью в руках; перемножается с перегрузом.</summary>
    private double CarrySpeedFactor => _held?.Definition is { } def
        ? AssemblyRules.CarrySpeedFactor(def.MassKg)
        : 1.0;

    private static string PartName(LoosePart loose) => loose.Definition?.Name ?? loose.DefId;

    /// <summary>Деталь в мире берут в руки вплотную: радиус подсказки, метры.</summary>
    private const float LooseReach = 1.6f;

    /// <summary>Кандидаты-детали в мире: id = <c>LooseCandidate - индекс в _loosePartBodies</c>.</summary>
    private const int LooseCandidate = -100;

    /// <summary>Имя детали по её телу в мире (для подсказки «[E] взять …»).</summary>
    private string? LooseName(RigidBody3D body)
    {
        string id = body.GetMeta("part_id").AsString();
        var loose = _car.Session.LooseParts.Find(id);
        return loose is null ? null : PartName(loose);
    }

    /// <summary>
    /// Кандидат «машина»: у детали, на которую смотрит игрок, иначе у середины кузова.
    /// Иначе при взгляде на моторный отсек цель уходит из конуса и проигрывает
    /// далёкой двери дома (проверено --selftest-assembly).
    /// </summary>
    private Candidate CarCandidateTarget(Vector3 from, Func<Vector3, float> angleTo)
    {
        var aimed = AimedPart(from, angleTo);
        Vector3 carAt = (aimed is { } slot ? AimedSocketPosition(slot) : null)
                        ?? _car.GlobalPosition + Vector3.Up * 0.8f;

        return new Candidate(CarCandidate, from.DistanceTo(carAt), angleTo(carAt),
            CarReach, Priority: 1);
    }

    /// <summary>
    /// Детали в мире (ADR 0003, п. 6): тела ездят и катятся, поэтому кандидаты
    /// берутся из их позиций, а не из точек кусков.
    /// </summary>
    private void AddLooseCandidates(Vector3 from, Vector3 eye, Func<Vector3, float> angleTo)
    {
        var looseRules = Rules(InteractionKind.LoosePart);

        for (int i = 0; i < _loosePartBodies.Count; i++)
        {
            var body = _loosePartBodies[i];
            if (!IsInstanceValid(body)) continue;

            Vector3 at = body.GlobalPosition;
            float distance = from.DistanceTo(at);
            bool reachable = distance <= LooseReach + 1f;
            var probe = new InteractionPoint(at, InteractionKind.LoosePart, LooseReach, Node: body);

            _candidates.Add(new Candidate(LooseCandidate - i, distance, angleTo(at), LooseReach,
                looseRules.Priority, looseRules.FromCar, InSight: reachable && InSight(eye, probe)));
        }
    }

    /// <summary>Показать кузов по состоянию ядра: после сборки машины, новой игры и загрузки.</summary>
    private void SyncAssemblyState()
    {
        if (_car.Assembly is { } assembly) assembly.State = new CoreAssemblyState(_car.Session.Vehicle);
    }

    /// <summary>Каждый кадр: деталь висит перед камерой, магнит ищет гнездо.</summary>
    private void UpdateHands()
    {
        if (_held is null)
        {
            if (_heldVisual is not null) ClearHeldVisuals();
            return;
        }

        var camera = GetViewport().GetCamera3D();
        if (camera is null) return;

        _heldVisual ??= AddHeldVisual(_held);

        // Деталь висит перед игроком на вытянутой руке — по направлению взгляда,
        // с наклоном: так её можно поднести к гнезду и в моторном отсеке, и внизу.
        // Магнит (0,6 м от детали до гнезда) считается от этой точки (ADR 0003, п. 2).
        Vector3 look = -camera.GlobalBasis.Z;
        if (look.LengthSquared() < 0.01f) look = -_car.GlobalBasis.Z;
        look = look.Normalized();

        Vector3 at = _player is not null
            ? _player.GlobalPosition + Vector3.Up * 1.35f + look * HeldReach
            : camera.GlobalPosition + look * 1.0f;

        _heldVisual.GlobalPosition = at;
        _heldVisual.GlobalRotation = new Vector3(0, camera.GlobalRotation.Y, 0);

        UpdateMagnet();
    }

    private Node3D AddHeldVisual(LoosePart loose)
    {
        var visual = LoosePartVisual(loose);
        visual.Name = "HeldPart";
        AddChild(visual);
        return visual;
    }

    /// <summary>Магнит: ближайшее свободное подходящее гнездо в радиусе ядра.</summary>
    private void UpdateMagnet()
    {
        if (_held?.Definition is not { } def)
        {
            ClearMagnet();
            return;
        }

        _snapCandidates.Clear();
        Vector3 from = _heldVisual?.GlobalPosition ?? _car.GlobalPosition;

        if (CarLayout.For(_car.Session.Vehicle.Spec.Id) is { } layout)
        {
            foreach (var spec in layout.Slots)
            {
                if (SnapPoint(spec.Slot, _held) is not { } at) continue;
                _snapCandidates.Add((spec.Slot, from.DistanceTo(at)));
            }
        }

        var snap = AssemblyRules.SnapTarget(_car.Session.Vehicle, def, _snapCandidates);
        if (snap != _snapSlot)
        {
            ClearMagnet();
            _snapSlot = snap;

            if (snap is not null)
            {
                _ghost = GhostVisual(_held);
                _ghost.Name = "SnapGhost";
                AddChild(_ghost);
            }
        }

        if (_ghost is not null && _snapSlot is { } target
            && _car.SocketGlobal(PartSlotIds.Of(target)) is { } socket)
            _ghost.GlobalTransform = socket;
    }

    private void ClearMagnet()
    {
        _ghost?.QueueFree();
        _ghost = null;
        _snapSlot = null;
    }

    private void ClearHeldVisuals()
    {
        _heldVisual?.QueueFree();
        _heldVisual = null;
        ClearMagnet();
    }

    private void ClearHands()
    {
        _held = null;
        ClearHeldVisuals();
    }

    /// <summary>Взять деталь из мира в руки (E на лежащей детали).</summary>
    private void TakeLoosePart(RigidBody3D body)
    {
        if (_held is not null)
        {
            _hud.Toast("Руки заняты: сначала поставь или положи");
            return;
        }

        string id = body.GetMeta("part_id").AsString();
        var loose = _car.Session.LooseParts.Find(id);
        if (loose is null) return;

        _loosePartBodies.Remove(body);
        body.QueueFree();

        _held = loose;
        _hud.Toast($"{PartName(loose)}: в руках");
        Sfx.PlayUi();
    }

    /// <summary>Снять установленную деталь в руки (R). Правила — в ядре.</summary>
    private void TakeAimedPart()
    {
        if (_held is not null)
        {
            _hud.Toast("Руки заняты: сначала поставь или положи");
            return;
        }

        var target = SelectTarget();
        if (target.Kind != TargetKind.CarPart || target.Slot is not { } slot)
        {
            _hud.Toast("Наведись на деталь машины");
            return;
        }

        var vehicle = _car.Session.Vehicle;
        switch (AssemblyRules.CanRemove(vehicle, slot))
        {
            case AssemblyResult.NeedsAccess:
                _hud.Toast("Сначала открой капот");
                return;
            case AssemblyResult.NotRemovable:
                _hud.Toast("Эта деталь не снимается");
                return;
            case AssemblyResult.Empty:
                return;
        }

        string name = vehicle.Get(slot)?.Name ?? "деталь";
        var part = AssemblyRules.Remove(vehicle, slot);
        if (part is null) return;

        var (x, y, z) = PlayerWorldPoint();
        var loose = _car.Session.LooseParts.Add(
            part.Definition.Id, part.Condition, x, y, z, PlayerYaw, LooseParts.World);

        _held = loose;
        _hud.Toast($"{name}: в руках");
        Sfx.PlayDoor(_car.GlobalPosition);
    }

    /// <summary>E у детали на машине: панель открыть-закрыть (иначе — подсказка про R).</summary>
    private void UseCarPart(PartSlot slot)
    {
        if (_held is not null)
        {
            InstallHeld();
            return;
        }

        var vehicle = _car.Session.Vehicle;
        if (CarLayout.For(vehicle.Spec.Id)?.Find(slot) is not { Opens: true })
        {
            switch (AssemblyRules.CanRemove(vehicle, slot))
            {
                case AssemblyResult.Ok:
                    _hud.Toast("Снять деталь — R");
                    break;
                case AssemblyResult.NeedsAccess:
                    _hud.Toast("Сначала открой капот");
                    break;
                case AssemblyResult.NotRemovable:
                    _hud.Toast("Эта деталь не снимается");
                    break;
            }

            return;
        }

        bool open = !AssemblyRules.IsOpen(vehicle, slot);
        if (!AssemblyRules.SetOpen(vehicle, slot, open)) return;
        Sfx.PlayDoor(_car.GlobalPosition);
    }

    /// <summary>Поставить деталь из рук в гнездо магнита; нет гнезда — положить.</summary>
    private void InstallHeld()
    {
        if (_held is null) return;

        if (_snapSlot is not { } slot || _held.ToPart() is not { } part)
        {
            DropHeld();
            return;
        }

        string name = PartName(_held);
        switch (AssemblyRules.Install(_car.Session.Vehicle, slot, part))
        {
            case AssemblyResult.Ok:
                _car.Session.LooseParts.Remove(_held.Id);
                _hud.Toast($"{name}: поставлено");
                Sfx.PlayDoor(_car.GlobalPosition);
                ClearHands();
                break;
            case AssemblyResult.NeedsAccess:
                _hud.Toast("Сначала открой капот");
                break;
            case AssemblyResult.Occupied:
                _hud.Toast("Гнездо занято");
                break;
            default:
                _hud.Toast("Сюда не встаёт");
                break;
        }
    }

    /// <summary>Положить деталь перед собой (нет гнезда магнита или так решил игрок).</summary>
    private void DropHeld()
    {
        if (_held is null) return;

        var (x, y, z) = PlayerWorldPoint(forward: 0.7, up: 0.35);
        _held.X = x;
        _held.Y = y;
        _held.Z = z;
        _held.Yaw = PlayerYaw;
        AddLoosePartBody(_held);

        _hud.Toast($"{PartName(_held)}: положено");
        Sfx.PlayUi();
        ClearHands();
    }

    /// <summary>Перед сейвом деталь в руках сохраняется у ног игрока (ADR 0003, п. 3).</summary>
    private void SyncHeldToFeet()
    {
        if (_held is null) return;

        var (x, y, z) = PlayerWorldPoint(up: 0.2);
        _held.X = x;
        _held.Y = y;
        _held.Z = z;
        _held.Yaw = PlayerYaw;
    }

    private float PlayerYaw => _player?.GlobalRotation.Y ?? _car.GlobalRotation.Y;

    /// <summary>Точка у ног игрока в мировых координатах: куда лечь детали.</summary>
    private (double X, double Y, double Z) PlayerWorldPoint(double forward = 0, double up = 0)
    {
        Vector3 at = _player?.GlobalPosition ?? _car.GlobalPosition;

        if (forward > 0)
        {
            Vector3 dir = -(_player?.GlobalBasis ?? _car.GlobalBasis).Z;
            dir.Y = 0;
            at += dir.Normalized() * (float)forward;
        }

        at += Vector3.Up * (float)up;
        var (x, z) = WorldOrigin.ToWorld(at);
        return (x, at.Y, z);
    }

    /// <summary>Мировая точка гнезда: у открытой панели — её видимый узел, иначе гнездо.</summary>
    /// <summary>
    /// Куда подносить деталь для магнита: центр её модели, стоящей в гнезде. Деталь
    /// в руках держат за центр, а у двери петля в полуметре от него (B1).
    /// </summary>
    private Vector3? SnapPoint(PartSlot slot, LoosePart part) =>
        _car.SocketGlobal(PartSlotIds.Of(slot)) is { } socket ? socket * PartCenter(part) : null;

    private Vector3? SocketPosition(PartSlot slot)
    {
        string id = PartSlotIds.Of(slot);

        if (_car.Assembly?.PartNode(id) is { } part && IsInstanceValid(part))
            return part.GlobalPosition;

        return _car.SocketGlobal(id)?.Origin;
    }

    /// <summary>
    /// Гнездо под взглядом: угол до гнезда и расстояние от игрока считает сцена,
    /// выбирает ядро (<see cref="AssemblyRules.AimedSlot"/>). null — не смотрит на деталь.
    /// </summary>
    private PartSlot? AimedPart(Vector3 from, Func<Vector3, float> angleTo)
    {
        var vehicle = _car.Session.Vehicle;
        if (CarLayout.For(vehicle.Spec.Id) is not { } layout) return null;

        _aim.Clear();
        foreach (var spec in layout.Slots)
        {
            if (!vehicle.Parts.ContainsKey(spec.Slot)) continue;
            if (SocketPosition(spec.Slot) is not { } at) continue;

            _aim.Add((spec.Slot, angleTo(at), from.DistanceTo(at)));
        }

        return AssemblyRules.AimedSlot(_aim);
    }

    /// <summary>Точка гнезда, на которое смотрит игрок (для кандидата машины).</summary>
    private Vector3? AimedSocketPosition(PartSlot slot) => SocketPosition(slot);

    /// <summary>Подсказка, когда в руках деталь.</summary>
    private string HandsPrompt()
    {
        if (_held is null) return string.Empty;

        string name = PartName(_held);
        if (_snapSlot is { } slot)
        {
            string where = _car.Session.Vehicle.Get(slot)?.Name ?? name;
            return $"[E] установить: {where}";
        }

        return $"[E] положить {name}";
    }

    /// <summary>Подсказка у детали на машине: что сделает E и что снимет R.</summary>
    private string CarPartPrompt(PartSlot slot)
    {
        var vehicle = _car.Session.Vehicle;
        string name = (vehicle.Get(slot)?.Name ?? "деталь").ToLowerInvariant();
        var canRemove = AssemblyRules.CanRemove(vehicle, slot);
        string remove = canRemove == AssemblyResult.Ok ? $"  [R] снять {name}" : string.Empty;

        if (CarLayout.For(vehicle.Spec.Id)?.Find(slot) is not { Opens: true })
            return canRemove == AssemblyResult.Ok ? $"[R] снять {name}" : string.Empty;

        string action = AssemblyRules.IsOpen(vehicle, slot) ? "закрыть" : "открыть";
        return $"[E] {action} {name}{remove}";
    }
}
