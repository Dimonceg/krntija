extends SceneTree
var failures := 0
func check(ok: bool, message: String):
    print("[place-art] ", "OK " if ok else "FAIL ", message)
    if not ok: failures += 1
func _initialize():
    var limits = {"well":Vector2(4,4),"shed":Vector2(5.2,4),"barn":Vector2(14,12),"abandoned":Vector2(10,8)}
    var anchors = {"well":["DrinkAnchor"],"gasstation":["PumpAnchor","ShopAnchor"],"village":["SleepAnchor","SellAnchor"]}
    for id in ["well","shed","barn","abandoned","gasstation","village"]:
        var model = load("res://assets/models/props/"+id+".glb").instantiate()
        root.add_child(model)
        var visuals = model.find_children("*","MeshInstance3D",true,false)
        check(visuals.size()==1,id+" one visual mesh")
        var visual: MeshInstance3D = visuals[0]
        var bounds = visual.transform * visual.get_aabb()
        print("[place-art] ",id," bounds=",bounds)
        check(visual.mesh.get_surface_count()==1,id+" one material")
        check(visual.get_active_material(0).vertex_color_use_as_albedo,id+" vertex palette")
        check(model.find_children("*","CollisionShape3D",true,false).size()>0,id+" simple collision proxies")
        if limits.has(id):
            var limit: Vector2=limits[id]
            check(bounds.size.x<=limit.x+.001 and bounds.size.z<=limit.y+.001,id+" within footprint")
            check(abs(bounds.position.x+bounds.size.x*.5)<.1 and abs(bounds.position.z+bounds.size.z*.5)<.1,id+" centered footprint")
        for name in anchors.get(id,[]):
            var node = model.find_child(name,true,false)
            check(node is Node3D and not node is MeshInstance3D,id+" "+name+" empty node")
            if node: print("[place-art] anchor ",name," = ",node.position)
        model.free()
    print("[place-art] failures=",failures)
    quit(0 if failures==0 else 1)
