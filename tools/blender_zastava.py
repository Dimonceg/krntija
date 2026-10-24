"""Build the original Krntija Zastava 101 asset, source scene and review renders.

Run: blender --background --python tools/blender_zastava.py
Optional: -- --no-render, or -- --view=front (front/rear/side).
Coordinates in this script are Godot (X, height, rearward Z), converted to
Blender on input. GLB export converts them back; no import rotation is needed.
Reference sources and integration dimensions: docs/ZASTAVA_MODEL.md.
"""
from pathlib import Path
import json
import math
import sys

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "game/assets/models/cars"
SOURCE = ROOT / "art/blender"
RENDERS = ROOT / "docs/screenshots/zastava"
for directory in (ASSETS, SOURCE, RENDERS):
    directory.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for collection in list(bpy.data.collections):
    if collection.name != 'Collection':
        bpy.data.collections.remove(collection)
body = bpy.data.collections.new('BODY - exported to game')
wheel = bpy.data.collections.new('WHEEL - local X axle')
studio = bpy.data.collections.new('STUDIO - preview only')
for collection in (body, wheel, studio):
    bpy.context.scene.collection.children.link(collection)
active_collection = body

def xyz(p):
    return (p[0], -p[2], p[1])

def put(obj, name, mat=None):
    obj.name = name
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    active_collection.objects.link(obj)
    if mat:
        obj.data.materials.append(mat)
    return obj

def material(name, color, metallic=0, roughness=.5):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Metallic'].default_value = metallic
    shader.inputs['Roughness'].default_value = roughness
    return mat

paint = material('01 - aged ivory enamel', (.64, .59, .45), .16, .36)
rubber = material('02 - charcoal rubber and panel gaps', (.018, .022, .023), 0, .78)
chrome = material('03 - brushed aluminium', (.48, .52, .53), .8, .25)
glass = material('04 - blue grey glass', (.045, .085, .095), .32, .19)
lens = material('05 - headlight glass', (.66, .69, .63), .3, .18)
amber = material('06 - amber indicator', (.9, .24, .025), .08, .3)
red = material('07 - red tail lens', (.48, .018, .012), .1, .28)
plate = material('08 - enamel number plate', (.77, .76, .67), .1, .45)

def bevel(obj, amount=.015, segments=1):
    mod = obj.modifiers.new('small stamped metal edge', 'BEVEL')
    mod.width = amount
    mod.segments = segments
    mod.limit_method = 'ANGLE'
    mod = obj.modifiers.new('weighted face normals', 'WEIGHTED_NORMAL')
    mod.keep_sharp = True
    return obj

