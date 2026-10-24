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

/// <summary>Точка входа: дорога, машина, пешеход, магазины, сейвы и headless-прогоны.</summary>
public partial class Main : Node3D
{
    private const string SavePath = "user://save.json";

    private Car _car = null!;
    private ChaseCamera _camera = null!;
    private Hud _hud = null!;
    private Player? _player;
    private bool _onFoot;
    private bool _smoke;
    private bool _smokeDone;
    private bool _selfTest;
    private bool _selfTestDone;
    private bool _selfTestOk;
    private bool _shot;
    private bool _openJournalInShot;
    private bool _shotMenu;
    private bool _shotDay;
    private bool _shotSettings;
    private bool _shotRain;
    private bool _shotCar;
    private bool _shotLineup;
    private int _frames;

    private enum InteractionKind
    {
        Pump,
        Shop,
        Gate,
    }

    private sealed record InteractionPoint(Vector3 Position, InteractionKind Kind, float Radius, CheckpointGate? Gate = null);

    private readonly List<InteractionPoint> _interactions = new();
    private RoadBuildResult? _road;
    private MenuUi _menu = null!;
    private RainFx _rain = null!;
    private DirectionalLight3D? _sun;
    private Godot.Environment? _environment;
    private Vector3? _objectivePosition;
    private string _objectiveTitle = "";
    private bool _objectiveDone;
    private float _offroadStartY;
    private float _playerStartY;
    private float _handbrakeSpeed;
    private Vector3 _creepPosition;

    public override void _Ready()
    {
        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--balance") >= 0)
        {
            BalanceProbe.Run();
            GetTree().Quit(0);
            return;
        }

        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--model=", StringComparison.Ordinal))
                Car.ModelOverride = arg["--model=".Length..];
        }
        SetupInput();
        GameSettings.Load();
        GameSettings.Apply();
        BuildWorld();

        var session = GameSession.NewGame();

        _car = Car.Build(session);
        AddChild(_car);
        // Ставим машину не на самом краю полотна, иначе половина висит в воздухе.
        Vector3 start = _road is { Centerline.Count: > 4 } ? _road.Centerline[4] : new Vector3(0, 0.6f, 0);
        _car.GlobalPosition = start + Vector3.Up * 0.62f;
        _car.Path = _road?.Centerline;

        _camera = new ChaseCamera { Target = _car };
        AddChild(_camera);
        _camera.GlobalPosition = _car.GlobalPosition + new Vector3(0, 4.5f, 10);

        _hud = new Hud { Car = _car };
        AddChild(_hud);

        _rain = new RainFx();
        AddChild(_rain);

        AddChild(new Sfx());

