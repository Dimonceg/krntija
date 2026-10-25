extends SceneTree
var sheet:SubViewport
var frames:=0
func _initialize():call_deferred("setup")
func setup():
    sheet=SubViewport.new();sheet.size=Vector2i(1440,900);sheet.render_target_update_mode=SubViewport.UPDATE_ALWAYS;root.add_child(sheet)
    var eyes=[Vector3(3,1.6,-4),Vector3(-3,1.5,4),Vector3(4,.6,0),Vector3(0,.45,-5)]
    var labels=["Golf II • передний ракурс","Задний ракурс","Профиль • база 2,475 м","Передняя часть"]
    for i in range(4):
        var panel=SubViewportContainer.new();panel.position=Vector2((i%2)*720,(i/2)*450);sheet.add_child(panel)
        var viewport=SubViewport.new();viewport.size=Vector2i(720,410);viewport.own_world_3d=true;panel.add_child(viewport)
        var world=Node3D.new();viewport.add_child(world)
        var env=WorldEnvironment.new();env.environment=Environment.new();env.environment.background_mode=Environment.BG_COLOR
        env.environment.background_color=Color("434b4d");env.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
        env.environment.ambient_light_color=Color("b7c1ca");env.environment.ambient_light_energy=.65;world.add_child(env)
        var light=DirectionalLight3D.new();light.rotation_degrees=Vector3(-45,-40,0);light.light_energy=1.3;world.add_child(light)
        world.add_child(load("res://assets/models/cars/golf_2.glb").instantiate())
        for x in [-.71,.71]:
            for z in [-1.2375,1.2375]:
                var wheel=load("res://assets/models/cars/golf_2_wheel.glb").instantiate();world.add_child(wheel);wheel.position=Vector3(x,-.295,z)
        var camera=Camera3D.new();world.add_child(camera);camera.projection=Camera3D.PROJECTION_ORTHOGONAL;camera.size=2.8
        camera.look_at_from_position(eyes[i],Vector3(0,.12,0));camera.current=true
        var label=Label.new();label.text=labels[i];label.position=panel.position+Vector2(0,411);label.size=Vector2(720,32)
        label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;label.add_theme_font_size_override("font_size",22);sheet.add_child(label)
func _process(_delta):
    frames+=1
    if frames==60:
        await RenderingServer.frame_post_draw
        print("[golf-shot] ",sheet.get_texture().get_image().save_png("res://../docs/screenshots/golf-mk2.png"))
        quit()
    return false
