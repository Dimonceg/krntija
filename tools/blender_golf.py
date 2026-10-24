"""Build the Krntija Golf Mk2 asset, source scene and review renders.

Run: blender --background --python tools/blender_golf.py
Optional: -- --no-render, or -- --view=front (front/rear/side).

Coordinates are Godot (X right, Y up, Z rearward), converted to Blender on
input. The GLB conversion brings them back, so no import rotation is needed.

Reference proportions (Volkswagen Golf II, 5-door):
  length 4.02 m, width 1.66 m, height 1.41 m, wheelbase 2.47 m, track 1.42 m.
Origin matches the Car node; ground sits at -(wheel radius + suspension rest).
"""
from pathlib import Path
import math
import sys

import bpy

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "game/assets/models/cars"
SOURCE = ROOT / "art/blender"
RENDERS = ROOT / "docs/screenshots/golf"
for directory in (ASSETS, SOURCE, RENDERS):
    directory.mkdir(parents=True, exist_ok=True)

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
NO_RENDER = "--no-render" in ARGS
VIEW = next((a.split("=")[1] for a in ARGS if a.startswith("--view=")), None)

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


paint = material('01 - lagos blue metallic', (.09, .16, .24), .35, .30)
rubber = material('02 - charcoal rubber and panel gaps', (.015, .017, .018), 0, .82)
chrome = material('03 - brushed aluminium', (.55, .57, .58), .85, .22)
glass = material('04 - blue grey glass', (.05, .09, .10), .35, .16)
lens = material('05 - headlight glass', (.72, .74, .68), .3, .12)
amber = material('06 - amber indicator', (.95, .32, .03), .08, .28)
red = material('07 - red tail lens', (.52, .015, .01), .1, .24)
plate = material('08 - enamel number plate', (.80, .79, .70), .1, .45)
plastic = material('09 - textured black plastic', (.035, .037, .038), .02, .62)


def bevel(obj, amount=.015, segments=1):
    mod = obj.modifiers.new('stamped edge', 'BEVEL')
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
    spline.points.add(len(points) - 1)
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


# ---------------------------------------------------------------- dimensions
RADIUS = .30
AXLES = (-1.235, 1.235)
WHEEL_Y = -.285
TRACK_X = .71
LENGTH = 4.02
HALF = .80

# ---------------------------------------------------------------- lower shell
# Each station is (rearward Z, half width, shoulder height).
stations = [(-1.93, .735, .200), (-1.82, .790, .232), (-1.52, .805, .252),
            (-1.10, .812, .268), (-.62, .812, .274), (.62, .806, .274),
            (1.24, .800, .268), (1.66, .780, .248), (1.93, .715, .210)]

vertices = []
for z, w, top in stations:
    vertices.extend([(0, top + .016, z), (w - .06, top, z), (w, top - .05, z),
                     (w, -.24, z), (w - .05, -.44, z), (0, -.45, z),
                     (-w + .05, -.44, z), (-w, -.24, z), (-w, top - .05, z), (-w + .06, top, z)])

faces = []
N = 10
for j in range(len(stations) - 1):
    for i in range(N):
        faces.append((j * N + i, j * N + (i + 1) % N, (j + 1) * N + (i + 1) % N, (j + 1) * N + i))
faces.extend([tuple(reversed(range(N))), tuple((len(stations) - 1) * N + i for i in range(N))])

shell = mesh('Golf body shell with wheel arch openings', vertices, faces, paint)

for z in AXLES:
    cutter = cylinder('arch cutting tool', (0, WHEEL_Y, z), .355, 2.2, rubber, vertices=32)
    bpy.context.view_layer.objects.active = shell
    cut = shell.modifiers.new('wheel opening', 'BOOLEAN')
    cut.operation = 'DIFFERENCE'
    cut.solver = 'EXACT'
    cut.object = cutter
    bpy.ops.object.modifier_apply(modifier=cut.name)
    bpy.data.objects.remove(cutter, do_unlink=True)
