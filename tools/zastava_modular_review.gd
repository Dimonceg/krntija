extends SceneTree
## Actual imported GLBs, socket transforms, closed / open / stripped cabin views.
var sheet: SubViewport
var frames := 0
var fitted := "--fitted" in OS.get_cmdline_user_args()
var detail := "--detail" in OS.get_cmdline_user_args()
const IDS = ["door_fl", "door_fr", "door_rl", "door_rr", "hood", "trunk"]
func _initialize(): call_deferred("setup")
func setup():
    sheet=SubViewport.new(); sheet.size=Vector2i(1800,800)
    sheet.render_target_update_mode=SubViewport.UPDATE_ALWAYS; root.add_child(sheet)
    for i in range(3):
        var container=SubViewportContainer.new(); container.position=Vector2(i*600,0); sheet.add_child(container)
        var view=SubViewport.new(); view.size=Vector2i(600,740); view.own_world_3d=true
        view.msaa_3d=Viewport.MSAA_4X; container.add_child(view)
        var world=Node3D.new(); view.add_child(world)
        var env=WorldEnvironment.new(); env.environment=Environment.new()
        env.environment.background_mode=Environment.BG_COLOR; env.environment.background_color=Color("414b50")
        env.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
        env.environment.ambient_light_color=Color("b7c1ca"); env.environment.ambient_light_energy=.8; world.add_child(env)
        if detail:
            var sky=Sky.new();sky.sky_material=ProceduralSkyMaterial.new()
            env.environment.sky=sky
            env.environment.reflected_light_source=Environment.REFLECTION_SOURCE_SKY
        var light=DirectionalLight3D.new(); light.rotation_degrees=Vector3(-40,-45,0); light.light_energy=1.5; world.add_child(light)
        var car=load("res://assets/models/cars/zastava_101_shell.glb").instantiate(); world.add_child(car)
        for id in IDS:
            if i==2: continue
            var part=load("res://assets/models/cars/zastava_101_"+id+".glb").instantiate()
            car.add_child(part)
            var anchor=car.find_child(("Socket_" if i==0 else "Open_")+id,true,false) as Node3D
            part.global_transform=anchor.global_transform
        for id in ["wheel_fl","wheel_fr","wheel_rl","wheel_rr"]:
            var wheel=load("res://assets/models/cars/zastava_101_wheel.glb").instantiate(); car.add_child(wheel)
            wheel.global_transform=(car.find_child("Socket_"+id,true,false) as Node3D).global_transform
        if fitted:
            for id in ["engine","radiator","battery"]:
                var file={"engine":"engine_1_6","radiator":"radiator_std","battery":"battery_std"}[id]
                var part=load("res://assets/models/parts/part_"+file+".glb").instantiate();car.add_child(part)
                part.global_transform=(car.find_child("Socket_"+id,true,false) as Node3D).global_transform
            if i==0:
                for id in ["tank","toolbox","headlights","bullbar","roofrack"]:
                    car.add_child(load("res://assets/models/cars/zastava_101_upgrade_"+id+".glb").instantiate())
        var camera=Camera3D.new(); world.add_child(camera); camera.projection=Camera3D.PROJECTION_ORTHOGONAL
        camera.size=5.9 if i==1 else 4.6
        camera.look_at_from_position(Vector3(-4,2.7,5 if i==2 else -5),Vector3(0,.1,0)); camera.current=true
        if detail and i==2:
            camera.size=1.7
            camera.look_at_from_position(Vector3(-1.4,.55,.70),Vector3(.0,.12,-.45))
        var label=Label.new(); label.text=["Closed — original silhouette","Open — engine bay empty","Panels removed — cabin"][i]
        if fitted:label.text=["Existing upgrades — unchanged fit","Open — mechanical parts installed","Cabin — panels removed"][i]
        if detail:label.text=["Paint, chrome and clear glazing","Hinges and engine bay","Cabin detail"][i]
        label.position=Vector2(i*600,750); label.size=Vector2(600,45); label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER
        label.add_theme_font_size_override("font_size",21); sheet.add_child(label)
func _process(_delta):
    frames+=1
    if frames==40:
        await RenderingServer.frame_post_draw
        var file="zastava-modular-fitted.png" if fitted else "zastava-modular.png"
        if detail:file="zastava-detail-fitted.png" if fitted else "zastava-detail.png"
        print("[modular-shot] ",sheet.get_texture().get_image().save_png("res://../docs/screenshots/"+file))
        quit()
    return false
