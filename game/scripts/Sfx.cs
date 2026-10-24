using Godot;

namespace Krntija.Game;

/// <summary>
/// Проигрывание звуков: интерфейс, двери, шаги, канистра, дождь.
/// Один узел на всю игру, чтобы не плодить плееры по сцене.
/// </summary>
public partial class Sfx : Node
{
    public static Sfx? Instance { get; private set; }

    private AudioStreamPlayer _ui = null!;
    private AudioStreamPlayer _rain = null!;
    private AudioStreamPlayer3D _pour = null!;
    private AudioStreamPlayer3D _door = null!;
    private AudioStreamPlayer3D _stepL = null!;
    private AudioStreamPlayer3D _stepR = null!;
    private bool _stepFlip;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        _ui = Create2D(SoundBank.Click(), "Ui");
        _pour = Create3D(SoundBank.Pour(), "Pour");

        _rain = Create2D(SoundBank.RainLoop(), "Rain");
        _rain.VolumeDb = -60f;

        _door = Create3D(SoundBank.Door(), "Door");
        _stepL = Create3D(SoundBank.Step(), "StepL");
        _stepR = Create3D(SoundBank.Step(), "StepR");
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
        base._ExitTree();
    }

    public static void PlayUi() => Instance?._ui.Play();

    public static void PlayDoor(Vector3 position)
    {
        if (Instance is null) return;
        Instance._door.GlobalPosition = position;
        Instance._door.Play();
    }

    public static void PlayStep(Vector3 position)
    {
        if (Instance is null) return;

        var player = Instance._stepFlip ? Instance._stepL : Instance._stepR;
        Instance._stepFlip = !Instance._stepFlip;

        player.GlobalPosition = position;
        player.PitchScale = (float)GD.RandRange(0.9, 1.1);
        player.Play();
    }

    public static void PlayPour(Vector3 position)
    {
        if (Instance is null) return;
        Instance._pour.GlobalPosition = position;
        Instance._pour.Play();
    }

    public static void SetRain(float intensity)
    {
        if (Instance is null) return;

        float clamped = Mathf.Clamp(intensity, 0f, 1f);
        Instance._rain.VolumeDb = clamped < 0.02f ? -60f : Mathf.LinearToDb(0.25f * clamped + 0.001f);
        if (clamped > 0.02f && !Instance._rain.Playing) Instance._rain.Play();
    }

    private AudioStreamPlayer Create2D(AudioStream stream, string name)
    {
        var player = new AudioStreamPlayer { Stream = stream, Name = name };
        AddChild(player);
        return player;
    }

    private AudioStreamPlayer3D Create3D(AudioStream stream, string name)
    {
        var player = new AudioStreamPlayer3D
        {
            Stream = stream,
            Name = name,
            MaxDistance = 40f,
            UnitSize = 4f,
        };
        AddChild(player);
        return player;
    }
}
