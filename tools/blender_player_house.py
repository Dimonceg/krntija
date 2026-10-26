"""Player's Balkan home: authored architectural kit and separate usable furniture.
Blender --background --python tools/blender_player_house.py
Godot coordinates, metres; no gameplay or collision exported.
"""
from pathlib import Path
helper=Path(__file__).with_name('blender_zastava.py')
exec(compile(helper.read_text(encoding='utf8').split('# Overall length with bumpers')[0],str(helper),'exec'))
import hashlib
import random
bpy.context.preferences.filepaths.save_version=0
ASSETS=ROOT/'game/assets/models/house';ASSETS.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'art/blender/house';SOURCE.mkdir(parents=True,exist_ok=True)
plaster=material('warm lime plaster',(.64,.59,.47),0,.95)
stone=material('weathered foundation',(.27,.29,.26),0,.96)
wood=material('old oak',(.25,.14,.075),0,.9)
lightwood=material('worn timber edges',(.43,.28,.15),0,.9)
tile=material('terracotta',(.29,.12,.065),0,.92)
tilelight=material('sun bleached tiles',(.32,.145,.085),0,.9)
steel=material('old metal',(.12,.16,.15),.25,.7)
rust=material('oxide',(.29,.12,.06),0,.95)
enamel=material('cream enamel',(.62,.63,.52),.1,.6)
fabric=material('faded olive cloth',(.23,.27,.17),0,1)
linen=material('old linen',(.66,.60,.46),0,1)
blue=material('faded shutters',(.19,.30,.29),0,.9)
black=material('dark gaps',(.035,.04,.033),0,.9)
palette_mat=material('house vertex palette',(1,1,1),0,.87)
vc=palette_mat.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color'
palette_mat.node_tree.links.new(vc.outputs['Color'],palette_mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
collections=[]
def begin(name):
    global active_collection
    active_collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(active_collection)
    collections.append((name,active_collection))
def b(name,p,s,m):return box(name,p,s,m,0)
def roof_joint(points,side):
    data=bpy.data.meshes.new('tile joint')
    data.from_pydata([xyz(p) for p in points],[],[(3,2,1,0)] if side==1 else [(0,1,2,3)])
    data.update()
    ob=bpy.data.objects.new('tile joint',data);active_collection.objects.link(ob)
    data.materials.append(rust)
def beam(name,a,c,width,mat):line(name,[a,c],mat,width/2)
def window(x,y,z,w,h,side=False,shutters=True):
    def q(dx,dy,dz,sx,sy,sz,mat):
        p=(x+dz,y+dy,z+dx) if side else (x+dx,y+dy,z+dz)
        s=(sz,sy,sx) if side else (sx,sy,sz)
        b('window frame',p,s,mat)
    for dx in (-w/2,w/2):q(dx,0,0,.08,h+.12,.29,wood)
    for dy in (-h/2,h/2):q(0,dy,0,w+.08,.08,.29,wood)
    q(0,0,0,.045,h,.08,enamel);q(0,0,0,w,.045,.08,enamel)
    q(0,-h/2-.08,0,w+.3,.10,.48,stone)
    if shutters:
        sign=-1 if (x if side else z)<0 else 1
        for dx in (-w*.76,w*.76):
            q(dx,0,sign*.20,w*.4,h,.065,blue)
            for dy in (-h*.32,h*.32):q(dx,dy,sign*.24,w*.4,.05,.025,wood)

begin('home_roof')
# Raised eaves: 4.6 m, ridge: 6.2 m, attic floor unchanged at 2.8 m.
for side in (-1,1):
    if side==1:
        b('attic knee wall',(0,3.65,side*4),(11,1.9,.24),plaster)
    else:
        cursor=-5.5
        for center in (-3.7,.8):
            left=center-.55;right=center+.55
            b('attic front pier',((cursor+left)/2,3.65,-4),(left-cursor,1.9,.24),plaster)
            b('attic window below',(center,3.05,-4),(1.1,.7,.24),plaster)
            b('attic window above',(center,4.4,-4),(1.1,.4,.24),plaster)
            window(center,3.8,-4,1.1,.8,False,False)
            cursor=right
        b('attic front pier',((cursor+5.5)/2,3.65,-4),(5.5-cursor,1.9,.24),plaster)
    # End walls with real openings at y=3.8, z=0.
    for z in (-2.3,2.3):b('attic end',(side*5.5,3.65,z),(.24,1.9,3.4),plaster)
    b('window below',(side*5.5,3.0,0),(.24,.6,1.2),plaster)
    b('window above',(side*5.5,4.45,0),(.24,.3,1.2),plaster)
    mesh('true triangular gable',[(side*5.38,4.6,-4.4),(side*5.38,4.6,4.4),(side*5.38,6.2,0),
         (side*5.62,4.6,-4.4),(side*5.62,4.6,4.4),(side*5.62,6.2,0)],
         [(0,1,2),(3,5,4),(0,3,4,1),(1,4,5,2),(2,5,3,0)],plaster)
    # Seven broad courses, each a separate folded strip; no thousands of tiles.
    for row in range(7):
        z0=side*row*4.5/7;z1=side*(row+1)*4.5/7
        y0=6.2-abs(z0)*1.6/4.5;y1=6.2-abs(z1)*1.6/4.5
        mesh('overlapping terracotta course', [(-5.9,y0+.045,z0),(5.9,y0+.045,z0),(5.9,y1+.025,z1),(-5.9,y1+.025,z1),
             (-5.9,y0-.065,z0),(5.9,y0-.065,z0),(5.9,y1-.065,z1),(-5.9,y1-.065,z1)],
             [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],tile if row%3 else tilelight)
        for column in range(16):
            x=-5.65+column*.72+(row%2)*.18
            roof_joint([(x,y0+.047,z0),(x+.012,y0+.047,z0),
                (x+.012,y1+.027,z1),(x,y1+.027,z1)],side)
    underside=b('timber roof lining',(0,5.32,side*2.25),(11.8,.045,math.sqrt(4.5**2+1.6**2)),wood)
    underside.rotation_euler[0]=side*math.atan2(1.6,4.5)
    b('gutter',(0,4.57,side*4.55),(11.9,.12,.14),steel)
    b('eaves timber',(0,4.5,side*4.35),(11.8,.18,.15),wood)
    for x in (-5.7,5.7):beam('bargeboard',(x,4.5,side*4.5),(x,6.17,0),.18,wood)
    window(side*5.5,3.8,0,1.2,1.0,True,False)
for x in (-4,-1,2,5):
    for side in (-1,1):beam('visible attic rafter',(x,4.48,side*4.3),(x,6.08,0),.12,wood)
b('ridge cap',(0,6.24,0),(12,.13,.22),tilelight)
b('chimney',(-3.7,5.87,1.6),(.65,1.95,.72),rust)
for y in (5.15,5.65,6.15,6.65):b('chimney mortar',(-3.7,y,1.6),(.66,.025,.73),stone)
b('chimney cap',(-3.7,6.86,1.6),(.88,.13,.92),stone)
b('chimney dark opening',(-3.7,6.93,1.6),(.46,.015,.53),black)

begin('home_facade')
for x,z,y,w,h,side in [(-3.7,-4,1.55,1.1,1,False),(.8,-4,1.55,1.1,1,False),
    (1.2,4,1.55,1.1,1,False),(-5.5,.6,1.55,1.1,1,True),(5.5,-2.6,1.6,1,.9,True)]:window(x,y,z,w,h,side)
for x in (-5.65,5.65):
    b('downpipe',(x,2.25,4.25),(.10,4.5,.10),steel)
    for y in (.4,2.2,4.1):b('pipe clamp',(x,y,4.23),(.18,.055,.16),rust)
for z in (-4.14,4.14):b('storey timber band',(0,2.74,z),(11.25,.12,.12),wood)
for x in (-5.64,5.64):b('storey timber band',(x,2.74,0),(.12,.12,8.3),wood)
# Low entrance canopy, side supports clear of the swinging entrance door.
awning=b('porch corrugated canopy',(-1.25,2.55,-4.75),(3.6,.10,1.8),tile)
awning.rotation_euler[0]=-.10
for x in (-2.85,.35):
    b('porch post',(x,1.24,-5.45),(.13,2.48,.13),wood)
    b('post stone foot',(x,.14,-5.45),(.23,.28,.23),stone)
    beam('porch brace',(x,1.9,-5.45),(x,2.48,-4.9),.10,wood)
for x in (-2.65,-2.15,-1.65,-1.15,-.65,-.15,.35):b('canopy seam',(x,2.61,-4.75),(.025,.02,1.8),rust)
# Local plaster damage and exposed brick at foot of facade, away from openings.
rng=random.Random(101)
for x in (-5,-4.6,-4.2,-3.8,-3.4,.1,.45,.8,1.15,1.5):
    b('exposed brick',(x,.22+rng.random()*.22,-4.127),(.29,.10,.014),rust)
b('mailbox',(-2.5,1.35,-4.24),(.36,.42,.18),blue)
b('mail slot',(-2.5,1.46,-4.34),(.25,.025,.012),black)
b('house number plaque',(-.32,1.85,-4.15),(.22,.30,.025),enamel)
b('number one',(-.32,1.85,-4.17),(.035,.19,.012),black)
# Rear pantry roof, still separate from the house so the back passage stays open.
pantry=b('pantry pitched metal roof',(0,2.25,6.8),(4.9,.12,3.7),steel)
pantry.rotation_euler[0]=.06
for x in (-2,-1,0,1,2):b('pantry roof seam',(x,2.33,6.8),(.03,.025,3.7),rust)

begin('home_furniture')
# Sofa: bevelled cushions, exposed wooden feet, faded fabric and blanket.
for x in (-4.95,-3.05):
    for z in (.16,.84):b('sofa foot',(x,.12,z),(.12,.24,.12),wood)
box('sofa base',(-4,.31,.5),(2.4,.28,.9),wood,.04)
for x in (-4.58,-3.42):box('seat cushion',(x,.52,.56),(1.12,.20,.77),fabric,.06)
box('sofa back',(-4,.8,.12),(2.4,.75,.20),fabric,.045)
for x in (-5.13,-2.87):box('sofa arm',(x,.65,.53),(.2,.45,.92),fabric,.04)
box('folded blanket',(-4.7,.64,.62),(.5,.06,.6),linen,.015)
# Dining table and four chairs, leaving the route along the right of the room.
box('table top',(-1.4,.78,1.2),(1.6,.09,1),lightwood,.015)
for x in (-2.05,-.75):
    for z in (.8,1.6):b('table leg',(x,.37,z),(.09,.74,.09),wood)
for x,z,sign in [(-1.9,.35,-1),(-.9,.35,-1),(-1.9,2.05,1),(-.9,2.05,1)]:
    b('chair seat',(x,.45,z),(.43,.07,.43),lightwood)
    for dx in (-.17,.17):
        for dz in (-.17,.17):b('chair leg',(x+dx,.22,z+dz),(.055,.44,.055),wood)
        b('chair back post',(x+dx,.68,z+sign*.18),(.055,.52,.055),wood)
    b('chair back',(x,.84,z+sign*.18),(.43,.17,.06),lightwood)
b('woven rug',(-.4,.012,-1.6),(2.7,.018,1.6),fabric)
for x in (-1.65,.85):b('rug border',(x,.023,-1.6),(.10,.005,1.5),linen)
# Stove and sink at the existing kitchen wall.
box('enamel cooker',(-5.1,.45,3.7),(.70,.90,.70),enamel,.025)
b('oven face',(-4.741,.43,3.7),(.018,.44,.51),black)
b('oven handle',(-4.70,.68,3.7),(.06,.045,.4),steel)
for z in (3.52,3.88):
    for x in (-5.26,-4.94):cylinder('hotplate',(x,.91,z),.11,.018,black,'Y',10)
b('kitchen backsplash',(-5.36,1.22,2.55),(.08,.6,1.1),enamel)
b('sink rim',(-5.1,.92,2.5),(.58,.05,.55),steel)
b('sink recess',(-5.1,.95,2.5),(.42,.01,.39),black)
line('tap',[(-5.32,.94,2.5),(-5.32,1.23,2.5),(-5.09,1.23,2.5),(-5.09,1.14,2.5)],steel,.018)
# Lamps have visible fixtures; lighting remains in the scene.
for x,y,z in [(0,2.40,-2),(-3.4,2.40,1.4),(4,2.40,-2),(0,5.35,.4)]:
    line('pendant cable',[(x,y+.20,z),(x,y+.03,z)],black,.009)
    cylinder('lamp shade',(x,y,z),.22,.09,enamel,'Y',12)
    cylinder('lamp diffuser',(x,y-.05,z),.16,.025,linen,'Y',12)
# Attic chest and pantry shelves, not loot items.
box('attic trunk',(1.6,3.06,-2.6),(.9,.5,.6),wood,.02)
for x in (1.3,1.9):b('trunk band',(x,3.07,-2.6),(.045,.51,.61),steel)
for y in (.5,1.05,1.6):b('pantry shelf',(0,y,8.05),(3.6,.07,.5),lightwood)
for x in (-1.7,1.7):b('shelf upright',(x,.9,8.05),(.07,1.8,.5),wood)
for x in (-1.2,-.65,0,.65,1.2):
    cylinder('preserve jar',(x,1.25,8.05),.12,.32,blue,'Y',8)
    cylinder('jar lid',(x,1.42,8.05),.125,.025,steel,'Y',8)

begin('home_workbench')
# Furniture exports use local ground origin; placement stays in PlayerHouse.
box('worktop',(0,.85,0),(2.2,.12,.7),lightwood,.025)
for x in (-.94,.94):
    for z in (-.25,.25):b('bench leg',(x,.41,z),(.11,.82,.11),steel)
b('tool shelf',(0,.22,0),(1.9,.07,.58),wood)
b('pegboard',(0,1.28,.29),(2.05,.65,.055),wood)
for x in (-.75,-.45,-.15,.15):
    b('hanging spanner',(x,1.32,.25),(.045,.28,.025),steel)
    b('spanner head',(x,1.46,.25),(.10,.055,.03),steel)
box('vice body',(.70,.98,-.11),(.25,.18,.23),steel,.015)
for x in (.59,.80):b('vice jaw',(x,1.10,-.11),(.07,.12,.25),steel)
line('vice handle',[(.69,.95,-.25),(.69,.95,-.43),(.69,1.10,-.43)],steel,.014)
box('tool drawer',(-.50,.64,0),(.72,.26,.57),blue,.01)
b('drawer pull',(-.5,.65,-.31),(.25,.03,.04),steel)

begin('home_chest')
box('kitchen cabinet',(0,.43,0),(.65,.84,2.2),blue,.015)
b('countertop',(0,.88,0),(.7,.07,2.24),lightwood)
for z in (-.73,0,.73):
    b('cabinet door',(.333,.43,z),(.025,.69,.65),wood)
    b('door inset',(.351,.43,z),(.016,.53,.49),blue)
    b('cupboard handle',(.38,.62,z+.20),(.05,.12,.025),steel)

begin('home_bed')
for x in (-.82,.82):
    for z in (-.36,.36):b('bed leg',(x,.18,z),(.09,.36,.09),wood)
b('bed frame',(0,.22,0),(1.95,.18,.94),wood)
box('mattress',(0,.39,0),(1.90,.23,.90),linen,.055)
box('blanket',(.20,.515,0),(1.18,.045,.92),fabric,.015)
for x in (-.93,.93):
    b('bed end',(x,.44,0),(.08,.58,.96),wood)
    b('bed end inset',(x,.54,0),(.09,.07,.84),lightwood)
box('pillow',(-.62,.55,0),(.40,.13,.63),linen,.055)

begin('home_door')
# Normalized leaf: hinge at zero, width/height 1; details on both sides.
b('door leaf',(.5,.5,0),(1,1,.075),wood)
for z in (-.045,.045):
    for y in (.28,.72):b('raised timber panel',(.5,y,z),(.73,.33,.025),lightwood)
    b('lock plate',(.88,.49,z*1.7),(.07,.12,.018),steel)
    b('door handle',(.84,.50,z*2.2),(.15,.025,.035),steel)

begin('home_garage_door')
b('garage leaf',(.5,-.5,0),(1,1,.07),blue)
for x in (.08,.22,.36,.50,.64,.78,.92):b('pressed metal rib',(x,-.5,-.05),(.025,.96,.03),steel)
for y in (-.04,-.96):b('edge rail',(.5,y,-.06),(1,.025,.04),rust)
b('garage handle',(.5,-.83,-.10),(.18,.025,.04),steel)

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'player_house.blend'))
stats={}
template=(ROOT/'game/assets/models/cars/zastava_101_upgrade_tank.glb.import').read_text(encoding='utf8')
for name,coll in collections:
    bpy.ops.object.select_all(action='DESELECT')
    for ob in list(coll.objects):
        bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.convert(target='MESH')
        color=ob.data.materials[0].diffuse_color[:]
        layer=ob.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
        for entry in layer.data:entry.color=color
        ob.data.materials.clear();ob.data.materials.append(palette_mat)
        for face in ob.data.polygons:face.material_index=0
        ob.select_set(False)
    for ob in coll.objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=list(coll.objects)[0];bpy.ops.object.join()
    ob=bpy.context.object;ob.name=name;bpy.context.scene.cursor.location=(0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR');bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    ob.data.calc_loop_triangles();stats[name]=len(ob.data.loop_triangles)
    bpy.ops.export_scene.gltf(filepath=str(ASSETS/(name+'.glb')),export_format='GLB',use_selection=True,
        export_yup=True,export_apply=True,export_animations=False,export_cameras=False,export_lights=False,export_all_vertex_colors=True)
    path='res://assets/models/house/'+name+'.glb'
    importpath='res://.godot/imported/'+name+'.glb-'+hashlib.md5(path.encode()).hexdigest()+'.scn'
    import re
    text=re.sub(r'^uid=.*\n','',template,flags=re.M)
    text=text.replace('res://assets/models/cars/zastava_101_upgrade_tank.glb',path)
    text=re.sub(r'res://\.godot/imported/zastava_101_upgrade_tank\.glb-[a-f0-9]+\.scn',importpath,text)
    (ASSETS/(name+'.glb.import')).write_text(text,encoding='utf8',newline='\n')
(SOURCE/'geometry.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf8')
print('HOUSE_GEOMETRY',json.dumps(stats))
