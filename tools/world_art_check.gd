extends SceneTree
## Verify imported assets, not just that the Blender generator ran successfully.
var failures := 0

func check(condition: bool, label: String):
    print("[art-check] ", "OK " if condition else "FAIL ", label)
    if not condition:
        failures += 1

func collect(node: Node, type: String, result: Array):
    if node.is_class(type):
        result.append(node)
    for child in node.get_children():
        collect(child, type, result)

func _initialize():
    for id in ["gasstation", "village", "farm", "ruins", "checkpoint", "tree_oak", "tree_poplar", "tree_pine", "shrub"]:
        var path = "res://assets/models/props/" + id + ".glb"
        var packed = load(path) as PackedScene
        check(packed != null, id + " imports")
        if packed == null:
            continue
        var model = packed.instantiate()
        var visuals: Array = []
        var shapes: Array = []
        collect(model, "MeshInstance3D", visuals)
        collect(model, "CollisionShape3D", shapes)
        check(visuals.size() == 1, id + " has one batched visual mesh")
        for visual in visuals:
            check(visual.mesh.get_surface_count() == 1, id + " has one material surface")
            check(not visual.name.contains("colonly"), id + " collision proxy is invisible")
            var material = visual.get_active_material(0) as StandardMaterial3D
            var colors = visual.mesh.surface_get_arrays(0)[Mesh.ARRAY_COLOR]
            check(material != null and material.vertex_color_use_as_albedo, id + " uses the vertex palette")
            check(colors != null and colors.size() > 0, id + " contains vertex colours")
        if id.begins_with("tree_") or id == "shrub":
            check(shapes.is_empty(), id + " is ready for MultiMesh (no physics nodes)")
            for visual in visuals:
                check(visual.transform.is_equal_approx(Transform3D.IDENTITY), id + " has baked transforms")
                var bounds = visual.mesh.get_aabb()
                check(bounds.size.y > 0.5 and bounds.size.y < 12, id + " metre scale")
        else:
            check(not shapes.is_empty(), id + " contains imported collision shapes")
        model.free()
    quit(0 if failures == 0 else 1)
