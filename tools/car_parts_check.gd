extends SceneTree
## Loose car parts (ADR 0003, B2): budget, attachment origin, one vertex-colour material.
## Size limits = room in the Zastava 101 engine bay (engine: ~0.5 m to the bonnet).
## godot --headless --path game --script ../tools/car_parts_check.gd
const PARTS={"engine_1_6":[1500,Vector3(.60,.50,.70)],"radiator_std":[500,Vector3(.66,.46,.16)],
    "battery_std":[400,Vector3(.30,.28,.20)]}
var failures:=0
func check(ok:bool,label:String):
    print("[car-parts] ","OK " if ok else "FAIL ",label)
    if not ok:failures+=1
func _initialize():
    for id in PARTS:
        var path="res://assets/models/parts/part_"+id+".glb"
        var scene=load(path) as PackedScene
        check(scene!=null,id+" imports")
        if scene==null:continue
        var model=scene.instantiate()
        var meshes=model.find_children("*","MeshInstance3D",true,false)
        check(meshes.size()==1,id+" one mesh")
        if meshes.size()==1:
            var visual:MeshInstance3D=meshes[0]
            check(visual.transform.is_equal_approx(Transform3D.IDENTITY),id+" baked transform")
            var triangles=visual.mesh.get_faces().size()/3
            check(triangles>0 and triangles<=PARTS[id][0],id+" budget: "+str(triangles))
            check(visual.mesh.get_surface_count()==1,id+" one material")
            var mat=visual.get_active_material(0) as StandardMaterial3D
            check(mat!=null and mat.vertex_color_use_as_albedo,id+" vertex palette")
            # Origin = attachment point: the part stands on it and is centred over it.
            var bounds=visual.get_aabb();var size:Vector3=PARTS[id][1]
            check(absf(bounds.position.y)<.02,id+" stands on its origin (bottom "+str(snappedf(bounds.position.y,.001))+")")
            check(absf(bounds.get_center().x)<.08 and absf(bounds.get_center().z)<.08,id+" centred over origin")
            check(bounds.size.x<=size.x and bounds.size.y<=size.y and bounds.size.z<=size.z,id+" fits its socket "+str(bounds.size))
        check(model.find_children("*","CollisionObject3D",true,false).is_empty(),id+" no collision")
        model.free()
    print("[car-parts] failures=",failures)
    quit(0 if failures==0 else 1)
