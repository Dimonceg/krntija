using Godot;

namespace Krntija.Game;

/// <summary>
/// Камера, которая висит за целью и мягко догоняет её.
/// Мышью можно осмотреться: камера облетает цель и сама возвращается назад.
/// </summary>
public partial class ChaseCamera : Camera3D
{
    private const float Sensitivity = 0.0025f;
    private const float RecenterDelay = 1.6f;

    private float _yawOffset;
    private float _pitch;
    private float _idle;

    public Node3D? Target { get; set; }

    public float Distance { get; set; } = 7.5f;
    public float Height { get; set; } = 3.2f;
    public float LookHeight { get; set; } = 0.9f;

    /// <summary>Пешком обзор крутит сам игрок, камера лишь следует за ним.</summary>
    public bool OrbitWithMouse { get; set; } = true;

    /// <summary>Пешком мышь двигает только наклон, поворот берётся у цели.</summary>
    public bool PitchWithMouse { get; set; }

    /// <summary>Поворот камеры берётся у цели (пешеход), мышью — только наклон.</summary>
    public bool UseTargetYaw { get; set; }

    /// <summary>Вид от первого лица: камера сидит в голове цели.</summary>
    public bool FirstPerson { get; set; }

    /// <summary>Смещение «головы» в локальных координатах цели.</summary>
    public Vector3 FirstPersonOffset { get; set; } = new(0f, 1.45f, 0f);

    /// <summary>Камера сама возвращается за спину, если мышью не трогали.</summary>
    public bool AutoRecenter { get; set; } = true;

    public void ResetOrbit()
    {
        _yawOffset = 0;
        _pitch = 0;
        _idle = RecenterDelay;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        bool pitchOnly = PitchWithMouse && !OrbitWithMouse;
        if ((!OrbitWithMouse && !pitchOnly) || Input.MouseMode != Input.MouseModeEnum.Captured) return;
        if (@event is not InputEventMouseMotion motion) return;

        if (OrbitWithMouse)
        {
            _yawOffset -= motion.Relative.X * Sensitivity * GameSettings.MouseSensitivity;
            _yawOffset = Mathf.Wrap(_yawOffset, -Mathf.Pi, Mathf.Pi);
        }

        // Мышь вверх — взгляд вверх. Знак один для всех режимов.
        float pitch = _pitch - motion.Relative.Y * Sensitivity * 0.8f * GameSettings.MouseSensitivity;
        _pitch = FirstPerson ? Mathf.Clamp(pitch, -1.1f, 1.1f) : Mathf.Clamp(pitch, -0.55f, 0.5f);
        _idle = 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Target is null) return;

        float dt = (float)delta;
        _idle += dt;

        if (FirstPerson)
        {
            Vector3 head = Target.GlobalPosition + Target.GlobalBasis * FirstPersonOffset;
            GlobalPosition = GlobalPosition.Lerp(head, Mathf.Clamp(dt * 22f, 0f, 1f));

            // Смотрим вперёд по цели, плюс поворот мышью.
            Vector3 look = -Target.GlobalBasis.Z.Rotated(Vector3.Up, _yawOffset);
            look = look.Rotated(Target.GlobalBasis.X.Normalized(), _pitch);
            LookAt(GlobalPosition + look, Vector3.Up);
            return;
        }

        // Если мышью давно не трогали — плавно возвращаем камеру за спину.
        if (AutoRecenter && _idle > RecenterDelay)
        {
            _yawOffset = Mathf.Lerp(_yawOffset, 0, Mathf.Clamp(dt * 2f, 0f, 1f));
            _pitch = Mathf.Lerp(_pitch, 0, Mathf.Clamp(dt * 2f, 0f, 1f));
        }

        // За спиной цель или пешеход сам крутит обзор — решает UseTargetYaw.
        float yaw = UseTargetYaw ? 0f : _yawOffset;
        Vector3 back = Target.GlobalBasis.Z.Rotated(Vector3.Up, yaw);

        // Взгляд вверх опускает камеру и поднимает точку фокуса, вниз — наоборот.
        float height = Mathf.Max(0.65f, Height - _pitch * 6f);
        float focusLift = _pitch * 6f;

        Vector3 focus = Target.GlobalPosition + Vector3.Up * (LookHeight + focusLift);
        focus.Y = Mathf.Max(focus.Y, Target.GlobalPosition.Y + 0.25f);

        Vector3 desired = Target.GlobalPosition + back * Distance + Vector3.Up * height;

        float t = Mathf.Clamp(dt * 6f, 0f, 1f);
        GlobalPosition = GlobalPosition.Lerp(desired, t);
        LookAt(focus, Vector3.Up);
    }
}
