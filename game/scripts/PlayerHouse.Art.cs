using Godot;

namespace Krntija.Game;

public static partial class PlayerHouse
{
    /// <summary>Actual capsule clearance under the raised roof, checked by --check-house.</summary>
    public static bool CheckAtticClearance(Node3D house)
    {
        bool ok = true;
        using var capsule = new CapsuleShape3D { Radius = .34f, Height = 1.78f };
        foreach (float x in new[] { -4f, 0f, 4f })
        foreach (float z in new[] { -2.8f, 0f, 2.8f })
        {
            using var query = new PhysicsShapeQueryParameters3D
            {
                Shape = capsule,
                Transform = new Transform3D(Basis.Identity, house.ToGlobal(new Vector3(x, 3.72f, z))),
            };
            var hits = house.GetWorld3D().DirectSpaceState.IntersectShape(query, 1);
            bool free = hits.Count == 0;
            ok &= free;
            GD.Print($"[house-headroom] ({x}, {z}): {(free ? "OK" : "FAIL")}");
        }
        return ok;
    }

    private static Node3D AddHouseModel(Node3D parent, string id)
    {
        var model = ModelLibrary.Instantiate($"res://assets/models/house/{id}.glb")
            ?? throw new System.InvalidOperationException($"Missing house asset: {id}");
        model.Name = id;
        parent.AddChild(model);
        return model;
    }

    private static void AddFurniture(Node3D parent, string id, string use, Vector3 position, Vector3 size, bool solid = true)
    {
        var model = AddHouseModel(parent, id);
        model.Name = "Use_" + use;
        model.Position = position;
        model.AddToGroup(StaticBatcher.SkipGroup);
        if (solid) Blocker(model, size, new Vector3(0, size.Y / 2, 0));
    }

    private static void DressDoor(Door door)
    {
        var hinge = door.GetNode<Node3D>("Hinge");
        foreach (var child in hinge.GetChildren())
            if (child is MeshInstance3D mesh) mesh.Visible = false;
        var model = AddHouseModel(hinge, door.IsGarage ? "home_garage_door" : "home_door");
        model.Scale = new Vector3(door.Width, door.Height, 1);
    }

    private static Material HouseMaterial(Color color)
    {
        if (color == Wall || color == WallInner)
            return ResourceLoader.Load<Material>("res://assets/materials/house_plaster.tres");
        if (color == Floor || color == Furniture)
            return ResourceLoader.Load<Material>("res://assets/materials/house_wood.tres");
        return new StandardMaterial3D { AlbedoColor = color, Roughness = .94f };
    }

    private static void WallWithWindows(Node3D parent, float fixedAt, float from, float to, bool alongX,
        float[] windows, float y, float width, float height, Color color)
    {
        void Panel(float a, float b, float bottom, float top)
        {
            if (b - a < .01f) return;
            Slab(parent, alongX ? new Vector3(b-a, top-bottom, Thickness) : new Vector3(Thickness, top-bottom, b-a),
                alongX ? new Vector3((a+b)/2, (bottom+top)/2, fixedAt) : new Vector3(fixedAt, (bottom+top)/2, (a+b)/2), color);
        }
        float cursor = from;
        foreach (float center in windows)
        {
            if (center-width/2 < from || center+width/2 > to) continue;
            Panel(cursor, center-width/2, 0, WallHeight);
            Panel(center-width/2, center+width/2, 0, y-height/2);
            Panel(center-width/2, center+width/2, y+height/2, WallHeight);
            cursor = center+width/2;
        }
        Panel(cursor, to, 0, WallHeight);
    }

    private static void WindowGlass(Node3D parent, Vector3 position, Vector3 size)
    {
        var pane = Slab(parent, size, position, Glass);
        pane.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(.48f,.62f,.58f,.18f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = .23f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        pane.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }
}
