using Godot;

namespace Krntija.Game;

/// <summary>Шлагбаум на блокпосту: закрыт, пока не заплатишь, потом поднимается.</summary>
public partial class CheckpointGate : Node3D
{
    private Node3D _arm = null!;
    private CollisionShape3D _shape = null!;

    public bool IsOpen { get; private set; }

    public static CheckpointGate Create(Vector3 position, float heading)
    {
        var gate = new CheckpointGate { Position = position, Rotation = new Vector3(0, -heading, 0) };
        gate.Build();
        return gate;
    }

    private void Build()
    {
        var postMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.52f, 0.5f, 0.46f), Roughness = 0.9f };
        var boomMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.82f, 0.2f, 0.16f), Roughness = 0.6f };

        foreach (float x in new[] { -5.2f, 5.2f })
        {
            AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.3f, 1.3f, 0.3f) },
                Position = new Vector3(x, 0.65f, 0),
                MaterialOverride = postMaterial,
            });
        }

        // Стрела вращается вокруг столба: поднимается, когда проезд оплачен.
        _arm = new Node3D { Position = new Vector3(-4f, 1.15f, 0) };
        AddChild(_arm);

        _arm.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(8, 0.18f, 0.18f) },
            Position = new Vector3(4, 0, 0),
            MaterialOverride = boomMaterial,
        });

        var body = new StaticBody3D();
        _shape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(8, 0.18f, 0.18f) },
            Position = new Vector3(4, 0, 0),
        };
        body.AddChild(_shape);
        _arm.AddChild(body);
    }

    public void Open() => IsOpen = true;

    /// <summary>Закрыть шлагбаум: нужно при старте новой игры в том же мире.</summary>
    public void Close() => IsOpen = false;

    public override void _Process(double delta)
    {
        float target = IsOpen ? Mathf.DegToRad(74) : 0f;
        _arm.Rotation = new Vector3(0, 0, Mathf.Lerp(_arm.Rotation.Z, target, (float)delta * 4f));

        if (!IsOpen) return;
        if (!_shape.Disabled) _shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
    }
}