def mesh(name, points, faces, mat, edge=0):
    data = bpy.data.meshes.new(name)
    data.from_pydata([xyz(p) for p in points], [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    active_collection.objects.link(obj)
    obj.data.materials.append(mat)
    # Recalculate consistently; unlike the old exporter this also handles concavity.
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)
    if edge:
        bevel(obj, edge)
    return obj

def box(name, center, size, mat, edge=.008):
    bpy.ops.mesh.primitive_cube_add(size=1, location=xyz(center))
    obj = put(bpy.context.object, name, mat)
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if edge:
        bevel(obj, edge)
    obj.select_set(False)
    return obj

def line(name, points, mat, radius=.004, closed=False):
    data = bpy.data.curves.new(name, 'CURVE')
    data.dimensions = '3D'
    data.resolution_u = 1
    data.bevel_depth = radius
    data.bevel_resolution = 0
    spline = data.splines.new('POLY')
    spline.points.add(len(points)-1)
    for v, p in zip(spline.points, points):
        v.co = (*xyz(p), 1)
    spline.use_cyclic_u = closed
    obj = bpy.data.objects.new(name, data)
    active_collection.objects.link(obj)
    obj.data.materials.append(mat)
    return obj

def cylinder(name, center, radius, depth, mat, axis='X', vertices=24, edge=0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=xyz(center))
    obj = put(bpy.context.object, name, mat)
    # Default Blender cylinder axis Z = logical height Y.
    if axis == 'X':
        obj.rotation_euler[1] = math.pi / 2
    elif axis == 'Z':
        obj.rotation_euler[0] = math.pi / 2
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for face in obj.data.polygons:
        face.use_smooth = len(face.vertices) == 4
    if edge:
        bevel(obj, edge)
    obj.select_set(False)
    return obj

# Overall length with bumpers 3.80 m, body width 1.58 m; 2.449 m wheelbase.
AXLES = (-1.2245, 1.2245)
WHEEL_Y = -.295
RADIUS = .29

# Lower shell: the shoulder has a distinct crease; bonnet and boot are not boxes.
# Each station is (rearward Z, half width, bonnet height).
stations = [(-1.85,.715,.205),(-1.74,.775,.255),(-1.35,.79,.285),
            (-.88,.79,.325),(-.50,.785,.335),(.45,.78,.335),
            (1.23,.765,.325),(1.68,.744,.295),(1.85,.695,.245)]
vertices = []
for z, w, top in stations:
    vertices.extend([(0,top+.018,z),(w-.055,top,z),(w,top-.045,z),
                     (w,-.22,z),(w-.045,-.415,z),(0,-.43,z),
                     (-w+.045,-.415,z),(-w,-.22,z),(-w,top-.045,z),(-w+.055,top,z)])
faces = []
N = 10
for j in range(len(stations)-1):
    for i in range(N):
        faces.append((j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i))
faces.extend([tuple(reversed(range(N))),tuple((len(stations)-1)*N+i for i in range(N))])
shell = mesh('pressed steel body with open wheel arches', vertices, faces, paint)
for z in AXLES:
    cutter = cylinder('arch cutting tool', (0,WHEEL_Y,z), .344, 2.2, rubber, vertices=32)
    bpy.context.view_layer.objects.active = shell
    cut = shell.modifiers.new('real wheel opening', 'BOOLEAN')
    cut.operation = 'DIFFERENCE'
    cut.solver = 'EXACT'
    cut.object = cutter
    bpy.ops.object.modifier_apply(modifier=cut.name)
    bpy.data.objects.remove(cutter, do_unlink=True)
bevel(shell, .012, 2)

# Thin formed lips follow the holes; dark wheel wells sit behind the metal.
for side in (-1,1):
    for z in AXLES:
        w = .789 if z < 0 else .764
        points = []
        for i in range(21):
            a=math.radians(-19+i*218/20)
            h=WHEEL_Y+.352*math.sin(a)
            inset=.045*max(0,min(1,(-h-.22)/.195))
            points.append((side*(w-inset+.002),h,z+.352*math.cos(a)))
        line('rolled wheel arch lip', points, paint, .012)
        # A narrower inner shell leaves the wheel aperture genuinely open.
        points = [(side*.565,WHEEL_Y+.338*math.sin(a),z+.338*math.cos(a))
                  for a in [math.radians(i*180/16) for i in range(17)]]
        line('inner arch shadow',points,rubber,.028)

# Four-door greenhouse, sloping A pillar and characteristic fastback fifth door.
cab_stations = [(-.91,.741,.332),(-.43,.635,.802),(-.29,.632,.837),
                (.72,.628,.837),(.88,.640,.79),(1.67,.713,.307)]
v=[]
for index,(z,w,h) in enumerate(cab_stations):
    crown=.018 if index in (2,3) else 0
    v.extend([(-.744,.28,z),(-w,h-crown,z),(-w*.83,h,z),(w*.83,h,z),(w,h-crown,z),(.744,.28,z)])
f=[]
for j in range(len(cab_stations)-1):
    for i in range(5):
        f.append((j*6+i,j*6+i+1,(j+1)*6+i+1,(j+1)*6+i))
f += [tuple(reversed(range(6))),tuple((len(cab_stations)-1)*6+i for i in range(6))]
mesh('roof and pillars',v,f,paint,.012)

def side_x(h):
    return .744 - (h-.31)*.222

def side_window(name, coords, side):
    points=[(side*(side_x(h)+.009),h,z) for z,h in coords]
    mesh(name,points,[tuple(range(len(points)))],glass)
    line(name+' rubber gasket',points,rubber,.012,True)
    line(name+' bright surround',[(x+side*.004,h,z) for x,h,z in points],chrome,.003,True)

for side in (-1,1):
    side_window('front door glass', [(-.82,.36),(-.385,.763),(.12,.777),(.12,.36)],side)
    side_window('rear door glass',[(.205,.36),(.205,.777),(.71,.776),(1.05,.57),(1.05,.36)],side)
    # A small rear fixed pane helps the hatchback silhouette read at game distance.
    side_window('rear quarter glass',[(1.115,.365),(1.115,.53),(1.39,.365)],side)
    line('front quarterlight divider',[(side*(side_x(h)+.018),h,z) for z,h in [(-.58,.36),(-.58,.54)]],chrome,.008)
    line('roof rain gutter',[(side*.642,.790,-.40),(side*.638,.815,-.27),
                            (side*.634,.815,.72),(side*.647,.768,.88)],chrome,.005)
    # Door shutlines follow the outer shoulder and the taper of the sill.
    for z in (-.85,.165):
        line('door vertical shut line',[(side*.785,.30,z),(side*.788,-.20,z),(side*.745,-.395,z)],rubber,.0025)
    line('rear door shut line',[(side*.771,.30,1.09),(side*.774,.10,1.11),
                               (side*.77,-.02,.91),(side*.748,-.39,.84)],rubber,.0025)
    line('door lower seam',[(side*.75,-.393,-.85),(side*.75,-.393,.84)],rubber,.0025)
    line('lower sill chrome',[(side*.758,-.35,-.85),(side*.755,-.35,.84)],chrome,.006)
    for z in (.015,.91):
        box('door handle recess',(side*.79,.205,z),(.012,.047,.135),rubber,.009)
        box('chrome door handle',(side*.802,.217,z),(.025,.023,.12),chrome,.006)
    # Period door mirror, with a visible stalk and separate reflective insert.
    line('mirror stem',[(side*.755,.35,-.72),(side*.885,.39,-.69)],chrome,.012)
    box('mirror housing',(side*.906,.43,-.69),(.12,.086,.045),chrome,.018)
    box('mirror glass',(side*.907,.43,-.662),(.098,.064,.008),glass,.009)
    cylinder('front wing repeater',(side*.794,.115,-1.65),.024,.012,amber,vertices=12)

# Windshield and hatch glass are planar, with generous painted borders.
front_glass=[(-.691,.382,-.863),(.691,.382,-.863),(.592,.752,-.485),(-.592,.752,-.485)]
rear_glass=[(-.59,.741,.966),(.59,.741,.966),(.647,.415,1.50),(-.647,.415,1.50)]
for name,points in [('windscreen',front_glass),('hatch window',rear_glass)]:
    mesh(name,points,[(0,1,2,3)],glass)
    line(name+' gasket',points,rubber,.014,True)
    line(name+' chrome',points,chrome,.004,True)
for side in (-1,1):
    line('windscreen wiper',[(side*.30,.375,-.885),(side*.17,.435,-.826),(side*.43,.48,-.784)],rubber,.006)
    line('bonnet seam',[(side*.66,.226,-1.84),(side*.70,.272,-1.73),(side*.70,.305,-1.35),(side*.69,.342,-.94)],rubber,.0025)
for x in (-.43,.43):
    for i in range(7):
        box('cowl air slot',(x+(i-3)*.026,.350,-.94),(.012,.003,.075),rubber,0)

# Front identity: wide black grille, two round lamps, outboard amber indicators.
box('grille recess',(0,.055,-1.852),(1.35,.25,.028),rubber,.035)
line('grille chrome perimeter',[(-.65,-.06,-1.873),(.65,-.06,-1.873),(.68,-.025,-1.873),
     (.68,.15,-1.873),(.64,.18,-1.873),(-.64,.18,-1.873),(-.68,.15,-1.873),(-.68,-.025,-1.873)],chrome,.009,True)
for i in range(7):
    box('grille horizontal slat',(0,-.035+i*.029,-1.875),(.91,.005,.008),chrome,.001)
for i in range(23):
    box('grille vertical mesh',(-.44+i*.04,.05,-1.878),(.004,.20,.005),rubber,0)
for side in (-1,1):
    cylinder('headlamp chrome rim',(side*.535,.06,-1.881),.115,.036,chrome,'Z',32,.003)
    cylinder('headlamp rubber',(side*.535,.06,-1.903),.103,.014,rubber,'Z',32)
    cylinder('headlamp lens',(side*.535,.06,-1.914),.096,.012,lens,'Z',32,.004)
    # Glass fluting catches the light without using external textures.
    for k in range(-4,5):
        x=k*.018
        h=math.sqrt(.088**2-x*x)
        line('headlamp fluting',[(side*.535+x,.06-h,-1.922),(side*.535+x,.06+h,-1.922)],lens,.002)
    box('front indicator chrome',(side*.734,.04,-1.837),(.089,.13,.035),chrome,.008)
    box('front amber lens',(side*.734,.04,-1.86),(.073,.109,.018),amber,.006)
box('Z badge',(0,.07,-1.891),(.042,.067,.016),chrome,.005)
line('badge diagonal',[(.012,.09,-1.902),(-.012,.05,-1.902)],rubber,.003)

# Slim wraparound bumpers; overriders rather than modern plastic blocks.
for end in (-1,1):
    z=end*1.90
    line('chrome wraparound bumper',[(-.77,-.19,end*1.72),(-.785,-.19,end*1.84),
         (-.68,-.19,z),(0,-.19,end*1.925),(.68,-.19,z),(.785,-.19,end*1.84),(.77,-.19,end*1.72)],chrome,.033)
    for side in (-1,1):
        box('bumper overrider',(side*.52,-.19,end*1.934),(.055,.18,.06),chrome,.012)
        box('overrider rubber pad',(side*.52,-.19,end*1.97),(.036,.14,.015),rubber,.007)

# Rear panel, rectangular lamps and hatch seam.
for side in (-1,1):
    box('tail light surround',(side*.57,.065,1.857),(.29,.142,.032),chrome,.009)
    box('tail lamp gasket',(side*.57,.065,1.878),(.271,.125,.018),rubber,.007)
    box('tail red lens',(side*.535,.065,1.892),(.172,.105,.018),red,.005)
    box('tail amber lens',(side*.665,.065,1.892),(.07,.105,.018),amber,.005)
    line('hatch edge',[(side*.64,.78,.91),(side*.719,.34,1.65),(side*.65,.235,1.85)],rubber,.003)
box('rear plate recess',(0,.035,1.858),(.39,.135,.016),rubber,.008)
box('rear number plate',(0,.035,1.872),(.35,.11,.014),plate,.004)
box('front number plate',(0,-.285,-1.862),(.35,.105,.014),plate,.004)
box('hatch handle',(0,.237,1.86),(.10,.024,.027),chrome,.005)

def text_object(name, text, center, size, mat, rear=True):
    curve=bpy.data.curves.new(name,'FONT')
    curve.body=text
    curve.align_x='CENTER'
    curve.align_y='CENTER'
    curve.size=size
    curve.resolution_u=1
    curve.extrude=.0004
    obj=bpy.data.objects.new(name,curve)
    active_collection.objects.link(obj)
    obj.location=xyz(center)
    obj.rotation_euler=(math.pi/2,0,0 if rear else math.pi)
    curve.materials.append(mat)
    return obj

text_object('rear plate lettering','KG  101-73',(0,.036,1.882),.044,rubber)
text_object('front plate lettering','KG  101-73',(0,-.285,-1.873),.044,rubber,False)
text_object('rear model badge','ZASTAVA 101',(-.39,.241,1.863),.032,chrome)
cylinder('exhaust outlet',(-.48,-.375,1.88),.024,.18,rubber,'Z',16)

# A reusable wheel mesh, centered at its axle, used by the actual suspension.
active_collection=wheel
# Lathed section makes a rounded sidewall and flat tread; cylinder wheels looked toy-like.
profile=[(-.078,.162),(-.086,.19),(-.084,.247),(-.067,.280),(-.048,.29),
         (.048,.29),(.067,.280),(.084,.247),(.086,.19),(.078,.162)]
v=[]
segments=32
for x,r in profile:
    for i in range(segments):
        a=math.tau*i/segments
        v.append((x,r*math.cos(a),r*math.sin(a)))
f=[]
for j in range(len(profile)-1):
    for i in range(segments):
        f.append((j*segments+i,j*segments+(i+1)%segments,(j+1)*segments+(i+1)%segments,(j+1)*segments+i))
tire=mesh('155 R13 rounded tyre',v,f,rubber)
for polygon in tire.data.polygons:
    polygon.use_smooth=True
for side in (-1,1):
    cylinder('steel rim',(side*.078,0,0),.164,.012,chrome,vertices=32,edge=.003)
    cylinder('rim inner dish',(side*.088,0,0),.132,.012,rubber,vertices=32)
    cylinder('steel wheel centre',(side*.096,0,0),.114,.018,chrome,vertices=24,edge=.006)
    cylinder('domed hub cap',(side*.108,0,0),.078,.027,chrome,vertices=24,edge=.012)
    for i in range(8):
        a=math.tau*i/8
        cylinder('rim vent',(side*.107,.137*math.cos(a),.137*math.sin(a)),.014,.004,rubber,vertices=8)

def freeze_collection(collection):
    """Apply modifiers and join by material to keep draw calls and nodes small."""
    bpy.ops.object.select_all(action='DESELECT')
    for obj in list(collection.objects):
        obj.select_set(True)
    bpy.context.view_layer.objects.active=list(collection.objects)[0]
    bpy.ops.object.convert(target='MESH')
    bpy.ops.object.join()
    result=bpy.context.object
    # Store the origin at chassis/axle zero, not the first object's position.
    bpy.context.scene.cursor.location=(0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    # Joining repeats material slots; remove duplicates.
    mats=[]
    remap={}
    for i,mat in enumerate(result.data.materials):
        if mat not in mats: mats.append(mat)
        remap[i]=mats.index(mat)
    indices=[remap[p.material_index] for p in result.data.polygons]
    result.data.materials.clear()
    for mat in mats: result.data.materials.append(mat)
    for polygon,index in zip(result.data.polygons,indices): polygon.material_index=index
    result.data.calc_loop_triangles()
    return result

# Save editable separate objects before baking the export mesh.
editable=SOURCE/'zastava_101.blend'
bpy.context.preferences.filepaths.save_version=0
# Frame the editable body in the saved viewport and hide the wheel-at-origin template.
wheel.hide_viewport=True
for screen in bpy.data.screens:
    for area_space in screen.areas:
        if area_space.type == 'VIEW_3D':
            area_space.spaces.active.region_3d.view_location=Vector(xyz((0,.1,0)))
            area_space.spaces.active.region_3d.view_distance=6
            area_space.spaces.active.region_3d.view_rotation=(Vector(xyz((-4,2,-5)))-Vector(xyz((0,.1,0)))).to_track_quat('Z','Y')
            area_space.spaces.active.shading.color_type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(editable))
wheel.hide_viewport=False
body_mesh=freeze_collection(body)
body_mesh.name='Zastava101_Body'
wheel_mesh=freeze_collection(wheel)
wheel_mesh.name='Zastava101_Wheel'

def export(obj,path):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
                              export_yup=True,export_apply=True,export_cameras=False,export_lights=False)

