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
    private ImageTexture _puff = null!;
    private GradientTexture1D _fade = null!;
    private CurveTexture _grow = null!;

    public override void _Ready()
    {
        _puff = MakePuffTexture();
        _fade = MakeFadeRamp();
        _grow = MakeGrowCurve();


        _material = new StandardMaterial3D
        {
            AlbedoTexture = _puff,
            // Без этого ColorRamp (цвет по жизни) не виден: материал берёт цвет вершин.
            VertexColorUseAsAlbedo = true,
            AlbedoColor = new Color(0.62f, 0.57f, 0.47f, 1f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        _quad = new QuadMesh { Size = new Vector2(1.1f, 1.1f), Material = _material };

        for (int i = 0; i < _sources.Length; i++)
        {
            var process = new ParticleProcessMaterial
            {
                // TODO: направление и разброс — пыль идёт вверх и назад, шире, чем сейчас.
                Direction = new Vector3(0, 1, 0.5f),
                Spread = 55f,
                InitialVelocityMin = 0.3f,
                InitialVelocityMax = 1.2f,
                Gravity = new Vector3(0, 0.15f, 0),
                ScaleMin = 0.5f,
                ScaleMax = 1.2f,
                ColorRamp = _fade,
                ScaleCurve = _grow,
            };

            var source = new GpuParticles3D
            {
                Name = i == 0 ? "DustRearLeft" : "DustRearRight",
                // TODO: количество и время жизни — облачко должно жить ~1 с.
                Amount = 30,
                Lifetime = 1.3f,
                ProcessMaterial = process,
                DrawPass1 = _quad,
                Emitting = false,
                VisibilityAabb = new Aabb(new Vector3(-4, -1, -4), new Vector3(8, 4, 8)),
            };

            AddChild(source);
            _sources[i] = source;
        }
    }

    /// <summary>
    /// Мягкое круглое пятно без файла текстуры: альфа считается по расстоянию
    /// до центра. GradientTexture2D с Radial в проекте так и не дал мягкого края
    /// (вне диапазона заливки цвет не тот), поэтому картинка строится по пикселям.
    /// </summary>
    private static ImageTexture MakePuffTexture()
    {
        const int size = 64;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var center = new Vector2(size / 2f, size / 2f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x + 0.5f, y + 0.5f).DistanceTo(center) / (size / 2f);
                // Квадрат спада: пятно плотное в центре, к краю растворяется в ноль.
                float alpha = Mathf.Clamp(1.25f - distance, 0f, 1f);
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// Цвет и прозрачность по жизни частицы (ColorRamp). Сейчас постоянные —
    /// пыль не растворяется. Начало должно быть почти прозрачным, середина
    /// плотной, конец снова прозрачным.
    /// </summary>
    private static GradientTexture1D MakeFadeRamp()
    {
        // Точки задаём массивами: SetColor берёт ИНДЕКС точки, а не смещение —
        // из-за этого рампа раньше получалась наоборот (прозрачно в начале).
        var gradient = new Gradient
        {
            Offsets = new[] { 0f, 0.2f, 1f },
            Colors = new[]
            {
                new Color(1f, 1f, 1f, 0f),
                new Color(0.85f, 0.80f, 0.70f, 0.9f),
                new Color(1f, 1f, 1f, 0f),
            },
        };

        return new GradientTexture1D { Gradient = gradient, Width = 64 };
    }

    /// <summary>
    /// Размер по жизни (ScaleCurve). Сейчас постоянный — пятно не растёт.
    /// Сделай рост: маленькое пятно у колеса, крупное — когда улетело.
    /// </summary>
    private static CurveTexture MakeGrowCurve()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 0.35f));
        curve.AddPoint(new Vector2(0.5f, 0.8f));
        curve.AddPoint(new Vector2(1f, 1.0f));

        return new CurveTexture { Curve = curve };
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
