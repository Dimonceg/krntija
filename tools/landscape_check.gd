extends SceneTree
var failures := 0
func check(ok: bool, label: String):
    print("[landscape-check] ","OK " if ok else "FAIL ",label)
    if not ok: failures += 1
func _initialize(): call_deferred("run")
func run():
    var world = load("res://scenes/Main.tscn").instantiate()
    root.add_child(world); paused = false
    await physics_frame
    await physics_frame
    world.set_physics_process(false)
    var ground = world.get_node("LandscapeGround")
    var arrays = ground.mesh.surface_get_arrays(0)
    var visual_faces = arrays[Mesh.ARRAY_VERTEX]
    var faces = world.get_node("LandscapeGroundCollision").get_child(0).shape.get_faces()
    check(visual_faces == faces, "terrain visual and physical triangles coincide")
    var upward = true
    for n in arrays[Mesh.ARRAY_NORMAL]: upward = upward and n.y > 0
    check(upward, "terrain normals face up")
    var road = world.get_node("RoadSurface").get_child(0)
    var material = road.material_override
    check(material is StandardMaterial3D, "road keeps the weather material contract")
    var wet = "--shot-rain" in OS.get_cmdline_user_args()
    check(is_equal_approx(material.roughness, .55 if wet else .95), "weather still updates road roughness")
    check(material.albedo_texture.get_width() == 512 and material.albedo_texture.get_image().has_mipmaps(), "asphalt has mipmapped aggregate texture")
    check(road.mesh.surface_get_arrays(0)[Mesh.ARRAY_TEX_UV].size() > 0, "road UVs follow the ribbon")
    var grass_count = 0
    var grass_batches = 0
    for child in world.get_children():
        if child.name.begins_with("RoadGrass_"):
            grass_batches += 1; grass_count += child.multimesh.instance_count
            check(child.visibility_range_end <= 100, str(child.name) + " distance culling")
    check(grass_count > 1000 and grass_batches < 40, "grass uses spatial batches instead of individual nodes")
    for name in ["MountainRangeWest","MountainRangeEast"]:
        var range_mesh = world.get_node(name)
        check(range_mesh.mesh.get_aabb().size.y > 100, name + " has a mountain silhouette")
        check(range_mesh.get_child_count() == 0, name + " adds no distant physics obstacles")
    print("[landscape-check] grass=",grass_count," batches=",grass_batches," failures=",failures)
    quit(0 if failures == 0 else 1)
