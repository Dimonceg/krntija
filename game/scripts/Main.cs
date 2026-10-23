using System;
using Godot;
using Krntija.Core;
using Krntija.Core.Items;
using Krntija.Core.Parts;
using Krntija.Core.Workshop;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>Точка входа M1: полигон, машина, журнал, ремонт, сейвы и headless-прогон.</summary>
public partial class Main : Node3D
{
    private const string SavePath = "user://save.json";

    private Car _car = null!;
    private ChaseCamera _camera = null!;
    private Hud _hud = null!;
    private bool _smoke;
    private bool _smokeDone;
    private bool _selfTest;
    private bool _selfTestDone;
    private bool _selfTestOk;
    private bool _shot;
    private int _frames;

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
            _car.GlobalPosition = new Vector3(0, 0.6f, -40);
            _camera.GlobalPosition = new Vector3(5, 6, -26);
        }

        GD.Print($"[Krntija] старт. smoke={_smoke}, selftest={_selfTest}, машина: {session.Vehicle.Spec.Name}");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!(_smoke || _selfTest || _shot) || _smokeDone) return;

        _frames++;

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

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        switch (key.Keycode)
        {
            case Key.Escape:
                GetTree().Quit(0);
                break;
            case Key.J:
                _hud.JournalVisible = !_hud.JournalVisible;
                break;
            case Key.F5:
                SaveGame();
                break;
            case Key.F9:
                LoadGame();
                break;
            case Key.Up when _hud.JournalVisible:
                _hud.MoveSelection(-1);
                break;
            case Key.Down when _hud.JournalVisible:
                _hud.MoveSelection(1);
                break;
            case Key.T when _hud.JournalVisible:
                InstallSelectedPart();
                break;
            case Key.R when _hud.JournalVisible:
                RepairSelectedPart();
                break;
            case Key.F when _hud.JournalVisible:
                RefuelFromCan();
                break;
        }
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
            SaveGame();

            session.Vehicle.Fuel.SetLiters(30);
            engine.SetCondition(0.9);
            LoadGame();

            Check(Math.Abs(_car.Session.Vehicle.Fuel.Liters - 5) < 0.05, "топливо восстановлено из сейва");
            Check(Math.Abs(_car.Session.Vehicle.Get(PartSlot.Engine)!.Condition - 0.60) < 0.01, "износ восстановлен");
            Check(_car.Session.Vehicle.Spec.Id == "zastava_101", "модель машины восстановлена");

            _hud.JournalVisible = true;
            Check(_hud.JournalVisible, "журнал открывается");

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