export(body_mesh,ASSETS/'zastava_101.glb')
export(wheel_mesh,ASSETS/'zastava_101_wheel.glb')
stats={'body_triangles':len(body_mesh.data.loop_triangles),'wheel_triangles':len(wheel_mesh.data.loop_triangles),
       'body_materials':len(body_mesh.data.materials),'wheel_radius_m':RADIUS,
       'wheelbase_m':2.449,'track_m':1.30,'resting_wheel_y':WHEEL_Y}
(RENDERS/'geometry.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf-8')
print('ASSET_GEOMETRY',json.dumps(stats))

# Four real wheel instances, never included in the body export.
wheel_mesh.hide_render=True
wheel_mesh.hide_set(True)
active_collection=studio
for side in (-1,1):
    for z in AXLES:
        obj=bpy.data.objects.new('preview wheel',wheel_mesh.data)
        studio.objects.link(obj)
        obj.location=xyz((side*.65,WHEEL_Y,z))
floor=material('studio floor',(.065,.083,.09),0,.85)
box('ground',(0,-.59,0),(200,.01,200),floor,0)

scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.render.resolution_x=1440
scene.render.resolution_y=1000
scene.render.resolution_percentage=100
scene.world.color=(.3,.3,.3)
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.24,.29,.34,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.45
scene.view_settings.view_transform='AgX'

