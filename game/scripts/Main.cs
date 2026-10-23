using System;
using Godot;
using Krntija.Core;

namespace Krntija.Game;

/// <summary>Точка входа M0: полигон, машина, камера, HUD и headless-прогон.</summary>
public partial class Main : Node3D
{
    private Car _car = null!;
    private bool _smoke;
    private bool _smokeDone;
    private int _frames;

    public override void _Ready()
    {
        SetupInput();
        BuildWorld();

        _car = Car.Build(GameSession.NewGame());
        AddChild(_car);
        _car.GlobalPosition = new Vector3(0, 0.595f, 0);

        var camera = new ChaseCamera { Target = _car };
        AddChild(camera);
        camera.GlobalPosition = new Vector3(0, 4.5f, 10);

        AddChild(new Hud { Car = _car });

        _smoke = Array.IndexOf(OS.GetCmdlineUserArgs(), "--smoke") >= 0;
        _car.Autopilot = _smoke;

        GD.Print($"[Krntija] старт. smoke={_smoke}, тесты: W/S/A/D, V — зажигание, Space — ручник");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_smoke || _smokeDone) return;

        _frames++;
        if (_frames % 60 == 0)
            GD.Print($"[smoke] кадр {_frames}: {_car.SpeedMps:F2} м/с, колёс на земле {_car.GroundedWheels}, "
                     + $"y={_car.GlobalPosition.Y:F2}, наклон {_car.RotationDegrees.X:F0}/{_car.RotationDegrees.Z:F0}, "
                     + $"угл.скор {_car.AngularVelocity.Length():F2}, топливо {_car.Session.Vehicle.Fuel.Liters:F2} л");

        if (_frames < 600) return;

        _smokeDone = true;
        var v = _car.Session.Vehicle;
        float speed = _car.SpeedMps;

        GD.Print($"[smoke] кадров={_frames} скорость={speed:F2} м/с ({speed * 3.6f:F1} км/ч) "
                 + $"топливо={v.Fuel.Liters:F2} л температура={v.EngineTempC:F1} °C одометр={v.OdometerKm:F2} км");

        GetTree().Quit(speed > 5f ? 0 : 1);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
            GetTree().Quit(0);
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
