using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

public static partial class PlayerHouse
{
    // B3: 7 x 9 m assembly bay; the old 3.3 x 4 m room remains a repair workshop.
    private static void BuildGarage(Node3D root, List<(Vector3, Door)> doors)
    {
        AddHouseModel(root, "home_garage");
        var concrete = new Color(.36f, .37f, .34f);
        Slab(root, new Vector3(7,.6f,9), new Vector3(9,-.35f,-1), concrete);
        Slab(root, new Vector3(7,.2f,9), new Vector3(9,.05f,-1), concrete);
        Slab(root, new Vector3(.24f,2.8f,9), new Vector3(12.5f,1.4f,-1), Wall);
        Slab(root, new Vector3(7,2.8f,.24f), new Vector3(9,1.4f,3.5f), Wall);
        Slab(root, new Vector3(.24f,2.8f,1.5f), new Vector3(5.5f,1.4f,-4.75f), Wall);
        Slab(root, new Vector3(1.6f,2.8f,.24f), new Vector3(6.3f,1.4f,-5.5f), Wall);
        Slab(root, new Vector3(1.6f,2.8f,.24f), new Vector3(11.7f,1.4f,-5.5f), Wall);
        Slab(root, new Vector3(3.8f,.15f,.24f), new Vector3(9,2.725f,-5.5f), Wall);
        var roof = Blocker(root, new Vector3(7.7f,.12f,9.6f), new Vector3(9,3,-1));
        roof.Rotation = new Vector3(0,0,-Mathf.Atan2(.3f,7));
        Blocker(root, new Vector3(1.7f,.9f,.8f), new Vector3(11.3f,.60f,2.7f));
        // Flat apron to the threshold, then a shallow ramp; no wheel-height step.
        Slab(root, new Vector3(5,.16f,1.0f), new Vector3(9,.07f,-5.85f), concrete);
        var ramp = Slab(root, new Vector3(5,.12f,2.4f), new Vector3(9,0,-7.5f), concrete);
        ramp.Rotation = new Vector3(-.07f,0,0);
        AddLight(root,new Vector3(7,2.55f,-1),1.0f);
        AddLight(root,new Vector3(11,2.55f,-1),1.0f);
        var left = AddDoor(root,"GarageBayLeft",new Vector3(7.1f,.15f,-5.5f),0,1.9f,2.5f,GarageDoorColor);
        left.OpenAngle=-1.75f;
        var right = AddDoor(root,"GarageBayRight",new Vector3(10.9f,.15f,-5.5f),Mathf.Pi,1.9f,2.5f,GarageDoorColor);
        right.OpenAngle=1.75f;
        doors.Add((left.GlobalPosition,left));
        doors.Add((right.GlobalPosition,right));
    }

    public static bool CheckGarageClearance(Node3D house)
    {
        bool ok=true;
        foreach (string name in new[] { "GarageCarAnchor", "PartSpot_0", "PartSpot_1", "PartSpot_2", "PartSpot_3",
                     "PartSpot_4", "PartSpot_5", "PartSpot_6", "PartSpot_7" })
        {
            bool found=house.FindChild(name,true,false) is Node3D;
            GD.Print($"[garage-anchor] {name}: {found}");
            ok &= found;
        }
        // Conservative envelope including opened doors, plus standing passages on both sides.
        using var car = new BoxShape3D { Size = new Vector3(3.8f,1.8f,4.4f) };
        using var human = new CapsuleShape3D { Radius=.34f, Height=1.78f };
        bool Free(Shape3D shape, Vector3 point)
        {
            using var query = new PhysicsShapeQueryParameters3D
            {
                Shape=shape, Transform=new Transform3D(house.GlobalBasis,house.ToGlobal(point)),
            };
            var hits=house.GetWorld3D().DirectSpaceState.IntersectShape(query,1);
            if (hits.Count > 0)
                GD.Print($"[garage-clearance] blocked {point}: {PhysicsHit.Collider<Node3D>(hits[0])?.GetPath()}");
            return hits.Count==0;
        }
        ok &= Free(car,new Vector3(9,1.15f,-1));
        foreach (float x in new[] { 6.4f, 11.6f })
        foreach (float z in new[] { -3.5f,-1f,1.5f })
            ok &= Free(human,new Vector3(x,1.08f,z));
        // Assembled car envelope through the opened gate and apron.
        using var closedCar = new BoxShape3D { Size=new Vector3(2.0f,1.8f,4.2f) };
        foreach (float z in new[] { -2f,-3f,-4f,-5f,-6f,-7f,-8f })
            ok &= Free(closedCar,new Vector3(9,1.20f,z));
        GD.Print($"[house-garage] anchors, open doors, passages and drive-out: {(ok ? "OK" : "FAIL")}");
        return ok;
    }
}
