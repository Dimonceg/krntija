using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Krntija.Core;
using Krntija.Core.Economy;
using Krntija.Core.Interaction;
using Krntija.Core.Items;
using Krntija.Core.Parts;
using Krntija.Core.Vehicle;
using Krntija.Core.Workshop;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>Точки взаимодействия: подсказки, клавиша E, лавка, журнал, скупщик, дом.</summary>
public partial class Main
{
    private enum InteractionKind
    {
        Pump,
        Shop,
        Gate,
        Sleep,
        Door,
        Loot,
        SellAll,
        Drink,
        Pet,
        Talk,
        RepairExit,
        ClearCollapse,
        Chest,
        Workbench,
        SleepHome,
        Crate,

        /// <summary>Деталь, лежащая в мире (ADR 0003, п. 6): тела в <c>_loosePartBodies</c>.</summary>
        LoosePart,
    }

    /// <param name="StateId">Id в <see cref="WorldState"/>, если действие меняет мир и сохраняется.</param>
    /// <param name="Chunk">Кусок мира, которому принадлежит точка: снимается при его выгрузке.</param>
    /// <param name="Visual">Что обвести, когда точка выбрана, если это не <paramref name="Node"/> и не дверь.</param>
    private sealed record InteractionPoint(Vector3 Position, InteractionKind Kind, float Radius,
        CheckpointGate? Gate = null, Door? Door = null, string? LootId = null, Node3D? Node = null,
        string? StateId = null, int Chunk = -1, Node3D? Visual = null)
    {
        /// <summary>Модель цели: её обводит подсветка, её коллизия не закрывает точку от взгляда.</summary>
        public Node3D? Body => Visual ?? Node ?? Door;
    }

    private readonly List<InteractionPoint> _interactions = new();

    /// <summary>Что сделает E: точка мира, деталь в мире, деталь на машине, сесть, выйти…</summary>
    private enum TargetKind
    {
        None,
        Point,
        EnterCar,
        ExitCar,
        StopFirst,

        /// <summary>Деталь в мире: E берёт в руки.</summary>
        LoosePart,

        /// <summary>Деталь на машине: E открывает панель, R снимает.</summary>
        CarPart,
    }

    private readonly record struct InteractionTarget(TargetKind Kind, InteractionPoint? Point = null,
        PartSlot? Slot = null, RigidBody3D? Body = null);

    /// <summary>Id машины среди кандидатов: точки идут по индексу в <see cref="_interactions"/>.</summary>
    private const int CarCandidate = -1;

    /// <summary>Пешком ближе этого к машине — сесть можно и не глядя на неё.</summary>
    private const float CarReach = 3.5f;

    private readonly List<Candidate> _candidates = new();

    /// <summary>
    /// Приоритет точки и можно ли сделать это из машины. Новая точка — строка здесь;
    /// правило выбора (взгляд, радиус, состояние) — в ядре, <see cref="TargetSelector"/>.
    /// </summary>
    private static (int Priority, bool FromCar) Rules(InteractionKind kind) => kind switch
    {
        InteractionKind.Pump or InteractionKind.Shop or InteractionKind.Gate or InteractionKind.SellAll => (1, true),

        // Деталь под ногами важнее машины: её и берут, когда смотрят на неё.
        InteractionKind.LoosePart => (2, false),
        _ => (0, false),
    };

