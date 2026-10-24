using Godot;

namespace Krntija.Game;

/// <summary>Articulated Blender visual only; Player retains movement and collision ownership.</summary>
public partial class TravellerVisual : Node3D
{
    public const string AssetPath = "res://assets/models/characters/traveller.glb";
    public bool Hitchhiking { get; set; }
    private Node3D _hips = null!, _torso = null!, _head = null!;
    private readonly Node3D[] _arms = new Node3D[2], _elbows = new Node3D[2];
    private readonly Node3D[] _legs = new Node3D[2], _knees = new Node3D[2], _feet = new Node3D[2];
    private Vector3 _hipHome, _torsoHome;
    private float _phase, _time, _blend, _facing;

    public static TravellerVisual? Create(bool hitchhiking = false)
    {
        var model = ModelLibrary.Instantiate(AssetPath);
        if (model is null) return null;
        var visual = new TravellerVisual { Name = "Traveller", Hitchhiking = hitchhiking };
        visual.AddChild(model);
        Node3D? Find(string name) => model.Name == name ? model : model.FindChild(name, true, false) as Node3D;
        string[] required = { "Hips", "Torso", "Head", "LeftUpperArm", "RightUpperArm",
            "LeftForearm", "RightForearm", "LeftThigh", "RightThigh", "LeftShin", "RightShin",
            "LeftFoot", "RightFoot" };
        foreach (string name in required)
        {
            if (Find(name) is not null) continue;
            GD.PushWarning($"Traveller rig is missing {name}; using the procedural fallback.");
            visual.Free();
            return null;
        }
        visual._hips = Find("Hips")!; visual._torso = Find("Torso")!; visual._head = Find("Head")!;
        visual._hipHome = visual._hips.Position; visual._torsoHome = visual._torso.Position;
        for (int i = 0; i < 2; i++)
        {
            string side = i == 0 ? "Left" : "Right";
            visual._arms[i] = Find(side + "UpperArm")!;
            visual._elbows[i] = Find(side + "Forearm")!;
            visual._legs[i] = Find(side + "Thigh")!;
            visual._knees[i] = Find(side + "Shin")!;
            visual._feet[i] = Find(side + "Foot")!;
        }
        visual.Animate(0, Vector3.Zero, true);
        return visual;
    }

    public override void _Process(double delta)
    {
        if (Hitchhiking) Animate((float)delta, Vector3.Zero, true);
    }

    public void Animate(float dt, Vector3 localVelocity, bool grounded)
    {
        _time += dt;
        float speed = new Vector2(localVelocity.X, localVelocity.Z).Length();
        float target = grounded ? Mathf.Clamp(speed / 3.4f, 0, 1.5f) : 0;
        float smoothing = 1 - Mathf.Exp(-12 * dt);
        _blend = Mathf.Lerp(_blend, target, smoothing);
        if (speed > .2f && !Hitchhiking)
            _facing = Mathf.LerpAngle(_facing, Mathf.Atan2(-localVelocity.X, -localVelocity.Z), smoothing);
        Rotation = new Vector3(0, _facing, 0);
        _phase += dt * Mathf.Lerp(6, 11, Mathf.Clamp(speed / 5.8f, 0, 1));
        _hips.Position = _hipHome + Vector3.Up * (Mathf.Abs(Mathf.Sin(_phase)) * .018f * _blend);
        _hips.Rotation = new Vector3(0, Mathf.Sin(_phase) * .035f * _blend, 0);
        _torso.Position = _torsoHome + Vector3.Up * (Mathf.Sin(_time * 1.8f) * .003f);
        _torso.Rotation = new Vector3(-.045f * _blend, -Mathf.Sin(_phase) * .045f * _blend, 0);
        _head.Rotation = new Vector3(.025f * _blend, Mathf.Sin(_time * .7f) * .025f, 0);
        for (int i = 0; i < 2; i++)
        {
            float swing = Mathf.Sin(_phase + (i == 0 ? 0 : Mathf.Pi));
            float side = i == 0 ? -1 : 1;
            _legs[i].Rotation = new Vector3(swing * .55f * _blend, 0, 0);
            _knees[i].Rotation = new Vector3(-Mathf.Max(0, -swing) * .85f * _blend, 0, 0);
            _feet[i].Rotation = new Vector3(Mathf.Max(0, -swing) * .25f * _blend, 0, 0);
            _arms[i].Rotation = new Vector3(-swing * .48f * _blend, 0, side * .045f);
            _elbows[i].Rotation = new Vector3(.12f + Mathf.Max(0, swing) * .45f * _blend, 0, 0);
            if (!grounded)
            {
                _legs[i].Rotation = new Vector3(i == 0 ? .18f : -.12f, 0, 0);
                _knees[i].Rotation = new Vector3(-.28f, 0, 0);
                _arms[i].Rotation = new Vector3(-.2f, 0, side * .18f);
            }
        }
        if (Hitchhiking)
        {
            _arms[1].Rotation = new Vector3(-.12f, 0, 1.45f + Mathf.Sin(_time * 1.8f) * .06f);
            _elbows[1].Rotation = new Vector3(.18f, 0, .25f);
            _head.Rotation = new Vector3(0, -.22f, 0);
        }
    }
}
