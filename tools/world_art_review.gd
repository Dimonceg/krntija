extends SceneTree
## Render the real game world without changing Main's camera or screenshot code.
## godot --path game --script ../tools/world_art_review.gd -- --art-view=gasstation

var world: Node3D
var camera: Camera3D
var frames := 0
var view := "gasstation"
var output := "user://world-art.png"

func _initialize():
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--art-view="):
            view = arg.trim_prefix("--art-view=")
        if arg.begins_with("--art-output="):
            output = arg.trim_prefix("--art-output=")
    call_deferred("setup")

func setup():
    world = load("res://scenes/Main.tscn").instantiate()
    root.add_child(world)
    world.set_physics_process(false)
    # Main opens a paused menu; its input/UI are not part of this visual review.
    paused = false
    for child in world.get_children():
        if child is CanvasLayer:
            child.visible = false
        if child is Camera3D:
            child.process_mode = Node.PROCESS_MODE_DISABLED
        if child is RigidBody3D:
            child.freeze = true
    camera = Camera3D.new()
    world.add_child(camera)
    camera.current = true
    camera.fov = 58
    var key := "Landmark_GasStation" if view == "gasstation" else "Landmark_Village"
    var landmark: Node3D = world.get_node_or_null(key)
    if landmark == null:
        push_error("Missing landmark: " + key)
        quit(1)
        return
    var eye := Vector3(20, 9, 22)
    var target := Vector3(0, 2.0, -2.5)
    if view == "village":
        eye = Vector3(21, 10, 23)
        target = Vector3(-4, 2.4, 1)
    camera.look_at_from_position(landmark.to_global(eye), landmark.to_global(target))
    # Move the car to the forecourt, leaving the pump and shop entry unobstructed.
    if view == "gasstation":
        for child in world.get_children():
            if child is RigidBody3D:
                child.global_position = landmark.to_global(Vector3(3.1, 0.59, 1.0))
                child.rotation.y = landmark.rotation.y + 0.25

func _process(_delta):
    if camera == null:
        return false
    frames += 1
    if frames == 90:
        await RenderingServer.frame_post_draw
        print("[world-art-render] draw calls=", Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME),
            " primitives=", Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME))
        var error = root.get_texture().get_image().save_png(output)
        print("[world-art-shot] ", output, " result=", error)
        quit(0 if error == OK else 1)
    return false