    /// <summary>Один выбор цели и для клавиши E, и для подсказки — разойтись они не могут.</summary>
    private InteractionTarget SelectTarget()
    {
        if (_hud.Screen != HudScreen.None) return default;

        bool onFoot = _onFoot && _player is not null;
        Vector3 from = onFoot ? _player!.GlobalPosition : _car.GlobalPosition;
        Vector3 eye = from + Vector3.Up * 1.5f;
        Vector3? forward = GetViewport().GetCamera3D() is { } camera ? -camera.GlobalBasis.Z : null;

        float AngleTo(Vector3 target) =>
            forward is { } look ? Mathf.RadToDeg((target - eye).AngleTo(look)) : 180f;

        var stance = onFoot ? Stance.OnFoot : _car.SpeedMps < 2f ? Stance.InCarStopped : Stance.InCarMoving;

        _candidates.Clear();
        for (int i = 0; i < _interactions.Count; i++)
        {
            var point = _interactions[i];
            var (priority, fromCar) = Rules(point.Kind);
            float distance = from.DistanceTo(point.Position);

            // Луч — только к тем, кого вообще можно выбрать: точек в мире сотни.
            bool reachable = stance switch
            {
                Stance.OnFoot => distance <= Mathf.Max(point.Radius, (float)TargetSelector.LookDistance),
                Stance.InCarStopped => fromCar && distance <= point.Radius,
                _ => false,
            };
            _candidates.Add(new Candidate(i, distance, AngleTo(point.Position), point.Radius, priority, fromCar,
                InSight: reachable && InSight(eye, point)));
        }

        if (onFoot)
        {
            _candidates.Add(CarCandidateTarget(from, AngleTo));
            AddLooseCandidates(from, eye, AngleTo);
        }

        if (TargetSelector.Select(stance, _candidates) is { } chosen)
        {
            if (chosen.Id == CarCandidate)
            {
                // Смотрим на деталь машины — E открывает панель, R снимает (ADR 0003, п. 6).
                return AimedPart(from, AngleTo) is { } slot
                    ? new InteractionTarget(TargetKind.CarPart, Slot: slot)
                    : new InteractionTarget(TargetKind.EnterCar);
            }

            if (chosen.Id <= LooseCandidate)
                return new InteractionTarget(TargetKind.LoosePart,
                    Body: _loosePartBodies[LooseCandidate - chosen.Id]);

            return new InteractionTarget(TargetKind.Point, _interactions[chosen.Id]);
        }

        return stance switch
        {
            Stance.InCarStopped => new InteractionTarget(TargetKind.ExitCar),
            Stance.InCarMoving => new InteractionTarget(TargetKind.StopFirst),
            _ => default,
        };
    }

    private string BuildPrompt() => BuildPrompt(SelectTarget());

    private string BuildPrompt(InteractionTarget target)
    {
        // В руках деталь — подсказка только про неё: поставить или положить. Здесь,
        // а не выше: экран обновляет UpdateFocus через этот вариант.
        if (_held is not null) return HandsPrompt();

        return target.Kind switch
        {
            TargetKind.Point => PromptFor(target.Point!),
            TargetKind.LoosePart => target.Body is not null && LooseName(target.Body) is { } name
                ? $"[E] взять {name}"
                : "[E] взять деталь",
            TargetKind.CarPart => CarPartPrompt(target.Slot!.Value),
            TargetKind.EnterCar => "[E] сесть в машину",
            TargetKind.ExitCar => "[E] выйти из машины",
            TargetKind.None when _onFoot && _hud.Screen == HudScreen.None =>
                "мышь — обзор, W/S — идти, A/D — шаг вбок, Shift — бежать",
            _ => string.Empty,
        };
    }

    private static string PromptFor(InteractionPoint point) => point.Kind switch
    {
        InteractionKind.Shop => "[E] зайти в лавку",
        InteractionKind.SellAll => "[E] сдать всё скупщику",
        InteractionKind.Drink => "[E] попить из колодца",
        InteractionKind.Pet => "[E] погладить собаку",
        InteractionKind.Talk => "[E] поговорить",
        InteractionKind.RepairExit => $"[E] починить выезд ({Krntija.Core.Economy.Money.Format(WorldActions.ExitRepairPrice)} + изолента)",
        InteractionKind.ClearCollapse => "[E] разобрать завал",
        InteractionKind.Chest => "[E] открыть сундук",
        InteractionKind.Workbench => $"[E] верстак: починить деталь ({Krntija.Core.Economy.Money.Format(WorldActions.WorkbenchPrice)} + изолента)",
        InteractionKind.SleepHome => "[E] поспать дома",
        InteractionKind.Crate => "[E] открыть ящик",
        InteractionKind.Gate => point.Gate is { IsOpen: true } ? "[E] шлагбаум открыт" : $"[E] заплатить {Krntija.Core.Economy.Money.Format(point.Gate?.Toll ?? WorldActions.GateToll)} за проезд",
        InteractionKind.Sleep => $"[E] переночевать ({Krntija.Core.Economy.Money.Format(WorldActions.MotelPrice)})",
        InteractionKind.Door => point.Door is { IsOpen: true } ? "[E] закрыть дверь" : "[E] открыть дверь",
        InteractionKind.Loot => point.LootId is not null
            ? $"[E] поднять {Core.Items.ItemCatalog.Get(point.LootId).Name}"
            : "[E] поднять",
        _ => "[E] заправить",
    };

