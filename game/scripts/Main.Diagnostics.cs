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

/// <summary>Отладочные прогоны без рук: --smoke, --selftest, --perf, --shot-*, проверки дома, машин и пропов.</summary>
public partial class Main
{
    private bool _smoke;
    private bool _smokeDone;
    private bool _perf;
    private double _fpsSum;
    private double _fpsMin = 999;
    private bool _selfTest;
    private bool _selfTestDone;
    private bool _selfTestOk;
    private bool _selfTestCars;
    private bool _checkHouse;
    private int _houseCheckFrames = -1;
    private int _frames;
    private float _offroadGroundY;
    private float _playerGroundY;
    private float _handbrakeSpeed;
    private Vector3 _creepPosition;

    private bool _shot;
    private bool _openJournalInShot;
    private bool _shotMenu;
    private bool _shotDay;
    private bool _shotSettings;
    private bool _shotRain;
    private bool _shotCar;
    private bool _shotLineup;
    private bool _shotCars;
    private bool _shotBackpack;
    private bool _shotMap;
    private bool _shotMapUi;
    private string? _shotProp;

    /// <summary>В автопрогонах машина едет сама и не стоит на ручнике.</summary>
    private bool DrivesItself => _smoke || _selfTest || _perf || _selfTestCars;

    private static bool HasArg(string name) => Array.IndexOf(OS.GetCmdlineUserArgs(), name) >= 0;

