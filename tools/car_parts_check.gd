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
    call_deferred("run_checks")
func run_checks():
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
    check_modular_car()
    print("[car-parts] failures=",failures)
    quit(0 if failures==0 else 1)

func check_modular_car():
    var prefix="res://assets/models/cars/zastava_101_"
    var shell=load(prefix+"shell.glb").instantiate() as Node3D
    root.add_child(shell)
    var ids=["engine","radiator","battery","wheel_fl","wheel_fr","wheel_rl","wheel_rr",
        "door_fl","door_fr","door_rl","door_rr","hood","trunk"]
    var total=0
    for visual in shell.find_children("*","MeshInstance3D",true,false):
        total+=visual.mesh.get_faces().size()/3
    check(total<=3100,"shell triangle budget: "+str(total))
    for id in ids:
        var socket=shell.find_child("Socket_"+id,true,false) as Node3D
        check(socket!=null,id+" socket")
        if socket==null or not (id.begins_with("door") or id in ["hood","trunk"]):continue
        var opened=shell.find_child("Open_"+id,true,false) as Node3D
        check(opened!=null,id+" open transform")
        if opened==null:continue
        check(socket.position.distance_to(opened.position)<.0001,id+" stationary hinge")
        var angle=rad_to_deg(socket.basis.get_rotation_quaternion().angle_to(opened.basis.get_rotation_quaternion()))
        check(angle>=59 and angle<=76,id+" opening angle: "+str(angle))
        var panel=load(prefix+id+".glb").instantiate() as Node3D
        shell.add_child(panel)
        check(panel.transform.is_equal_approx(Transform3D.IDENTITY),id+" exported root at hinge zero")
        panel.transform=socket.transform
        check(panel.global_position.distance_to(socket.global_position)<.0001,id+" mounted origin matches socket")
        var tris=0
        for visual in panel.find_children("*","MeshInstance3D",true,false):
            check(visual.transform.is_equal_approx(Transform3D.IDENTITY),id+" baked mesh transform")
            tris+=visual.mesh.get_faces().size()/3
            # Hinge must touch the panel geometry, not sit at a far-away chassis origin.
            var bounds=visual.get_aabb().grow(.12)
            check(bounds.has_point(Vector3.ZERO),id+" hinge lies on panel edge")
        check(tris>0 and tris<=600,id+" triangle budget: "+str(tris));total+=tris
        panel.free()
    check(total<=5000,"assembled shell and panels budget: "+str(total))
    check(shell.find_children("*","CollisionObject3D",true,false).is_empty(),"shell no collisions")
    shell.free()