    private void Interact()
    {
        if (_hud.Screen != HudScreen.None)
        {
            _hud.Screen = HudScreen.None;
            return;
        }

        // С деталью в руках E всегда про неё: поставить в гнездо магнита или положить.
        if (_held is not null)
        {
            InstallHeld();
            return;
        }

        var target = SelectTarget();
        switch (target.Kind)
        {
            case TargetKind.Point:
                Use(target.Point!);
                break;
            case TargetKind.LoosePart:
                if (target.Body is not null) TakeLoosePart(target.Body);
                break;
            case TargetKind.CarPart:
                UseCarPart(target.Slot!.Value);
                break;
            case TargetKind.EnterCar:
                EnterCar();
                break;
            case TargetKind.ExitCar:
                ExitCar();
                break;
            case TargetKind.StopFirst:
                _hud.Toast("Сначала остановись");
                break;
        }
    }

    private void Use(InteractionPoint point)
    {
        switch (point.Kind)
        {
            case InteractionKind.Shop:
                _hud.Screen = HudScreen.Shop;
                break;

            case InteractionKind.Gate:
                PayForGate(point);
                break;

            case InteractionKind.SellAll:
                SellEverythingToDealer();
                break;

            case InteractionKind.Sleep:
                TrySleep();
                break;

            case InteractionKind.Loot:
                PickUpLoot(point);
                break;

            case InteractionKind.Door:
                point.Door?.Toggle();
                Sfx.PlayUi();
                break;

            case InteractionKind.Drink:
                DrinkFromWell();
                break;

            case InteractionKind.Pet:
                _hud.Toast("Собака виляет хвостом");
                Sfx.PlayUi();
                break;

            case InteractionKind.Talk:
                TalkToResident(point);
                break;

            case InteractionKind.RepairExit:
                TryRepairExit(point);
                break;

            case InteractionKind.ClearCollapse:
                ClearCollapse(point);
                break;

            case InteractionKind.Chest:
                OpenHomeChest();
                break;

            case InteractionKind.Workbench:
                RepairAtWorkbench();
                break;

            case InteractionKind.SleepHome:
                SleepAtHome();
                break;

            case InteractionKind.Crate:
                OpenCrate(point);
                break;

            default:
                RefuelAtPump();
                break;
        }
    }

    private void PayForGate(InteractionPoint point)
    {
        if (point.Gate is not { } gate) return;

        if (gate.IsOpen)
        {
            _hud.Toast("Проезд уже оплачен");
            return;
        }

        double toll = point.Gate?.Toll ?? WorldActions.GateToll;

        if (_car.Session.Wallet.TrySpend(toll))
        {
            gate.Open();
            MarkWorld(point.StateId);
            _hud.Toast($"Проезд оплачен: {Krntija.Core.Economy.Money.Format(point.Gate?.Toll ?? WorldActions.GateToll)}");
        }
        else
        {
            _hud.Toast($"Нет {Krntija.Core.Economy.Money.Format(point.Gate?.Toll ?? WorldActions.GateToll)} на проезд");
        }
    }

    /// <summary>За первое посещение каждого места — деньги: есть смысл ехать дальше.</summary>
    private void RewardNewPlaces() => RewardChunkPlaces();

    private void TrySleep()
    {
        _hud.Toast(WorldActions.TrySleepAtMotel(_car.Session)
            ? "Выспались. Утро, силы восстановлены"
            : $"Ночлег стоит {Krntija.Core.Economy.Money.Format(WorldActions.MotelPrice)}");
    }

    private float DistanceToCar() =>
        _player is null ? float.MaxValue : _player.GlobalPosition.DistanceTo(_car.GlobalPosition);
}
