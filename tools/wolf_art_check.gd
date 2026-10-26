extends SceneTree
const CLIPS={"idle":2.5,"walk":1.0,"run":0.6,"attack":0.8}
var failures:=0
func check(ok:bool,label:String):
    print("[wolf-art] ","OK " if ok else "FAIL ",label)
    if not ok:failures+=1
func _initialize():call_deferred("verify")
func verify():
    var model=load("res://assets/models/props/wolf.glb").instantiate()
    root.add_child(model)
    var meshes=model.find_children("*","MeshInstance3D",true,false)
    var skeletons=model.find_children("*","Skeleton3D",true,false)
    var players=model.find_children("*","AnimationPlayer",true,false)
    check(meshes.size()==1 and skeletons.size()==1 and players.size()==1,"one skinned mesh, one skeleton, imported clips")
    if meshes.size()!=1 or skeletons.size()!=1 or players.size()!=1:
        quit(1);return
    var visual:MeshInstance3D=meshes[0]
    var skeleton:Skeleton3D=skeletons[0]
    var player:AnimationPlayer=players[0]
    player.callback_mode_process=AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
    var bounds=visual.get_aabb()
    var triangles=visual.mesh.get_faces().size()/3
    check(triangles<=3000,"triangle budget: "+str(triangles))
    check(visual.mesh.get_surface_count()==1,"one material")
    check(visual.get_active_material(0) is StandardMaterial3D,"standard material, no shader script")
    check(visual.skin!=null,"skin exported")
    check(abs(bounds.position.y)<.005,"paws at ground in bind pose")
    check(bounds.size.y>.95 and bounds.size.y<1.10,"ear tip height; withers below ears")
    check(bounds.size.z>1.8 and bounds.size.z<2.2,"full length including muzzle and tail")
    check(abs(bounds.get_center().x)<.005 and abs(bounds.get_center().z)<.08,"centred on ground")
    check(model.find_children("*","Camera3D",true,false).is_empty() and model.find_children("*","Light3D",true,false).is_empty(),"no cameras or lights")
    check(model.find_children("*","CollisionObject3D",true,false).is_empty(),"no gameplay bodies")
    var names=Array(player.get_animation_list());names.erase("RESET")
    check(names.size()==4,"exactly four authored clips: "+str(names))
    var root_index=skeleton.find_bone("root")
    check(root_index>=0,"stable root bone")
    print("[wolf-art] bounds=",bounds," bones=",skeleton.get_bone_count())
    for clip in CLIPS:
        check(player.has_animation(clip),clip+" exact name")
        if not player.has_animation(clip):continue
        var anim:Animation=player.get_animation(clip)
        check(abs(anim.length-CLIPS[clip])<.002,clip+" duration "+str(anim.length))
        check(anim.loop_mode==(Animation.LOOP_NONE if clip=="attack" else Animation.LOOP_LINEAR),clip+" loop setting")
        # Sample the real last key, not a wrapped first frame at t == length.
        var saved_loop=anim.loop_mode
        anim.loop_mode=Animation.LOOP_NONE
        player.play(clip);player.seek(0,true);skeleton.force_update_all_bone_transforms()
        var start=[]
        for b in range(skeleton.get_bone_count()):start.append(skeleton.get_bone_pose(b))
        var moved:=false
        var rooted:=true
        var min_paw:=1.0
        var mesh_floor:=1.0
        for step in range(25):
            player.seek(anim.length*step/24.0,true);skeleton.force_update_all_bone_transforms()
            if root_index>=0:rooted=rooted and skeleton.get_bone_pose(root_index).is_equal_approx(start[root_index])
            for b in range(skeleton.get_bone_count()):
                moved=moved or not skeleton.get_bone_pose(b).is_equal_approx(start[b])
            for paw in ["fore_paw_L","fore_paw_R","hind_paw_L","hind_paw_R"]:
                min_paw=min(min_paw,skeleton.get_bone_global_pose(skeleton.find_bone(paw)).origin.y)
            if "--skin-check" in OS.get_cmdline_user_args():
                await process_frame
                var baked=visual.bake_mesh_from_current_skeleton_pose()
                if baked!=null:mesh_floor=min(mesh_floor,baked.get_aabb().position.y)
                else:check(false,clip+" could not bake posed skin")
        check(moved,clip+" has animated bone poses")
        check(rooted and model.transform.is_equal_approx(Transform3D.IDENTITY),clip+" no root motion")
        check(min_paw>=.035,clip+" paw pivots stay above ground: "+str(min_paw))
        if "--skin-check" in OS.get_cmdline_user_args():
            check(mesh_floor>=-.008 and mesh_floor<.02,clip+" animated skin at ground: "+str(mesh_floor))
        var seam_ok:=true
        for b in range(skeleton.get_bone_count()):
            var a:Transform3D=start[b];var end=skeleton.get_bone_pose(b)
            seam_ok=seam_ok and a.origin.distance_to(end.origin)<.001 and a.basis.is_equal_approx(end.basis)
        check(seam_ok,clip+" returns to opening pose")
        anim.loop_mode=saved_loop
    player.stop();model.queue_free()
    await process_frame
    print("[wolf-art] failures=",failures)
    quit(0 if failures==0 else 1)
