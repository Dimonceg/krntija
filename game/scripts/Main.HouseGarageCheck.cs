using System.Threading.Tasks;
using Godot;
using Krntija.Core.Vehicle;

namespace Krntija.Game;

public partial class Main
{
    /// <summary>Drive the real assembled car over the garage apron, not only a bounding box.</summary>
    private async Task<bool> CheckGarageDrive(Node3D house)
    {
        if (house.FindChild("GarageCarAnchor",true,false) is not Node3D anchor) return false;
        _car.Autopilot=false;
        _car.Freeze=true;
        _car.GlobalTransform=new Transform3D(anchor.GlobalBasis,anchor.GlobalPosition+Vector3.Up*.65f);
        _car.LinearVelocity=Vector3.Zero;
        _car.AngularVelocity=Vector3.Zero;
        _car.ForcedInput=new DriveInput(Brake:1);
        _car.Freeze=false;
        for (int frame=0;frame<45;frame++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        if (DisplayServer.GetName() != "headless")
        {
            _camera.ProcessMode=ProcessModeEnum.Disabled;
            _camera.LookAtFromPosition(house.ToGlobal(new Vector3(11.1f,2.1f,-4.6f)),
                anchor.GlobalPosition+Vector3.Up*.7f);
            for (int frame=0;frame<4;frame++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            using var image=GetViewport().GetTexture().GetImage();
            image.SavePng("user://house_garage_ready.png");
        }
        _car.SetHandbrake(false);
        _car.ForcedInput=new DriveInput(Ignition:true,Throttle:.55);
        bool left=false;
        for (int frame=0;frame<360;frame++)
        {
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            if (house.ToLocal(_car.GlobalPosition).Z < -10f) { left=true; break; }
        }
        _car.ForcedInput=new DriveInput(Brake:1);
        _car.SetHandbrake(true);
        GD.Print($"[house-drive] actual car exit: {(left ? "OK" : "FAIL")}, local={house.ToLocal(_car.GlobalPosition)}");
        return left;
    }
}
