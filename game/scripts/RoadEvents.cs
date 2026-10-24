using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core.Parts;
using Krntija.Core.Vehicle;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>
/// События на дороге: ямы, коровы, попутчики. Ставятся по сиду вдоль трассы,
/// срабатывают по расстоянию до машины — без физических триггеров, чтобы
/// не плодить узлы и не зависеть от кадров.
/// </summary>
public partial class RoadEvents : Node3D
{
    private sealed class Spot
    {
        public Node3D Node = null!;
        public Vector3 Position;
        public EventKind Kind;
        public bool Done;
        public float SpeedLogged;
    }

    private enum EventKind
    {
        Pothole,
        Cow,
        Hitchhiker,
        Breakdown,
        Control,
    }

    private readonly List<Spot> _spots = new();
    private static readonly PartSlot[] Wheels =
    {
        PartSlot.WheelFrontLeft, PartSlot.WheelFrontRight, PartSlot.WheelRearLeft, PartSlot.WheelRearRight,
    };

    private Car _car = null!;
    private Hud _hud = null!;
    private double _punctureToast;

    /// <summary>
    /// Машину пересоздали (новая игра или загрузка сейва) — обновляем ссылки,
    /// иначе события продолжают толкать удалённый узел.
    /// </summary>
    public void Rebind(Car car, Hud hud)
    {
        _car = car;
        _hud = hud;
    }

    public void Setup(List<Vector3> centerline, List<float> headings, Car car, Hud hud)
    {
        _car = car;
        _hud = hud;

        var rng = new Pcg32(20261024, sequence: 31);

        for (int i = 40; i < centerline.Count - 12; i += 26)
        {
            double roll = rng.NextDouble();
            EventKind kind = roll switch
            {
                < 0.45 => EventKind.Pothole,
                < 0.62 => EventKind.Cow,
                < 0.76 => EventKind.Hitchhiker,
                < 0.90 => EventKind.Breakdown,
                _ => EventKind.Control,
            };

            int side = rng.Chance(0.5) ? 1 : -1;
            var right = new Vector3(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));
            float lateral = kind switch
            {
                EventKind.Pothole => (float)rng.Range(-2.8f, 2.8f),
                EventKind.Cow => (float)rng.Range(-1.5f, 1.5f),
                EventKind.Breakdown => (float)rng.Range(-1.0f, 1.0f),
                _ => 6.4f * side,
            };

            Vector3 position = centerline[i] + right * lateral;
            position.Y = centerline[i].Y;

            var spot = new Spot { Position = position, Kind = kind };
            spot.Node = kind switch
            {
                EventKind.Pothole => BuildPothole(position, headings[i]),
                EventKind.Cow => BuildCow(position, headings[i] + (float)rng.Range(-0.8f, 0.8f)),
                EventKind.Breakdown => BuildBreakdown(position, headings[i]),
                EventKind.Control => BuildControl(position, headings[i]),
                _ => BuildHitchhiker(position, headings[i]),
            };

            AddChild(spot.Node);
            _spots.Add(spot);
            GD.Print($"[event] {kind} at index {i}");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Машину пересоздают при новой игре и загрузке сейва: старый узел может
        // на кадр оказаться вне дерева, и GlobalPosition уже не посчитать.
        if (_car is null || _hud is null || !IsInstanceValid(_car) || !_car.IsInsideTree()) return;

        Vector3 carPosition = _car.GlobalPosition;
        float speed = _car.SpeedMps;

        foreach (var spot in _spots)
        {
            if (spot.Done) continue;

            Vector3 delta3 = spot.Position - carPosition;
            float horizontal = new Vector2(delta3.X, delta3.Z).Length();

            switch (spot.Kind)
            {
                case EventKind.Pothole when horizontal < 1.1f && speed > 8f:
                    HitPothole(spot);
                    break;

                case EventKind.Cow when horizontal < 2.4f && speed > 0.8f:
                    HitCow(spot, speed);
                    break;

                case EventKind.Hitchhiker when horizontal < 6f && speed < 0.8f:
                    TakeHitchhiker(spot);
                    break;

                case EventKind.Breakdown when horizontal < 1.6f && speed > 3f:
                    BreakDown(spot);
                    break;

                // Пост контроля: медленно — пропустили, быстро — штраф.
                case EventKind.Control when horizontal < 4f && spot.SpeedLogged == 0f:
                    PassControl(spot, speed);
                    break;
            }
        }

        // Слишком далеко уехали — событие больше не ждём.
        foreach (var spot in _spots)
        {
            if (spot.Done) continue;
            if (new Vector2(spot.Position.X - carPosition.X, spot.Position.Z - carPosition.Z).Length() > 120f)
                spot.Done = true;
        }

        UpdatePuncture(delta);
    }

