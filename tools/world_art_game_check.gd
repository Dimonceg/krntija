extends SceneTree
## Verify the integrated PropFactory collision layout in the actual game world.
var world: Node3D
var failures := 0

func check(condition: bool, label: String):
    print("[art-game-check] ", "OK " if condition else "FAIL ", label)
    if not condition:
        failures += 1

func _initialize():
    call_deferred("run")

func hits(model: Node3D, center: Vector3, size: Vector3) -> int:
    var shape := BoxShape3D.new()
    shape.size = size
    var query := PhysicsShapeQueryParameters3D.new()
    query.shape = shape
    # Match player/car physics; layer 2 contains mouse-picking targets for loot.
    query.collision_mask = 1
    query.transform = Transform3D(model.global_basis, model.to_global(center))
    return world.get_world_3d().direct_space_state.intersect_shape(query).size()

func run():
    world = load("res://scenes/Main.tscn").instantiate()
    root.add_child(world)
    world.set_physics_process(false)
    paused = false
    for child in world.get_children():
        if child is RigidBody3D:
            child.freeze = true
    await physics_frame
    await physics_frame
    var station = world.get_node_or_null("Landmark_GasStation")
    var village = world.get_node_or_null("Landmark_Village")
    check(station != null and village != null, "landmarks spawned through PropFactory")
    if station != null:
        check(station.get_node_or_null("PropCollision") == null, "no solid box enclosing forecourt")
        for x in [-3.0, 3.0]:
            check(hits(station, Vector3(x, 1.0, 0), Vector3(1.8, 1.5, 5.0)) == 0,
                "car lane accessible at x=" + str(x))
        check(hits(station, Vector3(0, 1, -5.9), Vector3(0.7, 1.7, 0.7)) == 0, "shop approach accessible")
        # Imported concave proxies collide at their surfaces, not inside a closed shell.
        check(hits(station, Vector3(0.425, 1, 0), Vector3(0.3, 0.5, 0.3)) > 0, "pump remains solid")
        check(hits(station, Vector3(0, 1, -7), Vector3(0.3, 0.5, 0.3)) > 0, "shop remains solid")
    if village != null:
        var bed = village.find_child("SleepAnchor", true, false)
        check(bed != null, "motel sleep anchor imported")
        if bed != null:
            var local = village.to_local(bed.global_position)
            check(hits(village, local + Vector3(0, 1, 0), Vector3(0.7, 1.7, 0.7)) == 0, "motel entrance accessible")
        check(village.get_node_or_null("PropCollision") == null, "village courtyard remains open")
    quit(0 if failures == 0 else 1)