    /// <summary>Значение аргумента вида --name=value или null, если его нет.</summary>
    private static string? ArgValue(string prefix)
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith(prefix, StringComparison.Ordinal))
                return arg[prefix.Length..];
        }

        return null;
    }

    /// <summary>Флаги отладочных прогонов: --smoke, --selftest, --perf, --shot-* и проверки.</summary>
    private void ParseDebugArgs()
    {
        _smoke = HasArg("--smoke");
        _selfTest = HasArg("--selftest");
        _perf = HasArg("--perf");

        // Без этого --perf меряет частоту монитора, а не запас по кадру.
        if (_perf) DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        _selfTestCars = HasArg("--selftest-cars");
        _checkHouse = HasArg("--check-house");

        _openJournalInShot = HasArg("--shot-ui");
        _shotMenu = HasArg("--shot-menu");
        _shotDay = HasArg("--shot-day");
        _shotSettings = HasArg("--shot-settings");
        _shotRain = HasArg("--shot-rain");
        _shotCar = HasArg("--shot-car");
        _shotLineup = HasArg("--shot-lineup");
        _shotCars = HasArg("--shot-cars");
        _shotBackpack = HasArg("--shot-backpack");
        _shotMap = HasArg("--shot-map");
        _shotMapUi = HasArg("--shot-mapui");
        _shotProp = ArgValue("--shot-prop=");

        // --shot-prop разбирается до этой строки: раньше порядок был обратный,
        // и превью пропа без второго --shot-флага не включало режим снимка.
        _shot = HasArg("--shot") || _openJournalInShot || _shotMenu || _shotDay || _shotSettings
                || _shotRain || _shotCar || _shotLineup || _shotCars || _shotBackpack
                || _shotMap || _shotMapUi || _shotProp is not null;
    }

    private static void PrintSoundDiagnostics()
    {
        Sfx.PlayUi();
        GD.Print($"[sound] драйвер: {AudioServer.GetDriverName()}");
        GD.Print($"[sound] текущее устройство: {AudioServer.OutputDevice}");
        foreach (string device in AudioServer.GetOutputDeviceList())
            GD.Print($"[sound] доступно: {device}");
        GD.Print($"[sound] шин: {AudioServer.GetBusCount()}, мастер: {AudioServer.GetBusVolumeDb(0):F1} дБ, "
                 + $"mute={AudioServer.IsBusMute(0)}");
        GD.Print($"[sound] частота микса: {AudioServer.GetMixRate()} Гц");
    }

    /// <summary>Снимок для документации: ставим машину, свет, камеру и нужный экран.</summary>
    private void SetupShot()
    {
        if (_road is not { Centerline.Count: > 90 }) return;

        var road = _road;
        int index = ArgValue("--at=") is { } at
            ? Mathf.Clamp(int.Parse(at), 1, road.Centerline.Count - 2)
            : 80;

        _car.GlobalPosition = road.Centerline[index] + Vector3.Up * 0.6f;
        _car.Rotation = new Vector3(0, -road.Headings[index] + 0.35f, 0);
        _camera.GlobalPosition = _car.GlobalPosition + _car.GlobalBasis.Z * 9f + Vector3.Up * 4f;
        _car.Session.Clock.SetTotalMinutes((_shotDay || _shotCar ? 12.0 : 18.75) * 60);

        if (_shotRain)
        {
            _car.Session.Clock.SetTotalMinutes(15.0 * 60);
            _car.Session.Weather.Restore(WeatherKind.Rain, wetness: 1, rain: 1, minutesUntilChange: 30);
        }

        if (_shotCar)
        {
            // Камера в три четверти: снимаем модель, а не дорогу.
            _camera.ProcessMode = ProcessModeEnum.Disabled;
            _hud.Visible = false;
            Transform3D basis = _car.GlobalTransform;
            string view = ArgValue("--view=") ?? "side";

            Vector3 eye = view switch
            {
                "front" => basis * new Vector3(0.4f, 1.15f, -6.0f),
                "rear" => basis * new Vector3(0.4f, 1.35f, 6.0f),
                "quarter" => basis * new Vector3(-3.4f, 1.35f, -4.0f),
                _ => basis * new Vector3(-4.4f, 1.15f, -0.7f),
            };
            _camera.LookAtFromPosition(eye, _car.GlobalPosition + new Vector3(0, 0.7f, 0));
        }

        if (_shotLineup)
            BuildLineup(road, index);

        if (_shotBackpack)
        {
            _backpack.Toggle(_car.Session.Inventory, _car.Session.Trunk, _car.Session.Home);
            _hud.Visible = false;
        }

        if (_shotMapUi)
        {
            _mapUi.Toggle();
            _hud.Visible = false;
        }

        if (_shotMap)
        {
            Vector3 middle = road.Centerline[road.Centerline.Count / 2];
            _car.Visible = false;
            _hud.Visible = false;
            _camera.ProcessMode = ProcessModeEnum.Disabled;
            _car.Session.Clock.SetTotalMinutes(12.5 * 60);
            _camera.LookAtFromPosition(middle + new Vector3(0, 85, 55), middle);
        }

        if (_shotProp is not null)
            BuildPropPreview(road, index, _shotProp);
    }

    /// <summary>Превью пропа: ставим модель у дороги и снимаем её в три четверти.</summary>
    private void BuildPropPreview(RoadBuildResult road, int index, string id)
    {
        _car.Visible = false;
        _car.ProcessMode = ProcessModeEnum.Disabled;
        _hud.Visible = false;
        _camera.ProcessMode = ProcessModeEnum.Disabled;

        Vector3 center = road.Centerline[index];
        float heading = road.Headings[index];
        var right = new Vector3(Mathf.Cos(heading), 0, Mathf.Sin(heading));
        var forward = new Vector3(Mathf.Sin(heading), 0, -Mathf.Cos(heading));

        var prop = PropFactory.Spawn(id, center, heading, solid: false, this);
        if (prop is null)
        {
            GD.Print($"[prop] нет модели {id}.glb");
            return;
        }

        Vector3 eye = center + forward * 9f - right * 7f + Vector3.Up * 4.5f;
        _camera.LookAtFromPosition(eye, center + Vector3.Up * 1.6f);
    }

    /// <summary>Проверка ассетов мира: печатает габариты и центровку каждого пропа.</summary>
    private static void RunPropCheck()
    {
        string[] ids =
        {
            "gasstation_a", "gasstation_b", "gasstation",
            "village_a", "village_b", "village_c", "village",
            "ruins_a", "ruins_b", "ruins",
            "farm_a", "farm_b", "farm",
            "checkpoint_a", "checkpoint",
        };

        bool ok = true;
        foreach (string id in ids)
            ok &= PropFactory.Check(id);

        GD.Print(ok ? "[props] проверка пройдена" : "[props] есть проблемы");
    }

    /// <summary>Три варианта кузова рядом — для сравнения силуэта.</summary>
    private void BuildLineup(RoadBuildResult road, int index)
    {
        _car.Visible = false;
        _car.ProcessMode = ProcessModeEnum.Disabled;
        _hud.Visible = false;

        Vector3 center = road.Centerline[index];
        float heading = road.Headings[index];
        var right = new Vector3(Mathf.Cos(heading), 0, Mathf.Sin(heading));
        var forward = new Vector3(Mathf.Sin(heading), 0, -Mathf.Cos(heading));

        string[] names = { "zastava_boxy", "zastava_rounded", "zastava_hatch" };

        for (int i = 0; i < names.Length; i++)
        {
            var root = new Node3D
            {
                Position = center + right * ((i - 1) * 3.05f) + Vector3.Up * 0.595f,
                Rotation = new Vector3(0, -heading, 0),
            };
            AddChild(root);

            var model = ModelLibrary.Instantiate($"res://assets/models/cars/{names[i]}.glb");
            if (model is not null) root.AddChild(model);

            var rubber = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.29f, 0.28f), Roughness = 1f };
            foreach (var (x, z) in new[] { (-0.86f, -1.45f), (0.86f, -1.45f), (-0.86f, 1.45f), (0.86f, 1.45f) })
            {
                root.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { Height = 0.24f, TopRadius = 0.36f, BottomRadius = 0.36f },
                    MaterialOverride = rubber,
                    Position = new Vector3(x, -0.26f, z),
                    Rotation = new Vector3(0, 0, Mathf.Pi / 2),
                });
            }
        }

        _camera.ProcessMode = ProcessModeEnum.Disabled;
        Vector3 eye = center + forward * 7.2f - right * 2.4f + Vector3.Up * 2.1f;
        _camera.LookAtFromPosition(eye, center + Vector3.Up * 0.75f);
        _car.Session.Clock.SetTotalMinutes(12.0 * 60);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_houseCheckFrames >= 0)
        {
            if (--_houseCheckFrames < 0)
            {
                RunHouseCheck();
                return;
            }
        }

        if (!(_smoke || _selfTest || _shot || _perf) || _smokeDone) return;

        _frames++;

        if (_perf)
        {
            double fps = Performance.GetMonitor(Performance.Monitor.TimeFps);
            _fpsSum += fps;
            if (_frames == 1 || fps < _fpsMin) _fpsMin = fps;

            if (_frames >= 600)
            {
                _smokeDone = true;
                GD.Print($"[perf] средний FPS {_fpsSum / _frames:F1}, минимум {_fpsMin:F1}, " +
                         $"кадров {_frames}, объектов {Performance.GetMonitor(Performance.Monitor.ObjectCount):F0}, " +
                         $"узлов {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):F0}, " +
                         $"вызовов отрисовки {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):F0}");
                GetTree().Quit(0);
            }

            return;
        }

        if (_shot && _frames == 120 && _openJournalInShot)
            _hud.Screen = HudScreen.Journal;

        if (_shot && _frames >= 180)
        {
            _smokeDone = true;
            var image = GetViewport().GetTexture().GetImage();
            var path = ProjectSettings.GlobalizePath("user://shot.png");
            image.SavePng(path);
            GD.Print($"[shot] {path}");
            GetTree().Quit(0);
            return;
        }

        if (_selfTest)
        {
            if (_frames == 120) RunSelfTest();
            if (_frames == 230) CheckReverse();
            if (_frames == 240) SetupHandbrakeCheck();
            if (_frames == 300) CheckHandbrake();
            if (_frames == 380) CheckCreep();
            if (_frames == 400) { CheckOffroad(); SetupPlayerCheck(); }
            if (_frames == 470) CheckPlayerOffroad();
            if (_frames >= 480)
            {
                _smokeDone = true;
                GetTree().Quit(_selfTestOk ? 0 : 1);
            }
            return;
        }

        if (_frames % 60 == 0)
            GD.Print($"[drive] кадр {_frames}: {_car.SpeedMps:F2} м/с, колёс на земле {_car.GroundedWheels}, "
                     + $"передача {_car.Session.DriveTrain.Gear}, {_car.Session.DriveTrain.Rpm:F0} об/мин, "
                     + $"топливо {_car.Session.Vehicle.Fuel.Liters:F2} л");

        if (_frames < 600) return;

        _smokeDone = true;
        var v = _car.Session.Vehicle;
        float speed = _car.SpeedMps;

        GD.Print($"[smoke] итог: кадров={_frames} скорость={speed:F2} м/с ({speed * 3.6f:F1} км/ч) "
                 + $"топливо={v.Fuel.Liters:F2} л температура={v.EngineTempC:F1} °C одометр={v.OdometerKm:F2} км");

        GetTree().Quit(speed > 5f ? 0 : 1);
    }

    /// <summary>
    /// Прогон игровых сценариев без рук: ремонт, сейв, загрузка, журнал.
    /// Запускается как `--headless -- --selftest`, возвращает 0 при успехе.
    /// </summary>
    private void RunSelfTest()
    {
        if (_selfTestDone) return;
        _selfTestDone = true;
        _selfTestOk = true;

        try
        {
            var session = _car.Session;
            var engine = session.Vehicle.Get(PartSlot.Engine)!;

            engine.SetCondition(0.40);
            bool repaired = VehicleService.TryRepair(session.Vehicle, session.Inventory, PartSlot.Engine);
            Check(repaired && Math.Abs(engine.Condition - 0.60) < 0.01, "ремонт изолентой");

            session.Inventory.TryAdd("radiator_std");
            bool installed = VehicleService.TryReplacePart(
                session.Vehicle, session.Inventory, "radiator_std", PartSlot.Radiator, out _);
            Check(installed && session.Vehicle.Get(PartSlot.Radiator)!.Condition > 0.99, "замена детали из сумки");

            session.Vehicle.Fuel.SetLiters(5);

            var walletBefore = session.Wallet.Money;
            var (poured, spent) = TradeService.RefuelAtPump(session.Vehicle, session.Wallet, 20);
            Check(poured > 0.1 && spent > 0.1 && session.Wallet.Money < walletBefore, "заправка за деньги");

            Check(TradeService.TryBuy(session.Inventory, session.Wallet, "bread"), "покупка в лавке");

            double thirstBefore = session.Survival.Thirst;
            int waterBefore = session.Inventory.Count(ItemIds.Water);
            bool used = ItemUseService.TryUse(session, ItemIds.Water, out _);
            Check(used && session.Survival.Thirst > thirstBefore && session.Inventory.Count(ItemIds.Water) < waterBefore,
                "предмет из сумки используется");

            // Мир помнит подобранное: A поднят до сейва, B — после.
            var loot = _interactions.Where(p => p.Kind == InteractionKind.Loot && p.StateId is not null).Take(2).ToList();
            string? lootBefore = loot.ElementAtOrDefault(0)?.StateId;
            string? lootAfter = loot.ElementAtOrDefault(1)?.StateId;
            if (loot.Count > 0) PickUpLoot(loot[0]);

            double moneyAtSave = session.Wallet.Money;
            SaveGame();

            if (loot.Count > 1) PickUpLoot(loot[1]);
            session.Vehicle.Fuel.SetLiters(30);
            engine.SetCondition(0.9);
            LoadGame();

            bool LootLies(string? id) => _interactions.Any(p => p.StateId == id && p.Kind == InteractionKind.Loot);
            Check(lootBefore is not null && !LootLies(lootBefore) && _car.Session.WorldState.IsDone(lootBefore),
                "подобранный до сейва лут не вернулся после загрузки");
            Check(lootAfter is not null && LootLies(lootAfter), "лут, поднятый после сейва, вернулся при загрузке");

            Check(Math.Abs(_car.Session.Vehicle.Fuel.Liters - 5 - poured) < 0.05, "топливо восстановлено из сейва");
            Check(Math.Abs(_car.Session.Vehicle.Get(PartSlot.Engine)!.Condition - 0.60) < 0.01, "износ восстановлен");
            Check(_car.Session.Vehicle.Spec.Id == "zastava_101", "модель машины восстановлена");
            Check(Math.Abs(_car.Session.Wallet.Money - moneyAtSave) < 0.01, "деньги восстановлены");

            _hud.Screen = HudScreen.Journal;
            Check(_hud.Screen == HudScreen.Journal, "журнал открывается");
            _hud.Screen = HudScreen.None;

            _car.Autopilot = false;
            _car.LinearVelocity = Vector3.Zero;
            _car.AngularVelocity = Vector3.Zero;
            ExitCar();
            Check(_onFoot && _player is not null && !_car.Driven, "выход из машины");
            Check(_player!.GlobalPosition.DistanceTo(_car.GlobalPosition) < 3f, "пешеход рядом с машиной");

            EnterCar();
            Check(!_onFoot && _car.Driven && _player is null, "возвращение за руль");

            _car.LinearVelocity = Vector3.Zero;
            _car.AngularVelocity = Vector3.Zero;
            _car.ForcedInput = new DriveInput(Ignition: true, Brake: 1.0);

            var gatePoint = _interactions.FirstOrDefault(p => p.Kind == InteractionKind.Gate);
            if (gatePoint?.Gate is not null)
            {
                double before = _car.Session.Wallet.Money;
                PayForGate(gatePoint);
                Check(gatePoint.Gate.IsOpen && _car.Session.Wallet.Money < before, "шлагбаум за деньги");
                Check(gatePoint.StateId is not null && _car.Session.WorldState.IsDone(gatePoint.StateId),
                    "оплаченный шлагбаум записан в состояние мира");
            }

            GD.Print("[selftest] пройден");
        }
        catch (Exception e)
        {
            _selfTestOk = false;
            GD.PrintErr($"[selftest] исключение: {e}");
        }
    }

    /// <summary>Проверка заднего хода: машина должна поехать назад на «S».</summary>
    private void CheckReverse()
    {
        float forwardSpeed = _car.LinearVelocity.Dot(-_car.GlobalBasis.Z);
        Check(forwardSpeed < -0.5f, $"задний ход ({forwardSpeed:F2} м/с)");
        Check(_car.Session.DriveTrain.IsReverse, "коробка показывает R");

        _car.ForcedInput = null;
        _car.LinearVelocity = Vector3.Zero;
        _car.AngularVelocity = Vector3.Zero;
    }

    /// <summary>Ручник: разгоняем машину и проверяем, что она тормозит.</summary>
    private void SetupHandbrakeCheck()
    {
        _car.ForcedInput = null;
        _car.LinearVelocity = -_car.GlobalBasis.Z * 15f;
        _car.AngularVelocity = Vector3.Zero;
        _handbrakeSpeed = _car.SpeedMps;
        _car.ForcedInput = new DriveInput(Handbrake: true);
    }

    private void CheckHandbrake()
    {
        float speed = _car.SpeedMps;
        Check(speed < 6f, $"ручник тормозит ({speed:F1} м/с из {_handbrakeSpeed:F0})");

        // Ручник остаётся включённым: машина должна стоять на уклоне.
        _car.LinearVelocity = Vector3.Zero;
        _car.ForcedInput = new DriveInput(Handbrake: true);
        _creepPosition = _car.GlobalPosition;
    }

    /// <summary>Машина стоит с ручником — не должна скатываться.</summary>
    private void CheckCreep()
    {
        float moved = _car.GlobalPosition.DistanceTo(_creepPosition);
        Check(moved < 1.5f, $"ручник держит на уклоне (сдвиг {moved:F2} м)");

        _car.ForcedInput = null;
        SetupOffroadCheck();
    }

    /// <summary>Ставим машину на площадку у заправки: там раньше проваливалась.</summary>
    private void SetupOffroadCheck()
    {
        var pump = _interactions.FirstOrDefault(p => p.Kind == InteractionKind.Pump);
        if (pump is null)
        {
            Check(false, "нашлась колонка для проверки обочины");
            return;
        }

        // Высоту берём лучом до настоящей земли: рельеф вокруг заправки
        // меняется вместе с артом, а высота колонки — нет.
        Vector3 spot = pump.Position + new Vector3(7f, 0f, 7f);
        _offroadGroundY = GroundY(spot);
        _car.GlobalPosition = new Vector3(spot.X, _offroadGroundY + 0.62f, spot.Z);
        _car.LinearVelocity = Vector3.Zero;
        _car.AngularVelocity = Vector3.Zero;
    }

    private void CheckOffroad()
    {
        float y = _car.GlobalPosition.Y;
        Check(y > _offroadGroundY - 0.5f, $"машина не провалилась у заправки (Y={y:F1}, земля {_offroadGroundY:F1})");
        Check(_car.GroundedWheels > 0, "колёса стоят на земле у заправки");
    }

    /// <summary>Высаживаем пешехода в стороне от дороги: он тоже не должен падать.</summary>
    private void SetupPlayerCheck()
    {
        _car.LinearVelocity = Vector3.Zero;
        ExitCar();

        if (_player is null)
        {
            Check(false, "пешеход появился");
            return;
        }

        Vector3 spot = _car.GlobalPosition + new Vector3(-10f, 0f, 0f);
        _playerGroundY = GroundY(spot);
        _player.Teleport(new Vector3(spot.X, _playerGroundY + 1.0f, spot.Z));
    }

    private void CheckPlayerOffroad()
    {
        if (_player is null)
        {
            Check(false, "пешеход существует");
            return;
        }

        float y = _player.GlobalPosition.Y;
        Check(y > _playerGroundY - 0.5f, $"пешеход не провалился за дорогой (Y={y:F1}, земля {_playerGroundY:F1})");
        Check(_player.IsOnFloor(), "пешеход стоит на земле");
    }

    /// <summary>Высота первой твёрдой поверхности под точкой, без машины и пешехода.</summary>
    private float GroundY(Vector3 at)
    {
        var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * 40f, at + Vector3.Down * 40f);
        var exclude = new Godot.Collections.Array<Rid> { _car.GetRid() };
        if (_player is not null) exclude.Add(_player.GetRid());
        query.Exclude = exclude;

        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count > 0 ? ((Vector3)hit["position"]).Y : at.Y;
    }

    private void Check(bool condition, string what)
    {
        if (condition)
        {
            GD.Print($"[selftest] ok: {what}");
            return;
        }

        _selfTestOk = false;
        GD.PrintErr($"[selftest] FAIL: {what}");
    }

    /// <summary>
    /// Автопроверка экрана выбора машины: жмём каждую кнопку и убеждаемся,
    /// что сессия собралась на нужной спеке с её стартовым топливом.
    /// </summary>
    private void RunCarSelectCheck()
    {
        bool ok = true;

        _menu.ShowCarSelect();
        GD.Print($"[carselftest] кнопок в меню: {_menu.CarChoices}, машин в каталоге: {CarCatalog.All.Count}");
        ok &= _menu.CarChoices == CarCatalog.All.Count;

        for (int i = 0; i < CarCatalog.All.Count; i++)
        {
            CarSpec expected = CarCatalog.All[i];
            _menu.ShowCarSelect();
            _menu.PressCar(i);

            string actual = _car.Session.Vehicle.Spec.Id;
            double fuel = _car.Session.Vehicle.Fuel.Liters;
            bool match = actual == expected.Id && Math.Abs(fuel - expected.FuelStartL) < 0.001;

            GD.Print($"[carselftest] {i}: ждали {expected.Id}/{expected.FuelStartL:F0} л, "
                     + $"получили {actual}/{fuel:F0} л — {(match ? "ок" : "FAIL")}");
            ok &= match;
        }

        // Вторая часть: новая игра не должна копить события, награды, шлагбаумы и подобранный лут.
        var gatePoint = _interactions.FirstOrDefault(p => p.Gate is not null);
        gatePoint?.Gate?.Open();
        var lootPoint = _interactions.FirstOrDefault(p => p.Kind == InteractionKind.Loot && p.StateId is not null);
        if (lootPoint is not null) PickUpLoot(lootPoint);

        for (int i = 0; i < 2; i++)
        {
            _car.Session.WorldState.MarkDone(WorldState.Place(999));
            _menu.ShowCarSelect();
            _menu.PressCar(0);
        }

        // Считаем только живые узлы: QueueFree освобождает старые в конце кадра.
        int eventNodes = 0;
        foreach (var child in GetChildren())
        {
            if (child is RoadEvents events && !events.IsQueuedForDeletion()) eventNodes++;
        }

        bool singleEvents = eventNodes == 1;
        int worldDone = _car.Session.WorldState.Done.Count;
        bool visitedCleared = worldDone == 0;
        bool gateClosed = gatePoint?.Gate is { IsOpen: false };
        bool lootBack = lootPoint is not null
                        && _interactions.Any(p => p.Kind == InteractionKind.Loot && p.StateId == lootPoint.StateId);

        GD.Print($"[carselftest] события: {eventNodes} (ждём 1) — {(singleEvents ? "ок" : "FAIL")}, "
                 + $"состояние мира сброшено: {worldDone} — {(visitedCleared ? "ок" : "FAIL")}, "
                 + $"шлагбаум закрыт — {(gateClosed ? "ок" : "FAIL")}, "
                 + $"лут вернулся — {(lootBack ? "ок" : "FAIL")}");

        ok &= singleEvents && visitedCleared && gateClosed && lootBack;

        GD.Print(ok ? "[carselftest] OK" : "[carselftest] FAIL");
        GetTree().Quit(ok ? 0 : 1);
    }

    /// <summary>
    /// Проверка проходимости дверей: капсула игрока в центре каждого проёма
    /// не должна ни с чем пересекаться. Двери к этому моменту открыты.
    /// </summary>
    private void RunHouseCheck()
    {
        var space = GetWorld3D().DirectSpaceState;
        var capsule = new CapsuleShape3D { Radius = 0.34f, Height = 1.78f };
        bool ok = true;

        foreach (var point in _interactions)
        {
            if (point.Kind != InteractionKind.Door || point.Door is null) continue;
            var door = point.Door;

            Vector3 center = door.GlobalPosition + door.GlobalBasis.X.Normalized() * (door.Width * 0.5f);
            center.Y = _houseGroundY + 0.95f;

            var query = new PhysicsShapeQueryParameters3D
            {
                Shape = capsule,
                Transform = new Transform3D(Basis.Identity, center),
                CollideWithBodies = true,
            };

            var hits = space.IntersectShape(query, 4);
            bool free = hits.Count == 0 && door.IsOpen;
            ok &= free;

            string what = string.Empty;
            foreach (var hit in hits)
            {
                if (hit["collider"].As<GodotObject>() is Node3D node)
                {
                    Vector3 local = node.GetParent() is Node3D parent3D ? parent3D.Position : node.Position;
                    what += $"{node.Name}@локально({local.X:F1}; {local.Y:F1}; {local.Z:F1}) ";
                }
                else if (hit["collider"].As<GodotObject>() is Node other)
                    what += other.Name + " ";
            }

            GD.Print($"[housecheck] {door.Name,-16} {(free ? "свободно" : $"ЗАБЛОКИРОВАНО ({hits.Count}: {what.Trim()})")}");
        }

        // Проход тела игрока в каждую наружную дверь: старт в 4.5 м снаружи.
        foreach (var point in _interactions)
        {
            if (point.Kind != InteractionKind.Door || point.Door is null) continue;
            var door = point.Door;
            if (door.Name == "GarageInnerDoor" || door.Name == "KitchenDoor") continue;

            var body = new CharacterBody3D { Name = "WalkProbe" };
            body.AddChild(new CollisionShape3D { Shape = capsule, Position = new Vector3(0, 0.89f, 0) });
            AddChild(body);

            // Наружная стена смотрит вдоль локальной Z дома: знак задаёт сторону.
            var houseNode = GetNodeOrNull<Node3D>("PlayerHouse")!;
            float localZ = houseNode.ToLocal(door.GlobalPosition).Z;
            Vector3 inward = door.GlobalBasis.Z.Normalized() * (localZ > 0 ? -1f : 1f);
            Vector3 start = door.GlobalPosition
                            + door.GlobalBasis.X.Normalized() * (door.Width * 0.5f)
                            - inward * 4.5f;
            start.Y = _houseGroundY - 0.1f;
            body.GlobalPosition = start;

            Vector3 previous = body.GlobalPosition;
            string trace = string.Empty;

            for (int i = 0; i < 220; i++)
            {
                body.Velocity = inward * 2.2f - Vector3.Up * 0.25f;

                // Шаг вверх, как у игрока: пороги и крыльцо.
                if (body.IsOnFloor())
                {
                    var raised = new Transform3D(body.GlobalBasis, body.GlobalPosition + Vector3.Up * 0.34f);
                    if (body.TestMove(body.GlobalTransform, inward * 0.35f)
                        && !body.TestMove(raised, inward * 0.35f)
                        && body.TestMove(raised, Vector3.Down * 0.46f))
                        body.GlobalPosition += Vector3.Up * 0.34f;
                }

                previous = body.GlobalPosition;
                body.MoveAndSlide();

                // Первый кадр, где тело встало: печатаем, во что упёрлось.
                if (trace.Length == 0 && i > 5 && body.GlobalPosition.DistanceTo(previous) < 0.01f
                    && body.GetSlideCollisionCount() > 0)
                {
                    for (int c = 0; c < body.GetSlideCollisionCount(); c++)
                    {
                        var collision = body.GetSlideCollision(c);
                        if (collision.GetCollider() is Node3D hit)
                        {
                            Vector3 local = hit.GetParent() is Node3D parentNode ? parentNode.Position : hit.Position;
                            trace += $"{hit.Name}@({local.X:F1}; {local.Y:F1}; {local.Z:F1}) "
                                     + $"нормаль {collision.GetNormal()} ";
                        }
                    }
                }
            }

            float depth = (body.GlobalPosition - door.GlobalPosition).Dot(inward);
            bool inside = depth > 1.2f;
            ok &= inside;

            string blocked = string.Empty;
            var probe = new PhysicsShapeQueryParameters3D
            {
                Shape = capsule,
                Transform = new Transform3D(Basis.Identity, body.GlobalPosition + Vector3.Up * 0.95f),
                CollideWithBodies = true,
            };
            foreach (var hit in space.IntersectShape(probe, 4))
            {
                if (hit["collider"].As<GodotObject>() is Node3D node)
                {
                    Vector3 local = node.GetParent() is Node3D parent3D ? parent3D.Position : node.Position;
                    blocked += $"{node.Name}@локально({local.X:F1}; {local.Y:F1}; {local.Z:F1}) ";
                }
            }

            GD.Print($"[housewalk] {door.Name,-12} {depth,5:F2} м — {(inside ? "зашёл" : "НЕ ЗАШЁЛ")} {trace.Trim()} {blocked.Trim()}");
            body.QueueFree();
        }

        // Подъём на чердак: тело идёт от подножия вверх по пандусу.
        if (GetNodeOrNull<Node3D>("PlayerHouse") is { } house)
        {
            var climb = new CharacterBody3D { Name = "ClimbProbe" };
            climb.AddChild(new CollisionShape3D { Shape = capsule, Position = new Vector3(0, 0.89f, 0) });
            AddChild(climb);

            Vector3 startWorld = house.ToGlobal(new Vector3(4.2f, 0.35f, 3.8f));
            Vector3 topWorld = house.ToGlobal(new Vector3(4.2f, 0.35f, 0.5f));
            Vector3 direction = (topWorld - startWorld).Normalized();
            float climbLength = startWorld.DistanceTo(topWorld);
            climb.GlobalPosition = startWorld;

            Vector3 climbPrevious = climb.GlobalPosition;
            string climbTrace = string.Empty;

            for (int i = 0; i < 420; i++)
            {
                // Наверху лестницы останавливаемся: дальше уже чердак, а не подъём.
                if ((climb.GlobalPosition - startWorld).Dot(direction) >= climbLength) break;

                climb.Velocity = direction * 2.4f - Vector3.Up * 0.2f;

                // Тот же шаг вверх: иначе тест не повторит путь игрока.
                if (climb.IsOnFloor())
                {
                    var raised = new Transform3D(climb.GlobalBasis, climb.GlobalPosition + Vector3.Up * 0.34f);
                    if (climb.TestMove(climb.GlobalTransform, direction * 0.35f)
                        && !climb.TestMove(raised, direction * 0.35f)
                        && climb.TestMove(raised, Vector3.Down * 0.46f))
                        climb.GlobalPosition += Vector3.Up * 0.34f;
                }

                climbPrevious = climb.GlobalPosition;
                climb.MoveAndSlide();

                if (climbTrace.Length == 0 && i > 5 && climb.GlobalPosition.DistanceTo(climbPrevious) < 0.01f
                    && climb.GetSlideCollisionCount() > 0)
                {
                    for (int c = 0; c < climb.GetSlideCollisionCount(); c++)
                    {
                        var collision = climb.GetSlideCollision(c);
                        if (collision.GetCollider() is Node3D hit)
                        {
                            Vector3 local = house.ToLocal(hit.GlobalPosition);
                            climbTrace += $"{hit.Name}@({local.X:F1}; {local.Y:F1}; {local.Z:F1}) "
                                          + $"нормаль {collision.GetNormal()} ";
                        }
                    }
                }
            }

            float height = house.ToLocal(climb.GlobalPosition).Y;
            bool reached = height > 2.3f;
            ok &= reached;
            GD.Print($"[houseclimb] чердак: {height:F2} м — {(reached ? "ок" : "НЕ ПОДНЯЛСЯ")} {climbTrace.Trim()}");

            // По чердаку к фасаду: пол держит, наружу не выйти.
            for (int i = 0; i < 240; i++)
            {
                climb.Velocity = direction * 2.4f - Vector3.Up * 2f;
                climb.MoveAndSlide();
            }

            Vector3 attic = house.ToLocal(climb.GlobalPosition);
            bool stays = attic.Y > 2.5f && Mathf.Abs(attic.Z) < 4f;
            ok &= stays;
            GD.Print($"[houseattic] по чердаку: Y={attic.Y:F2}, Z={attic.Z:F2} — {(stays ? "ок" : "ПРОВАЛИЛСЯ ИЛИ ВЫШЕЛ")}");
            climb.QueueFree();
        }

        GD.Print(ok ? "[housecheck] OK" : "[housecheck] FAIL");
        GetTree().Quit(ok ? 0 : 1);
    }
}
