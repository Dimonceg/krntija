"""Original low-poly Balkan environment kit. No downloaded meshes/textures.
blender --background --python tools/blender_world.py [-- --render=gasstation]
All authoring coordinates: x, height, rearward z (Godot metres).
"""
import bpy
import math
import random
import json
import sys
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'game/assets/models/props'
SOURCE=ROOT/'art/blender/world'
SHOTS=ROOT/'docs/screenshots/world-art'
for folder in (OUT,SOURCE,SHOTS): folder.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version=0
random.seed(101)

def xyz(p): return (p[0],-p[2],p[1])

def anchor(name,p):
 obj=bpy.data.objects.new(name,None);current.objects.link(obj);obj.location=xyz(p)

palette={
 'plaster':(.64,.59,.46),'cream':(.76,.72,.59),'ochre':(.62,.42,.23),
 'white':(.81,.79,.68),'stone':(.34,.35,.31),'concrete':(.46,.47,.42),
 'roof':(.39,.12,.064),'tile':(.49,.18,.09),'darktile':(.28,.074,.036),
 'wood':(.25,.16,.088),'woodlight':(.40,.27,.13),'dark':(.025,.034,.033),
 'glass':(.085,.16,.18),'metal':(.34,.39,.38),'red':(.48,.055,.031),
 'teal':(.10,.25,.23),'green':(.17,.25,.09),'grass':(.25,.32,.10),
 'hay':(.57,.43,.16),'brick':(.40,.17,.09), 'blue':(.08,.18,.30),
}
def mat(name,vertex=False):
 m=bpy.data.materials.new(name);m.diffuse_color=(.6,.6,.6,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.82
 if vertex:
  vc=m.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color'
  m.node_tree.links.new(vc.outputs['Color'],bs.inputs['Base Color'])
 return m
surface=mat('Painted plaster, timber and foliage - vertex colours',True)
current=None
colliders=[]
statistics={}

def collection(name):
 global current,colliders
 current=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(current)
 colliders=[]
 return current

def colorize(obj,color,wind=0,variation=0):
 if isinstance(color,str):color=palette[color]
 layer=obj.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
 for poly in obj.data.polygons:
  k=1+random.uniform(-variation,variation)
  for index in poly.loop_indices:
   layer.data[index].color=(*(min(1,max(0,c*k)) for c in color),wind)
 obj.data.materials.clear();obj.data.materials.append(surface)

def register(obj,name,color='cream',wind=0,variation=0):
 obj.name=name
 for c in list(obj.users_collection):c.objects.unlink(obj)
 current.objects.link(obj)
 if obj.type=='MESH':colorize(obj,color,wind,variation)
 obj.select_set(False)
 return obj

def box(name,p,size,color='cream',bevel=0):
 bpy.ops.mesh.primitive_cube_add(size=1,location=xyz(p));obj=bpy.context.object
 obj.dimensions=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=obj.modifiers.new('soft edges','BEVEL');mod.width=bevel;mod.segments=1
  bpy.ops.object.modifier_apply(modifier=mod.name)
 return register(obj,name,color)

def mesh(name,pts,faces,color='cream',wind=0,variation=0):
 data=bpy.data.meshes.new(name);data.from_pydata([xyz(p) for p in pts],[],faces);data.update()
 obj=bpy.data.objects.new(name,data);current.objects.link(obj)
 bpy.context.view_layer.objects.active=obj;obj.select_set(True)
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
 colorize(obj,color,wind,variation);obj.select_set(False)
 return obj

def cylinder(name,p,r,h,color='wood',n=8,top=None):
 bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r,radius2=r if top is None else top,depth=h,location=xyz(p))
 return register(bpy.context.object,name,color)

def beam(name,a,b,r,color='wood',n=6):
 d=Vector(xyz(b))-Vector(xyz(a));mid=(Vector(xyz(a))+Vector(xyz(b)))*.5
 bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r,radius2=r*.70,depth=d.length,location=mid)
 obj=bpy.context.object;obj.rotation_euler=d.to_track_quat('Z','Y').to_euler()
 return register(obj,name,color)

