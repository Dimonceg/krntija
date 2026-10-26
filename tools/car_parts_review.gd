extends SceneTree
## Review sheet for loose car parts (ADR 0003, B2): each part in three-quarter view.
## godot --path game --rendering-method gl_compatibility --script ../tools/car_parts_review.gd
const PARTS=[["engine_1_6","Двигатель 1.6",1.0],["radiator_std","Радиатор",.85],["battery_std","Аккумулятор",.45]]
var sheet:SubViewport
var frames:=0
func _initialize():call_deferred("setup")
func setup():
    sheet=SubViewport.new();sheet.size=Vector2i(1440,520);sheet.render_target_update_mode=SubViewport.UPDATE_ALWAYS;root.add_child(sheet)
    for i in range(PARTS.size()):
        var panel=SubViewportContainer.new();panel.position=Vector2(i*480,0);sheet.add_child(panel)
        var viewport=SubViewport.new();viewport.size=Vector2i(480,470);viewport.own_world_3d=true;viewport.msaa_3d=Viewport.MSAA_4X;panel.add_child(viewport)
        var world=Node3D.new();viewport.add_child(world)
        var env=WorldEnvironment.new();env.environment=Environment.new();env.environment.background_mode=Environment.BG_COLOR
        env.environment.background_color=Color("434b4d");env.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
        env.environment.ambient_light_color=Color("b7c1ca");env.environment.ambient_light_energy=.7;world.add_child(env)
        var light=DirectionalLight3D.new();light.rotation_degrees=Vector3(-45,-40,0);light.light_energy=1.4;world.add_child(light)
        var part=load("res://assets/models/parts/part_"+PARTS[i][0]+".glb").instantiate();world.add_child(part)
        var size:float=PARTS[i][2]
        var camera=Camera3D.new();world.add_child(camera);camera.projection=Camera3D.PROJECTION_ORTHOGONAL;camera.size=size
        camera.look_at_from_position(Vector3(1.6,1.2,-1.8)*size,Vector3(0,size*.22,0));camera.current=true
        var label=Label.new();label.text=PARTS[i][1];label.position=panel.position+Vector2(0,474);label.size=Vector2(480,38)
        label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;label.add_theme_font_size_override("font_size",23);sheet.add_child(label)
func _process(_delta):
    frames+=1
    if frames==40:
        await RenderingServer.frame_post_draw
        print("[parts-shot] ",sheet.get_texture().get_image().save_png("res://../docs/screenshots/car-parts.png"))
        quit()
    return false
