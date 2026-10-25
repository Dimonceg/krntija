extends SceneTree
var frames:=0
var sheet:SubViewport
func _initialize():call_deferred("setup")
func setup():
    root.size=Vector2i(1440,900)
    sheet=SubViewport.new();sheet.size=Vector2i(1440,900)
    sheet.render_target_update_mode=SubViewport.UPDATE_ALWAYS;root.add_child(sheet)
    var ids=["resident","dog","wolf","rockslide","roadblock","wooden_crate"]
    var labels=["Житель","Собака","Волк","Каменный завал","Заграждение выезда","Деревянный ящик"]
    for i in range(ids.size()):
        var panel=SubViewportContainer.new();panel.position=Vector2((i%3)*480,(i/3)*450);sheet.add_child(panel)
        var viewport=SubViewport.new();viewport.size=Vector2i(480,410);viewport.own_world_3d=true;panel.add_child(viewport)
        var world=Node3D.new();viewport.add_child(world)
        var env=WorldEnvironment.new();env.environment=Environment.new();env.environment.background_mode=Environment.BG_COLOR
        env.environment.background_color=Color("333b3d");env.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
        env.environment.ambient_light_color=Color("b7c1ca");env.environment.ambient_light_energy=.65;world.add_child(env)
        var light=DirectionalLight3D.new();light.rotation_degrees=Vector3(-45,-40,0);light.light_energy=1.3;world.add_child(light)
        var model=load("res://assets/models/props/"+ids[i]+".glb").instantiate();world.add_child(model)
        var visual=model.find_children("*","MeshInstance3D",true,false)[0]
        var bounds: AABB=visual.transform*visual.get_aabb()
        var size=max(bounds.size.x,max(bounds.size.y,bounds.size.z))
        var target=bounds.get_center()
        var camera=Camera3D.new();world.add_child(camera);camera.projection=Camera3D.PROJECTION_ORTHOGONAL;camera.size=size*1.45
        camera.look_at_from_position(target+Vector3(1.5,.8,-2.5)*size,target);camera.current=true
        var label=Label.new();label.text=labels[i];label.position=panel.position+Vector2(0,411);label.size=Vector2(480,32)
        label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;label.add_theme_font_size_override("font_size",22);sheet.add_child(label)
func _process(_delta):
    frames+=1
    if frames==60:
        await RenderingServer.frame_post_draw
        print("[life-shot] ",sheet.get_texture().get_image().save_png("res://../docs/screenshots/life-props.png"))
        quit()
    return false