def pipe(name,points,r,color='dark',n=5):
 for a,b in zip(points,points[1:]):beam(name,a,b,r,color,n)

def ico(name,p,size,color='green',sub=1,wind=1):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub,radius=1,location=xyz(p))
 obj=bpy.context.object;obj.scale=(size[0],size[2],size[1])
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 # Break the mathematical symmetry of each crown cluster.
 for vert in obj.data.vertices:vert.co*=random.uniform(.89,1.11)
 return register(obj,name,color,wind,.12)

def text(name,caption,p,size=.3,color='white',facing='front'):
 curve=bpy.data.curves.new(name,'FONT');curve.body=caption;curve.align_x='CENTER';curve.align_y='CENTER'
 curve.size=size;curve.resolution_u=1;curve.extrude=0
 obj=bpy.data.objects.new(name,curve);current.objects.link(obj);obj.location=xyz(p)
 obj.rotation_euler=(math.pi/2,0,0 if facing=='front' else math.pi/2)
 bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.convert(target='MESH')
 colorize(obj,color);obj.select_set(False)
 return obj

def collision(name,p,size):
 obj=box(name+'_'+str(len(colliders))+'-colonly',p,size,'dark');colliders.append(obj)
 obj.hide_render=True
 return obj

def window(x,y,z,w=1.05,h=1.2,shutters=True):
 box('window recess',(x,y,z),(w+.16,h+.16,.06),'wood')
 box('blue window',(x,y,z+.042),(w,h,.025),'glass')
 for dx in (-w/2,0,w/2):box('window mullion',(x+dx,y,z+.065),(.045,h+.05,.045),'white')
 for dy in (-h/2,h/2):box('window frame',(x,y+dy,z+.065),(w+.08,.045,.045),'white')
 box('projecting sill',(x,y-h/2-.07,z+.10),(w+.27,.12,.25),'stone')
 if shutters:
  for side in (-1,1):
   box('wooden shutter',(x+side*(w*.5+.24),y,z+.04),(.35,h,.07),'teal')
   for j in range(5):box('shutter slat',(x+side*(w*.5+.24),y-h*.4+j*h*.2,z+.083),(.30,.024,.018),'wood')

def roof(cx,cz,w,d,y,rise):
 half=w*.5+.40;depth=d*.5+.38
 mesh('gable infill',[(cx-w/2,y,cz-d/2),(cx+w/2,y,cz-d/2),(cx,y+rise,cz-d/2),
                     (cx-w/2,y,cz+d/2),(cx+w/2,y,cz+d/2),(cx,y+rise,cz+d/2)],[(0,1,2),(3,5,4)],'plaster')
 for side in (-1,1):
  mesh('terracotta roof',[(cx,y+rise,cz-depth),(cx+side*half,y-.06,cz-depth),
       (cx+side*half,y-.06,cz+depth),(cx,y+rise,cz+depth)],[(0,1,2,3)],'roof')
  # Alternating tile rows, raised edges and restrained seams: readable from the road.
  rows=max(4,int(half/.33));cols=max(6,int(d/.38))
  for row in range(rows):
   t0=row/rows;t1=(row+1)/rows
   for col in range(cols):
    z0=cz-depth+col*2*depth/cols;z1=cz-depth+(col+1)*2*depth/cols-.012
    h0=y+rise-(rise+.06)*t0+.018;h1=y+rise-(rise+.06)*t1+.028
    mesh('clay tile',[(cx+side*half*t0,h0,z0),(cx+side*half*t1,h1,z0),
         (cx+side*half*t1,h1,z1),(cx+side*half*t0,h0,z1)],[(0,1,2,3)],random.choice(['roof','roof','tile','darktile']))
  pipe('gutter',[(cx+side*half,y-.08,cz-depth),(cx+side*half,y-.08,cz+depth)],.055,'metal',6)
 pipe('ridge',[(cx,y+rise+.04,cz-depth),(cx,y+rise+.04,cz+depth)],.10,'tile',8)

