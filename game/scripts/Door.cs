using Godot;

namespace Krntija.Game;

/// <summary>
/// Дверь на петлях: панель с коллизией внутри узла-петли, открывается по E.
/// Коллизия — дочерняя у петли, поэтому поворачивается вместе с панелью.
/// </summary>
public partial class Door : Node3D
{
    private Node3D _hinge = null!;
    private CollisionShape3D _shape = null!;
    private float _target;

    public bool IsOpen { get; private set; }
    public float Width { get; private set; } = 1.0f;
    public float Height { get; private set; } = 2.05f;
    public float OpenAngle { get; set; } = 1.75f;

    /// <summary>Широкая подъёмная дверь гаража: крутится вокруг верхней кромки.</summary>
    public bool IsGarage { get; private set; }

    /// <summary>Середина закрытого полотна в сцене: сюда смотрит игрок, а не на петлю у порога.</summary>
    public Vector3 Center => ToGlobal(new Vector3(Width / 2, IsGarage ? -Height / 2 : Height / 2, 0));

    public static Door Create(string name, Vector3 position, float yaw, float width, float height,
        Color color, bool garage = false)
    {
        var door = new Door
        {
            Name = name,
            Position = position,
            Rotation = new Vector3(0, yaw, 0),
            Width = width,
            Height = height,
            IsGarage = garage,
        };

        door.Build(color);
        return door;
    }

    private void Build(Color color)
    {
        _hinge = new Node3D { Name = "Hinge" };
        AddChild(_hinge);

        var material = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.7f };

        // Панель висит на петле сбоку (обычная дверь) или сверху (гараж).
        var panel = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(Width, Height, 0.08f) },
            Position = new Vector3(Width / 2, IsGarage ? -Height / 2 : Height / 2, 0),
            MaterialOverride = material,
        };
        _hinge.AddChild(panel);

        var handle = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.06f, 0.06f, 0.22f) },
            Position = new Vector3(IsGarage ? 0 : Width - 0.12f, IsGarage ? -Height / 2 : 1.0f, 0.08f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.75f, 0.7f, 0.5f), Metallic = 0.7f, Roughness = 0.3f },
        };
        _hinge.AddChild(handle);

        // Коллизия едет вместе с петлёй, но у открытой двери отключается:
        // иначе полотно, ушедшее в сторону, всё равно считается стеной.
        var body = new StaticBody3D { Name = "DoorBody" };
        _shape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(Width, Height, 0.1f) },
            Position = new Vector3(Width / 2, IsGarage ? -Height / 2 : Height / 2, 0),
        };
        body.AddChild(_shape);
        _hinge.AddChild(body);
    }

    public void Toggle() => IsOpen = !IsOpen;

    public override void _Process(double delta)
    {
        if (_hinge is null) return;

        _target = IsOpen ? (IsGarage ? -OpenAngle : -OpenAngle) : 0f;
        float current = IsGarage ? _hinge.Rotation.X : _hinge.Rotation.Y;
        float next = Mathf.Lerp(current, _target, Mathf.Clamp((float)delta * 7f, 0f, 1f));

        if (IsGarage) _hinge.Rotation = new Vector3(next, 0, 0);
        else _hinge.Rotation = new Vector3(0, next, 0);

        // Открытая дверь не должна быть стеной.
        if (_shape is not null && _shape.Disabled != IsOpen) _shape.Disabled = IsOpen;
    }
}