    /// <summary>Пробитое колесо тянет машину: сильное сопротивление и напоминание.</summary>
    private void UpdatePuncture(double delta)
    {
        bool punctured = false;
        foreach (var slot in Wheels)
        {
            if (_car.Session.Vehicle.Parts[slot].IsBroken) punctured = true;
        }

        if (!punctured) return;

        Vector3 velocity = _car.LinearVelocity;
        _car.ApplyCentralForce(-new Vector3(velocity.X, 0f, velocity.Z) * 700f);

        _punctureToast -= delta;
        if (_punctureToast > 0) return;

        _punctureToast = 14;
        _hud.Toast("Колесо пробито — замените из сумки (J, затем T)");
    }

    private void HitPothole(Spot spot)
    {
        spot.Done = true;
        _car.Session.Vehicle.Parts[PartSlot.SuspensionFront].Wear(0.035);
        _car.Session.Vehicle.Parts[PartSlot.SuspensionRear].Wear(0.025);

        // Толчок вверх — удар чувствуется, но машину не разворачивает.
        _car.ApplyCentralImpulse(new Vector3(0f, 220f, 0f));

        if (_car.SpeedMps > 14f && GD.Randf() < 0.45f)
        {
            var wheel = Wheels[(int)(GD.Randi() % (uint)Wheels.Length)];
            _car.Session.Vehicle.Parts[wheel].Wear(1.0);
            _hud.Toast("Яма! Пробило колесо — машину тянет в сторону");
            return;
        }

        _hud.Toast("Яма! Подвеска получила по зубам");
    }

    private void HitCow(Spot spot, float speed)
    {
        spot.Done = true;
        _car.Session.Vehicle.Parts[PartSlot.Radiator].Wear(0.06 + speed * 0.004);
        _car.Session.Vehicle.Parts[PartSlot.Body].Wear(0.05);
        _car.ApplyCentralImpulse(new Vector3(0f, 120f, 0f));

        spot.Node.QueueFree();
        _hud.Toast("Корова! Радиатор помят, корова ушла");
    }

    private void TakeHitchhiker(Spot spot)
    {
        spot.Done = true;
        _car.Session.Wallet.Earn(12);
        spot.Node.QueueFree();
        _hud.Toast("Подобрали попутчика: +12 €");
    }

    private void BreakDown(Spot spot)
    {
        spot.Done = true;

        _car.Session.Vehicle.EngineRunning = false;
        _car.Session.Vehicle.Parts[PartSlot.Radiator].Wear(0.04);
        _car.Session.Vehicle.EngineTempC = Mathf.Min(120f, (float)_car.Session.Vehicle.EngineTempC + 25f);

        _hud.Toast("Двигатель заглох. Зажигание — V, радиатор проверить");
    }

    private void PassControl(Spot spot, float speed)
    {
        spot.SpeedLogged = Mathf.Max(0.1f, speed);

        if (speed > 6f)
        {
            const double fine = 15;
            if (_car.Session.Wallet.TrySpend(fine))
            {
                _hud.Toast("Контроль: штраф 15 € за скорость");
            }
            else
            {
                _hud.Toast("Контроль: денег нет, записали в долг");
            }

            _car.Session.Vehicle.Parts[PartSlot.Body].Wear(0.005);
        }
        else
        {
            _hud.Toast("Контроль пройден");
        }
    }