def door(x,y,z,color='wood',w=1.0):
 box('door frame',(x,y+1.05,z),(w+.14,2.15,.13),'stone')
 box('timber door',(x,y+1.02,z+.09),(w,2.02,.06),color)
 for dy in (.45,1.35):box('door inset',(x,y+dy,z+.13),(w*.78,.65,.015),'woodlight')
 box('handle',(x+w*.35,y+1.0,z+.17),(.04,.12,.05),'metal')
 box('door step',(x,y+.07,z+.35),(w+.45,.14,.65),'stone')

def house(cx,cz,w=6,d=5,stories=1,color='plaster'):
 h=2.8*stories;front=cz+d/2
 box('rubble foundation',(cx,-.30,cz),(w+.2,.8,d+.2),'stone')
 box('rendered walls',(cx,h/2,cz),(w,h,d),color)
 box('base course',(cx,.25,cz),(w+.055,.5,d+.055),'stone')
 collision('house',(cx,h/2,cz),(w,h,d))
 roof(cx,cz,w,d,h,1.55)
 for level in range(stories):
  for x in (-w*.29,w*.29):window(cx+x,1.85+level*2.65,front+.035,w=1.0)
 door(cx,0,front+.04)
 box('brick chimney',(cx+w*.25,h+1.35,cz-.9),(.60,1.8,.60),'brick')
 box('chimney cap',(cx+w*.25,h+2.26,cz-.9),(.76,.14,.76),'stone')
 for j in range(6):box('chimney mortar',(cx+w*.25,h+.62+j*.26,cz-.9),(.607,.026,.607),'stone')
 # Side window visible from the road (front facade faces local +Z).
 before=set(current.objects)
 window(0,1.8,0,w=1.1,shutters=False)
 for ob in set(current.objects)-before:
  p=ob.location.copy();ob.location=(cx+w/2+.04+p[1]*-1,-cz-p[0],p[2]);ob.rotation_euler[2]+=math.pi/2
 # A balcony gives the two-storey house a distinct silhouette.
 if stories==2:
  box('balcony slab',(cx,2.8,front+.7),(w*.65,.18,1.45),'concrete')
  for i in range(9):box('balcony baluster',(cx-w*.29+i*w*.58/8,3.35,front+1.35),(.035,1.0,.035),'metal')
  box('balcony rail',(cx,3.85,front+1.35),(w*.65,.06,.06),'metal')
  for side in (-1,1):box('balcony return',(cx+side*w*.31,3.85,front+.7),(.06,.06,1.4),'metal')
 # Small uneven patches of exposed masonry near ground level.
 for i in range(5):box('exposed brick',(cx-w*.40+i*.25,.61+(i%2)*.13,front+.026),(.23,.11,.018),'brick')

def fence(x,z,length,axis='x'):
 for i in range(int(length/.30)+1):
  dx=i*.30 if axis=='x' else 0;dz=i*.30 if axis=='z' else 0
  box('fence picket',(x+dx,.58,z+dz),(.13,1.1,.07) if axis=='x' else (.07,1.1,.13),'woodlight')
 for h in (.32,.84):box('fence rail',(x+length/2 if axis=='x' else x,h,z+length/2 if axis=='z' else z),
                        (length,.07,.09) if axis=='x' else (.09,.07,length),'wood')

def barrel(x,z):
 cylinder('steel drum',(x,.43,z),.30,.86,'teal',12)
 for h in (.12,.70):cylinder('drum hoop',(x,h,z),.315,.045,'metal',12)

def bench(x,z):
 for dx in (-.8,.8):box('bench leg',(x+dx,.3,z),(.1,.6,.42),'metal')
 for dz in (-.16,0,.16):box('bench seat',(x,.61,z+dz),(2.0,.07,.13),'woodlight')
 for h in (.90,1.12):box('bench back',(x,h,z-.23),(2.0,.15,.07),'woodlight')

