extends SceneTree
var world:Node
var car:RigidBody3D
var ticks:=0
var failures:=0
func check(ok:bool,label:String):
    print("[golf-game] ","OK " if ok else "FAIL ",label)
    if not ok:failures+=1
func _initialize():call_deferred("setup")
func setup():
    world=load("res://scenes/Main.tscn").instantiate();root.add_child(world)
    world.set_physics_process(false);paused=false
    car=get_first_node_in_group("car")
func _physics_process(_delta):
    if car==null:return false
    ticks+=1
    if ticks==120:
        check(car.find_child("golf_2",true,false)!=null,"authored Golf body loaded")
        for i in range(4):
            var wheel=car.get_node("Wheel"+str(i))
            var imported=wheel.get_node("RollingMesh").get_child(0)
            check(imported.scene_file_path=="res://assets/models/cars/golf_2_wheel.glb","wheel "+str(i)+" uses authored mesh")
            check(abs(abs(wheel.position.x)-.71)<.025 and abs(abs(wheel.position.z)-1.2375)<.025,"wheel "+str(i)+" matches arch coordinates")
        check(car.get("GroundedWheels")==4,"four wheels grounded at rest")
        check(car.linear_velocity.length()<.15,"stable on handbrake")
        Input.action_press("steer_left")
    if ticks==130:
        check(abs(car.get_node("Wheel0").rotation.y)>.4,"front wheels steer")
        check(abs(car.get_node("Wheel2").rotation.y)<.001,"rear axle stays straight")
        Input.action_release("steer_left")
        if "--capture" in OS.get_cmdline_user_args():
            for layer in world.find_children("*","CanvasLayer",true,false):layer.visible=false
            var camera=Camera3D.new();world.add_child(camera);camera.current=true
            camera.look_at_from_position(car.to_global(Vector3(-3.4,1.4,-4.4)),car.to_global(Vector3(0,.1,0)))
    if ticks==145:
        if "--capture" in OS.get_cmdline_user_args():
            await RenderingServer.frame_post_draw
            print("[golf-game-shot] ",root.get_texture().get_image().save_png("res://../docs/screenshots/golf-mk2-game.png"))
        print("[golf-game] failures=",failures)
        world.call("QuitSoon",0 if failures==0 else 1)
    return false
