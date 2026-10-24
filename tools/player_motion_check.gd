extends SceneTree
var failures := 0

func check(ok: bool, label: String):
    print("[motion-check] ", "OK " if ok else "FAIL ", label)
    if not ok: failures += 1

func box(parent, pos, size, angle = 0.0):
    var body = StaticBody3D.new()
    var collision = CollisionShape3D.new()
    var shape = BoxShape3D.new(); shape.size = size; collision.shape = shape
    body.add_child(collision); body.position = pos; body.rotation.x = angle
    parent.add_child(body)

func _initialize(): call_deferred("run")

func scenario(label: String, slope: float, step: float, moving: bool, sprint := false):
    var scene = Node3D.new(); root.add_child(scene)
    box(scene, Vector3(0,-.1,0), Vector3(12,.2,60), slope)
    if step > 0: box(scene, Vector3(0,step/2,-5), Vector3(8,step,10))
    var player = load("res://scripts/Player.cs").new()
    scene.add_child(player); player.position = Vector3(0,1,2)
    for i in range(60): await physics_frame
    var start = player.position
    var last_y = player.position.y
    var max_rise = 0.0
    var max_gap = 0.0
    var airborne = 0
    if moving: Input.action_press("accelerate")
    if sprint: Input.action_press("sprint")
    for i in range(120):
        await physics_frame
        max_rise = max(max_rise, player.position.y - last_y)
        last_y = player.position.y
        var expected = -tan(slope) * player.position.z + .1 / cos(slope) - .1
        if step > 0: expected = step
        max_gap = max(max_gap, player.position.y - expected)
        if not player.is_on_floor(): airborne += 1
    Input.action_release("accelerate"); Input.action_release("sprint")
    print("[motion-trace] ", label, " rise=", snapped(max_rise,.001), " gap=", snapped(max_gap,.001),
        " airborne=", airborne, " start=", start, " end=", player.position)
    if step == 0:
        check(max_rise < .05 and max_gap < .06 and airborne < 3, label + " no unsolicited hops")
        if moving: check(start.z - player.position.z > 5, label + " moves normally")
    elif step <= .34:
        check(player.position.z < -1 and abs(player.position.y-step) < .04, label + " climbs and rests on top")
        check(max_gap < .08, label + " no launch above the step")
    else:
        check(player.position.z > 0 and max_rise < .02, label + " cannot climb a wall")
    scene.queue_free(); await process_frame

func run():
    for action in ["accelerate","brake","steer_left","steer_right","sprint"]:
        if not InputMap.has_action(action): InputMap.add_action(action)
    await scenario("standing", 0, 0, false)
    await scenario("flat walk", 0, 0, true)
    await scenario("uphill walk", .12, 0, true)
    await scenario("downhill run", -.12, 0, true, true)
    await scenario("10cm step", 0, .10, true)
    await scenario("25cm step", 0, .25, true)
    await scenario("33cm step", 0, .33, true)
    await scenario("wall", 0, 1.5, true)
    if DisplayServer.get_name() == "headless":
        print("[motion-check] SKIP Space input: run with a window to test captured mouse input")
    else:
        await jump_check()
    await road_check()
    print("[motion-check] failures=", failures)
    quit(0 if failures == 0 else 1)

func jump_check():
    var scene = Node3D.new(); root.add_child(scene)
    box(scene, Vector3(0,-.1,0), Vector3(12,.2,12))
    var player = load("res://scripts/Player.cs").new()
    scene.add_child(player); player.position = Vector3(0,1,0)
    for i in range(60): await physics_frame
    Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
    var press = InputEventKey.new(); press.keycode = KEY_SPACE; press.pressed = true
    Input.parse_input_event(press)
    var launches = 0
    var last_up = false
    var apex = 0.0
    for i in range(150):
        await physics_frame
        var up = player.velocity.y > 1.0
        if up and not last_up: launches += 1
        last_up = up
        apex = max(apex, player.position.y)
        if i % 10 == 0:
            var echo = InputEventKey.new(); echo.keycode = KEY_SPACE; echo.pressed = true; echo.echo = true
            Input.parse_input_event(echo)
    var release = InputEventKey.new(); release.keycode = KEY_SPACE
    Input.parse_input_event(release)
    check(launches == 1 and apex > .35 and player.is_on_floor(),
        "one Space press jumps once; key repeat does not retrigger (launches=" + str(launches) + ")")
    scene.queue_free(); await process_frame

func road_check():
    # Run with --fp-foot --shot-day so Main creates its real player and terrain.
    var world = load("res://scenes/Main.tscn").instantiate()
    root.add_child(world); world.set_physics_process(false); paused = false
    var player: CharacterBody3D
    for child in world.get_children():
        if child is RigidBody3D: child.freeze = true
        if child.get_script() == load("res://scripts/Player.cs"): player = child
    check(player != null, "real world player spawned")
    if player == null: return
    for i in range(90): await physics_frame
    var previous = player.position.y
    var rise = 0.0
    var airborne = 0
    Input.action_press("accelerate")
    for i in range(180):
        await physics_frame
        rise = max(rise, player.position.y - previous)
        previous = player.position.y
        if not player.is_on_floor(): airborne += 1
    Input.action_release("accelerate")
    print("[motion-trace] actual road rise=", rise, " airborne=", airborne)
    check(rise < .06 and airborne < 3, "actual road has no automatic hops")
    world.queue_free(); await process_frame
