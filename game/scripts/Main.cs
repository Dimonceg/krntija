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

/// <summary>
/// Точка входа: собирает сцену, крутит кадр, разбирает ввод.
/// Остальное — в partial-файлах Main.*.cs: мир, взаимодействия, лут,
/// меню и сейвы, отладочные прогоны.
/// </summary>
public partial class Main : Node3D
{
    private const string SavePath = "user://save.json";

    private Car _car = null!;
    private ChaseCamera _camera = null!;
    private bool _firstPerson;
    private Hud _hud = null!;
    private Player? _player;
    private bool _onFoot;
    private BackpackUi _backpack = null!;
    private MapUi _mapUi = null!;
    private MapUi _miniMap = null!;
    private float _miniMapTimer;
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
    private Vector3? _wolvesCenter;
    private float _wolfCooldown;

    public override void _Ready()
    {
        if (HasArg("--balance"))
        {
            BalanceProbe.Run();
            GetTree().Quit(0);
            return;
        }

        if (ArgValue("--model=") is { } model)
            Car.ModelOverride = model;
        SetupInput();
        GameSettings.Load();
        GameSettings.Apply();
        ParseDebugArgs();

        // Проверка ассетов мира: печатает габариты и центровку каждого пропа.
        if (HasArg("--check-props"))
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

        _mapUi = new MapUi();
        AddChild(_mapUi);
        if (_road is not null)
            _mapUi.SetData(_road.Centerline, _map.Branches, _map.Places, _map.ClosedExits);

        // Маленькая карта в углу: видна всегда.
        _miniMap = new MapUi { Compact = true };
        AddChild(_miniMap);
        _miniMap.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _miniMap.OffsetLeft = -246;
        _miniMap.OffsetTop = 10;
        _miniMap.OffsetRight = -12;
        _miniMap.OffsetBottom = 170;
        _miniMap.Visible = true;
        if (_road is not null)
            _miniMap.SetData(_road.Centerline, _map.Branches, _map.Places, _map.ClosedExits);

        // Дом игрока у старта: слева от дороги, со своими дверями и чердаком.
        BuildPlayerHouse();
        SpawnLoot();
        BatchStaticGeometry();

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
        bool fpCar = HasArg("--fp");
        bool fpFoot = HasArg("--fp-foot");
        if (fpCar || fpFoot)
        {
            _firstPerson = true;
            _camera.FirstPerson = true;
            _camera.AutoRecenter = false;

            if (fpFoot) ExitCar();
            else _camera.FirstPersonOffset = new Vector3(0.30f, 0.86f, -1.05f);
        }

        // Диагностика звука: печатает драйвер, шину и состояние плееров.
        if (HasArg("--check-sound"))
        {
            PrintSoundDiagnostics();
            GetTree().Quit(0);
            return;
        }

        _car.Autopilot = DrivesItself;

        if (HasArg("--check-scene"))
        {
            RunSceneReport();
            GetTree().Quit(0);
            return;
        }

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

        if (_shot)
            SetupShot();

        GD.Print($"[Krntija] старт. smoke={_smoke}, selftest={_selfTest}, машина: {session.Vehicle.Spec.Name}");
    }

    public override void _Process(double delta)
    {
        if (_hud is null || _car is null) return;

        // Перегруз: полный карман замедляет пешехода, багажник на скорость не влияет.
        if (_player is not null)
        {
            var bag = _car.Session.Inventory;
            _player.SpeedMultiplier = Core.Items.Encumbrance.SpeedFactor(bag.WeightKg, bag.MaxWeightKg);
        }

        _hud.Prompt = BuildPrompt();

        if (_mapUi is not null && _mapUi.IsOpen)
            _mapUi.UpdatePlayer(_car.GlobalPosition);

        // Маленькая карта обновляется четыре раза в секунду, чтобы не грузить кадр.
        _miniMapTimer -= (float)delta;
        if (_miniMap is not null && _miniMapTimer <= 0f)
        {
            _miniMapTimer = 0.25f;
            _miniMap.UpdatePlayer(_car.GlobalPosition);
        }

        // Волки: подошёл близко — теряешь здоровье.
        _wolfCooldown = Mathf.Max(0f, _wolfCooldown - (float)delta);
        if (_wolvesCenter is { } wolves && _wolfCooldown <= 0f
            && _car.GlobalPosition.DistanceTo(wolves) < 13f)
        {
            _wolfCooldown = 5f;
            _car.Session.Survival.Health = Mathf.Max(0f, _car.Session.Survival.Health - 0.05f);
            _hud.Toast("Волки! Отъезжай");
        }

        UpdateSky();
        ApplyWeather();
        RewardNewPlaces();

        if (_objectiveDone || _objectivePosition is null) return;

        _hud.Objective = $"Добраться до: {_objectiveTitle}  (+{WorldActions.ObjectiveReward:F0} €)";

        if (_car.GlobalPosition.DistanceTo(_objectivePosition.Value) > 30f) return;

        _objectiveDone = true;
        _car.Session.Wallet.Earn(WorldActions.ObjectiveReward);
        _hud.Objective = string.Empty;
        _hud.Toast($"Задача выполнена: {_objectiveTitle}. +{WorldActions.ObjectiveReward:F0} €");
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
            case Key.M:
                _mapUi.Toggle();
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