def shrub(x,z,s=1):
 for dx,dy,dz in [(-.4,.5,0),(.4,.6,.2),(0,.8,-.2)]:
  ico('garden shrub',(x+dx*s,dy*s,z+dz*s),(.6*s,.6*s,.65*s),'green',1,0)

def tree(kind):
 if kind=='oak':
  beam('oak trunk',(0,0,0),(.13,3.9,.12),.22)
  clusters=[(-1.2,3.6,.2),(1.1,3.9,0),(0,4.7,0),(-.4,3.8,1.2),(.4,3.7,-1.15),(.9,4.8,.8),(-.8,5.0,-.6)]
  for x,y,z in clusters:
   beam('oak limb',(.05,2.1,0),(x*.75,y-.4,z*.75),.11)
   ico('oak crown',(x,y,z),(1.25,1.25,1.20),(.17,.29,.085),2)
 elif kind=='poplar':
  beam('poplar trunk',(0,0,0),(.12,6.7,0),.15,'woodlight')
  for i in range(7):
   a=i*2.4;r=.55 if i<5 else .28
   ico('poplar leaves',(math.cos(a)*r,2.5+i*.65,math.sin(a)*r),(.72,1.25,.75),(.27,.36,.12),1)
 elif kind=='pine':
  beam('pine trunk',(0,0,0),(0,7.3,0),.19)
  for level in range(5):
   y=2.3+level*.85;r=1.45-level*.22
   for k in range(5):
    a=k*math.tau/5+level*.8
    x,z=math.cos(a)*r*.55,math.sin(a)*r*.55
    beam('pine bough',(0,y-.2,0),(x,y,z),.06)
    ico('pine sprays',(x,y+.25,z),(r*.67,.65,r*.67),(.11,.22,.13),1)
  ico('pine leader',(0,7,0),(.45,.85,.45),(.13,.25,.13),1)
 else:shrub(0,0)

def gasstation():
 anchor('PumpAnchor',(1.25,0,0))
 anchor('ShopAnchor',(0,0,-6.4))
 # Interaction anchors intentionally match Main: pump (0,0,0), shop (0,0,-6.4).
 box('forecourt',(0,-.12,-2),(15,.3,19),'concrete')
 collision('forecourt',(0,-.12,-2),(15,.3,19))
 for x in (-5,5):
  for z in (-2.8,2.8):
   box('canopy footing',(x,.14,z),(.62,.28,.62),'stone')
   box('cream canopy column',(x,2.10,z),(.25,4.2,.25),'cream')
   box('column painted base',(x,.48,z),(.27,.65,.27),'red')
   collision('column',(x,2.10,z),(.25,4.2,.25))
 box('canopy soffit',(0,4.15,0),(12,.16,8),'cream')
 box('red canopy fascia',(0,4.43,0),(12.3,.43,8.3),'red',.04)
 box('white fascia stripe',(0,4.28,0),(12.34,.045,8.34),'white')
 box('canopy top',(0,4.69,0),(12.4,.10,8.4),'stone')
 text('canopy branding','JADRAN  /  BENZIN',(0,4.48,4.17),.31)
 text('roadside canopy branding','JADRAN',(6.17,4.48,0),.31,facing='side')
 for x in (-2.6,2.6):box('ceiling lamp',(x,4.04,0),(1.2,.06,.28),'white')
 for z in (0,2.0):
  box('pump island',(0,.12,z),(2.5,.24,1.10),'stone',.06)
  box('red pump pedestal',(0,.67,z),(.65,1.10,.48),'red',.04)
  box('pump enamel head',(0,1.52,z),(.82,.70,.53),'white',.04)
  box('analog readout',(0,1.60,z+.28),(.61,.24,.025),'dark')
  text('fuel digits','0 0 0 . 0',(0,1.60,z+.300),.10,'cream')
  text('pump label','SUPER  95',(0,1.31,z+.29),.09,'red')
  for side in (-1,1):
   pipe('rubber fuel hose',[(side*.39,1.60,z),(side*.67,1.35,z),(side*.72,.45,z),
          (side*.55,.33,z+.04),(side*.46,.72,z+.13),(side*.43,1.20,z+.20)],.032,'dark',6)
   box('fuel nozzle',(side*.43,1.20,z+.20),(.08,.24,.09),'metal')
  collision('pump',(0,.94,z),(.85,1.88,.62))
  for x in (-1.0,1.0):cylinder('safety bollard',(x,.43,z),.07,.86,'red')
 # Small roadside shop, front door remains at the existing interaction point.
 house(0,-10,9,6,1,'cream')
 box('shop sign board',(0,2.65,-6.93),(5.6,.45,.13),'red')
 text('shop sign','PRODAVNICA',(0,2.66,-6.84),.32)
 bench(-3,-5.8);barrel(4.3,-5.8)
 for x in (-3.3,3.3):
  box('shop planter',(x,.24,-6.0),(1.4,.45,.60),'brick')
  shrub(x,-6.0,.55)
 # Price pylon beside the entrance, not across the drivable approach.
 cylinder('price sign post',(6.1,2.8,4.2),.085,5.6,'metal')
 box('price sign',(6.1,4.8,4.2),(1.9,1.65,.15),'red')
 text('price brand','JADRAN',(6.1,5.32,4.29),.25)
 for caption,h in [('BENZIN  95',4.92),('DIZEL',4.55),('00 - 24',4.17)]:text('price text',caption,(6.1,h,4.29),.18)
 for x in (-6.0,6.0):
  for z in (-2,1,4):box('forecourt paint',(x,.037,z),(.08,.006,1.5),'white')

