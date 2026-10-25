extends SceneTree
const IDS=["resident","dog","wolf","rockslide","roadblock","wooden_crate"]
var failures:=0
func check(ok:bool,label:String):
    print("[life-art] ","OK " if ok else "FAIL ",label)
    if not ok: failures+=1
func _initialize():
    for id in IDS:
        var model=load("res://assets/models/props/"+id+".glb").instantiate()
        var visuals=model.find_children("*","MeshInstance3D",true,false)
        check(visuals.size()==1,id+" single mesh")
        check(model.find_children("*","CollisionShape3D",true,false).is_empty(),id+" visual only")
        var visual:MeshInstance3D=visuals[0]
        var bounds=visual.transform*visual.get_aabb()
        check(abs(bounds.position.y)<.005,id+" feet/base at zero")
        check(visual.mesh.get_surface_count()==1,id+" single surface")
        check(visual.get_active_material(0).vertex_color_use_as_albedo,id+" palette enabled")
        check(visual.mesh.get_faces().size()/3<=3200,id+" triangle budget")
        print("[life-art] ",id," bounds=",bounds)
        model.free()
    print("[life-art] failures=",failures)
    quit(0 if failures==0 else 1)
