@tool
extends EditorScenePostImport
## Art import only. Keep glTF PBR textures and glass alpha; opt into COLOR_0.
func _post_import(scene):
    finish_materials(scene)
    return scene
func finish_materials(node: Node):
    if node is MeshInstance3D:
        for index in node.mesh.get_surface_count():
            var mat=node.get_active_material(index) as StandardMaterial3D
            if mat==null:continue
            mat=mat.duplicate()
            mat.vertex_color_use_as_albedo=true
            mat.vertex_color_is_srgb=false
            if "Glass" in mat.resource_name:
                mat.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA
                mat.cull_mode=BaseMaterial3D.CULL_DISABLED
            node.set_surface_override_material(index,mat)
    for child in node.get_children():finish_materials(child)