def area(name,loc,power,size,color):
    data=bpy.data.lights.new(name,'AREA')
    data.energy=power
    data.shape='DISK'
    data.size=size
    data.color=color
    obj=bpy.data.objects.new(name,data)
    studio.objects.link(obj)
    obj.location=xyz(loc)
    obj.rotation_euler=(Vector(xyz((0,.1,0)))-obj.location).to_track_quat('-Z','Y').to_euler()

area('large warm key',(-3,5,-4),1000,5,(1,.91,.78))
area('cool fill',(4,3,-.5),750,4,(.7,.82,1))
area('roof strip',(-1,4,4),1200,3,(1,1,1))
data=bpy.data.cameras.new('Review Camera')
camera=bpy.data.objects.new('Review Camera',data)
studio.objects.link(camera)
scene.camera=camera
data.type='ORTHO'
data.ortho_scale=5.5
data.clip_end=500
views={'front':(-4,2.1,-5.6),'rear':(4,1.9,5.6),'side':(-6,1.0,0)}
requested=next((a.split('=',1)[1] for a in sys.argv if a.startswith('--view=')),None)
for name,pos in views.items():
    if requested and name!=requested: continue
    camera.location=xyz(pos)
    camera.rotation_euler=(Vector(xyz((0,.1,0)))-camera.location).to_track_quat('-Z','Y').to_euler()
    if '--no-render' not in sys.argv:
        scene.render.filepath=str(RENDERS/f'{name}.png')
        bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'zastava_101_preview.blend'))
print('ZASTAVA_ASSET_COMPLETE')