bevel(shell, .010, 2)

# ---------------------------------------------------------------- greenhouse
# Five-door: windscreen, roof, near-vertical hatch.
cab_stations = [(-.86, .762, .274), (-.34, .700, .782), (-.22, .697, .812),
                (.62, .695, .812), (.98, .706, .782), (1.62, .742, .272)]

v = []
for index, (z, w, h) in enumerate(cab_stations):
    crown = .014 if index in (2, 3) else 0
    v.extend([(-.752, .30, z), (-w, h - crown, z), (-w * .84, h, z),
              (w * .84, h, z), (w, h - crown, z), (.752, .30, z)])

f = []
for j in range(len(cab_stations) - 1):
    for i in range(5):
        f.append((j * 6 + i, j * 6 + i + 1, (j + 1) * 6 + i + 1, (j + 1) * 6 + i))
f += [tuple(reversed(range(6))), tuple((len(cab_stations) - 1) * 6 + i for i in range(6))]
mesh('roof and pillars', v, f, paint, .010)


def side_x(h):
    return .752 - (h - .33) * .19


def side_window(name, coords, side):
    points = [(side * (side_x(h) + .008), h, z) for z, h in coords]
    mesh(name, points, [tuple(range(len(points)))], glass)
    line(name + ' gasket', points, rubber, .010, True)


for side in (-1, 1):
    side_window('front door glass', [(-.74, .318), (-.40, .752), (0.02, .768), (.02, .318)], side)
    side_window('rear door glass', [(0.10, .318), (0.10, .768), (.72, .762), (.86, .318)], side)
    side_window('rear quarter glass', [(.94, .322), (.94, .690), (1.28, .322)], side)

    # Black plastic moulding along the doors and sills, the Golf II signature.
    box('side moulding', (side * .815, .055, .10), (.022, .075, 2.55), plastic, .006)
    box('sill trim', (side * .800, -.300, .10), (.020, .055, 2.30), plastic, .005)
    for z in (-.86, .06):
        line('door shut line', [(side * .806, .33, z), (side * .808, -.20, z), (side * .760, -.42, z)],
             rubber, .0022)
    for z in (-.30, .58):
        box('door handle', (side * .822, .215, z), (.020, .026, .125), plastic, .006)
    line('mirror stem', [(side * .780, .36, -.72), (side * .900, .40, -.69)], plastic, .014)
    box('mirror housing', (side * .925, .44, -.69), (.115, .080, .045), plastic, .014)
    box('mirror glass', (side * .928, .44, -.664), (.094, .058, .008), glass, .006)
    box('front wing repeater', (side * .812, .075, -1.62), (.012, .030, .055), amber, .004)

    # Arch trims: dark plastic lips around both openings.
    for z in AXLES:
        w = .806 if z < 0 else .800
        points = []
        for i in range(21):
            a = math.radians(-18 + i * 216 / 20)
            h = WHEEL_Y + .368 * math.sin(a)
            inset = .040 * max(0, min(1, (-h - .24) / .20))
            points.append((side * (w - inset + .004), h, z + .368 * math.cos(a)))
        line('arch plastic trim', points, plastic, .011)

# ---------------------------------------------------------------- glass panes
front_glass = [(-.700, .322, -.850), (.700, .322, -.850), (.600, .764, -.400), (-.600, .764, -.400)]
rear_glass = [(-.575, .756, .960), (.575, .756, .960), (.640, .350, 1.470), (-.640, .350, 1.470)]

for name, points in [('windscreen', front_glass), ('hatch window', rear_glass)]:
    mesh(name, points, [(0, 1, 2, 3)], glass)
    line(name + ' gasket', points, rubber, .012, True)

for side in (-1, 1):
    line('windscreen wiper', [(side * .28, .395, -.872), (side * .16, .450, -.820), (side * .42, .495, -.782)],
         plastic, .005)
    line('bonnet seam', [(side * .70, .216, -1.90), (side * .716, .250, -1.60),
                         (side * .716, .270, -1.10), (side * .700, .286, -.90)], rubber, .0022)

