extends SceneTree
var sheet:SubViewport
var frames:=0
var views=[]
var cycle_sheet:=false
func _initialize():call_deferred("setup")
func setup():
    cycle_sheet="--cycle-sheet" in OS.get_cmdline_user_args()
    sheet=SubViewport.new();sheet.size=Vector2i(1440,960);sheet.render_target_update_mode=SubViewport.UPDATE_ALWAYS;root.add_child(sheet)
    var clips=["idle","walk","run","attack"]
    var cell=Vector2i(360,240) if cycle_sheet else Vector2i(720,480)
    for index in range(16 if cycle_sheet else 4):
        var i=int(index/4) if cycle_sheet else index
        var panel=SubViewportContainer.new();panel.position=Vector2((index%4)*cell.x,int(index/4)*cell.y) if cycle_sheet else Vector2((i%2)*720,int(i/2)*480);sheet.add_child(panel)
        var viewport=SubViewport.new();viewport.size=cell-Vector2i(0,40);viewport.own_world_3d=true;viewport.msaa_3d=Viewport.MSAA_4X;panel.add_child(viewport)
        var world=Node3D.new();viewport.add_child(world)
        var env=WorldEnvironment.new();env.environment=Environment.new();env.environment.background_mode=Environment.BG_COLOR
        env.environment.background_color=Color("424a48");env.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
        env.environment.ambient_light_color=Color("c0c5ce");env.environment.ambient_light_energy=.7;world.add_child(env)
        var light=DirectionalLight3D.new();light.rotation_degrees=Vector3(-45,-35,0);light.light_energy=1.4;light.shadow_enabled=true;world.add_child(light)
        var floor_mesh=MeshInstance3D.new();floor_mesh.mesh=PlaneMesh.new();floor_mesh.mesh.size=Vector2(8,8)
        var material=StandardMaterial3D.new();material.albedo_color=Color("555c50");floor_mesh.material_override=material;world.add_child(floor_mesh)
        var model=load("res://assets/models/props/wolf.glb").instantiate();world.add_child(model)
        var player:AnimationPlayer=model.find_children("*","AnimationPlayer",true,false)[0]
        player.callback_mode_process=AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
        player.play(clips[i]);views.append([player,clips[i],player.get_animation(clips[i]).length*(index%4)/4.0 if cycle_sheet else [.6,.18,.12,.25][i]])
        var camera=Camera3D.new();world.add_child(camera);camera.projection=Camera3D.PROJECTION_ORTHOGONAL;camera.size=1.65
        camera.look_at_from_position(Vector3(3,.95,.2) if cycle_sheet else (Vector3(2.5,1.25,-2.4) if i==0 or i==3 else Vector3(3,.95,.2)),Vector3(0,.49,0));camera.current=true
        var label=Label.new();label.text=clips[i]+[" • 2,5 с"," • 1 с"," • 0,6 с"," • 0,8 с"][i]
        if cycle_sheet:label.text=clips[i]+" • "+str((index%4)*25)+"%"
        label.position=panel.position+Vector2(0,cell.y-38);label.size=Vector2(cell.x,38);label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER
        label.add_theme_font_size_override("font_size",20 if cycle_sheet else 24);sheet.add_child(label)
func _process(_delta):
    frames+=1
    for i in range(views.size()):
        var player:AnimationPlayer=views[i][0]
        player.seek(views[i][2],true)
    if frames==40:
        await RenderingServer.frame_post_draw
        print("[wolf-shot] ",sheet.get_texture().get_image().save_png("res://../docs/screenshots/wolf-cycles.png" if cycle_sheet else "res://../docs/screenshots/wolf-animations.png"))
        quit()
    return false
