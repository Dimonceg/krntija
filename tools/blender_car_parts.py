"""Loose car parts for hands-on assembly (ADR 0003, task B2).
blender --background --python tools/blender_car_parts.py
Only reuses the Zastava authoring prelude; never regenerates its body.
Coordinates are Godot (X, height, rearward Z); origin = attachment point in the
socket, at the bottom centre of the part. One mesh, one vertex-colour material.
"""
from pathlib import Path
helper=Path(__file__).with_name('blender_zastava.py')
exec(compile(helper.read_text(encoding='utf8').split('# Overall length with bumpers')[0],str(helper),'exec'))
bpy.context.preferences.filepaths.save_version=0
SOURCE=ROOT/'art/blender/parts';SOURCE.mkdir(parents=True,exist_ok=True)
PARTS=ROOT/'game/assets/models/parts';PARTS.mkdir(parents=True,exist_ok=True)

iron=material('cast iron block',(.22,.23,.22),.45,.7)
enamel=material('engine green enamel',(.17,.27,.2),.2,.55)
alloy=material('dull alloy',(.33,.33,.31),.6,.45)
steel=material('weathered steel',(.13,.15,.14),.55,.65)
rust=material('weld rust',(.28,.105,.045),.15,.92)
rubber_hose=material('perished rubber',(.03,.03,.03),0,.95)
copper=material('radiator copper',(.36,.18,.07),.6,.5)
core_black=material('radiator core paint',(.045,.05,.05),.3,.7)
case=material('battery case',(.05,.055,.05),0,.55)
red=material('positive cap',(.45,.05,.03),0,.6)
lead=material('lead terminal',(.42,.42,.40),.7,.5)
label=material('faded battery label',(.55,.48,.20),0,.8)
palette_mat=material('part vertex palette',(1,1,1),.3,.65)
vc=palette_mat.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color'
palette_mat.node_tree.links.new(vc.outputs['Color'],palette_mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'])

def engine():
    """Inline four, longitudinal: front of the engine (pulleys, fan) toward -Z."""
    box('oil sump',(0,.06,.03),(.30,.12,.46),steel,.01)
    box('sump gasket',(0,.125,.03),(.32,.012,.48),rubber_hose,0)
    box('cylinder block',(0,.25,0),(.36,.25,.56),iron,.012)
    box('cylinder head',(0,.42,0),(.32,.10,.54),alloy,.01)
    box('valve cover',(0,.495,0),(.27,.05,.48),enamel,.018)
    box('oil filler cap',(0,.53,-.15),(.06,.025,.06),steel,0)
    for z in (-.18,-.06,.06,.18):
        line('spark plug lead',[(.12,.47,z),(.19,.47,z),(.19,.52,-.26)],rubber_hose,.005)
    # Carburettor and round air filter on the intake side.
    line('intake manifold',[(-.17,.43,-.15),(-.26,.45,0),(-.17,.43,.15)],alloy,.03)
    box('carburettor',(-.27,.52,0),(.09,.10,.09),alloy,.008)
    cylinder('air filter can',(-.27,.60,0),.13,.07,enamel,'Y',16)
    cylinder('air filter nut',(-.27,.64,0),.02,.02,steel,'Y',6)
    # Exhaust side.
    for z in (-.18,-.06,.06,.18):
        line('exhaust runner',[(.18,.38,z),(.25,.33,z*.6),(.27,.20,0)],rust,.022)
    cylinder('exhaust flange',(.27,.17,0),.045,.03,rust,'Y',8)
    # Front: water pump, pulleys, fan, belt.
    cylinder('crank pulley',(0,.17,-.30),.07,.03,steel,'Z',16)
    cylinder('water pump pulley',(0,.36,-.30),.055,.03,steel,'Z',16)
    cylinder('alternator body',(.20,.30,-.24),.065,.13,alloy,'Z',12)
    cylinder('cooling fan',(0,.36,-.335),.13,.012,enamel,'Z',12)
    cylinder('fan hub',(0,.36,-.345),.03,.02,steel,'Z',8)
    box('thermostat housing',(0,.44,-.25),(.07,.05,.06),alloy,0)
    cylinder('top hose stub',(0,.45,-.30),.022,.08,rubber_hose,'Z',8)
    # Mounts and gearbox face.
    for side in (-1,1):
        box('engine mount',(side*.20,.20,-.12),(.05,.08,.10),rubber_hose,0)
        box('mount bracket',(side*.21,.26,-.12),(.03,.07,.12),rust,0)
    box('bellhousing face',(0,.24,.30),(.40,.32,.04),alloy,.01)
    line('dipstick',[(.19,.22,.18),(.21,.48,.18)],steel,.005)
    box('dipstick handle',(.21,.495,.18),(.03,.02,.02),red,0)

def radiator():
    """Core faces the car front (-Z); tanks top and bottom, hoses toward the engine."""
    box('lower tank',(0,.04,0),(.60,.06,.07),copper,.008)
    box('radiator core',(0,.20,0),(.56,.26,.05),core_black,0)
    for y in (.09,.13,.17,.21,.25,.29):
        box('core fin row',(0,y,-.027),(.55,.008,.004),copper,0)
    box('upper tank',(0,.36,0),(.60,.06,.07),copper,.008)
    cylinder('filler neck',(.20,.40,0),.025,.03,copper,'Y',10)
    cylinder('pressure cap',(.20,.42,0),.035,.014,steel,'Y',10)
    for side in (-1,1):
        box('side channel',(side*.29,.20,0),(.025,.30,.06),steel,0)
        box('mounting tab',(side*.31,.30,.02),(.04,.04,.01),rust,0)
    cylinder('top hose outlet',(-.18,.36,.06),.022,.07,copper,'Z',8)
    cylinder('bottom hose outlet',(.18,.04,.06),.022,.07,copper,'Z',8)
    cylinder('drain tap',(-.24,.02,-.03),.01,.03,steel,'Z',6)

def battery():
    """12 V, terminals toward +X (the car's right when mounted)."""
    box('battery case',(0,.085,0),(.26,.17,.17),case,.008)
    box('battery lid',(0,.18,0),(.265,.02,.175),case,.006)
    box('faded label',(0,.10,-.086),(.18,.07,.003),label,0)
    for i in range(6):
        cylinder('cell cap',(-.10+i*.04,.195,0),.012,.012,case,'Y',6)
    cylinder('positive terminal',(.10,.205,-.05),.016,.03,lead,'Y',8)
    cylinder('positive cap',(.10,.225,-.05),.02,.012,red,'Y',8)
    cylinder('negative terminal',(.10,.205,.05),.014,.03,lead,'Y',8)
    line('carry strap',[(-.12,.19,0),(-.12,.25,0),(.04,.25,0),(.04,.19,0)],rubber_hose,.006)
    box('acid stain',(.06,.192,.03),(.05,.002,.04),rust,0)

# id -> (builder, triangle budget, scale); file: parts/part_<id>.glb (ADR 0003, п. 8).
# The engine is drawn at workshop scale and shrunk to fit under the Zastava bonnet:
# about 0.5 m from its socket to the bonnet skin.
builders={'engine_1_6':(engine,1500,.72),'radiator_std':(radiator,500,1),'battery_std':(battery,400,1)}
stats={}
collections=[]
for part,(builder,budget,scale) in builders.items():
    name='part_'+part
    active_collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(active_collection)
    collections.append((name,active_collection,budget,scale));builder()
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'car_parts.blend'))
for name,coll,budget,scale in collections:
    bpy.ops.object.select_all(action='DESELECT')
    for ob in list(coll.objects):
        bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.convert(target='MESH')
        color=ob.data.materials[0].diffuse_color[:]
        layer=ob.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
        for entry in layer.data:entry.color=color
        ob.data.materials.clear();ob.data.materials.append(palette_mat)
        for polygon in ob.data.polygons:polygon.material_index=0
        ob.select_set(False)
    for ob in coll.objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=list(coll.objects)[0];bpy.ops.object.join();merged=bpy.context.object;merged.name=name
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    merged.scale=(scale,scale,scale)
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    merged.data.calc_loop_triangles();tris=len(merged.data.loop_triangles)
    assert tris<=budget,(name,tris,budget)
    stats[name]={'triangles':tris,'budget':budget,'materials':1,'origin':[0,0,0]}
    bpy.ops.export_scene.gltf(filepath=str(PARTS/(name+'.glb')),export_format='GLB',use_selection=True,
        export_yup=True,export_apply=True,export_animations=False,export_cameras=False,export_lights=False,export_all_vertex_colors=True)
(SOURCE/'geometry.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf8')
print('CAR_PARTS',json.dumps(stats))
