using System;
using Godot;
using Krntija.Core;
using Krntija.Core.Items;
using Krntija.Core.Parts;
using Krntija.Core.Workshop;

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
            _car.GlobalPosition = new Vector3(0, 0.6f, -104);
            _camera.GlobalPosition = new Vector3(6, 5.5f, -94);
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
            case Key.R when _hud.JournalVisible:
                RepairWorstPart();
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

    private void RepairWorstPart()
    {
        var session = _car.Session;
        var worst = VehicleService.WorstPart(session.Vehicle);
        if (worst is null) return;

        if (session.Inventory.Count(ItemIds.DuctTape) == 0)
        {
            _hud.Toast("Нет изоленты");
            return;
        }

        _hud.Toast(VehicleService.TryRepair(session.Vehicle, session.Inventory, worst.Slot)
            ? $"{worst.Name}: подмотано"
            : $"{worst.Name} в порядке");
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

        var road = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(9, 0.06f, 2000) },
            Position = new Vector3(0, 0.03f, -900),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.13f, 0.13f, 0.14f), Roughness = 0.95f },
        };
        AddChild(road);

        // Пара ориентиров, чтобы было видно движение.
        var concrete = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.53f, 0.48f), Roughness = 0.9f };
        for (int i = 0; i < 8; i++)
        {
            var pillar = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.4f, 4f, 0.4f) },
                Position = new Vector3(i % 2 == 0 ? -6.5f : 6.5f, 2f, -20f - i * 25f),
                MaterialOverride = concrete,
            };
            AddChild(pillar);
        }

        AddGasStation(new Vector3(-13, 0, -120));
    }

    /// <summary>Первый POI: навес, колонка и магазинчик у дороги.</summary>
    private void AddGasStation(Vector3 origin)
    {
        var concrete = new StandardMaterial3D { AlbedoColor = new Color(0.7f, 0.68f, 0.62f), Roughness = 0.9f };
        var red = new StandardMaterial3D { AlbedoColor = new Color(0.66f, 0.22f, 0.18f), Roughness = 0.7f };
        var yellow = new StandardMaterial3D { AlbedoColor = new Color(0.83f, 0.74f, 0.4f), Roughness = 0.6f };

        var station = new Node3D { Position = origin };
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