def busstop(x,z):
 box('stop slab',(x,.08,z),(3.8,.16,1.9),'concrete')
 for dx in (-1.7,1.7):box('shelter frame',(x+dx,1.3,z),(.08,2.6,1.65),'teal')
 box('shelter back',(x,1.4,z-.8),(3.5,2.1,.10),'cream')
 box('shelter roof',(x,2.65,z),(4.1,.18,2.2),'teal')
 bench(x,z-.3)
 box('timetable',(x+1.0,1.7,z-.73),(.35,.5,.03),'white')
 for h in (1.55,1.65,1.75,1.85):box('timetable line',(x+1.0,h,z-.71),(.26,.012,.01),'dark')
 text('bus sign','AUTOBUS',(x,2.34,z-.73),.26,'teal')

def village():
 # All houses stay to local -X (away from the road at local +16).
 house(0,-1,6.0,5.0,1,'cream')
 house(-9,-7,6.4,5.6,2,'plaster')
 house(-7,8,5.6,4.8,1,'ochre')
 box('motel sign',(-7,2.55,10.49),(3.4,.42,.10),'teal')
 text('motel lettering','MOTEL',(-7,2.55,10.55),.30)
 anchor('SleepAnchor',(-7,0,11.5))
 anchor('SellAnchor',(0,0,2.6))
 for x,z,l in [(-3.8,4.5,3.0),(.8,4.5,3),(-11,12,7.5)]:fence(x,z,l)
 fence(4.2,-4,8.5,'z')
 for x,z in [(-4.0,3),(3.3,2),(-12,5),(-10,11)]:shrub(x,z,1.1)
 barrel(-4,-3);bench(-2,4)
 # Washing line, small courtyard details, rather than more empty houses.
 for x in (-11,-6):cylinder('washing pole',(x,2.1,3),.035,4.2,'metal',6)
 pipe('washing line',[(-11,3.2,3),(-8.5,3.0,3),(-6,3.2,3)],.012,'dark',4)
 for i,c in enumerate(['white','teal','ochre','white']):
  box('laundry',(-10.3+i*.95,2.65,3),(.6,.8,.018),c)
 busstop(5.5,6.8)

