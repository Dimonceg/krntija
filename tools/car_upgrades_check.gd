extends SceneTree
const CARS=["zastava_101","vaz_2106"]
const UPGRADES=["tank","toolbox","headlights","bullbar","roofrack"]
var failures:=0
func check(ok:bool,label:String):
    print("[car-upgrades] ","OK " if ok else "FAIL ",label)
    if not ok:failures+=1
func _initialize():
    for car in CARS:
        var count:=0
        for upgrade in UPGRADES:
            var id=car+"_upgrade_"+upgrade
            var scene=load("res://assets/models/cars/"+id+".glb") as PackedScene
            check(scene!=null,id+" imports")
            if scene==null:continue
            var model=scene.instantiate()
            check(model.transform.is_equal_approx(Transform3D.IDENTITY),id+" chassis origin")
            var meshes=model.find_children("*","MeshInstance3D",true,false)
            check(meshes.size()==1,id+" one mesh")
            if meshes.size()==1:
                var visual:MeshInstance3D=meshes[0]
                check(visual.transform.is_equal_approx(Transform3D.IDENTITY),id+" baked transform")
                var triangles=visual.mesh.get_faces().size()/3;count+=triangles
                check(triangles>0 and triangles<=600,id+" budget: "+str(triangles))
                check(visual.mesh.get_surface_count()==1,id+" one material")
                var mat=visual.get_active_material(0) as StandardMaterial3D
                check(mat!=null and mat.vertex_color_use_as_albedo,id+" standard vertex palette")
                var bounds=visual.get_aabb()
                if upgrade in ["tank","toolbox","roofrack"]:
                    check(bounds.position.y>.75 and bounds.end.y<1.40,id+" roof mounting height")
                    check(bounds.position.z>-.35 and bounds.end.z<.70,id+" clear of front/rear glass")
                else:
                    check(bounds.end.z<-1.8 and bounds.position.z>-2.4,id+" front mounting")
                print("[car-upgrades] ",id," bounds=",bounds)
            check(model.find_children("*","CollisionObject3D",true,false).is_empty(),id+" no collision")
            check(model.find_children("*","AnimationPlayer",true,false).is_empty(),id+" no animation")
            check(model.find_children("*","Light3D",true,false).is_empty(),id+" no lights")
            model.free()
        var data=JSON.parse_string(FileAccess.get_file_as_string("res://assets/models/cars/"+car+"_upgrade_springs.json"))
        check(data is Dictionary and data.size()==1 and is_equal_approx(float(data.get("lift",0)),.04),car+" lift 0.04 m")
        print("[car-upgrades] ",car," all attachments triangles=",count)
    print("[car-upgrades] failures=",failures)
    quit(0 if failures==0 else 1)
