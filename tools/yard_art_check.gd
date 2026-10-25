extends SceneTree
const IDS=["abandoned_a","abandoned_b","shed_a","shed_b","rusty_barrel","tyre_stack","broken_bench","wooden_pallet","scrap_heap","warning_sign"]
var failures:=0
func check(ok:bool,label:String):
    print("[yard-art] ","OK " if ok else "FAIL ",label)
    if not ok: failures+=1
func _initialize():
    for id in IDS:
        var model=load("res://assets/models/props/"+id+".glb").instantiate()
        var visuals=model.find_children("*","MeshInstance3D",true,false)
        check(visuals.size()==1,id+" single mesh")
        var visual:MeshInstance3D=visuals[0]
        var bounds=visual.transform*visual.get_aabb()
        check(abs(bounds.position.y)<.005,id+" ground origin")
        check(visual.mesh.get_surface_count()==1,id+" one surface")
        check(visual.get_active_material(0).vertex_color_use_as_albedo,id+" palette")
        check(visual.mesh.get_faces().size()/3<=1500,id+" triangle budget")
        var building=id.begins_with("abandoned") or id.begins_with("shed")
        var shapes=model.find_children("*","CollisionShape3D",true,false)
        check(not shapes.is_empty() if building else shapes.is_empty(),id+" collision contract")
        if building:
            var limit=Vector2(5,4) if id.begins_with("abandoned") else Vector2(2.6,2)
            check(bounds.position.x>=-limit.x-.001 and bounds.end.x<=limit.x+.001 and bounds.position.z>=-limit.y-.001 and bounds.end.z<=limit.y+.001,id+" inside centered footprint")
        print("[yard-art] ",id," bounds=",bounds)
        model.free()
    print("[yard-art] failures=",failures)
    quit(0 if failures==0 else 1)