def farm():
 house(-5,-4,5.7,5,1,'cream')
 # Timber barn with large double doors and a visible structural frame.
 box('barn plinth',(0,-.2,6),(8,.6,6),'stone')
 box('barn walls',(0,2.2,6),(8,4.4,6),'wood')
 collision('barn',(0,2.2,6),(8,4.4,6))
 roof(0,6,8,6,4.4,1.8)
 for x in range(-4,5):box('barn board',(x,2.15,9.03),(.04,4.2,.04),'woodlight')
 for x in (-1.0,1.0):
  box('barn door',(x,1.65,9.08),(1.9,3.3,.10),'woodlight')
  pipe('door diagonal',[(x-.8,.2,9.15),(x+.8,3.1,9.15)],.045,'wood')
 cylinder('grain silo',(6,3.2,5),1.45,6.4,'metal',16)
 collision('silo',(6,3.2,5),(2.85,6.4,2.85))
 cylinder('silo conical cap',(6,6.9,5),1.55,1.1,'stone',16,0)
 for h in (1.2,2.4,3.6,4.8,6.0):cylinder('silo steel band',(6,h,5),1.47,.045,'stone',16)
 for x,z in [(-5,6),(-5,7.1),(-5.8,6.4)]:
  ob=cylinder('hay bale',(x,.55,z),.55,1.0,'hay',12);ob.rotation_euler[1]=math.pi/2
 fence(-5,11,11);barrel(4,0)

def ruins():
 for x in (-3.5,3.5):
  mesh('broken wall',[(x-.22,-.5,-3),(x+.22,-.5,-3),(x+.22,3.5,-3),(x-.22,3.5,-3),
     (x-.22,-.5,3),(x+.22,-.5,3),(x+.22,1.3,3),(x-.22,1.3,3)],
     [(0,1,2,3),(4,7,6,5),(0,4,5,1),(3,2,6,7),(1,5,6,2),(0,3,7,4)],'stone')
  collision('wall',(x,1.3,0),(.44,2.6,6))
 box('back ruined wall',(0,1.6,-3),(7,3.2,.45),'plaster')
 collision('back wall',(0,1.6,-3),(7,3.2,.45))
 for i in range(22):
  x=random.uniform(-3,4);z=random.uniform(-2,4)
  ob=box('fallen brick',(x,.1,z),(random.uniform(.2,.6),.22,.3),random.choice(['brick','stone']))
  ob.rotation_euler[2]=random.uniform(0,6.28)
 for x in (-2,-.5,1,2.5):beam('collapsed roof beam',(x,2.5,-2.8),(x+.6,.12,2.5),.11)
 for x,z in [(-4,1),(4,-1),(2,3)]:shrub(x,z)
 # Partial window frame and exposed brickwork make the ruin recognisable.
 box('empty window recess',(-1,2.0,-2.74),(1.2,1.1,.02),'dark')
 for x in (-1.65,-.35):box('window stone surround',(x,2.0,-2.71),(.14,1.4,.13),'cream')

def checkpoint():
 house(0,0,3.2,3.2,1,'plaster')
 box('control sign',(0,2.55,1.68),(2.6,.4,.10),'blue')
 text('control lettering','KONTROLA',(0,2.56,1.75),.24)
 for x in (-3.0,3.0):
  box('concrete barrier',(x,.48,2.5),(1.8,.96,.55),'concrete',.08)
  for dx in (-.55,0,.55):box('barrier stripe',(x+dx,.5,2.79),(.18,.7,.02),'white')
 cylinder('flag pole',(-2.3,3.4,-1.4),.04,6.8,'metal',6)
 box('flag',(-1.85,6.2,-1.4),(.9,.55,.025),'red')

