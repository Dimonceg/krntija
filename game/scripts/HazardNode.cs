using Godot;
using Krntija.Core.WorldGen;

namespace Krntija.Game;

/// <summary>
/// Дорожная угроза из плана: яма тормозит машину, корова перекрывает проезд,
/// попутчик даёт деньги. Простая геометрия; числа приходят из плана (ядро).
/// </summary>
public partial class HazardNode : Node3D
{
    public HazardKind Kind { get; set; }
    public double Amount { get; set; }

    private bool _done;
    private float _timer;

    public static HazardNode Create(HazardKind kind, double amount, Vector3 position)
    {
        var node = new HazardNode { Name = $"Hazard_{kind}", Kind = kind, Amount = amount, Position = position };
        node.Build();
        return node;
    }

    private void Build()
    {
        var color = Kind switch
        {
            HazardKind.Pothole => new Color(0.16f, 0.15f, 0.14f),
            HazardKind.Cow => new Color(0.72f, 0.7f, 0.66f),
            _ => new Color(0.35f, 0.3f, 0.42f),
        };

        var mesh = new MeshInstance3D
        {
            Mesh = Kind switch
            {
                HazardKind.Pothole => new CylinderMesh { Height = 0.05f, TopRadius = 1.4f, BottomRadius = 1.4f },
                HazardKind.Cow => new BoxMesh { Size = new Vector3(0.6f, 1.1f, 1.6f) },
                _ => new CapsuleMesh { Radius = 0.25f, Height = 1.1f },
            },
            Position = new Vector3(0, Kind == HazardKind.Pothole ? 0.02f : 0.6f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.95f },
        };
        AddChild(mesh);

        if (Kind != HazardKind.Cow) return;

        // Корова стоит на дороге: пока не объедешь — не проедешь.
        var body = new StaticBody3D { Name = "CowBody" };
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(0.7f, 1.4f, 1.8f) },
            Position = new Vector3(0, 0.7f, 0),
        });
        AddChild(body);
    }

    public override void _Process(double delta)
    {
        if (_done) return;

        _timer -= (float)delta;
        if (_timer > 0f) return;
        _timer = 0.3f;

        if (GetTree().GetFirstNodeInGroup("car") is not Car car) return;
        if (GlobalPosition.DistanceTo(car.GlobalPosition) > 7f) return;

        _done = true;

        switch (Kind)
        {
            case HazardKind.Pothole:
                car.LinearVelocity *= Mathf.Max(0.5f, 1f - (float)Amount * 10f);
                break;
            case HazardKind.Hitchhiker:
                car.Session.Wallet.Earn(Amount);
                break;
        }
    }
}
