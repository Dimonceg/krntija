extends SceneTree
## Actual game player, controllable model visibility and deterministic pose previews.
var world: Node3D
var player: CharacterBody3D
var camera: Camera3D
var frames := 0
var output := "user://traveller-game.png"
var pose := "idle"

func _initialize():
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--art-output="): output = arg.trim_prefix("--art-output=")
        if arg.begins_with("--pose="): pose = arg.trim_prefix("--pose=")
    call_deferred("setup")

func setup():
    world = load("res://scenes/Main.tscn").instantiate()
    root.add_child(world)
    world.set_physics_process(false)
    paused = false
    for child in world.get_children():
        if child is CanvasLayer: child.visible = false
        if child is Camera3D: child.process_mode = Node.PROCESS_MODE_DISABLED
        if child is RigidBody3D: child.freeze = true
        if child.get_script() == load("res://scripts/Player.cs"): player = child
    if player == null:
        push_error("Use --fp-foot --shot-day to spawn the game player")
        quit(1)
        return
    player.call("SetModelVisible", true)
    camera = Camera3D.new(); camera.fov = 40
    world.add_child(camera); camera.current = true

func _process(_delta):
    if camera == null: return false
    frames += 1
    if frames == 50:
        player.set_physics_process(false)
        var visual = player.get_node("Traveller")
        if pose == "walk":
            for i in range(14): visual.call("Animate", 0.025, Vector3(0,0,-3.4), true)
        if pose == "hitch":
            visual.set("Hitchhiking", true)
            visual.call("Animate", 0.1, Vector3.ZERO, true)
            visual.set_process(false)
        camera.look_at_from_position(player.to_global(Vector3(1.65,1.65,-3.45)),
            player.to_global(Vector3(0,.94,0)))
    if frames == 90:
        await RenderingServer.frame_post_draw
        var error = root.get_texture().get_image().save_png(output)
        print("[character-shot] ", output, " result=", error)
        quit(0 if error == OK else 1)
    return false