    // ------------------------------------------------------------- внешний вид

    private static Node3D BuildPothole(Vector3 position, float heading)
    {
        var root = new Node3D
        {
            Position = position + Vector3.Up * 0.02f,
            Rotation = new Vector3(0, -heading, 0),
        };

        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3((float)GD.RandRange(1.1, 2.2), 0.02f, (float)GD.RandRange(0.7, 1.4)) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.05f, 0.05f, 0.06f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        });

        return root;
    }

    private static Node3D BuildCow(Vector3 position, float heading)
    {
        var root = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };

        var hide = new Color(0.82f, 0.79f, 0.72f);
        var spots = new Color(0.25f, 0.22f, 0.2f);

        AddBox(root, new Vector3(0.7f, 0.75f, 1.5f), new Vector3(0, 1.05f, 0), hide);
        AddBox(root, new Vector3(0.45f, 0.5f, 0.55f), new Vector3(0, 1.15f, -0.95f), hide);
        AddBox(root, new Vector3(0.36f, 0.2f, 0.3f), new Vector3(0, 1.38f, -1.0f), spots);

        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
                AddBox(root, new Vector3(0.14f, 0.7f, 0.14f), new Vector3(sx * 0.24f, 0.35f, sz * 0.55f), spots);
        }

        return root;
    }

    private static Node3D BuildBreakdown(Vector3 position, float heading)
    {
        var root = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };

        // Лужа масла на асфальте — видно, что тут кто-то встал.
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(2.2f, 0.02f, 1.2f) },
            Position = new Vector3(0, 0.02f, 0),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.06f, 0.05f, 0.04f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        });

        return root;
    }

    private static Node3D BuildControl(Vector3 position, float heading)
    {
        var root = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };

        var post = new Color(0.62f, 0.6f, 0.55f);
        var board = new Color(0.86f, 0.83f, 0.7f);
        var red = new Color(0.7f, 0.16f, 0.13f);

        AddBox(root, new Vector3(0.12f, 2.6f, 0.12f), new Vector3(-2.6f, 1.3f, 0), post);
        AddBox(root, new Vector3(0.12f, 2.6f, 0.12f), new Vector3(2.6f, 1.3f, 0), post);
        AddBox(root, new Vector3(4.4f, 0.6f, 0.08f), new Vector3(0, 2.4f, 0), board);
        AddBox(root, new Vector3(4.4f, 0.16f, 0.14f), new Vector3(0, 1.2f, 0), red);

        root.AddChild(new Label3D
        {
            Text = "КОНТРОЛЬ",
            FontSize = 52,
            PixelSize = 0.004f,
            Position = new Vector3(0, 2.4f, 0.07f),
            Modulate = new Color(0.16f, 0.15f, 0.14f),
        });

        return root;
    }

    private static Node3D BuildHitchhiker(Vector3 position, float heading)
    {
        var root = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };

        var shirt = new Color(0.42f, 0.36f, 0.28f);
        var skin = new Color(0.78f, 0.62f, 0.48f);
        var trousers = new Color(0.22f, 0.23f, 0.28f);

        AddBox(root, new Vector3(0.42f, 0.62f, 0.24f), new Vector3(0, 1.12f, 0), shirt);
        AddBox(root, new Vector3(0.36f, 0.5f, 0.22f), new Vector3(0, 0.56f, 0), trousers);
        AddBox(root, new Vector3(0.24f, 0.26f, 0.24f), new Vector3(0, 1.62f, 0), skin);

        // Поднятая рука — видно издалека, что голосуют.
        AddBox(root, new Vector3(0.12f, 0.55f, 0.12f), new Vector3(0.3f, 1.5f, 0), shirt);

        return root;
    }

    private static void AddBox(Node3D parent, Vector3 size, Vector3 offset, Color color)
    {
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = offset,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f },
        });
    }
}
