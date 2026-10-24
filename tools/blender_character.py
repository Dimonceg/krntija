"""Original articulated low-poly traveller. Authoring coordinates are Godot metres, facing -Z.
blender --background --python tools/blender_character.py [-- --render]
"""
import bpy
import bmesh
import math
import json
import sys
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'game/assets/models/characters'
SOURCE = ROOT / 'art/blender/characters'
SHOTS = ROOT / 'docs/screenshots/character-art'
for folder in (OUT, SOURCE, SHOTS):
    folder.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version = 0

def xyz(p): return (p[0], -p[2], p[1])

palette = {
    'skin': (.56,.34,.22), 'skinlight': (.67,.43,.29), 'shadow': (.36,.19,.13),
    'hair': (.055,.036,.025), 'brow': (.07,.042,.025), 'eye': (.025,.032,.029),
    'white': (.72,.68,.56), 'jacket': (.12,.20,.19), 'seam': (.19,.28,.25),
    'darkcloth': (.055,.085,.081), 'shirt': (.57,.49,.35),
    'denim': (.095,.14,.20), 'denimlight': (.13,.18,.24),
    'leather': (.10,.063,.039), 'sole': (.035,.032,.028), 'metal': (.38,.36,.29),
}
material = bpy.data.materials.new('Traveller vertex palette')
material.use_nodes = True
bs = material.node_tree.nodes.get('Principled BSDF')
bs.inputs['Roughness'].default_value = .88
vc = material.node_tree.nodes.new('ShaderNodeVertexColor'); vc.layer_name = 'Color'
material.node_tree.links.new(vc.outputs['Color'], bs.inputs['Base Color'])
joints = {}; parts = {}

def joint(name, world, parent=None):
    obj = bpy.data.objects.new(name, None); bpy.context.collection.objects.link(obj)
    obj.location = xyz(world)
    if parent:
        obj.parent = joints[parent]
        obj.location = Vector(xyz(world)) - obj.parent.matrix_world.translation
    bpy.context.view_layer.update()
    joints[name] = obj; parts[name] = []
    return obj

def register(obj, name, color, owner):
    obj.name = name
    mesh = obj.data
    layer = mesh.color_attributes.new(name='Color', type='BYTE_COLOR', domain='CORNER')
    for item in layer.data: item.color = (*palette[color], 1)
    mesh.materials.clear(); mesh.materials.append(material)
    matrix = obj.matrix_world.copy(); obj.parent = joints[owner]; obj.matrix_world = matrix
    parts[owner].append(obj); obj.select_set(False)
    return obj

