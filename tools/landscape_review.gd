extends SceneTree
var frames := 0
var camera: Camera3D
var output := "user://landscape.png"
var view := "road"

func _initialize():
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--art-output="): output = arg.trim_prefix("--art-output=")
        if arg.begins_with("--art-view="): view = arg.trim_prefix("--art-view=")
    call_deferred("setup")

func setup():
    var world = load("res://scenes/Main.tscn").instantiate()
    root.add_child(world); world.set_physics_process(false); paused = false
    for child in world.get_children():
        if child is CanvasLayer: child.visible = false
        if child is Camera3D: child.process_mode = Node.PROCESS_MODE_DISABLED
        if child is RigidBody3D: child.freeze = true
    var road = world.get_node("RoadSurface").get_child(0)
    var vertices = road.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
    var i = 85 * 6
    var center = (vertices[i] + vertices[i+2]) * .5
    var forward = (vertices[i+1] - vertices[i]).normalized()
    var right = (vertices[i+2] - vertices[i]).normalized()
    camera = Camera3D.new(); camera.fov = 65; camera.far = 3000
    world.add_child(camera); camera.current = true
    var eye = center - forward*12 + right*1.5 + Vector3.UP*2.2
    var target = center + forward*50 + Vector3.UP*4
    if view == "overview":
        eye = center - forward*30 + right*33 + Vector3.UP*17
        target = center + forward*40 + Vector3.UP*7
    camera.look_at_from_position(eye, target)
    for child in world.get_children():
        if child is Camera3D and child != camera: child.global_position = camera.global_position
    # Let Main apply the selected time/weather before freezing its screenshot timer.
    world.set_physics_process(true)
    await physics_frame
    await physics_frame
    world.set_physics_process(false)

func _process(_delta):
    if camera == null: return false
    frames += 1
    if frames == 90:
        await RenderingServer.frame_post_draw
        print("[landscape-render] calls=",Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME),
            " primitives=",Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME))
        var result = root.get_texture().get_image().save_png(output)
        print("[landscape-shot] ",output," result=",result)
        quit(0 if result == OK else 1)
    return false
