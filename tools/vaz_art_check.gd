extends SceneTree
var failures:=0
func check(ok:bool,label:String):
    print("[vaz-art] ","OK " if ok else "FAIL ",label)
    if not ok:failures+=1
func _initialize():
    for id in ["vaz_2106","vaz_2106_wheel"]:
        var model=load("res://assets/models/cars/"+id+".glb").instantiate()
        var meshes=model.find_children("*","MeshInstance3D",true,false)
        check(meshes.size()==1,id+" single mesh")
        if meshes.size()!=1:
            model.free()
            continue
        var visual:MeshInstance3D=meshes[0]
        check(visual.transform.is_equal_approx(Transform3D.IDENTITY),id+" baked origin")
        var bounds=visual.get_aabb()
        var tris=visual.mesh.get_faces().size()/3
        check(tris<=5000 if id=="vaz_2106" else tris<=1800,id+" triangle budget")
        check(visual.mesh.get_surface_count()<=4,id+" material budget")
        for surface in range(visual.mesh.get_surface_count()):
            check(visual.get_active_material(surface).vertex_color_use_as_albedo,id+" palette")
        if id=="vaz_2106_wheel":
            check(bounds.get_center().length()<.001,id+" axle at zero")
            check(abs(bounds.size.y-.62)<.001 and abs(bounds.size.z-.62)<.001,id+" radius 0.31")
        else:
            check(bounds.size.z>4.1 and bounds.size.z<4.2,id+" correct metre scale")
        print("[vaz-art] ",id," triangles=",tris," bounds=",bounds)
        model.free()
    print("[vaz-art] failures=",failures)
    quit(0 if failures==0 else 1)
