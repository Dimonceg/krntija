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

    public void ResetOrbit()
    {
        _yawOffset = 0;
        _pitch = 0;
        _idle = RecenterDelay;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!OrbitWithMouse || Input.MouseMode != Input.MouseModeEnum.Captured) return;
        if (@event is not InputEventMouseMotion motion) return;

        _yawOffset -= motion.Relative.X * Sensitivity * GameSettings.MouseSensitivity;
        _yawOffset = Mathf.Wrap(_yawOffset, -Mathf.Pi, Mathf.Pi);
        _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * Sensitivity * 0.8f * GameSettings.MouseSensitivity, -0.25f, 0.5f);
        _idle = 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Target is null) return;

        float dt = (float)delta;
        _idle += dt;

        // Если мышью давно не трогали — плавно возвращаем камеру за спину.
        if (_idle > RecenterDelay)
        {
            _yawOffset = Mathf.Lerp(_yawOffset, 0, Mathf.Clamp(dt * 2f, 0f, 1f));
            _pitch = Mathf.Lerp(_pitch, 0, Mathf.Clamp(dt * 2f, 0f, 1f));
        }

        Vector3 back = Target.GlobalBasis.Z.Rotated(Vector3.Up, _yawOffset);
        float height = Height + _pitch * 9f;
        Vector3 desired = Target.GlobalPosition + back * Distance + Vector3.Up * height;

        float t = Mathf.Clamp(dt * 6f, 0f, 1f);
        GlobalPosition = GlobalPosition.Lerp(desired, t);
        LookAt(Target.GlobalPosition + Vector3.Up * LookHeight, Vector3.Up);
    }
}