def mesh(name, points, faces, color, owner):
    data = bpy.data.meshes.new(name); data.from_pydata([xyz(p) for p in points], [], faces)
    bm = bmesh.new(); bm.from_mesh(data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(data); bm.free(); data.update()
    obj = bpy.data.objects.new(name, data); bpy.context.collection.objects.link(obj)
    return register(obj, name, color, owner)

def box(name, p, size, color, owner, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=xyz(p)); obj = bpy.context.object
    obj.dimensions = (size[0],size[2],size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('soft corners', 'BEVEL'); mod.width=bevel; mod.segments=1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return register(obj, name, color, owner)

def ellipsoid(name, p, size, color, owner, segments=12, rings=6):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=xyz(p))
    obj = bpy.context.object; obj.scale = (size[0],size[2],size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return register(obj, name, color, owner)

def loft(name, rings, color, owner, sides=10):
    # ring = centre x, height, centre z, half width, half depth
    points = []
    for x,y,z,rx,rz in rings:
        for i in range(sides):
            a = math.tau*i/sides
            points.append((x+math.cos(a)*rx,y,z+math.sin(a)*rz))
    faces = [tuple(reversed(range(sides)))]
    for j in range(len(rings)-1):
        for i in range(sides):
            k=j*sides+i; n=j*sides+(i+1)%sides
            faces.append((k,n,n+sides,k+sides))
    faces.append(tuple(range((len(rings)-1)*sides,len(rings)*sides)))
    return mesh(name, points, faces, color, owner)

joint('Hips', (0,.96,0))
joint('Torso', (0,1.03,0), 'Hips')
joint('Head', (0,1.51,0), 'Torso')
loft('trouser seat', [(0,.88,0,.155,.105),(0,.97,0,.17,.105),(0,1.03,0,.15,.09)], 'denim','Hips')
loft('work jacket', [(0,.98,0,.17,.105),(0,1.06,0,.175,.12),(0,1.27,0,.20,.135),
    (0,1.40,0,.235,.12),(0,1.46,0,.17,.09)], 'jacket','Torso',12)
loft('jacket hem', [(0,.978,0,.174,.108),(0,1.013,0,.176,.11)], 'darkcloth','Torso',12)
loft('neck', [(0,1.44,0,.061,.057),(0,1.56,0,.058,.055)], 'skin','Head')
mesh('shirt opening', [(-.065,1.455,-.103),(.065,1.455,-.103),(.045,1.26,-.144),(-.045,1.26,-.144)], [(0,1,2,3)], 'shirt','Torso')
for s in (-1,1):
    mesh('folded collar', [(s*.065,1.468,-.083),(s*.15,1.42,-.10),(s*.085,1.335,-.139),(s*.035,1.40,-.126)], [(0,1,2,3)], 'seam','Torso')
    box('chest pocket',(s*.12,1.29,-.127),(.09,.10,.018),'seam','Torso',.008)
    box('pocket flap',(s*.12,1.333,-.139),(.094,.025,.012),'darkcloth','Torso',.004)
    box('pocket stud',(s*.12,1.33,-.147),(.009,.009,.006),'metal','Torso')
    box('lower pocket seam',(s*.12,1.08,-.118),(.065,.012,.012),'seam','Torso')
box('jacket zip',(0,1.135,-.125),(.012,.255,.012),'metal','Torso')
box('zip pull',(0,1.25,-.139),(.016,.029,.008),'metal','Torso')

# A shaped jaw, brow, ears and projecting nose make the head read as a face.
loft('face', [(0,1.545,-.007,.051,.055),(0,1.565,-.008,.073,.073),
    (0,1.62,0,.09,.083),(0,1.69,.005,.092,.087),(0,1.745,.008,.076,.075),
    (0,1.775,.01,.041,.045)], 'skinlight','Head',12)
loft('hair cap', [(0,1.716,.014,.092,.082),(0,1.75,.014,.081,.08),
    (0,1.782,.012,.05,.052),(0,1.79,.012,.016,.018)], 'hair','Head',12)
box('back hair',(0,1.68,.079),(.15,.105,.026),'hair','Head',.012)
for s in (-1,1):
    ellipsoid('ear',(s*.093,1.65,.008),(.021,.036,.021),'skin','Head',8,4)
    ellipsoid('ear fold',(s*.105,1.65,-.002),(.009,.020,.012),'shadow','Head',8,4)
    box('sideburn',(s*.085,1.687,-.002),(.014,.052,.038),'hair','Head',.004)
    ellipsoid('eye socket',(s*.037,1.675,-.073),(.023,.014,.014),'skin','Head',8,4)
    ellipsoid('eye',(s*.037,1.676,-.083),(.014,.005,.007),'white','Head',8,4)
    ellipsoid('iris',(s*.037,1.676,-.089),(.005,.005,.003),'eye','Head',8,4)
    box('eyebrow',(s*.037,1.696,-.087),(.044,.009,.008),'brow','Head',.003)
mesh('nose bridge',[(-.014,1.69,-.081),(.014,1.69,-.081),(.019,1.635,-.091),
    (-.019,1.635,-.091),(0,1.644,-.128)],[(0,1,4),(1,2,4),(2,3,4),(3,0,4),(0,3,2,1)],'skin','Head')
box('upper lip',(0,1.607,-.087),(.046,.007,.008),'shadow','Head',.003)
box('lower lip',(0,1.598,-.086),(.036,.007,.006),'skin','Head',.002)

for s,label in [(-1,'Left'),(1,'Right')]:
    upper=label+'UpperArm'; lower=label+'Forearm'; hand=label+'Hand'
    joint(upper,(s*.225,1.385,0),'Torso')
    joint(lower,(s*.275,1.105,0),upper)
    joint(hand,(s*.29,.855,-.005),lower)
    loft('jacket sleeve',[(s*.235,1.40,0,.075,.085),(s*.253,1.29,0,.076,.077),
        (s*.275,1.10,0,.058,.06)],'jacket',upper)
    loft('forearm sleeve',[(s*.275,1.12,0,.058,.06),(s*.283,.99,0,.06,.057),
        (s*.29,.85,-.005,.044,.044)],'jacket',lower)
    loft('ribbed cuff',[(s*.29,.85,-.005,.047,.046),(s*.289,.886,-.004,.048,.047)],'darkcloth',lower)
    ellipsoid('palm',(s*.29,.805,-.005),(.043,.067,.029),'skinlight',hand,10,5)
    ellipsoid('thumb',(s*.255,.819,-.025),(.018,.038,.019),'skin',hand,8,4)
    for j in range(3):
        box('finger crease',(s*.29+(j-1)*.018,.777,-.031),(.003,.024,.004),'skin',hand)
    thigh=label+'Thigh'; shin=label+'Shin'; foot=label+'Foot'
    joint(thigh,(s*.094,.935,0),'Hips')
    joint(shin,(s*.105,.515,0),thigh)
    joint(foot,(s*.105,.115,0),shin)
    loft('jeans thigh',[(s*.094,.96,0,.084,.095),(s*.10,.79,0,.082,.09),
        (s*.105,.52,0,.066,.068)],'denim',thigh)
    loft('jeans shin',[(s*.105,.53,0,.066,.068),(s*.105,.37,.008,.067,.068),
        (s*.105,.14,.005,.052,.055)],'denim',shin)
    box('knee crease',(s*.105,.54,-.065),(.095,.018,.009),'denimlight',thigh,.004)
    loft('jeans cuff',[(s*.105,.14,.005,.054,.057),(s*.105,.17,.005,.055,.058)],'denimlight',shin)
    box('boot sole',(s*.105,.027,-.043),(.136,.05,.275),'sole',foot,.018)
    box('boot upper',(s*.105,.079,-.042),(.128,.087,.258),'leather',foot,.025)
    loft('boot ankle',[(s*.105,.07,.017,.06,.071),(s*.105,.157,.017,.052,.055)],'leather',foot)
    for j in range(3):
        box('boot lace',(s*.105,.124-j*.003,-.044-j*.019),(.070,.006,.008),'metal',foot)

# Save unjoined components so the face and clothing remain editable in Blender.
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'traveller.blend'))
triangles=0
for name,objects in parts.items():
    if not objects: continue
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join(); obj=bpy.context.object; obj.name=name+'_Mesh'
    obj.data.materials.clear(); obj.data.materials.append(material)
    for poly in obj.data.polygons: poly.material_index=0
    obj.data.calc_loop_triangles(); triangles+=len(obj.data.loop_triangles)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=str(OUT/'traveller.glb'),export_format='GLB',use_selection=True,
    export_yup=True,export_apply=True,export_cameras=False,export_lights=False,export_all_vertex_colors=True)
