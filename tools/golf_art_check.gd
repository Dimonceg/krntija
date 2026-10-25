extends SceneTree
var failures:=0
func check(ok:bool,label:String):
    print("[golf-art] ","OK " if ok else "FAIL ",label)
    if not ok:failures+=1
func _initialize():
    for id in ["golf_2","golf_2_wheel"]:
        var model=load("res://assets/models/cars/"+id+".glb").instantiate()
        var meshes=model.find_children("*","MeshInstance3D",true,false)
        check(meshes.size()==1,id+" single mesh")
        var visual:MeshInstance3D=meshes[0]
        check(visual.transform.is_equal_approx(Transform3D.IDENTITY),id+" baked origin")
        var bounds=visual.get_aabb()
        var tris=visual.mesh.get_faces().size()/3
        check(tris<=5000 if id=="golf_2" else tris<=1800,id+" triangle budget")
        check(visual.mesh.get_surface_count()<=4,id+" material budget")
        for surface in range(visual.mesh.get_surface_count()):
            check(visual.get_active_material(surface).vertex_color_use_as_albedo,id+" palette")
        if id=="golf_2_wheel":
            check(bounds.get_center().length()<.001,id+" axle at zero")
            check(abs(bounds.size.y-.6)<.001 and abs(bounds.size.z-.6)<.001,id+" radius 0.30")
        else:
            check(bounds.size.z>3.9 and bounds.size.z<4.1,id+" correct metre scale")
        print("[golf-art] ",id," triangles=",tris," bounds=",bounds)
        model.free()
    print("[golf-art] failures=",failures)
    quit(0 if failures==0 else 1)
