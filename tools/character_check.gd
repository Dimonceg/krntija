extends SceneTree
var failures := 0
var world: Node3D

func check(condition: bool, label: String):
    print("[character-check] ", "OK " if condition else "FAIL ", label)
    if not condition: failures += 1

func meshes(node: Node, result: Array):
    if node is MeshInstance3D: result.append(node)
    for child in node.get_children(): meshes(child, result)

func _initialize(): call_deferred("run")

func run():
    var packed = load("res://assets/models/characters/traveller.glb") as PackedScene
    check(packed != null, "GLB imports")
    if packed == null:
        quit(1)
        return
    var model = packed.instantiate()
    root.add_child(model)
    var parts: Array = []
    meshes(model, parts)
    check(parts.size() == 15, "15 articulated visual parts")
    var bounds: AABB
    for i in range(parts.size()):
        var part = parts[i]
        var box = part.global_transform * part.mesh.get_aabb()
        bounds = box if i == 0 else bounds.merge(box)
        check(part.mesh.get_surface_count() == 1, str(part.name) + " one surface")
        var material = part.get_active_material(0) as StandardMaterial3D
        check(material != null and material.vertex_color_use_as_albedo, str(part.name) + " palette enabled")
    check(abs(bounds.position.y) < .03 and bounds.size.y > 1.75 and bounds.size.y < 1.83,
        "feet on ground, adult height: " + str(bounds))
    model.free()

    world = load("res://scenes/Main.tscn").instantiate()
    root.add_child(world); world.set_physics_process(false); paused = false
    var player: CharacterBody3D
    for child in world.get_children():
        if child is RigidBody3D: child.freeze = true
        if child.get_script() == load("res://scripts/Player.cs"): player = child
    check(player != null, "actual game player spawned")
    var hitchhikers := 0
    for node in world.find_children("*", "Node3D", true, false):
        if node.get_script() == load("res://scripts/TravellerVisual.cs") and node.get("Hitchhiking"):
            hitchhikers += 1
            check(node.find_child("RightUpperArm", true, false).rotation.z > 1.0,
                "roadside traveller raises his arm")
    check(hitchhikers > 0, "road events also use the new human model")
    if player == null:
        quit(1)
        return
    var visual = player.get_node_or_null("Traveller")
    check(visual != null, "player uses Blender model instead of capsule")
    if visual != null:
        player.call("SetModelVisible", false)
        check(not visual.is_visible_in_tree(), "first person hides all body parts")
        player.call("SetModelVisible", true)
        check(visual.is_visible_in_tree(), "third person restores body")
        var thigh = visual.find_child("LeftThigh", true, false)
        var knee = visual.find_child("LeftShin", true, false)
        for i in range(20): visual.call("Animate", .025, Vector3(0,0,-3.4), true)
        check(abs(thigh.rotation.x) > .1 or abs(knee.rotation.x) > .1, "walking moves articulated legs")
        var soles_grounded = true
        for i in range(40):
            visual.call("Animate", .025, Vector3(0,0,-3.4), true)
            var lowest = INF
            for side in ["Left", "Right"]:
                var shoe = visual.find_child(side + "Foot_Mesh", true, false)
                var box = shoe.global_transform * shoe.mesh.get_aabb()
                lowest = min(lowest, box.position.y - player.global_position.y)
            soles_grounded = soles_grounded and abs(lowest) < .01
        check(soles_grounded, "walking keeps a supporting sole at ground level")
        visual.call("Animate", .1, Vector3(0,3,0), false)
        check(knee.rotation.x < -.1, "jump pose bends knees")
    # Let the real CharacterBody settle, then walk on the real terrain.
    for i in range(60): await physics_frame
    var start = player.global_position
    Input.action_press("accelerate")
    for i in range(30): await physics_frame
    Input.action_release("accelerate")
    check(player.global_position.distance_to(start) > .8, "player can still walk with imported model")
    check(player.is_on_floor(), "unchanged capsule remains grounded")
    print("[character-check] failures=", failures)
    quit(0 if failures == 0 else 1)