(SHOTS/'geometry.json').write_text(json.dumps({'triangles':triangles,'articulated_parts':len(parts),
    'height_m':1.79,'materials':1},indent=2)+'\n',encoding='utf8')
print('CHARACTER_EXPORTED',triangles)

if '--render' in sys.argv:
    # Studio presentation of the actual exported geometry.
    scene=bpy.context.scene; scene.render.engine='CYCLES';scene.cycles.samples=32
    scene.cycles.use_denoising=True
    scene.render.resolution_x=1000;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
    scene.world.use_nodes=True; scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.35,.40,.45,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.005))
    floor=bpy.context.object; floor_mat=bpy.data.materials.new('studio floor');floor_mat.diffuse_color=(.16,.19,.19,1)
    floor.data.materials.append(floor_mat)
    for p,power,size in [((-3,4,-3),400,4),((3,3,1),320,3)]:
        data=bpy.data.lights.new('softbox','AREA');data.energy=power;data.shape='DISK';data.size=size
        light=bpy.data.objects.new('softbox',data);bpy.context.collection.objects.link(light);light.location=xyz(p)
        light.rotation_euler=(Vector(xyz((0,1,0)))-light.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new('Review');cam=bpy.data.objects.new('Review',data);bpy.context.collection.objects.link(cam)
    cam.location=xyz((2.3,1.85,-4.5));cam.rotation_euler=(Vector(xyz((0,.94,0)))-cam.location).to_track_quat('-Z','Y').to_euler()
    data.type='ORTHO';data.ortho_scale=2.13;scene.camera=cam
    scene.render.filepath=str(SHOTS/'traveller-studio.png');bpy.ops.render.render(write_still=True)
print('CHARACTER_COMPLETE')
