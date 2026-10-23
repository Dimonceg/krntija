using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Krntija.Core;
using Krntija.Core.Economy;
using Krntija.Core.Items;
using Krntija.Core.Parts;
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
    private int _frames;

    private enum InteractionKind
    {
        Pump,
        Shop,
    }

    private sealed record InteractionPoint(Vector3 Position, InteractionKind Kind, float Radius);

    private readonly List<InteractionPoint> _interactions = new();
    private InteractionPoint? _shotSpot;

    public override void _Ready()
    {
        SetupInput();
        BuildWorld();

        var session = GameSession.NewGame();

        _car = Car.Build(session);
        AddChild(_car);
        _car.GlobalPosition = new Vector3(0, 0.595f, 0);

        _camera = new ChaseCamera { Target = _car };
        AddChild(_camera);
        _camera.GlobalPosition = new Vector3(0, 4.5f, 10);

        _hud = new Hud { Car = _car };
        AddChild(_hud);

        _smoke = Array.IndexOf(OS.GetCmdlineUserArgs(), "--smoke") >= 0;
        _selfTest = Array.IndexOf(OS.GetCmdlineUserArgs(), "--selftest") >= 0;
        _shot = Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot") >= 0;
        _car.Autopilot = _smoke || _selfTest;

        if (_shot)
        {
            var pump = _interactions.FirstOrDefault(p => p.Kind == InteractionKind.Pump);
            _shotSpot = pump;

            if (pump is not null)
            {
                _car.GlobalPosition = pump.Position + new Vector3(12f, 0.6f, 10f);
                _camera.GlobalPosition = _car.GlobalPosition + new Vector3(4, 4, 4);
            }
        }

        GD.Print($"[Krntija] старт. smoke={_smoke}, selftest={_selfTest}, машина: {session.Vehicle.Spec.Name}");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!(_smoke || _selfTest || _shot) || _smokeDone) return;

        _frames++;

        if (_shot && _frames == 110 && _shotSpot is not null)
        {
            _car.LinearVelocity = Vector3.Zero;
            ExitCar();
            _player!.Teleport(_shotSpot.Position + new Vector3(2.2f, 0.3f, 2.2f));
            _player.Yaw = 0.78f;
        }

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
            if (_frames >= 160)
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

    public override void _Process(double delta)
    {
        if (_hud is null || _car is null) return;
        _hud.Prompt = BuildPrompt();
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

        switch (key.Keycode)
        {
            case Key.Escape:
                GetTree().Quit(0);
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

        if (_onFoot && _player is not null)
        {
            if (DistanceToCar() < 3.5f) return "[E] сесть в машину";

            var point = NearestInteraction();
            if (point is not null)
                return point.Kind == InteractionKind.Shop ? "[E] зайти в лавку" : "[E] заправить";

            return "W/S — идти, A/D — поворот, Shift — бежать";
        }

        if (NearestInteraction()?.Kind == InteractionKind.Pump && _car.SpeedMps < 2f)
            return "[E] заправить";

        return _car.SpeedMps < 2f ? "[E] выйти из машины" : string.Empty;
    }

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
            if (point is null) return;

            if (point.Kind == InteractionKind.Shop) _hud.Screen = HudScreen.Shop;
            else RefuelAtPump();

            return;
        }

        if (NearestInteraction()?.Kind == InteractionKind.Pump && _car.SpeedMps < 2f)
        {
            RefuelAtPump();
            return;
        }

        if (_car.SpeedMps >= 2f)
        {
            _hud.Toast("Сначала остановись");
            return;
        }

        ExitCar();
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

            GD.Print("[selftest] пройден");
        }
        catch (Exception e)
        {
            _selfTestOk = false;
            GD.PrintErr($"[selftest] исключение: {e}");
        }
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

    // ---------- игровые действия ----------

    private void SaveGame()
    {
        var path = ProjectSettings.GlobalizePath(SavePath);
        SaveSerializer.SaveFile(path, _car.Session.ToSave());
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

        var session = GameSession.FromSave(SaveSerializer.LoadFile(path));
        Respawn(session);
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

        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky { SkyMaterial = skyMaterial },
                AmbientLightSource = Godot.Environment.AmbientSource.Sky,
                AmbientLightEnergy = 0.7f,
                FogEnabled = true,
                FogLightColor = new Color(0.7f, 0.68f, 0.6f),
                FogDensity = 0.0015f,
            },
        });

        var sun = new DirectionalLight3D { LightEnergy = 1.3f, ShadowEnabled = true };
        sun.RotationDegrees = new Vector3(-48, -35, 0);
        AddChild(sun);

        var ground = new StaticBody3D { Position = new Vector3(0, -1, 0) };
        ground.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2000, 2, 2000) } });
        ground.AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(2000, 2000) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.24f, 0.27f, 0.17f), Roughness = 1f },
        });
        AddChild(ground);

        BuildRoad();
    }

    /// <summary>Дорога из плана ядра: лента, коллизия и точки интереса по обочинам.</summary>
    private void BuildRoad()
    {
        var plan = RoadPlanner.Generate(seed: 20261023, totalMeters: 2500);
        var pois = RoadMeshBuilder.Build(plan, this);

        foreach (var poi in pois)
        {
            var right = new Vector3(Mathf.Cos(poi.Heading), 0, Mathf.Sin(poi.Heading));
            Vector3 spot = poi.Position - right * 16f; // левая обочина
            AddLandmark(poi.Segment.Feature, spot, poi.Heading);
        }

        GD.Print($"[road] {plan.Segments.Count} сегментов, {plan.TotalMeters:F0} м, точек интереса: {pois.Count}");
    }

    private void AddLandmark(RoadFeature feature, Vector3 position, float heading)
    {
        switch (feature)
        {
            case RoadFeature.GasStation: AddGasStation(position, heading); break;
            case RoadFeature.Village: AddVillage(position, heading); break;
            case RoadFeature.Ruins: AddRuins(position, heading); break;
            case RoadFeature.Farm: AddFarm(position, heading); break;
            case RoadFeature.Checkpoint: AddCheckpoint(position, heading); break;
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

    private void AddCheckpoint(Vector3 position, float heading)
    {
        var checkpoint = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };
        AddChild(checkpoint);

        Box(checkpoint, new Vector3(2.4f, 2.6f, 2.4f), new Vector3(-3, 1.3f, 0), new Color(0.72f, 0.7f, 0.6f));
        Box(checkpoint, new Vector3(0.2f, 0.2f, 8), new Vector3(2, 1.1f, 4), new Color(0.85f, 0.75f, 0.3f));
        Box(checkpoint, new Vector3(0.15f, 1.1f, 0.15f), new Vector3(2, 0.55f, 0), new Color(0.5f, 0.5f, 0.5f));
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

        // Точки взаимодействия: колонка и вход в лавку (снаружи, у стены).
        _interactions.Add(new InteractionPoint(
            station.ToGlobal(new Vector3(0, 1f, 0)), InteractionKind.Pump, 4.5f));
        _interactions.Add(new InteractionPoint(
            station.ToGlobal(new Vector3(0, 1.6f, -6.4f)), InteractionKind.Shop, 4.5f));
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
        AddKey("journal", Key.J);
    }

    private static void AddKey(string action, Key key)
    {
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action, deadzone: 0.2f);

        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }
}
