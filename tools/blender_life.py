"""Five static visual replacements. Animated wolf is owned by blender_wolf.py.
Blender --background --python tools/blender_life.py
"""
import sys
import runpy
import math
import json
from pathlib import Path
from mathutils import Vector

saved_args=sys.argv[:];sys.argv=['blender','--only=__helpers__']
kit=runpy.run_path(str(Path(__file__).with_name('blender_world.py')))
sys.argv=saved_args
globals().update({k:kit[k] for k in ('bpy','box','mesh','beam','cylinder','ico','collection','bake_export','register','xyz','mat','palette','ROOT','OUT')})
context=collection.__globals__
SOURCE=ROOT/'art/blender/life';SOURCE.mkdir(parents=True,exist_ok=True)
palette.update({'fur':(.29,.20,.115),'furcream':(.55,.43,.28),'nose':(.025,.024,.021),
 'wolf':(.30,.32,.30),'ruff':(.43,.43,.37),'saddle':(.16,.18,.17),'rust':(.35,.16,.075)})

def oval(name,p,r,color,n=10,rings=5):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=n,ring_count=rings,radius=1,location=xyz(p))
 ob=bpy.context.object;ob.scale=(r[0],r[2],r[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 return register(ob,name,color)

def resident():
 # Reuse our authored human anatomy, not a stack of placeholder boxes.
 bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/blender/characters/traveller.blend'))
 bpy.context.view_layer.update()
 objects=[o for o in bpy.data.objects if o.type=='MESH']
 transforms={o:o.matrix_world.copy() for o in objects}
 for ob in objects:
  ob.parent=None;ob.matrix_world=transforms[ob]
 for ob in list(bpy.data.objects):
  if ob.type!='MESH':bpy.data.objects.remove(ob,do_unlink=True)
 collection('resident')
 context['surface']=mat('Resident vertex palette',True)
 for ob in objects:
  for coll in list(ob.users_collection):coll.objects.unlink(ob)
  context['current'].objects.link(ob)
  ob.data.materials.clear();ob.data.materials.append(context['surface'])
  # A worn brown jacket and grey hair distinguish the local from the traveller.
  col=ob.data.color_attributes.get('Color')
  if col:
   target=None
   if any(s in ob.name for s in ('jacket','sleeve','pocket','collar')):target=(.24,.18,.105,1)
   if any(s in ob.name for s in ('hair','sideburn','eyebrow')):target=(.27,.26,.22,1)
   if target:
    for entry in col.data:entry.color=target
 oval('wool cap',(0,1.77,.015),(.105,.07,.105),'wood',12,5)
 box('cap visor',(0,1.765,-.105),(.18,.025,.13),'woodlight',.012)

def canine(wolf=False):
 color='wolf' if wolf else 'fur';light='ruff' if wolf else 'furcream'
 # Shoulder, ribcage and tucked abdomen; muzzle points -Z.
 oval('ribcage',(0,.65,0),(.20,.24,.43),color)
 oval('tucked waist',(0,.66,.30),(.15,.16,.23),color)
 oval('haunches',(0,.64,.43),(.19,.22,.18),color)
 oval('chest',(0,.66,-.31),(.19,.27,.20),light)
 beam('raised neck',(0,.62,-.32),(0,.94,-.48),.17,color,10)
 oval('head',(0,.99,-.50),(.145,.17,.205),color)
 oval('cheeks',(0,.91,-.55),(.15,.095,.13),light)
 oval('long muzzle',(0,.955,-.715),(.09,.075,.19),light)
 oval('black nose',(0,.975,-.88),(.065,.043,.038),'nose',8,4)
 box('mouth line',(0,.922,-.74),(.145,.012,.21),'nose')
 for side in (-1,1):
  oval('eye',(side*.12,1.025,-.61),(.018,.024,.029),'nose',8,4)
  oval('eye glint',(side*.132,1.032,-.623),(.005,.008,.008),'hay',8,3)
  if wolf:
   pts=[(side*.045,1.09,-.53),(side*.155,1.09,-.47),(side*.13,1.32,-.43),
        (side*.055,1.09,-.39),(side*.165,1.09,-.37)]
   mesh('alert pointed ear',pts,[(0,1,2),(1,4,2),(4,3,2),(3,0,2),(0,3,4,1)],color)
   mesh('ear inner',[(side*.067,1.12,-.52),(side*.137,1.12,-.48),(side*.126,1.27,-.447)],[(0,1,2)],'saddle')
  else:
   oval('folded ear',(side*.15,1.015,-.43),(.055,.16,.09),'wood')
  # Forelegs straight, hind legs with visible hock and forward-facing paws.
  x=side*.145
  beam('front upper leg',(x,.69,-.29),(x,.36,-.31),.075,color,8)
  beam('front shin',(x,.37,-.31),(x,.085,-.34),.044,light,8)
  oval('front paw',(x,.06,-.385),(.065,.06,.105),light,8,4)
  oval('hind thigh',(x,.54,.40),(.095,.19,.14),color)
  beam('hind calf',(x,.43,.33),(x,.25,.53),.06,color,8)
  beam('hind hock',(x,.25,.53),(x,.07,.45),.035,light,8)
  oval('hind paw',(x,.06,.40),(.06,.06,.10),light,8,4)
 if wolf:
  oval('dark back saddle',(0,.82,.04),(.16,.09,.39),'saddle')
  beam('heavy tail',(0,.72,.55),(0,.43,.91),.095,color,8)
  beam('tail tip',(0,.43,.91),(0,.24,1.02),.07,'saddle',8)
  for side in (-1,1):
   mesh('cheek fur',[(side*.12,.96,-.46),(side*.23,.87,-.34),(side*.13,.82,-.46)],[(0,1,2)],light)
 else:
  beam('tail',(0,.71,.54),(0,.84,.86),.055,color,8)
  beam('tail tip',(0,.84,.86),(0,1.02,.86),.038,light,8)
  # Broad red collar, small metal tag.
  beam('old collar',(0,.80,-.405),(0,.85,-.43),.169,'red',10)
  box('collar tag',(0,.75,-.53),(.055,.065,.025),'metal',.006)

def rockslide():
 for i,(x,y,z,s) in enumerate([(-1.7,.53,-.1,.8),(-.6,.69,.25,1),( .6,.48,-.2,.8),(1.65,.58,.25,.9),(-.15,1.12,.3,.75)]):
  ico('fractured limestone',(x,y,z),(s,s*.78,s*.7),'stone' if i%2 else 'concrete',1,0)
 for i in range(11):
  x=-2.1+i*.4;z=-.65+(i%3)*.56
  ico('rock fragments',(x,.13,z),(.24,.19,.22),'stone',1,0)
 bpy.context.view_layer.update()
 for ob in context['current'].objects:
  ob.location.z-=min((ob.matrix_world@Vector(c)).z for c in ob.bound_box)

def roadblock():
 for x in (-3,3):
  box('concrete footing',(x,.17,0),(.62,.34,.65),'concrete',.035)
  box('weathered post',(x,1.2,0),(.24,2.4,.26),'wood',.025)
  box('post cap',(x,2.41,0),(.30,.08,.32),'rust')
 for h in (.82,1.32):
  box('barrier plank',(0,h,-.19),(6.4,.24,.13),'red')
  for x in (-2.7,-1.8,-.9,0,.9,1.8,2.7):box('faded warning stripe',(x,h,-.261),(.32,.235,.012),'cream')
 for x in (-3,3):
  for h in (.82,1.32):cylinder('bolt',(x,h,-.27),.035,.02,'metal',8).rotation_euler[0]=math.pi/2
 box('warning plate',(0,1.82,-.20),(1.05,.52,.06),'rust',.02)
 box('warning pale field',(0,1.82,-.235),(.93,.41,.012),'cream')
 box('warning horizontal bar',(0,1.82,-.25),(.65,.10,.012),'red')

def crate():
 box('dark inner box',(0,.35,0),(.83,.64,.63),'wood')
 for i in range(5):
  x=-.36+i*.18
  box('lid plank',(x,.672,0),(.17,.055,.70),'woodlight')
  for z in (-.34,.34):box('side plank',(x,.35,z),(.17,.60,.045),'woodlight' if i%2 else 'wood')
 for x in (-.44,.44):
  for i in range(4):box('end board',(x,.12+i*.155,0),(.045,.145,.67),'woodlight')
 for x in (-.35,.35):
  for z in (-.375,.375):box('corner batten',(x,.35,z),(.08,.7,.05),'wood')
  box('lid batten',(x,.72,0),(.08,.045,.69),'wood')
 for z in (-.407,.407):
  beam('diagonal brace',(-.29,.10,z),(.29,.61,z),.035,'wood',4)
  for x in (-.35,.35):
   for h in (.12,.59):box('iron nail',(x,h,z),(.018,.018,.009),'metal')

stats={}
wolf_stats=SOURCE/'wolf_geometry.json'
if wolf_stats.exists():
 data=json.loads(wolf_stats.read_text(encoding='utf8'))
 stats['wolf']={k:data[k] for k in ('triangles','size_x_y_z')}
for name,builder in [('resident',resident),('dog',lambda:canine(False)),
                     ('rockslide',rockslide),('roadblock',roadblock),('wooden_crate',crate)]:
 collection(name);builder()
 scale=.8 if name=='dog' else 1.0
 for ob in context['current'].objects:
  ob.location*=scale;ob.scale*=scale
 bpy.context.view_layer.update()
 bottom=min((ob.matrix_world@Vector(c)).z for ob in context['current'].objects for c in ob.bound_box)
 for ob in context['current'].objects:ob.location.z-=bottom
 bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')))
 visual=bake_export(name)
 visual.data.calc_loop_triangles()
 stats[name]={'triangles':len(visual.data.loop_triangles),'size_x_y_z':[visual.dimensions.x,visual.dimensions.z,visual.dimensions.y]}
 for ob in list(bpy.data.objects):bpy.data.objects.remove(ob,do_unlink=True)
(SOURCE/'geometry.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf8')
print('LIFE_ASSETS',json.dumps(stats))
