extends SceneTree
var failures := 0
const IDS = ["jerry_can","motor_oil","water","bread","medkit","duct_tape","wrench",
    "spark_plug","cassette","cigarettes","spare_tire","map","radio_part","scrap_metal","bottle_cap"]

func check(ok: bool, label: String):
    print("[loot-check] ","OK " if ok else "FAIL ",label)
    if not ok: failures += 1

func collect_meshes(node: Node, result: Array):
    if node is MeshInstance3D: result.append(node)
    for child in node.get_children(): collect_meshes(child,result)

func _initialize(): call_deferred("run")
func run():
    for id in IDS:
        var packed = load("res://assets/models/props/loot_"+id+".glb") as PackedScene
        check(packed != null,id+" model imports")
        if packed == null: continue
        var model = packed.instantiate()
        var meshes: Array = []; collect_meshes(model,meshes)
        check(meshes.size() == 1,id+" one visual mesh")
        for mesh in meshes:
            check(mesh.mesh.get_surface_count() == 1,id+" one material")
            var material = mesh.get_active_material(0) as StandardMaterial3D
            check(material != null and material.vertex_color_use_as_albedo,id+" palette enabled")
            var bounds = mesh.mesh.get_aabb()
            check(bounds.position.y >= -.025 and bounds.size.length() < 1,id+" metre scale and ground origin")
        model.free()
        var icon = load("res://assets/models/props/icons/"+id+".png") as Texture2D
        check(icon != null and icon.get_width() == 256 and icon.get_height() == 256,id+" square inventory icon")
        if icon != null:
            var pixels = icon.get_image()
            check(pixels.get_pixel(0,0).a == 0 and pixels.get_used_rect().size.x > 20,id+" transparent icon contains item")

    var world = load("res://scenes/Main.tscn").instantiate()
    root.add_child(world); world.set_physics_process(false); paused = false
    var authored := 0
    for node in world.find_children("Loot_*","Node3D",true,false):
        if node.get_script() != load("res://scripts/LootItem.cs"): continue
        var id = node.get("ItemId")
        if id not in IDS: continue
        var models = node.find_children("loot_*","Node3D",true,false)
        check(not models.is_empty(),str(id)+" actual pickup uses authored model")
        for child in node.get_children():
            if child is StaticBody3D:
                check(child.collision_layer == 2 and child.collision_mask == 0,str(id)+" retains mouse picking without blocking movement")
        authored += 1
    check(authored > 0,"new models are used by real spawned loot")
    print("[loot-check] authored pickups=",authored," failures=",failures)
    quit(0 if failures == 0 else 1)
