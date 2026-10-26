extends SceneTree
# Photograph the actual built/batched game house, not an independent mock-up.
func _initialize():
    call_deferred("run")
func run():
    root.size=Vector2i(1280,800)
    root.scaling_3d_scale=1.0
    var scene=load("res://scenes/Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    paused=false
    scene.process_mode=Node.PROCESS_MODE_DISABLED
    for ui in scene.find_children("*","CanvasLayer",true,false):ui.hide()
    for ui in scene.find_children("*","Control",true,false):ui.hide()
    var house=scene.find_child("PlayerHouse",true,false)
    if house==null:
        push_error("PlayerHouse not found");quit(1);return
    var camera=Camera3D.new();scene.add_child(camera);camera.current=true;camera.fov=72
    for env in scene.find_children("*","WorldEnvironment",true,false):
        env.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
        env.environment.ambient_light_color=Color("bec6cd")
        env.environment.ambient_light_energy=.55
    for sun in scene.find_children("*","DirectionalLight3D",true,false):
        sun.rotation_degrees=Vector3(-38,-25,0);sun.light_energy=1.1
    var views=[
        [Vector3(-10,6.5,-12),Vector3(0,2.6,0)],
        [Vector3(1.5,1.7,-2.8),Vector3(-3.5,1,1.4)],
        [Vector3(1.7,4.4,1.4),Vector3(-3.4,3.5,-1)],
        [Vector3(3,1.7,-3.4),Vector3(4.3,1,-.5)]]
    var sheet=Image.create_empty(2560,1600,false,Image.FORMAT_RGB8)
    for i in range(views.size()):
        camera.look_at_from_position(house.to_global(views[i][0]),house.to_global(views[i][1]))
        for frame in range(8):await process_frame
        await RenderingServer.frame_post_draw
        var shot=root.get_texture().get_image();shot.convert(Image.FORMAT_RGB8)
        sheet.blit_rect(shot,Rect2i(0,0,1280,800),Vector2i((i%2)*1280,(i/2)*800))
    var error=sheet.save_png("res://../docs/screenshots/player-house.png")
    print("[house-art] screenshot=",error)
    scene.notification(Node.NOTIFICATION_WM_CLOSE_REQUEST)
    quit(error)
