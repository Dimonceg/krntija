extends SceneTree
# Pin render resolution: GameSettings.Apply otherwise resizes the CLI window
# to the available desktop area (or the ultrawide monitor in fullscreen).
func _initialize():call_deferred("run")
func run():
    var scene=load("res://scenes/Main.tscn").instantiate()
    root.add_child(scene)
    root.content_scale_mode=Window.CONTENT_SCALE_MODE_VIEWPORT
    root.content_scale_size=Vector2i(1920,1080)
    root.content_scale_aspect=Window.CONTENT_SCALE_ASPECT_KEEP
    root.scaling_3d_scale=1.0
    if "--house-view" in OS.get_cmdline_user_args():
        var house=scene.find_child("PlayerHouse",true,false)
        var camera=Camera3D.new();scene.add_child(camera);camera.current=true;camera.fov=72
        camera.look_at_from_position(house.to_global(Vector3(-10,6.5,-12)),house.to_global(Vector3(0,2.6,0)))