        _smoke = Array.IndexOf(OS.GetCmdlineUserArgs(), "--smoke") >= 0;
        _selfTest = Array.IndexOf(OS.GetCmdlineUserArgs(), "--selftest") >= 0;
        _shot = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-ui") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-menu") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-day") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-settings") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-rain") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-car") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-lineup") >= 0;
        _openJournalInShot = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-ui") >= 0;
        _shotMenu = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-menu") >= 0;
        _shotDay = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-day") >= 0;
        _shotSettings = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-settings") >= 0;
        _shotRain = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-rain") >= 0;
        _shotCar = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-car") >= 0;
        _shotLineup = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-lineup") >= 0;
        _car.Autopilot = _smoke || _selfTest;

        _menu = new MenuUi();
        AddChild(_menu);
        _menu.NewGameRequested += StartNewGame;
        _menu.ContinueRequested += ContinueGame;
        _menu.ResumeRequested += ResumeGame;
        _menu.SaveRequested += SaveGame;
        _menu.MainMenuRequested += GoToMainMenu;
        _menu.QuitRequested += () => GetTree().Quit(0);
        _menu.EscapePressed += OnEscape;

        // В автоматических прогонах меню не показываем: тесты должны ехать сразу.
        bool automated = _smoke || _selfTest || _shot;
        if (!automated)
        {
            _menu.ShowMainMenu(HasSave());
            _hud.Visible = false;
            GetTree().Paused = true;
        }
        else if (_shotMenu || _shotSettings)
        {
            if (_shotSettings) _menu.ShowSettings();
            else _menu.ShowMainMenu(HasSave());
            _hud.Visible = false;
        }

        if (_shot && _road is { Centerline.Count: > 90 })
        {
            var road = _road;
            int index = 80;

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
                Vector3 eye = basis * new Vector3(-4.4f, 1.15f, -0.7f);
                _camera.LookAtFromPosition(eye, _car.GlobalPosition + new Vector3(0, 0.7f, 0));
            }

            if (_shotLineup)
                BuildLineup(road, index);
        }

        GD.Print($"[Krntija] старт. smoke={_smoke}, selftest={_selfTest}, машина: {session.Vehicle.Spec.Name}");
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
        if (!(_smoke || _selfTest || _shot) || _smokeDone) return;

        _frames++;

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

    private float RoadOffset()
    {
        if (_road is null) return 0;

        float best = float.MaxValue;
        foreach (var point in _road.Centerline)
            best = Mathf.Min(best, point.DistanceTo(_car.GlobalPosition));

        return best;
    }

    public override void _Process(double delta)
    {
        if (_hud is null || _car is null) return;

        _hud.Prompt = BuildPrompt();
        UpdateSky();
        ApplyWeather();

        if (_objectiveDone || _objectivePosition is null) return;

        _hud.Objective = $"Добраться до: {_objectiveTitle}  (+80 €)";

        if (_car.GlobalPosition.DistanceTo(_objectivePosition.Value) > 30f) return;

        _objectiveDone = true;
        _car.Session.Wallet.Earn(80);
        _hud.Objective = string.Empty;
        _hud.Toast($"Задача выполнена: {_objectiveTitle}. +80 €");
    }

    /// <summary>Солнце по игровым часам: рассвет, полдень, закат, ночь.</summary>
    private void UpdateSky()
    {
        if (_sun is null || _environment is null) return;

        double hour = _car.Session.Clock.HourOfDay;

        // 5:30 — рассвет, 12:00 — зенит, 18:30 — закат.
        float daylight = Mathf.Clamp(Mathf.Sin((float)((hour - 5.5) / 13.0 * Math.PI)), 0f, 1f);

        float pitch = -(float)((hour - 5.5) / 13.0 * 180.0);
        _sun.RotationDegrees = new Vector3(pitch, -35, 0);

        float rain = (float)_car.Session.Weather.RainIntensity;

        _sun.LightEnergy = (0.05f + 1.25f * daylight) * (1f - 0.65f * rain);
        _sun.LightColor = new Color(1f, 0.72f, 0.45f).Lerp(Colors.White, daylight);

        _environment.AmbientLightEnergy = (0.14f + 0.56f * daylight) * (1f - 0.35f * rain);
        _environment.FogLightColor = new Color(0.08f, 0.1f, 0.16f).Lerp(new Color(0.7f, 0.68f, 0.6f), daylight);
        _environment.FogDensity = 0.0022f + 0.0055f * rain;
    }

    /// <summary>Дождь: капли вокруг камеры и мокрое полотно.</summary>
    private void ApplyWeather()
    {
        var weather = _car.Session.Weather;
        float rain = (float)weather.RainIntensity;
        float wet = (float)weather.Wetness;

        _rain.GlobalPosition = _camera.GlobalPosition + Vector3.Up * 8f;
        _rain.Apply(rain);
        Sfx.SetRain(rain);

        _car.GripScale = (float)weather.GripFactor;

        if (_road?.RoadMaterial is { } material)
        {
            material.Roughness = Mathf.Lerp(0.95f, 0.55f, wet);
            material.Metallic = Mathf.Lerp(0f, 0.08f, wet);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        if (_hud.Screen == HudScreen.Shop && key.Keycode >= Key.Key1 && key.Keycode <= Key.Key6)
        {
            BuyShopItem((int)key.Keycode - (int)Key.Key1);
            return;
        }

        if (_hud.Screen == HudScreen.Shop && key.Keycode == Key.S)
        {
            SellCarriedPart();
            return;
        }

        if (_hud.Screen == HudScreen.Journal && key.Keycode >= Key.Key1 && key.Keycode <= Key.Key9)
        {
            UseInventoryItem((int)key.Keycode - (int)Key.Key1);
            return;
        }

        switch (key.Keycode)
        {
            case Key.Escape:
                break;
            case Key.E:
                Interact();
                break;
            case Key.J: _hud.Screen = _hud.Screen == HudScreen.None ? HudScreen.Journal : HudScreen.None; break;
            case Key.F5:
                SaveGame();
                break;
            case Key.F9:
                LoadGame();
                break;
            case Key.Up when _hud.Screen == HudScreen.Journal:
                _hud.MoveSelection(-1);
                break;
            case Key.Down when _hud.Screen == HudScreen.Journal:
                _hud.MoveSelection(1);
                break;
            case Key.T when _hud.Screen == HudScreen.Journal:
                InstallSelectedPart();
                break;
            case Key.R when _hud.Screen == HudScreen.Journal:
                RepairSelectedPart();
                break;
            case Key.F when _hud.Screen == HudScreen.Journal:
                RefuelFromCan();
                break;
        }
    }

    // ---------- пешеход и взаимодействия ----------

    private string BuildPrompt()
    {
        if (_hud.Screen != HudScreen.None) return string.Empty;

        var point = NearestInteraction();

        if (_onFoot && _player is not null)
        {
            if (DistanceToCar() < 3.5f) return "[E] сесть в машину";
            if (point is not null) return PromptFor(point);
            return "мышь — обзор, W/S — идти, A/D — шаг вбок, Shift — бежать";
        }

        if (point is not null && _car.SpeedMps < 2f) return PromptFor(point);

        return _car.SpeedMps < 2f ? "[E] выйти из машины" : string.Empty;
    }

    private static string PromptFor(InteractionPoint point) => point.Kind switch
    {
        InteractionKind.Shop => "[E] зайти в лавку",
        InteractionKind.Gate => point.Gate is { IsOpen: true } ? "[E] шлагбаум открыт" : "[E] заплатить 15 € за проезд",
        _ => "[E] заправить",
    };

    private void Interact()
    {
        if (_hud.Screen != HudScreen.None)
        {
            _hud.Screen = HudScreen.None;
            return;
        }

        if (_onFoot)
        {
            if (DistanceToCar() < 3.5f)
            {
                EnterCar();
                return;
            }

            var point = NearestInteraction();
            if (point is not null) Use(point);

            return;
        }

        var inCar = NearestInteraction();
        if (inCar is not null && _car.SpeedMps < 2f && inCar.Kind != InteractionKind.Shop)
        {
            Use(inCar);
            return;
        }

        if (_car.SpeedMps >= 2f)
        {
            _hud.Toast("Сначала остановись");
            return;
        }

        ExitCar();
    }

    private void Use(InteractionPoint point)
    {
        switch (point.Kind)
        {
            case InteractionKind.Shop:
                _hud.Screen = HudScreen.Shop;
                break;

            case InteractionKind.Gate:
                PayForGate(point.Gate);
                break;

            default:
                RefuelAtPump();
                break;
        }
    }

    private void PayForGate(CheckpointGate? gate)
    {
        if (gate is null) return;

        if (gate.IsOpen)
        {
            _hud.Toast("Проезд уже оплачен");
            return;
        }

        if (_car.Session.Wallet.TrySpend(15))
        {
            gate.Open();
            _hud.Toast("Проезд оплачен: 15 €");
        }
        else
        {
            _hud.Toast("Нет 15 € на проезд");
        }
    }

    private void ExitCar()
    {
        Vector3 side = -_car.GlobalBasis.X.Normalized() * 1.5f;
        Vector3 spawn = _car.GlobalPosition + side + Vector3.Up * 0.4f;

        _player = new Player { Yaw = _car.Rotation.Y };
        AddChild(_player);
        _player.Teleport(spawn);

        _onFoot = true;
        _car.Driven = false;

        _camera.Target = _player;
        _camera.Distance = 4.6f;
        _camera.Height = 2.4f;
        _camera.LookHeight = 1.4f;
        _camera.OrbitWithMouse = false;
        _camera.ResetOrbit();
        Sfx.PlayDoor(_car.GlobalPosition);

        _hud.Toast("Вышел из машины");
    }

    private void EnterCar()
    {
        _player?.QueueFree();
        _player = null;
        _onFoot = false;
        _car.Driven = true;

        _camera.Target = _car;
        _camera.Distance = 7.5f;
        _camera.Height = 3.2f;
        _camera.LookHeight = 0.9f;
        _camera.OrbitWithMouse = true;
        _camera.ResetOrbit();
        Sfx.PlayDoor(_car.GlobalPosition);

        _hud.Toast("За рулём");
    }

    private float DistanceToCar() =>
        _player is null ? float.MaxValue : _player.GlobalPosition.DistanceTo(_car.GlobalPosition);

    private InteractionPoint? NearestInteraction()
    {
        Vector3 from = _onFoot && _player is not null ? _player.GlobalPosition : _car.GlobalPosition;

        InteractionPoint? best = null;
        float bestDistance = float.MaxValue;

        foreach (var point in _interactions)
        {
            float distance = from.DistanceTo(point.Position);
            if (distance > point.Radius || distance >= bestDistance) continue;

            best = point;
            bestDistance = distance;
        }

        return best;
    }

    private void RefuelAtPump()
    {
        var session = _car.Session;
        double need = session.Vehicle.Fuel.CapacityLiters - session.Vehicle.Fuel.Liters;
        var (liters, cost) = TradeService.RefuelAtPump(session.Vehicle, session.Wallet, need);

        if (liters > 0.01)
            Sfx.PlayPour(_car.GlobalPosition);

        _hud.Toast(liters > 0.01
            ? $"Залито {liters:F1} л за {cost:F0} €"
            : session.Wallet.Money < TradeService.FuelPricePerLiter * need
                ? "Не хватает денег"
                : "Бак полный");
    }

    private void BuyShopItem(int index)
    {
        if (index < 0 || index >= TradeService.Shop.Count) return;

        var entry = TradeService.Shop[index];
        bool ok = TradeService.TryBuy(_car.Session.Inventory, _car.Session.Wallet, entry.ItemId);

        _hud.Toast(ok
            ? $"Куплено: {ItemCatalog.Get(entry.ItemId).Name}"
            : "Не хватает денег или места");
    }

    /// <summary>Использовать предмет из сумки по номеру в журнале.</summary>
    private void UseInventoryItem(int index)
    {
        var session = _car.Session;
        var item = session.Inventory.Items.ElementAtOrDefault(index);

        if (item is null)
        {
            _hud.Toast("В сумке нет такого предмета");
            return;
        }

        string name = item.Definition.Name;
        ItemUseService.TryUse(session, item.DefId, out string message);
        _hud.Toast($"{name}: {message}");
    }

    /// <summary>Сдать первую попавшуюся деталь из сумки за половину цены.</summary>
    private void SellCarriedPart()
    {
        var session = _car.Session;
        var item = session.Inventory.Items.FirstOrDefault(i => PartCatalog.All.ContainsKey(i.DefId));

        if (item is null)
        {
            _hud.Toast("Нечего сдавать");
            return;
        }

        string name = item.Definition.Name;
        double price = PartCatalog.All[item.DefId].Price * 0.5;

        _hud.Toast(TradeService.TrySellPart(session.Inventory, session.Wallet, item.DefId)
            ? $"Сдано: {name} за {price:F0} €"
            : "Не получилось сдать");
    }

    // ---------- самопроверка M1 ----------

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

            double moneyAtSave = session.Wallet.Money;
            SaveGame();

            session.Vehicle.Fuel.SetLiters(30);
            engine.SetCondition(0.9);
            LoadGame();

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
                PayForGate(gatePoint.Gate);
                Check(gatePoint.Gate.IsOpen && _car.Session.Wallet.Money < before, "шлагбаум за деньги");
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

        _car.GlobalPosition = pump.Position + new Vector3(7f, 1.2f, 7f);
        _car.LinearVelocity = Vector3.Zero;
        _car.AngularVelocity = Vector3.Zero;
        _offroadStartY = _car.GlobalPosition.Y;
    }

    private void CheckOffroad()
    {
        float y = _car.GlobalPosition.Y;
        Check(y > _offroadStartY - 2f, $"машина не провалилась у заправки (Y={y:F1})");
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

        _player.Teleport(_car.GlobalPosition + new Vector3(-10f, 1.5f, 0f));
        _playerStartY = _player.GlobalPosition.Y;
    }

    private void CheckPlayerOffroad()
    {
        if (_player is null)
        {
            Check(false, "пешеход существует");
            return;
        }

        float y = _player.GlobalPosition.Y;
        Check(y > _playerStartY - 2f, $"пешеход не провалился за дорогой (Y={y:F1})");
        Check(_player.IsOnFloor(), "пешеход стоит на земле");
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

    // ---------- меню и пауза ----------

    private bool HasSave() => System.IO.File.Exists(ProjectSettings.GlobalizePath(SavePath));

    private void OnEscape()
    {
        if (!_menu.IsOpen)
        {
            PauseGame();
            return;
        }

        if (!_menu.InMainMode) ResumeGame();
    }

    private void PauseGame()
    {
        GetTree().Paused = true;
        _hud.Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _menu.ShowPause();
    }

    private void ResumeGame()
    {
        GetTree().Paused = false;
        _hud.Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _menu.Close();
    }

    private void StartNewGame()
    {
        // Сцена перезагружается только при выходе в главное меню,
        // поэтому здесь сессия уже свежая — просто снимаем паузу.
        ResumeGame();
    }

    private void ContinueGame()
    {
        if (!HasSave())
        {
            StartNewGame();
            return;
        }

        ResumeGame();
        LoadGame();
    }

    private void GoToMainMenu()
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    // ---------- игровые действия ----------

    private void SaveGame()
    {
        var path = ProjectSettings.GlobalizePath(SavePath);
        var data = _car.Session.ToSave();
        data.ObjectiveDone = _objectiveDone;
        data.CarX = _car.GlobalPosition.X;
        data.CarY = _car.GlobalPosition.Y;
        data.CarZ = _car.GlobalPosition.Z;
        data.CarYaw = _car.Rotation.Y;

        SaveSerializer.SaveFile(path, data);
        _hud.Toast("Сохранено");
        GD.Print($"[save] {path}");
    }

    private void LoadGame()
    {
        var path = ProjectSettings.GlobalizePath(SavePath);
        if (!System.IO.File.Exists(path))
        {
            _hud.Toast("Сейв не найден");
            return;
        }

        var data = SaveSerializer.LoadFile(path);
        var session = GameSession.FromSave(data);
        Respawn(session);

        _objectiveDone = data.ObjectiveDone;

        _car.GlobalPosition = new Vector3((float)data.CarX, (float)data.CarY, (float)data.CarZ);
        _car.Rotation = new Vector3(0, (float)data.CarYaw, 0);
        _car.LinearVelocity = Vector3.Zero;
        _car.AngularVelocity = Vector3.Zero;

        _hud.Toast("Загружено");
    }

    private void RepairSelectedPart()
    {
        var session = _car.Session;
        var slot = _hud.SelectedSlot;
        var part = session.Vehicle.Get(slot);
        if (part is null) return;

        if (session.Inventory.Count(ItemIds.DuctTape) == 0)
        {
            _hud.Toast("Нет изоленты");
            return;
        }

        _hud.Toast(VehicleService.TryRepair(session.Vehicle, session.Inventory, slot)
            ? $"{part.Name}: подмотано до {part.Condition * 100:F0}%"
            : $"{part.Name} в порядке");
    }

    private void InstallSelectedPart()
    {
        var session = _car.Session;
        var slot = _hud.SelectedSlot;

        string? defId = VehicleService.FindPartInBag(session.Inventory, slot);
        if (defId is null)
        {
            _hud.Toast("Нет подходящей детали в сумке");
            return;
        }

        if (!VehicleService.TryReplacePart(session.Vehicle, session.Inventory, defId, slot, out _))
        {
            _hud.Toast("Не удалось поставить деталь");
            return;
        }

        _hud.Toast($"{session.Vehicle.Get(slot)!.Name}: установлена");
    }

    private void RefuelFromCan()
    {
        double poured = VehicleService.RefuelFromCan(_car.Session.Vehicle, _car.Session.Inventory);
        _hud.Toast(poured > 0.01 ? $"Залито {poured:F1} л" : "Канистра пуста");
    }

    private void Respawn(GameSession session)
    {
        Vector3 position = _car.GlobalPosition;
        Vector3 rotation = _car.GlobalRotation;

        _car.QueueFree();
        _car = Car.Build(session);
        AddChild(_car);
        _car.GlobalPosition = position;
        _car.GlobalRotation = rotation;
        _car.Autopilot = _smoke;

        _camera.Target = _car;
        _hud.Car = _car;
    }

    // ---------- мир ----------

    private void BuildWorld()
    {
        var skyMaterial = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.35f, 0.45f, 0.6f),
            SkyHorizonColor = new Color(0.72f, 0.68f, 0.58f),
            GroundHorizonColor = new Color(0.45f, 0.42f, 0.33f),
        };

        _environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = skyMaterial },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.7f,
            FogEnabled = true,
            FogLightColor = new Color(0.7f, 0.68f, 0.6f),
            FogDensity = 0.0022f,
        };
        AddChild(new WorldEnvironment { Environment = _environment });

        _sun = new DirectionalLight3D { LightEnergy = 1.3f, ShadowEnabled = true };
        _sun.RotationDegrees = new Vector3(-48, -35, 0);
        AddChild(_sun);


        BuildRoad();
    }

    /// <summary>Дорога из плана ядра: лента, коллизия, точки интереса и обочины.</summary>
    private void BuildRoad()
    {
        var plan = RoadPlanner.Generate(seed: 20261023, totalMeters: 2500);
        var road = RoadMeshBuilder.Build(plan, this);
        _road = road;

        foreach (var poi in road.Points)
        {
            var right = new Vector3(Mathf.Cos(poi.Heading), 0, Mathf.Sin(poi.Heading));
            Vector3 spot = poi.Position - right * 16f; // левая обочина
            spot.Y = TerrainBuilder.SurfaceHeight(poi.Position.Y, -16f, spot);
            AddLandmark(poi.Segment.Feature, spot, poi.Heading, poi.Position);
        }

        TerrainBuilder.Build(road.Centerline, road.Headings, this);
        WorldScatter.ScatterTrees(road.Centerline, road.Headings, this, seed: 20261023);
        WorldScatter.ScatterPowerLine(road.Centerline, road.Headings, this, seed: 20261023);
        WorldScatter.AddKilometerPosts(road.Centerline, road.Headings, this);

        var target = road.Points.Count > 0 ? road.Points[^1] : null;
        if (target is not null)
        {
            _objectivePosition = target.Position;
            _objectiveTitle = target.Segment.Title;
        }

        GD.Print($"[road] {plan.Segments.Count} сегментов, {plan.TotalMeters:F0} м, точек интереса: {road.Points.Count}");
    }

    private void AddLandmark(RoadFeature feature, Vector3 position, float heading, Vector3 roadPoint)
    {
        var model = ModelLibrary.Prop(feature.ToString().ToLowerInvariant());

        if (model is not null)
        {
            model.Position = position;
            model.Rotation = new Vector3(0, -heading, 0);
            AddChild(model);
        }

        switch (feature)
        {
            case RoadFeature.GasStation:
                if (model is null) AddGasStation(position, heading);
                else AddGasStationInteractions(position, heading);
                break;

            case RoadFeature.Village: if (model is null) AddVillage(position, heading); break;
            case RoadFeature.Ruins: if (model is null) AddRuins(position, heading); break;
            case RoadFeature.Farm: if (model is null) AddFarm(position, heading); break;
            case RoadFeature.Checkpoint: AddCheckpoint(position, heading, roadPoint, model is null); break;
        }
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

    private void AddVillage(Vector3 position, float heading)
    {
        var village = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };
        AddChild(village);

        var wall = new Color(0.78f, 0.74f, 0.64f);
        var roof = new Color(0.55f, 0.28f, 0.2f);

        for (int i = 0; i < 3; i++)
        {
            var house = new Node3D { Position = new Vector3(i * 9f - 9f, 0, -i * 6f) };
            village.AddChild(house);
            Box(house, new Vector3(6, 3, 5), new Vector3(0, 1.5f, 0), wall);
            Box(house, new Vector3(6.6f, 0.4f, 5.6f), new Vector3(0, 3.2f, 0), roof);
        }
    }

    private void AddRuins(Vector3 position, float heading)
    {
        var ruins = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };
        AddChild(ruins);

        var stone = new Color(0.6f, 0.58f, 0.52f);
        Box(ruins, new Vector3(8, 2.4f, 0.5f), new Vector3(0, 1.2f, 0), stone);
        Box(ruins, new Vector3(0.5f, 3.6f, 6), new Vector3(4, 1.8f, 0), stone);
        Box(ruins, new Vector3(3, 1.1f, 0.5f), new Vector3(-3, 0.55f, 0.2f), stone);
    }

    private void AddFarm(Vector3 position, float heading)
    {
        var farm = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };
        AddChild(farm);

        Box(farm, new Vector3(10, 4, 7), new Vector3(0, 2, 0), new Color(0.62f, 0.5f, 0.36f));
        Box(farm, new Vector3(11, 0.5f, 7.8f), new Vector3(0, 4.2f, 0), new Color(0.45f, 0.3f, 0.22f));

        var silo = new MeshInstance3D
        {
            Mesh = new CylinderMesh { Height = 8, TopRadius = 2, BottomRadius = 2 },
            Position = new Vector3(8, 4, -2),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.7f, 0.64f), Roughness = 0.8f },
        };
        farm.AddChild(silo);
    }

    private void AddCheckpoint(Vector3 position, float heading, Vector3 roadPoint, bool buildBooth = true)
    {
        if (buildBooth)
        {
            var checkpoint = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };
            AddChild(checkpoint);

            // Будка смотрителя и козырёк на обочине.
            Box(checkpoint, new Vector3(2.4f, 2.6f, 2.4f), new Vector3(0, 1.3f, 0), new Color(0.72f, 0.7f, 0.6f));
            Box(checkpoint, new Vector3(3.0f, 0.2f, 3.0f), new Vector3(0, 2.7f, 0), new Color(0.4f, 0.38f, 0.34f));
        }

        // Сам шлагбаум стоит поперёк дороги и нужен всегда — это механика.
        var gate = CheckpointGate.Create(roadPoint, heading);
        AddChild(gate);

        _interactions.Add(new InteractionPoint(
            roadPoint + new Vector3(0, 1.2f, 0), InteractionKind.Gate, 9f, gate));
    }

    /// <summary>Заправка: навес, колонка и магазинчик у дороги.</summary>
    private void AddGasStation(Vector3 origin, float heading)
    {
        var concrete = new StandardMaterial3D { AlbedoColor = new Color(0.7f, 0.68f, 0.62f), Roughness = 0.9f };
        var red = new StandardMaterial3D { AlbedoColor = new Color(0.66f, 0.22f, 0.18f), Roughness = 0.7f };
        var yellow = new StandardMaterial3D { AlbedoColor = new Color(0.83f, 0.74f, 0.4f), Roughness = 0.6f };

        var station = new Node3D { Position = origin, Rotation = new Vector3(0, -heading, 0) };
        AddChild(station);

        station.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(12, 0.35f, 8) },
            Position = new Vector3(0, 4.3f, 0),
            MaterialOverride = red,
        });

        foreach (float dx in new[] { -5f, 5f })
        foreach (float dz in new[] { -3f, 3f })
        {
            station.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.3f, 4.3f, 0.3f) },
                Position = new Vector3(dx, 2.15f, dz),
                MaterialOverride = concrete,
            });
        }

        // Колонка: визуал + коллизия, чтобы в неё можно было въехать.
        var pumpBody = new StaticBody3D { Position = new Vector3(0, 0.8f, 0) };
        pumpBody.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.8f, 1.6f, 0.6f) } });
        pumpBody.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.8f, 1.6f, 0.6f) },
            MaterialOverride = yellow,
        });
        station.AddChild(pumpBody);

        // Магазин.
        var shop = new StaticBody3D { Position = new Vector3(0, 1.7f, -10) };
        shop.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(10, 3.4f, 6) } });
        shop.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(10, 3.4f, 6) },
            MaterialOverride = concrete,
        });
        station.AddChild(shop);

        AddGasStationInteractions(origin, heading);
    }

    /// <summary>Колонка и вход в лавку — считаются от точки POI, без привязки к узлу.</summary>
    private void AddGasStationInteractions(Vector3 origin, float heading)
    {
        Vector3 pump = origin + new Vector3(0, 1f, 0).Rotated(Vector3.Up, -heading);
        Vector3 shop = origin + new Vector3(0, 1.6f, -6.4f).Rotated(Vector3.Up, -heading);

        _interactions.Add(new InteractionPoint(pump, InteractionKind.Pump, 4.5f));
        _interactions.Add(new InteractionPoint(shop, InteractionKind.Shop, 4.5f));
    }

    // ---------- ввод ----------

    private static void SetupInput()
    {
        AddKey("accelerate", Key.W);
        AddKey("brake", Key.S);
        AddKey("steer_left", Key.A);
        AddKey("steer_right", Key.D);
        AddKey("ignition", Key.V);
        AddKey("handbrake", Key.Space);
        AddKey("sprint", Key.Shift);
        AddKey("journal", Key.J);
    }

    private static void AddKey(string action, Key key)
    {
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action, deadzone: 0.2f);

        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }
}
