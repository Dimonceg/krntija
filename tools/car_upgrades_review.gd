extends SceneTree
var sheet:SubViewport
var frames:=0
func _initialize():call_deferred("setup")
func setup():
    sheet=SubViewport.new();sheet.size=Vector2i(1440,960);sheet.render_target_update_mode=SubViewport.UPDATE_ALWAYS;root.add_child(sheet)
    for i in range(4):
        var car="zastava_101" if i<2 else "vaz_2106"
        var rear=i%2==1
        var panel=SubViewportContainer.new();panel.position=Vector2((i%2)*720,int(i/2)*480);sheet.add_child(panel)
        var viewport=SubViewport.new();viewport.size=Vector2i(720,435);viewport.own_world_3d=true;viewport.msaa_3d=Viewport.MSAA_4X;panel.add_child(viewport)
        var world=Node3D.new();viewport.add_child(world)
        var env=WorldEnvironment.new();env.environment=Environment.new();env.environment.background_mode=Environment.BG_COLOR
        env.environment.background_color=Color("434b4d");env.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
        env.environment.ambient_light_color=Color("b7c1ca");env.environment.ambient_light_energy=.7;world.add_child(env)
        var light=DirectionalLight3D.new();light.rotation_degrees=Vector3(-45,-40,0);light.light_energy=1.4;world.add_child(light)
        # Simulate visual body lift only; wheels stay at their usual positions.
        var body_root=Node3D.new();world.add_child(body_root);body_root.position.y=.04
        body_root.add_child(load("res://assets/models/cars/"+car+".glb").instantiate())
        for upgrade in ["tank","toolbox","headlights","bullbar","roofrack"]:
            body_root.add_child(load("res://assets/models/cars/"+car+"_upgrade_"+upgrade+".glb").instantiate())
        var track=.65 if car=="zastava_101" else .6825
        var axle=1.2245 if car=="zastava_101" else 1.212
        for x in [-track,track]:
            for z in [-axle,axle]:
                var wheel=load("res://assets/models/cars/"+car+"_wheel.glb").instantiate();world.add_child(wheel);wheel.position=Vector3(x,-.295,z)
        var camera=Camera3D.new();world.add_child(camera);camera.projection=Camera3D.PROJECTION_ORTHOGONAL;camera.size=3.15
        camera.look_at_from_position(Vector3(-3,2.2,4) if rear else Vector3(3,2.1,-4),Vector3(0,.30,0));camera.current=true
        var label=Label.new();label.text=("Zastava 101" if i<2 else "ВАЗ-2106")+" • "+("сзади" if rear else "спереди")
        label.position=panel.position+Vector2(0,438);label.size=Vector2(720,38);label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER
        label.add_theme_font_size_override("font_size",23);sheet.add_child(label)
func _process(_delta):
    frames+=1
    if frames==40:
        await RenderingServer.frame_post_draw
        print("[upgrades-shot] ",sheet.get_texture().get_image().save_png("res://../docs/screenshots/car-upgrades.png"))
        quit()
    return false
