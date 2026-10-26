using Godot;

namespace Krntija.Game;

/// <summary>
/// Пыль из-под задних колёс: два источника частиц в точках контакта, плотность
/// от скорости. Чистая графика — правил игры не меняет (A9, ADR 0004 не трогаем).
/// </summary>
public partial class CarDust : Node3D
{
    /// <summary>Машина; ставит Main (меняется при смене машины). null — ничего не делаем.</summary>
    public Car? Target { get; set; }

    private readonly GpuParticles3D[] _sources = new GpuParticles3D[2];
    private StandardMaterial3D _material = null!;
    private QuadMesh _quad = null!;

    public override void _Ready()
    {
        _material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.72f, 0.68f, 0.58f, 0.14f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        _quad = new QuadMesh { Size = new Vector2(0.28f, 0.28f), Material = _material };

        for (int i = 0; i < _sources.Length; i++)
        {
            var process = new ParticleProcessMaterial
            {
                Direction = new Vector3(0, 1, 0.5f),
                Spread = 30f,
                InitialVelocityMin = 0.8f,
                InitialVelocityMax = 2f,
                Gravity = new Vector3(0, 0.25f, 0),
                ScaleMin = 0.5f,
                ScaleMax = 1f,
            };

            var source = new GpuParticles3D
            {
                Name = i == 0 ? "DustRearLeft" : "DustRearRight",
                Amount = 20,
                Lifetime = 0.7,
                ProcessMaterial = process,
                DrawPass1 = _quad,
                Emitting = false,
                VisibilityAabb = new Aabb(new Vector3(-4, -1, -4), new Vector3(8, 4, 8)),
            };

            AddChild(source);
            _sources[i] = source;
        }
    }

    public override void _Process(double delta)
    {
        if (Target is null) return;

        var spec = Target.Session.Vehicle.Spec;
        float halfBase = (float)spec.WheelbaseM / 2f;
        float halfTrack = (float)spec.TrackM / 2f;
        float radius = (float)spec.WheelRadiusM;

        for (int i = 0; i < _sources.Length; i++)
        {
            Vector3 position = Target.ToGlobal(new Vector3((i == 0 ? -1 : 1) * halfTrack, 0, halfBase)) - Vector3.Up * radius;
            _sources[i].Transform = new Transform3D(Basis.Identity, position);
            _sources[i].Emitting = Target.GroundedWheels >= 2 && Target.SpeedMps > 1;
            _sources[i].AmountRatio = Mathf.Clamp((Target.SpeedMps - 1f) / 12f, 0.05f, 1f);
            _sources[i].Visible = _sources[i].Emitting;
        }
    }
}
