"""Independent chassis-origin attachments, <=600 triangles each.
blender --background --python tools/blender_car_upgrades.py
Only reuse the Zastava authoring prelude; never regenerate its body.
"""
from pathlib import Path
helper=Path(__file__).with_name('blender_zastava.py')
exec(compile(helper.read_text(encoding='utf8').split('# Overall length with bumpers')[0],str(helper),'exec'))
bpy.context.preferences.filepaths.save_version=0
SOURCE=ROOT/'art/blender/upgrades';SOURCE.mkdir(parents=True,exist_ok=True)
steel=material('weathered steel',(.13,.15,.14),.55,.65)
rust=material('weld rust',(.28,.105,.045),.15,.92)
green=material('old olive tank',(.19,.23,.105),.25,.73)
tool_red=material('faded toolbox red',(.32,.075,.035),.2,.7)
tape=material('cloth tape',(.07,.08,.07),0,.95)
bright=material('auxiliary lamp lens',(.78,.74,.53),.1,.28)
palette_mat=material('attachment vertex palette',(1,1,1),.25,.65)
vc=palette_mat.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color'
palette_mat.node_tree.links.new(vc.outputs['Color'],palette_mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'])

def foot(x,z,roof):
    box('rubber roof pad',(x,roof+.008,z),(.12,.025,.12),tape,0)
    box('welded mounting foot',(x,roof+.045,z),(.045,.065,.065),steel,0)

def tank(cfg):
    h=cfg['roof'];z=cfg['load_z'];x=.29
    for dx in (-.13,.13):
        for dz in (-.17,.17):foot(x+dx,z+dz,h)
    box('auxiliary fuel tank',(x,h+.26,z),(.36,.36,.46),green,.035)
    for dz in (-.155,.155):
        line('steel retaining strap',[(x-.19,h+.085,z+dz),(x-.19,h+.43,z+dz),
             (x+.19,h+.43,z+dz),(x+.19,h+.085,z+dz)],steel,.014)
        box('strap tensioner',(x-.202,h+.19,z+dz),(.025,.07,.05),rust,0)
    cylinder('filler neck',(x,h+.455,z-.11),.041,.04,steel,'Y',10)
    cylinder('filler cap',(x,h+.480,z-.11),.052,.022,rust,'Y',10)
    box('cap grip',(x,h+.496,z-.11),(.08,.016,.018),steel,0)
    line('carrying handle',[(x-.07,h+.445,z+.10),(x-.07,h+.50,z+.10),
         (x+.07,h+.50,z+.10),(x+.07,h+.445,z+.10)],steel,.012)
    line('weld seam',[(x-.15,h+.10,z-.233),(x-.15,h+.39,z-.233),(x+.15,h+.39,z-.233)],rust,.003)
    box('repaired enamel patch',(x+.04,h+.23,z-.233),(.15,.09,.004),rust,0)
    box('tape over repair',(x+.04,h+.23,z-.237),(.18,.026,.003),tape,0)

def toolbox(cfg):
    h=cfg['roof'];z=cfg['load_z'];x=-.29
    for dx in (-.14,.14):
        for dz in (-.18,.18):foot(x+dx,z+dz,h)
    box('tool chest',(x,h+.18,z),(.42,.20,.50),tool_red,.018)
    box('lid gasket',(x,h+.279,z),(.425,.015,.505),tape,0)
    box('lid',(x,h+.305,z),(.44,.045,.52),tool_red,.012)
    line('folding handle',[(x-.08,h+.332,z),(x-.08,h+.385,z),(x+.08,h+.385,z),(x+.08,h+.332,z)],steel,.009)
    for dx in (-.14,.14):
        box('latch',(x+dx,h+.26,z-.266),(.045,.09,.022),steel,0)
        box('hinge',(x+dx,h+.28,z+.265),(.075,.028,.02),rust,0)
    for dz in (-.14,.14):
        line('independent fastening strap',[(x-.223,h+.085,z+dz),(x-.223,h+.337,z+dz),
             (x+.223,h+.337,z+dz),(x+.223,h+.085,z+dz)],tape,.009)
    box('old repair plate',(x,h+.18,z-.254),(.15,.07,.005),steel,0)
    for dx in (-.17,.17):
        box('rust at lid corner',(x+dx,h+.33,z-.20),(.07,.003,.06),rust,0)

def roofrack(cfg):
    h=cfg['roof'];z=cfg['load_z'];half=.60
    for x in (-half,half):
        for dz in (-.35,.35):
            foot(x,z+dz,h-.025)
            box('gutter clamp',(x,h-.018,z+dz),(.09,.05,.12),rust,0)
    for dz in (-.35,.35):box('cross rail',(0,h+.06,z+dz),(1.25,.035,.045),steel,0)
    for x in (-.48,-.24,0,.24,.48):box('basket floor slat',(x,h+.06,z),(.035,.025,.91),steel,0)
    for y in (h+.085,h+.25):
        line('basket perimeter',[(-half,y,z-.45),(half,y,z-.45),(half,y,z+.45),(-half,y,z+.45)],steel,.013,True)
    for x in (-half,half):
        for dz in (-.45,.45):
            box('basket upright',(x,h+.165,z+dz),(.027,.17,.027),steel,0)
            box('weld bead',(x,h+.11,z+dz),(.033,.028,.033),rust,0)
    box('tape on front rail',(-.32,h+.25,z-.45),(.16,.031,.031),tape,0)

def bullbar(cfg):
    front=cfg['front'];z=front-.21
    for side in (-1,1):
        x=side*.80
        line('outer welded hoop',[(x,-.34,front+.03),(x,-.34,z),(x,.27,z),
             (side*.68,.34,z),(side*.40,.34,z)],steel,.025)
        box('bumper mounting plate',(side*.60,-.22,front),(.12,.12,.055),rust,0)
        line('bumper bracket',[(side*.60,-.22,front+.03),(x,-.22,z)],steel,.016)
        line('weld at upright',[(x,-.17,z-.026),(x,-.11,z-.026)],rust,.006)
    line('top cross tube',[(-.40,.34,z),(.40,.34,z)],steel,.025)
    line('lower cross tube',[(-.80,-.34,z),(.80,-.34,z)],steel,.025)
    box('taped grip',(.25,.34,z),(.18,.055,.055),tape,0)
    for x in (-.60,.60):
        cylinder('mount bolt',(x,-.22,front-.035),.018,.016,chrome,'Z',6)

def headlights(cfg):
    front=cfg['front'];z=front-.09
    for side in (-1,1):
        x=side*.24
        box('independent lamp bracket',(x,-.04,front+.008),(.065,.26,.035),steel,0)
        box('lamp foot',(x,.09,z+.035),(.10,.025,.14),rust,0)
        box('work lamp housing',(x,.19,z),(.23,.17,.10),steel,.016)
        box('lamp rubber seal',(x,.19,z-.057),(.203,.143,.016),tape,0)
        box('lamp glass',(x,.19,z-.067),(.18,.122,.008),bright,.006)
        for dx in (-.056,0,.056):
            box('lens flute',(x+dx,.19,z-.073),(.003,.11,.003),plate,0)
        line('wire with tape',[(x,.13,z+.05),(x,.02,front+.012),(x+.04,-.08,front+.012)],tape,.007)
        cylinder('mounting screw',(x,.07,z+.03),.016,.11,chrome,'X',6)

configs={'zastava_101':{'roof':.837,'front':-1.96,'load_z':.18},
         'vaz_2106':{'roof':.837,'front':-2.083,'load_z':.20}}
builders={'upgrade_tank':tank,'upgrade_toolbox':toolbox,'upgrade_headlights':headlights,
          'upgrade_bullbar':bullbar,'upgrade_roofrack':roofrack}
stats={}
for car,cfg in configs.items():
    collections=[]
    for upgrade,builder in builders.items():
        name=car+'_'+upgrade
        active_collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(active_collection)
        collections.append((name,active_collection));builder(cfg)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(car+'_upgrades.blend')))
    for name,coll in collections:
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
        bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
        merged.data.calc_loop_triangles();tris=len(merged.data.loop_triangles)
        assert tris<=600,(name,tris)
        stats[name]={'triangles':tris,'materials':1,'origin':[0,0,0]}
        bpy.ops.export_scene.gltf(filepath=str(ASSETS/(name+'.glb')),export_format='GLB',use_selection=True,
            export_yup=True,export_apply=True,export_animations=False,export_cameras=False,export_lights=False,export_all_vertex_colors=True)
    (ASSETS/(car+'_upgrade_springs.json')).write_text('{ "lift": 0.04 }\n',encoding='utf8')
    for name,coll in collections:
        for ob in list(coll.objects):bpy.data.objects.remove(ob,do_unlink=True)
        bpy.data.collections.remove(coll)
(SOURCE/'geometry.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf8')
print('CAR_UPGRADES',json.dumps(stats))
