extends SceneTree
var frames := 0
func _initialize():
    call_deferred("setup")
func setup():
    var world=Node3D.new();root.add_child(world)
    var env=WorldEnvironment.new();env.environment=Environment.new()
    env.environment.background_mode=Environment.BG_COLOR
    env.environment.background_color=Color("798185")
    env.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
    env.environment.ambient_light_color=Color("b6bbc0")
    env.environment.ambient_light_energy=.65
    world.add_child(env)
    var sun=DirectionalLight3D.new();sun.rotation_degrees=Vector3(-50,-30,0);sun.light_energy=1.4;sun.shadow_enabled=true;world.add_child(sun)
    var floor=MeshInstance3D.new();floor.mesh=BoxMesh.new();floor.mesh.size=Vector3(75,.1,48);floor.position.y=-.10
    var material=StandardMaterial3D.new();material.albedo_color=Color("646650");floor.material_override=material;world.add_child(floor)
    var ids=["well","shed","abandoned","barn"]
    var positions=[Vector3(-15,0,4),Vector3(-7,0,3),Vector3(5,0,2),Vector3(0,0,19)]
    for i in range(ids.size()):
        var model=load("res://assets/models/props/"+ids[i]+".glb").instantiate();world.add_child(model);model.position=positions[i]
    var camera=Camera3D.new();world.add_child(camera);camera.projection=Camera3D.PROJECTION_ORTHOGONAL;camera.size=42
    camera.look_at_from_position(Vector3(29,25,-37),Vector3(-1,1.8,8));camera.current=true
func _process(_delta):
    frames+=1
    if frames==60:
        await RenderingServer.frame_post_draw
        var path="res://../docs/screenshots/chunk-places.png"
        print("[place-shot] ",root.get_texture().get_image().save_png(path))
        quit()
    return false
