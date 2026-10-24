@tool
extends EditorScenePostImport
## This pack uses one material and vertex colours instead of a texture atlas.
## Explicitly enable the vertex palette: the GLB importer may leave it disabled.

func _post_import(scene):
    apply_palette(scene)
    return scene

func apply_palette(node: Node):
    if node is MeshInstance3D:
        for index in node.mesh.get_surface_count():
            var original = node.get_active_material(index) as StandardMaterial3D
            if original != null:
                var material = original.duplicate() as StandardMaterial3D
                material.vertex_color_use_as_albedo = true
                material.vertex_color_is_srgb = false
                material.albedo_color = Color.WHITE
                node.set_surface_override_material(index, material)
    for child in node.get_children():
        apply_palette(child)