# ---------------------------------------------------------------- front end
# Wide black grille with two rectangular lamps per side.
box('grille recess', (0, .150, -1.930), (1.28, .18, .030), plastic, .012)
for i in range(4):
    box('grille slat', (0, .085 + i * .038, -1.938), (.92, .010, .008), plastic, 0)
box('vw badge', (0, .140, -1.948), (.115, .115, .012), chrome, .004)
for side in (-1, 1):
    box('headlamp housing', (side * .545, .155, -1.930), (.30, .150, .028), plastic, .008)
    box('headlamp outer lens', (side * .545, .155, -1.950), (.265, .125, .012), lens, .004)
    box('headlamp divider', (side * .545, .155, -1.952), (.012, .125, .014), plastic, 0)
    box('indicator front', (side * .600, .045, -1.975), (.145, .050, .018), amber, .004)

box('front bumper', (0, -.075, -1.960), (1.58, .165, .155), plastic, .014)
box('front plate', (0, -.070, -2.035), (.30, .075, .012), plate, .003)
box('front lip', (0, -.230, -1.880), (1.46, .055, .060), plastic, .008)

# ---------------------------------------------------------------- rear end
box('rear bumper', (0, -.075, 1.950), (1.58, .165, .155), plastic, .014)
box('rear plate', (0, -.050, 2.012), (.30, .085, .012), plate, .003)
for side in (-1, 1):
    box('tail lamp red', (side * .545, .150, 1.935), (.32, .150, .022), red, .006)
    box('tail lamp amber', (side * .545, .050, 1.940), (.32, .055, .020), amber, .006)
box('hatch seam', (0, .358, 1.472), (1.12, .012, .012), rubber, 0)
box('hatch handle', (0, .300, 1.500), (.20, .035, .022), plastic, .004)
box('rear wiper', (0, .392, 1.400), (.32, .010, .010), plastic, 0)

# ---------------------------------------------------------------- exhaust + mirrors
cylinder('exhaust', (.48, -.330, 2.040), .034, .180, chrome, 'Z', 14)

# ---------------------------------------------------------------- wheels
def build_wheel():
    global active_collection
    active_collection = wheel
    cylinder('tyre', (0, 0, 0), RADIUS, .190, rubber, 'X', 40)
    cylinder('rim', (0, 0, 0), RADIUS * .66, .196, chrome, 'X', 32)
    cylinder('hub', (0, 0, 0), RADIUS * .22, .205, chrome, 'X', 20)
    for i in range(5):
        a = math.radians(i * 72)
        cylinder('rim hole', (0, RADIUS * .42 * math.sin(a), RADIUS * .42 * math.cos(a)),
                 RADIUS * .12, .200, rubber, 'X', 12)
    active_collection = body


build_wheel()

# ---------------------------------------------------------------- export
def freeze_collection(collection):
    bpy.ops.object.select_all(action='DESELECT')
    meshes = [o for o in collection.objects if o.type in {'MESH', 'CURVE'}]
    for obj in meshes:
        obj.select_set(True)
    if not meshes:
        return None
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.convert(target='MESH')
    bpy.ops.object.join()
    joined = bpy.context.object
    joined.name = collection.name
    return joined


bpy.context.view_layer.update()
saved_body = freeze_collection(body)
saved_wheel = freeze_collection(wheel)


def export(obj, path):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(filepath=str(path), export_format='GLB', use_selection=True,
                              export_yup=True, export_apply=True, export_cameras=False,
                              export_lights=False)


if saved_body is not None:
    export(saved_body, ASSETS / 'golf_2.glb')
if saved_wheel is not None:
    export(saved_wheel, ASSETS / 'golf_2_wheel.glb')
print('exported golf_2.glb and golf_2_wheel.glb')
