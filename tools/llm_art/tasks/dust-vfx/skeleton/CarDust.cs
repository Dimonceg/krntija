using System;
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

    public override void _Ready()
    {
        var material = new StandardMaterial3D
        {
            // TODO: цвет пыли по заданию (например, 0.62, 0.55, 0.42 при альфе 0.35).
            AlbedoColor = new Color(0.62f, 0.55f, 0.42f, 0.35f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        // TODO: размер частицы по заданию (около 0.5 x 0.5 м).
        var quad = new QuadMesh { Size = new Vector2(0.5f, 0.5f), Material = material };

        for (int i = 0; i < _sources.Length; i++)
        {
            var process = new ParticleProcessMaterial
            {
                // TODO: направление, разброс, скорости, гравитация и размер по заданию.
                Direction = new Vector3(0, 1, 0.4f),
                Spread = 25f,
                InitialVelocityMin = 1.5f,
                InitialVelocityMax = 3f,
                Gravity = new Vector3(0, 0.4f, 0),
                ScaleMin = 0.6f,
                ScaleMax = 1.4f,
            };

            var source = new GpuParticles3D
            {
                Name = i == 0 ? "DustRearLeft" : "DustRearRight",
                // TODO: количество частиц и время жизни по заданию (<= 32 и ~0.9 с).
                Amount = 32,
                Lifetime = 0.9,
                ProcessMaterial = process,
                DrawPass1 = quad,
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

        // TODO: каждый кадр поставить источники в точки контакта задних колёс
        // (задняя ось — +Z в осях машины):
        //   spec = Target.Session.Vehicle.Spec
        //   halfBase = (float)spec.WheelbaseM / 2f
        //   halfTrack = (float)spec.TrackM / 2f
        //   radius = (float)spec.WheelRadiusM
        //   side = -halfTrack для левого источника (i == 0), +halfTrack для правого;
        //   позиция = Target.ToGlobal(new Vector3(side, 0, halfBase)) - Vector3.Up * radius
        // TODO: включить пыль, когда колёса на земле (Target.GroundedWheels >= 2)
        // и машина едет (Target.SpeedMps > 1), и выставить AmountRatio по скорости:
        //   Mathf.Clamp((Target.SpeedMps - 1f) / 12f, 0.05f, 1f)
        // TODO: Visible = Emitting, чтобы выключенная пыль ничего не стоила.
    }
}
