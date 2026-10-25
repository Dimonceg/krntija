extends SceneTree
var sheet:SubViewport
var frames:=0
var buildings:=false
func _initialize():
    buildings="--buildings" in OS.get_cmdline_user_args()
    call_deferred("setup")
func setup():
    sheet=SubViewport.new();sheet.size=Vector2i(1440,900);sheet.render_target_update_mode=SubViewport.UPDATE_ALWAYS;root.add_child(sheet)
    var ids=["abandoned_a","abandoned_b","shed_a","shed_b"] if buildings else ["rusty_barrel","tyre_stack","broken_bench","wooden_pallet","scrap_heap","warning_sign"]
    var columns=2 if buildings else 3
    var width=1440/columns
    for i in range(ids.size()):
        var panel=SubViewportContainer.new();panel.position=Vector2((i%columns)*width,(i/columns)*450);sheet.add_child(panel)
        var viewport=SubViewport.new();viewport.size=Vector2i(width,410);viewport.own_world_3d=true;panel.add_child(viewport)
        var world=Node3D.new();viewport.add_child(world)
        var env=WorldEnvironment.new();env.environment=Environment.new();env.environment.background_mode=Environment.BG_COLOR
        env.environment.background_color=Color("333b3d");env.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
        env.environment.ambient_light_color=Color("b7c1ca");env.environment.ambient_light_energy=.65;world.add_child(env)
        var light=DirectionalLight3D.new();light.rotation_degrees=Vector3(-45,-40,0);light.light_energy=1.3;world.add_child(light)
        var model=load("res://assets/models/props/"+ids[i]+".glb").instantiate()
        # Studio view has no physics simulation; retain only visible geometry.
        for body in model.find_children("*","CollisionObject3D",true,false): body.free()
        world.add_child(model)
        var visual=model.find_children("*","MeshInstance3D",true,false)[0]
        var bounds:AABB=visual.transform*visual.get_aabb()
        var extent=max(bounds.size.x,max(bounds.size.y,bounds.size.z))
        var target=bounds.get_center()
        var camera=Camera3D.new();world.add_child(camera);camera.projection=Camera3D.PROJECTION_ORTHOGONAL;camera.size=extent*(1.05 if buildings else 1.45)
        camera.look_at_from_position(target+Vector3(1.5,1.2,-2.5)*extent,target);camera.current=true
        var label=Label.new();label.text=ids[i]+".glb";label.position=panel.position+Vector2(0,411);label.size=Vector2(width,32)
        label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;label.add_theme_font_size_override("font_size",22);sheet.add_child(label)
func _process(_delta):
    frames+=1
    if frames==60:
        await RenderingServer.frame_post_draw
        var filename="yard-buildings.png" if buildings else "yard-props.png"
        print("[yard-shot] ",sheet.get_texture().get_image().save_png("res://../docs/screenshots/"+filename))
        quit()
    return false
