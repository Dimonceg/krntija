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
    private bool _firstPerson;
    private Hud _hud = null!;
    private Player? _player;
    private bool _onFoot;
    private bool _smoke;
    private bool _smokeDone;
    private bool _perf;
    private double _fpsSum;
    private double _fpsMin = 999;
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
    private string? _shotProp;
    private BackpackUi _backpack = null!;
    private LootItem? _hoveredLoot;
    private bool _cursorFree;
    private bool _shotBackpack;
    private bool _shotCars;
    private bool _selfTestCars;
    private bool _checkHouse;
    private int _houseCheckFrames = -1;
    private float _houseGroundY;
    private int _frames;

    private enum InteractionKind
    {
        Pump,
        Shop,
        Gate,
        Sleep,
        Door,
        Loot,
    }

    private sealed record InteractionPoint(Vector3 Position, InteractionKind Kind, float Radius,
        CheckpointGate? Gate = null, Door? Door = null, string? LootId = null, Node3D? Node = null);

    private readonly List<InteractionPoint> _interactions = new();
    private RoadBuildResult? _road;
    private MenuUi _menu = null!;
    private RoadEvents? _events;
    private RainFx _rain = null!;
    private DirectionalLight3D? _sun;
    private Godot.Environment? _environment;
    private ProceduralSkyMaterial? _skyMaterial;
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

        // Проверка ассетов мира: печатает габариты и центровку каждого пропа.
        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--check-props") >= 0)
        {
            RunPropCheck();
            GetTree().Quit(0);
            return;
        }

        BuildWorld();

        var session = GameSession.NewGame(car: SelectedCar());

        _car = Car.Build(session);
        AddChild(_car);
        PlaceCarAtStart();

        _camera = new ChaseCamera
        {
            Target = _car,
            Distance = 5.2f,
            Height = 2.15f,
            LookHeight = 0.8f,
            Fov = 68f,
        };
        AddChild(_camera);
        _camera.GlobalPosition = _car.GlobalPosition + new Vector3(0, 4.5f, 10);

        _hud = new Hud { Car = _car };
        AddChild(_hud);

        _backpack = new BackpackUi();
        _backpack.Clicked += OnBackpackClick;
        AddChild(_backpack);

        // Дом игрока у старта: слева от дороги, со своими дверями и чердаком.
        BuildPlayerHouse();
        SpawnLoot();

        // Дорожные события: ямы, коровы, попутчики.
        if (_road is not null)
        {
            _events = new RoadEvents();
            AddChild(_events);
            _events.Setup(_road.Centerline, _road.Headings, _car, _hud);
        }

        _rain = new RainFx();
        AddChild(_rain);

        AddChild(new Sfx());

        // Отладочный старт от первого лица для скриншотов.
        bool fpCar = Array.IndexOf(OS.GetCmdlineUserArgs(), "--fp") >= 0;
        bool fpFoot = Array.IndexOf(OS.GetCmdlineUserArgs(), "--fp-foot") >= 0;
        if (fpCar || fpFoot)
        {
            _firstPerson = true;
            _camera.FirstPerson = true;
            _camera.AutoRecenter = false;

            if (fpFoot) ExitCar();
            else _camera.FirstPersonOffset = new Vector3(0.30f, 0.86f, -1.05f);
        }
        _smoke = Array.IndexOf(OS.GetCmdlineUserArgs(), "--smoke") >= 0;
        _selfTest = Array.IndexOf(OS.GetCmdlineUserArgs(), "--selftest") >= 0;
        _shot = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-ui") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-menu") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-day") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-settings") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-rain") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-car") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-lineup") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-cars") >= 0
                || Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-backpack") >= 0
                || _shotProp is not null;
        _shotBackpack = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-backpack") >= 0;
        _openJournalInShot = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-ui") >= 0;
        _shotMenu = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-menu") >= 0;
        _shotDay = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-day") >= 0;
        _shotSettings = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-settings") >= 0;
        _shotRain = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-rain") >= 0;
        _shotCar = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-car") >= 0;
        _shotLineup = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-lineup") >= 0;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--shot-prop=", StringComparison.Ordinal))
                _shotProp = arg["--shot-prop=".Length..];
        }
        _shotCars = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot-cars") >= 0;
        _selfTestCars = Array.IndexOf(OS.GetCmdlineUserArgs(), "--selftest-cars") >= 0;
        _checkHouse = Array.IndexOf(OS.GetCmdlineUserArgs(), "--check-house") >= 0;

        // Диагностика звука: печатает драйвер, шину и состояние плееров.
        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--check-sound") >= 0)
        {
            Sfx.PlayUi();
            GD.Print($"[sound] драйвер: {AudioServer.GetDriverName()}");
            GD.Print($"[sound] текущее устройство: {AudioServer.OutputDevice}");
            foreach (string device in AudioServer.GetOutputDeviceList())
                GD.Print($"[sound] доступно: {device}");
            GD.Print($"[sound] шин: {AudioServer.GetBusCount()}, мастер: {AudioServer.GetBusVolumeDb(0):F1} дБ, "
                     + $"mute={AudioServer.IsBusMute(0)}");
            GD.Print($"[sound] частота микса: {AudioServer.GetMixRate()} Гц");
            GetTree().Quit(0);
            return;
        }

        _perf = Array.IndexOf(OS.GetCmdlineUserArgs(), "--perf") >= 0;
        _car.Autopilot = _smoke || _selfTest || _perf || _selfTestCars;

        // Проверка дверей: открываем все и через полсотни кадров меряем проёмы.
        if (_checkHouse)
        {
            foreach (var point in _interactions)
                if (point.Kind == InteractionKind.Door) point.Door?.Toggle();

            _houseCheckFrames = 45;
        }

        // Новая игра начинается на ручнике: машина не катится сама по себе.
        // В автотестах ручник не взводим, иначе автопилот не тронется.
        EngageParkingBrake();

        _menu = new MenuUi();
        AddChild(_menu);
        _menu.NewGameRequested += StartNewGame;
        _menu.ContinueRequested += ContinueGame;
        _menu.ResumeRequested += ResumeGame;
        _menu.SaveRequested += SaveGame;
        _menu.MainMenuRequested += GoToMainMenu;
        _menu.QuitRequested += () => GetTree().Quit(0);
        _menu.EscapePressed += OnEscape;

        // Проверка выбора машины: жмём кнопки и сверяем спеку. Без окна и мыши.
        if (_selfTestCars)
        {
            RunCarSelectCheck();
            return;
        }

        // В автоматических прогонах меню не показываем: тесты должны ехать сразу.
        bool automated = _smoke || _selfTest || _shot || _perf || _checkHouse;
        if (!automated)
        {
            _menu.ShowMainMenu(HasSave());
            _hud.Visible = false;
            GetTree().Paused = true;
        }
        else if (_shotCars)
        {
            _menu.ShowCarSelect();
            _hud.Visible = false;
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
            foreach (string arg in OS.GetCmdlineUserArgs())
            {
                if (arg.StartsWith("--at=", StringComparison.Ordinal))
                    index = Mathf.Clamp(int.Parse(arg["--at=".Length..]), 1, road.Centerline.Count - 2);
            }

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
                string view = "side";
                foreach (string arg in OS.GetCmdlineUserArgs())
                {
                    if (arg.StartsWith("--view=", StringComparison.Ordinal))
                        view = arg["--view=".Length..];
                }

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
                _backpack.Toggle(_car.Session.Inventory, _car.Session.Trunk);
                _hud.Visible = false;
            }

            if (_shotProp is not null)
                BuildPropPreview(road, index, _shotProp);
        }

        GD.Print($"[Krntija] старт. smoke={_smoke}, selftest={_selfTest}, машина: {session.Vehicle.Spec.Name}");
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

    private float RoadOffset()
    {
        if (_road is null) return 0;

        float best = float.MaxValue;
        foreach (var point in _road.Centerline)
            best = Mathf.Min(best, point.DistanceTo(_car.GlobalPosition));

        return best;
    }

    private readonly HashSet<int> _visitedPoi = new();

    public override void _Process(double delta)
    {
        if (_hud is null || _car is null) return;

        _hud.Prompt = BuildPrompt();
        UpdateSky();
        ApplyWeather();
        RewardNewPlaces();

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

        // Утренний туман в низинах: плотнее всего около 6:30.
        float dawn = Mathf.Clamp(1f - Mathf.Abs((float)hour - 6.5f) / 2.2f, 0f, 1f);
        _environment.FogDensity += 0.0045f * dawn;
        _environment.FogHeightDensity = 0.25f * dawn;

        // Небо теплеет к закату и темнеет к ночи.
        if (_skyMaterial is not null)
        {
            float dusk = 1f - daylight;
            _skyMaterial.SkyTopColor = new Color(0.35f, 0.45f, 0.6f).Lerp(new Color(0.15f, 0.14f, 0.25f), dusk * 0.92f);
            _skyMaterial.SkyHorizonColor = new Color(0.72f, 0.68f, 0.58f).Lerp(new Color(0.80f, 0.44f, 0.26f), dusk * 0.85f);
            _skyMaterial.GroundHorizonColor = new Color(0.45f, 0.42f, 0.33f).Lerp(new Color(0.20f, 0.19f, 0.21f), dusk * 0.9f);
        }
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
        // Правая кнопка мыши — свободный курсор: можно навести на предмет и щёлкнуть.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
        {
            SetCursorFree(!_cursorFree);
            return;
        }

        if (_cursorFree)
        {
            if (@event is InputEventMouseMotion motion)
            {
                UpdateHover(motion.Position);
                return;
            }

            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                PickHoveredLoot();
                return;
            }
        }

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
                if (_backpack.IsOpen) _backpack.Close();
                break;
            case Key.B:
                ToggleBackpack();
                break;
            case Key.X when _hud.Screen == HudScreen.Shop:
                SellJunk();
                break;
            case Key.E:
                Interact();
                break;
            case Key.J: _hud.Screen = _hud.Screen == HudScreen.None ? HudScreen.Journal : HudScreen.None; break;
            case Key.C: ToggleCameraMode(); break;
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
            if (point is not null && point.Position.DistanceTo(_player.GlobalPosition) < DistanceToCar() + 0.5f)
                return PromptFor(point);
            if (DistanceToCar() < 3.5f) return "[E] сесть в машину";
            return "мышь — обзор, W/S — идти, A/D — шаг вбок, Shift — бежать";
        }

        if (point is not null && _car.SpeedMps < 2f) return PromptFor(point);

        return _car.SpeedMps < 2f ? "[E] выйти из машины" : string.Empty;
    }

    private static string PromptFor(InteractionPoint point) => point.Kind switch
    {
        InteractionKind.Shop => "[E] зайти в лавку",
        InteractionKind.Gate => point.Gate is { IsOpen: true } ? "[E] шлагбаум открыт" : "[E] заплатить 15 € за проезд",
        InteractionKind.Sleep => "[E] переночевать (15 €)",
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

        if (_onFoot)
        {
            // Сначала ближайшее действие: у дома E не должно сажать обратно за руль.
            var point = NearestInteraction();
            if (point is not null && point.Position.DistanceTo(_player!.GlobalPosition) < DistanceToCar() + 0.5f)
            {
                Use(point);
                return;
            }

            if (DistanceToCar() < 3.5f)
            {
                EnterCar();
                return;
            }

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

    /// <summary>За первое посещение каждого места — деньги: есть смысл ехать дальше.</summary>
    private void RewardNewPlaces()
    {
        if (_road is null) return;

        for (int i = 0; i < _road.Points.Count; i++)
        {
            if (_visitedPoi.Contains(i)) continue;

            var point = _road.Points[i];
            if (_car.GlobalPosition.DistanceTo(point.Position) > 25f) continue;

            _visitedPoi.Add(i);
            _car.Session.Wallet.Earn(40);
            _hud.Toast($"{point.Segment.Title}: +40 €");
        }
    }

    private void TrySleep()
    {
        var session = _car.Session;

        if (!session.Wallet.TrySpend(15))
        {
            _hud.Toast("Ночлег стоит 15 €");
            return;
        }

        session.Survival.Sleep(8);

        double now = session.Clock.TotalMinutes;
        double dayStart = Math.Floor(now / GameClock.MinutesPerDay) * GameClock.MinutesPerDay;
        double morning = dayStart + 7 * 60;
        if (morning <= now) morning += GameClock.MinutesPerDay;
        session.Clock.SetTotalMinutes(morning);

        _hud.Toast("Выспались. Утро, силы восстановлены");
    }

    /// <summary>Машина из --car=&lt;id&gt;: нужно для отладки и скриншотов разных кузовов.</summary>
    private static CarSpec SelectedCar()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--car=", StringComparison.Ordinal))
                return CarCatalog.Get(arg["--car=".Length..]);
        }

        return CarCatalog.Zastava101;
    }

    /// <summary>Переключение вида: от первого лица / от третьего.</summary>
    private void ToggleCameraMode()
    {
        _firstPerson = !_firstPerson;

        _camera.FirstPerson = _firstPerson;
        _camera.AutoRecenter = !_firstPerson;
        _camera.ResetOrbit();

        if (_onFoot)
        {
            _camera.FirstPersonOffset = new Vector3(0f, 1.62f, 0f);
            _camera.OrbitWithMouse = true;
            _camera.PitchWithMouse = true;
            _camera.UseTargetYaw = true;
            _player?.SetModelVisible(!_firstPerson);
        }
        else
        {
            _camera.FirstPersonOffset = new Vector3(0.30f, 0.86f, -1.05f);
            _camera.OrbitWithMouse = true;
            _camera.PitchWithMouse = false;
            _camera.UseTargetYaw = false;
        }

        _hud.Toast(_firstPerson ? "Вид от первого лица (C)" : "Вид от третьего лица (C)");
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
        _camera.Distance = 4.2f;
        _camera.Height = 2.0f;
        _camera.LookHeight = 1.45f;
        _camera.Fov = 66f;
        _camera.OrbitWithMouse = true;
        _camera.PitchWithMouse = true;
        _camera.UseTargetYaw = true;
        _camera.FirstPersonOffset = new Vector3(0f, 1.62f, 0f);
        _player.SetModelVisible(!_firstPerson);
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
        _camera.Distance = 5.2f;
        _camera.Height = 2.15f;
        _camera.LookHeight = 0.8f;
        _camera.Fov = 68f;
        _camera.OrbitWithMouse = true;
        _camera.PitchWithMouse = false;
        _camera.UseTargetYaw = false;
        _camera.FirstPersonOffset = new Vector3(0.30f, 0.86f, -1.05f);
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

    /// <summary>
    /// Новая игра на выбранной машине: сессия собирается заново, поэтому
    /// топливо, детали, деньги, время и задача начинаются с чистого листа.
    /// Мир (дорога, террейн) остаётся, но его состояние сбрасывается:
    /// события, открытые шлагбаумы и награды за посещённые места.
    /// </summary>
    private void StartNewGame(CarSpec spec)
    {
        Respawn(GameSession.NewGame(car: spec));
        PlaceCarAtStart();
        EngageParkingBrake();
        ResetWorldState();

        _objectiveDone = false;
        _hud.Objective = string.Empty;

        ResumeGame();
        _hud.Toast($"Новая игра: {spec.Name}");
        GD.Print($"[newgame] {spec.Id}");
    }

    /// <summary>Сброс состояния мира при новой игре в той же сцене.</summary>
    private void ResetWorldState()
    {
        _visitedPoi.Clear();

        foreach (var point in _interactions)
            point.Gate?.Close();

        // События хранят сработавшие точки и ссылки на старую машину:
        // проще собрать их заново, чем чинить флаги и удалённые узлы.
        if (_road is not null)
        {
            _events?.QueueFree();
            _events = new RoadEvents();
            AddChild(_events);
            _events.Setup(_road.Centerline, _road.Headings, _car, _hud);
        }
    }

    private void ContinueGame()
    {
        if (!HasSave())
        {
            _menu.ShowCarSelect();
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

        // Сначала убираем старую машину из дерева: QueueFree освобождает узел
        // в конце кадра, и без RemoveChild две машины успели бы побыть в мире
        // одновременно — старая ещё участвует в физике.
        RemoveChild(_car);
        _car.QueueFree();

        _car = Car.Build(session);
        AddChild(_car);
        _car.GlobalPosition = position;
        _car.GlobalRotation = rotation;
        _car.Autopilot = _smoke || _selfTest || _perf || _selfTestCars;

        _camera.Target = _car;
        _hud.Car = _car;

        // События держат ссылку на машину: без Rebind ямы и коровы
        // продолжали бы толкать удалённый объект после загрузки сейва.
        _events?.Rebind(_car, _hud);
    }

    /// <summary>Игрок начинает на ручнике; в автотестах ручник не мешает движению.</summary>
    private void EngageParkingBrake()
    {
        if (_smoke || _selfTest || _perf || _selfTestCars) return;
        _car.SetHandbrake(true);
    }

    /// <summary>Поставить машину на старт дороги: новую игру всегда начинаем там.</summary>
    private void PlaceCarAtStart()
    {
        // Не на самом краю полотна, иначе половина машины висит в воздухе.
        Vector3 start = _road is { Centerline.Count: > 4 } ? _road.Centerline[4] : new Vector3(0, 0.6f, 0);
        _car.GlobalPosition = start + Vector3.Up * 0.62f;
        _car.LinearVelocity = Vector3.Zero;
        _car.AngularVelocity = Vector3.Zero;
        _car.Path = _road?.Centerline;
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

        // Вторая часть: новая игра не должна копить события, награды и шлагбаумы.
        var gatePoint = _interactions.FirstOrDefault(p => p.Gate is not null);
        gatePoint?.Gate?.Open();

        for (int i = 0; i < 2; i++)
        {
            _visitedPoi.Add(999);
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
        bool visitedCleared = _visitedPoi.Count == 0;
        bool gateClosed = gatePoint?.Gate is { IsOpen: false };

        GD.Print($"[carselftest] события: {eventNodes} (ждём 1) — {(singleEvents ? "ок" : "FAIL")}, "
                 + $"награды сброшены: {_visitedPoi.Count} — {(visitedCleared ? "ок" : "FAIL")}, "
                 + $"шлагбаум закрыт — {(gateClosed ? "ок" : "FAIL")}");

        ok &= singleEvents && visitedCleared && gateClosed;

        GD.Print(ok ? "[carselftest] OK" : "[carselftest] FAIL");
        GetTree().Quit(ok ? 0 : 1);
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
        WorldScatter.ScatterTrees(road.Centerline, road.Headings, this, seed: 20261023, landmarks: road.Points);
        WorldScatter.ScatterPowerLine(road.Centerline, road.Headings, this, seed: 20261023);
        WorldScatter.AddKilometerPosts(road.Centerline, road.Headings, this);
        WorldScatter.ScatterSigns(road.Centerline, road.Headings, this, seed: 20261023);

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
                break;
            case RoadFeature.Ruins: if (model is null) AddRuins(position, heading); break;
            case RoadFeature.Farm: if (model is null) AddFarm(position, heading); break;
            case RoadFeature.Checkpoint: AddCheckpoint(position, heading, roadPoint, model is null); break;
        }

        if (title.Length > 0) AddFeatureSign(roadPoint, heading, title);
    }

    /// <summary>Какие файлы ищем для каждого типа места: base, _a, _b, _c.</summary>
    private static IReadOnlyList<string> PropVariants(string featureId) => new[]
    {
        featureId + "_a", featureId + "_b", featureId + "_c", featureId,
    };

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
            climb.GlobalPosition = startWorld;

            Vector3 climbPrevious = climb.GlobalPosition;
            string climbTrace = string.Empty;

            for (int i = 0; i < 420; i++)
            {
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
            climb.QueueFree();
        }

        GD.Print(ok ? "[housecheck] OK" : "[housecheck] FAIL");
        GetTree().Quit(ok ? 0 : 1);
    }

    /// <summary>Клик в рюкзаке: использовать, переложить в багажник или забрать обратно.</summary>
    private void OnBackpackClick(string itemId, bool fromTrunk)
    {
        var session = _car.Session;
        string name = Core.Items.ItemCatalog.Get(itemId).Name;

        if (fromTrunk)
        {
            if (!session.Inventory.TryAdd(itemId))
            {
                _hud.Toast("В кармане нет места");
                return;
            }

            session.Trunk.Remove(itemId, 1);
            _hud.Toast($"Из багажника: {name}");
        }
        else if (Core.Items.ItemUseService.TryUse(session, itemId, out string message))
        {
            _hud.Toast(message);
        }
        else if (session.Trunk.TryAdd(itemId))
        {
            session.Inventory.Remove(itemId, 1);
            _hud.Toast($"В багажник: {name}");
        }
        else
        {
            _hud.Toast("Багажник полон");
        }

        _backpack.Refresh(session.Inventory, session.Trunk);
    }

    /// <summary>Скупка всего хлама — и из кармана, и из багажника.</summary>
    private void SellJunk()
    {
        var (pocketCount, pocketMoney) =
            Core.Economy.TradeService.SellJunk(_car.Session.Inventory, _car.Session.Wallet);
        var (trunkCount, trunkMoney) =
            Core.Economy.TradeService.SellJunk(_car.Session.Trunk, _car.Session.Wallet);

        int total = pocketCount + trunkCount;
        _hud.Toast(total > 0
            ? $"Продано {total} шт. за {pocketMoney + trunkMoney:F0} €"
            : "Нечего продавать");

        _backpack.Refresh(_car.Session.Inventory, _car.Session.Trunk);
    }

    /// <summary>Рюкзак: открыть/закрыть, заодно отпустить или захватить мышь.</summary>
    private void ToggleBackpack()
    {
        _backpack.Toggle(_car.Session.Inventory, _car.Session.Trunk);
        SetCursorFree(_backpack.IsOpen);
    }

    private bool _orbitBeforeFree;

    /// <summary>Свободный курсор: обзор не крутится, мышь видна.</summary>
    private void SetCursorFree(bool free)
    {
        if (_cursorFree == free) return;

        _cursorFree = free;
        Input.MouseMode = free ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;

        if (free)
        {
            _orbitBeforeFree = _camera.OrbitWithMouse;
            _camera.OrbitWithMouse = false;
        }
        else
        {
            _camera.OrbitWithMouse = _orbitBeforeFree;
            ClearHover();
        }
    }

    /// <summary>Луч из камеры под курсор: что за предмет, подсветить и запомнить.</summary>
    private void UpdateHover(Vector2 screenPosition)
    {
        var camera = GetViewport().GetCamera3D();
        var space = GetWorld3D()?.DirectSpaceState;
        if (camera is null || space is null) return;

        Vector3 from = camera.ProjectRayOrigin(screenPosition);
        Vector3 to = from + camera.ProjectRayNormal(screenPosition) * 9f;

        var query = new PhysicsRayQueryParameters3D { From = from, To = to, CollisionMask = 2 };
        var hit = space.IntersectRay(query);

        LootItem? found = null;
        if (hit.Count > 0 && hit["collider"].As<GodotObject>() is Node3D body)
            found = body.GetParent() as LootItem;

        if (found == _hoveredLoot) return;

        ClearHover();
        _hoveredLoot = found;

        if (found is not null)
        {
            found.Scale = Vector3.One * 1.45f;
            _hud.Toast($"{Core.Items.ItemCatalog.Get(found.ItemId).Name} — ЛКМ поднять");
        }
    }

    private void ClearHover()
    {
        if (_hoveredLoot is not null) _hoveredLoot.Scale = Vector3.One;
        _hoveredLoot = null;
    }

    private void PickHoveredLoot()
    {
        if (_hoveredLoot is null) return;

        var point = _interactions.FirstOrDefault(p => p.Node == _hoveredLoot);
        if (point is not null) PickUpLoot(point);

        ClearHover();
    }

    /// <summary>Подобрать предмет: в инвентарь, узел убрать, точку взаимодействия снять.</summary>
    private void PickUpLoot(InteractionPoint point)
    {
        if (point.LootId is null || point.Node is null) return;

        int count = point.Node is LootItem item ? item.Count : 1;
        string name = Core.Items.ItemCatalog.Get(point.LootId).Name;

        if (!_car.Session.Inventory.TryAdd(point.LootId, count))
        {
            _hud.Toast("Инвентарь полон");
            return;
        }

        _hud.Toast($"Подобрано: {name} ×{count}");
        Sfx.PlayUi();
        _interactions.Remove(point);
        point.Node.QueueFree();
    }

    /// <summary>Разложить лут у точек интереса и внутри дома.</summary>
    private void SpawnLoot()
    {
        if (_road is null) return;

        int placed = 0;
        foreach (var (position, itemId, count) in LootSpawner.PlanLandmarks(_road.Points))
        {
            AddLoot(position, itemId, count);
            placed++;
        }

        int inHouse = 0;
        if (GetNodeOrNull<Node3D>("PlayerHouse") is { } house)
            foreach (var (position, itemId, count) in LootSpawner.PlanHouse(house))
            {
                AddLoot(position, itemId, count);
                inHouse++;
            }

        GD.Print($"[loot] предметов: {placed} у мест, {inHouse} в доме");
    }

    private void AddLoot(Vector3 position, string itemId, int count)
    {
        var item = LootItem.Create(itemId, count, position);
        AddChild(item);

        _interactions.Add(new InteractionPoint(position + Vector3.Up * 0.25f, InteractionKind.Loot, 2.2f,
            LootId: itemId, Node: item));
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
        foreach (var (position, door) in doors)
        {
            // Подсказка ищется от центра полотна, а не от петли: у петли радиус мал.
            Vector3 center = position + door.GlobalBasis.X.Normalized() * (door.Width * 0.5f);
            _interactions.Add(new InteractionPoint(center, InteractionKind.Door, 3.2f, Door: door));
        }

        GD.Print($"[house] дом игрока: {doors.Count} дверей, {spot}");
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

    private void AddVillage(Vector3 position, float heading)
    {
        var village = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };
        AddChild(village);

        var wall = new Color(0.80f, 0.76f, 0.66f);
        var roof = new Color(0.52f, 0.26f, 0.19f);
        var window = new Color(0.15f, 0.18f, 0.21f);
        var door = new Color(0.33f, 0.23f, 0.15f);
        var fence = new Color(0.42f, 0.34f, 0.24f);

        for (int i = 0; i < 3; i++)
        {
            var house = new Node3D { Position = new Vector3(i * 10f - 10f, 0, -i * 7f) };
            village.AddChild(house);

            float width = 8.0f + i * 0.5f;
            float depth = 6.4f;
            float height = 3.4f + i * 0.2f;

            Box(house, new Vector3(width, height, depth), new Vector3(0, height / 2, 0), wall);
            GableRoof(house, height, width, depth, 2.0f, roof);

            Box(house, new Vector3(0.9f, 1.1f, 0.12f), new Vector3(-1.7f, 1.8f, depth / 2), window);
            Box(house, new Vector3(0.9f, 1.1f, 0.12f), new Vector3(1.7f, 1.8f, depth / 2), window);
            Box(house, new Vector3(1.1f, 2.0f, 0.14f), new Vector3(0, 1.0f, depth / 2 + 0.02f), door);

            // труба на крыше
            Box(house, new Vector3(0.5f, 1.3f, 0.5f), new Vector3(width * 0.26f, height + 1.3f, -depth * 0.2f),
                new Color(0.46f, 0.41f, 0.37f));
        }

        // Забор вдоль улицы и столб с указателем села.
        for (int i = 0; i < 11; i++)
            Box(village, new Vector3(0.12f, 1.0f, 0.12f), new Vector3(-16 + i * 3.2f, 0.5f, 3.4f), fence);

        Box(village, new Vector3(32, 0.08f, 0.08f), new Vector3(0, 0.85f, 3.4f), fence);
        Box(village, new Vector3(32, 0.08f, 0.08f), new Vector3(0, 0.45f, 3.4f), fence);

        var sign = Box(village, new Vector3(2.4f, 0.7f, 0.08f), new Vector3(-13f, 2.3f, 3.0f),
            new Color(0.85f, 0.82f, 0.7f));
        sign.Rotation = new Vector3(0, 0.3f, 0);

        AddBusStop(village, new Vector3(3.5f, 0, 7.5f));
        AddMotel(village, new Vector3(-6f, 0, 9.5f));
    }

    /// <summary>Мотель у села: переночевать за 15 €, проснуться утром.</summary>
    private void AddMotel(Node3D parent, Vector3 position)
    {
        var motel = new Node3D { Position = position, Rotation = new Vector3(0, 0.18f, 0) };
        parent.AddChild(motel);

        var wall = new Color(0.72f, 0.66f, 0.56f);
        var roof = new Color(0.44f, 0.30f, 0.22f);
        var window = new Color(0.15f, 0.18f, 0.21f);

        Box(motel, new Vector3(8.8f, 3.6f, 6.2f), new Vector3(0, 1.8f, 0), wall);
        GableRoof(motel, 3.6f, 8.8f, 6.2f, 1.9f, roof);

        for (int i = -1; i <= 1; i++)
            Box(motel, new Vector3(0.9f, 0.9f, 0.12f), new Vector3(i * 2f, 1.9f, 2.52f), window);

        Box(motel, new Vector3(1.1f, 2f, 0.14f), new Vector3(0, 1f, 2.53f), new Color(0.30f, 0.22f, 0.16f));

        var sign = Box(motel, new Vector3(2.8f, 0.62f, 0.1f), new Vector3(0, 3.35f, 2.45f),
            new Color(0.86f, 0.83f, 0.70f));
        sign.Rotation = new Vector3(0, 0, 0);

        motel.AddChild(new Label3D
        {
            Text = "МОТЕЛЬ",
            FontSize = 56,
            PixelSize = 0.0045f,
            Position = new Vector3(0, 3.35f, 2.52f),
            Modulate = new Color(0.16f, 0.15f, 0.14f),
        });

        _interactions.Add(new InteractionPoint(
            motel.ToGlobal(new Vector3(0, 0, 3.4f)), InteractionKind.Sleep, 4.5f));
    }

    /// <summary>Автобусная остановка: бетонный навес, лавка и табличка.</summary>
    private static void AddBusStop(Node3D parent, Vector3 position)
    {
        var stop = new Node3D { Position = position, Rotation = new Vector3(0, -0.25f, 0) };
        parent.AddChild(stop);

        var concrete = new Color(0.66f, 0.64f, 0.58f);
        var bench = new Color(0.45f, 0.32f, 0.2f);

        Box(stop, new Vector3(5.0f, 0.18f, 2.2f), new Vector3(0, 2.6f, 0), concrete);
        Box(stop, new Vector3(4.4f, 1.3f, 0.14f), new Vector3(0, 1.75f, -0.95f), concrete);
        Box(stop, new Vector3(0.14f, 2.45f, 2.0f), new Vector3(-2.15f, 1.22f, 0), concrete);
        Box(stop, new Vector3(0.14f, 2.45f, 2.0f), new Vector3(2.15f, 1.22f, 0), concrete);

        Box(stop, new Vector3(3.4f, 0.12f, 0.5f), new Vector3(0, 0.55f, -0.5f), bench);
        Box(stop, new Vector3(3.4f, 0.45f, 0.1f), new Vector3(0, 0.82f, -0.72f), bench);

        stop.AddChild(new Label3D
        {
            Text = "АУТОБУС",
            FontSize = 64,
            PixelSize = 0.004f,
            Position = new Vector3(0, 2.05f, -0.88f),
            Modulate = new Color(0.95f, 0.92f, 0.8f),
            OutlineSize = 6,
        });
    }

    private void AddRuins(Vector3 position, float heading)
    {
        var ruins = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };
        AddChild(ruins);

        var stone = new Color(0.61f, 0.58f, 0.51f);
        var beam = new Color(0.29f, 0.22f, 0.16f);

        Box(ruins, new Vector3(10.5f, 3.2f, 0.45f), new Vector3(0, 1.6f, 0), stone);
        Box(ruins, new Vector3(0.45f, 4.2f, 7), new Vector3(4.9f, 2.1f, 0), stone);
        Box(ruins, new Vector3(0.45f, 1.6f, 4), new Vector3(-4.2f, 0.8f, 0), stone);
        Box(ruins, new Vector3(3.4f, 1.0f, 0.45f), new Vector3(-2.4f, 0.5f, 0.1f), stone);

        var collapsed = Box(ruins, new Vector3(5.4f, 0.22f, 0.22f), new Vector3(0.6f, 1.6f, 1.2f), beam);
        collapsed.Rotation = new Vector3(0, 0, 0.38f);

        var collapsed2 = Box(ruins, new Vector3(4.2f, 0.2f, 0.2f), new Vector3(-1.1f, 0.95f, -0.9f), beam);
        collapsed2.Rotation = new Vector3(0, 0, -0.52f);

        AddWreck(ruins, new Vector3(6.4f, 0, 3.4f));
    }

    /// <summary>Брошенная машина у руин: кузов, крыша и колёса, слегка завалена.</summary>
    private static void AddWreck(Node3D parent, Vector3 position)
    {
        var wreck = new Node3D { Position = position, Rotation = new Vector3(0.02f, 0.8f, 0.05f) };
        parent.AddChild(wreck);

        var rust = new Color(0.44f, 0.29f, 0.19f);
        var dark = new Color(0.13f, 0.13f, 0.13f);

        Box(wreck, new Vector3(1.6f, 0.5f, 3.7f), new Vector3(0, 0.45f, 0), rust);
        Box(wreck, new Vector3(1.42f, 0.42f, 1.7f), new Vector3(0, 0.88f, 0.15f), rust);
        Box(wreck, new Vector3(1.36f, 0.28f, 1.5f), new Vector3(0, 0.92f, 0.15f), dark);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int axle = -1; axle <= 1; axle += 2)
            {
                wreck.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { Height = 0.22f, TopRadius = 0.3f, BottomRadius = 0.3f, RadialSegments = 10 },
                    Position = new Vector3(side * 0.78f, 0.3f, axle * 1.3f),
                    Rotation = new Vector3(0, 0, Mathf.Pi / 2),
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.19f, 0.18f), Roughness = 1f },
                });
            }
        }
    }

    private void AddFarm(Vector3 position, float heading)
    {
        var farm = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };
        AddChild(farm);

        var wall = new Color(0.64f, 0.52f, 0.38f);
        var roof = new Color(0.42f, 0.30f, 0.22f);
        var fence = new Color(0.42f, 0.34f, 0.24f);

        Box(farm, new Vector3(12.5f, 4.6f, 8.5f), new Vector3(0, 2.3f, 0), wall);
        GableRoof(farm, 4.6f, 12.5f, 8.5f, 2.7f, roof);
        Box(farm, new Vector3(2.4f, 2.8f, 0.14f), new Vector3(0, 1.4f, 3.55f), new Color(0.29f, 0.21f, 0.15f));

        var silo = new MeshInstance3D
        {
            Mesh = new CylinderMesh { Height = 9, TopRadius = 1.9f, BottomRadius = 2.1f, RadialSegments = 12 },
            Position = new Vector3(8.5f, 4.5f, -2),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.74f, 0.72f, 0.66f), Roughness = 0.85f },
        };
        farm.AddChild(silo);

        farm.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1.95f, Height = 1.6f },
            Position = new Vector3(8.5f, 9.2f, -2),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.5f, 0.48f), Roughness = 0.8f },
        });

        for (int i = 0; i < 6; i++)
            Box(farm, new Vector3(0.14f, 1.1f, 0.14f), new Vector3(-6 + i * 2.4f, 0.55f, 5.2f), fence);

        Box(farm, new Vector3(14, 0.1f, 0.1f), new Vector3(0, 0.95f, 5.2f), fence);
    }

    /// <summary>Двускатная крыша: две наклонённые плиты с коньком по центру.</summary>
    private static void GableRoof(Node3D parent, float baseY, float width, float depth, float height, Color color)
    {
        float half = width / 2 + 0.45f;
        float slant = Mathf.Sqrt(half * half + height * height);
        float angle = Mathf.Atan2(height, half);

        for (int side = -1; side <= 1; side += 2)
        {
            var slab = Box(parent, new Vector3(slant, 0.18f, depth + 0.9f),
                new Vector3(side * half / 2, baseY + height / 2, 0), color);
            slab.Rotation = new Vector3(0, 0, -side * angle);
        }
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