def bake_export(name):
 objects=list(current.objects)
 anchors=[o for o in objects if o.type=='EMPTY']
 visuals=[o for o in objects if o not in colliders and o not in anchors]
 bpy.ops.object.select_all(action='DESELECT')
 for ob in visuals:ob.select_set(True)
 bpy.context.view_layer.objects.active=visuals[0]
 bpy.ops.object.convert(target='MESH');bpy.ops.object.join()
 merged=bpy.context.object;merged.name=name+'_visual'
 bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 merged.data.materials.clear();merged.data.materials.append(surface)
 for polygon in merged.data.polygons:polygon.material_index=0
 merged.data.calc_loop_triangles()
 statistics[name]={'triangles':len(merged.data.loop_triangles),'colliders':len(colliders),'surfaces':1}
 for ob in colliders+anchors:ob.select_set(True)
 bpy.ops.export_scene.gltf(filepath=str(OUT/(name+'.glb')),export_format='GLB',use_selection=True,
  export_yup=True,export_apply=True,export_cameras=False,export_lights=False,export_all_vertex_colors=True)
 return merged

builders={'gasstation':gasstation,'village':village,'farm':farm,'ruins':ruins,'checkpoint':checkpoint,
          'tree_oak':lambda:tree('oak'),'tree_poplar':lambda:tree('poplar'),
          'tree_pine':lambda:tree('pine'),'shrub':lambda:tree('shrub')}
collections={}
only=next((s.split('=',1)[1] for s in sys.argv if s.startswith('--only=')),None)
preview=next((s.split('=',1)[1] for s in sys.argv if s.startswith('--preview=')),None)
if (SHOTS/'geometry.json').exists():
 statistics=json.loads((SHOTS/'geometry.json').read_text(encoding='utf8'))
for name,builder in builders.items():
 if preview or (only and name!=only):continue
 coll=collection(name);collections[name]=coll;builder()
 # Keep each editable asset as its own source scene, with collisions hidden only in renders.
 bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')))
 bake_export(name)
 # Prevent all preceding assets entering the next saved scene or render.
 for ob in list(coll.objects):bpy.data.objects.remove(ob,do_unlink=True)
 bpy.data.collections.remove(coll)

(SHOTS/'geometry.json').write_text(json.dumps(statistics,indent=2)+'\n',encoding='utf8')
print('WORLD_ASSETS',json.dumps(statistics))

requested=preview or next((s.split('=',1)[1] for s in sys.argv if s.startswith('--render=')),None)
if requested:
 bpy.ops.wm.open_mainfile(filepath=str(SOURCE/(requested+'.blend')))
 surface=bpy.data.materials['Painted plaster, timber and foliage - vertex colours']
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
 scene.render.resolution_x=1440;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
 current=collection('studio')
 box('ground',(0,-.55,0),(200,.1,200),'grass')
 scene.world.use_nodes=True
 scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.55,.64,.74,1)
 scene.world.node_tree.nodes['Background'].inputs[1].default_value=.6
 for loc,power,size in [((-10,18,10),2200,12),((10,12,-8),1800,10)]:
  data=bpy.data.lights.new('softbox','AREA');data.energy=power;data.shape='DISK';data.size=size
  ob=bpy.data.objects.new('softbox',data);current.objects.link(ob);ob.location=xyz(loc)
  ob.rotation_euler=(Vector(xyz((0,1,0)))-ob.location).to_track_quat('-Z','Y').to_euler()
 data=bpy.data.cameras.new('Review');cam=bpy.data.objects.new('Review',data);current.objects.link(cam);scene.camera=cam
 pos=(20,14,23);target=(-1,1,-3);scale=32
 if requested=='village':pos=(24,18,25);target=(-4,2,1);scale=36
 if requested.startswith('tree') or requested=='shrub':pos=(9,6,10);target=(0,3,0);scale=10
 cam.location=xyz(pos);cam.rotation_euler=(Vector(xyz(target))-cam.location).to_track_quat('-Z','Y').to_euler()
 data.type='ORTHO';data.ortho_scale=scale;data.clip_end=500
 scene.render.filepath=str(SHOTS/(requested+'-studio.png'));bpy.ops.render.render(write_still=True)
print('WORLD_PACK_COMPLETE')
