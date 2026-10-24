using Godot;

namespace Krntija.Game;

/// <summary>
/// Дождь: облако капель вокруг камеры. Интенсивность — 0..1, чтобы
/// морось и ливень были одним узлом.
/// </summary>
public partial class RainFx : Node3D
{
    private GpuParticles3D _particles = null!;

    public override void _Ready()
    {
        var process = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, -1, 0),
            Spread = 1.5f,
            InitialVelocityMin = 16f,
            InitialVelocityMax = 24f,
            Gravity = new Vector3(0, -9.8f, 0),
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(32, 1, 32),
        };

        var dropMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.78f, 0.84f, 0.9f, 0.55f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        var drop = new QuadMesh { Size = new Vector2(0.025f, 0.6f) };
        drop.Material = dropMaterial;

        _particles = new GpuParticles3D
        {
            Amount = 1600,
            Lifetime = 1.6,
            ProcessMaterial = process,
            DrawPass1 = drop,
            Emitting = false,
            VisibilityAabb = new Aabb(new Vector3(-40, -30, -40), new Vector3(80, 60, 80)),
        };

        AddChild(_particles);
    }

    public void Apply(float intensity)
    {
        float clamped = Mathf.Clamp(intensity, 0f, 1f);
        _particles.Emitting = clamped > 0.03f;
        _particles.AmountRatio = Mathf.Max(clamped, 0.05f);
        Visible = clamped > 0.03f;
    }
}
