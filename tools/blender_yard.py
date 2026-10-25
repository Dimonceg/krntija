"""Balkan yard kit and building variants. Original geometry, metres, front -Z.
blender --background --python tools/blender_yard.py
"""
import sys
import runpy
import math
import json
from pathlib import Path
from mathutils import Vector

args=sys.argv[:];sys.argv=['blender','--only=__helpers__']
kit=runpy.run_path(str(Path(__file__).with_name('blender_world.py')));sys.argv=args
globals().update({k:kit[k] for k in ('bpy','box','mesh','beam','cylinder','collision','collection','bake_export','register','xyz','palette','ROOT')})
context=collection.__globals__
SOURCE=ROOT/'art/blender/yard';SOURCE.mkdir(parents=True,exist_ok=True)
palette.update({'rust':(.35,.16,.075),'faded':(.30,.31,.26),'rubber':(.045,.05,.045)})

def solid(name,p,size,color):
 box(name,p,size,color);collision(name,p,size)

def shell(w,d,h,color):
 # 1.5 m open doorway, same position for every variant; no threshold collider.
 for x in (-w/2+.12,w/2-.12):solid('side wall',(x,h/2,0),(.24,h,d),color)
 solid('back wall',(0,h/2,d/2-.12),(w,h,.24),color)
 wing=(w-1.5)/2
 for s in (-1,1):solid('front pier',(s*(.75+wing/2),h/2,-d/2+.12),(wing,h,.24),color)
 solid('lintel',(0,h-.18,-d/2+.12),(1.5,.36,.24),color)

def sloped_roof(w,d,low,high,broken=False):
 for j in range(8):
  z=-d/2+j*d/8
  if broken and j==5:continue
  mesh('corrugated sheet',[(-w/2,high,z),(w/2,low,z),(w/2,low,z+d/8-.02),(-w/2,high,z+d/8-.02)],[(0,1,2,3)],'rust' if j%3 else 'faded')
  beam('raised seam',(-w/2,high+.02,z),(w/2,low+.02,z),.022,'metal',4)
 for x,h in [(-w/2,high),(w/2,low)]:beam('eaves',(x,h,-d/2),(x,h,d/2),.045,'wood',4)

def gable(w,d,h,rise,broken=False):
 for s in (-1,1):
  for j in range(8):
   z=-d/2+j*d/8
   if broken and s==1 and j in (3,4,5):continue
   mesh('clay roof panel',[(0,h+rise,z),(s*w/2,h,z),(s*w/2,h,z+d/8-.02),(0,h+rise,z+d/8-.02)],[(0,1,2,3)],'roof' if j%3 else 'tile')
  for z in (-d/2,0,d/2):beam('roof rafter',(0,h+rise,z),(s*w/2,h,z),.055,'wood',4)
 beam('ridge',(0,h+rise,-d/2),(0,h+rise,d/2),.07,'tile',6)

def window(x,z,y=1.8,broken=False):
 box('window void',(x,y,z),(1.25,1.15,.035),'dark')
 for dx in (-.68,.68):box('window jamb',(x+dx,y,z-.05),(.10,1.35,.10),'cream')
 box('window sill',(x,y-.63,z-.09),(1.5,.12,.22),'stone')
 if broken:
  beam('diagonal shutter',(x-.62,y-.43,z-.11),(x+.62,y+.38,z-.11),.07,'woodlight',4)
 else:
  for dy in (-.3,0,.3):box('shutter board',(x,y+dy,z-.08),(1.28,.17,.055),'teal')

def shed_a():
 shell(4.5,3.4,2.6,'wood')
 sloped_roof(5,3.8,2.66,3.4)
 mesh('upper siding',[(-2.25,2.6,-1.7),(2.25,2.6,-1.7),(-2.25,3.33,-1.7)],[(0,1,2)],'woodlight')
 for x in (-2.1,-1.8,-1.5,-1.2,1.2,1.5,1.8,2.1):box('vertical board',(x,1.3,-1.73),(.20,2.5,.045),'woodlight')
 box('slid door',(-1.45,1.15,-1.80),(1.3,2.3,.08),'teal')
 beam('door brace',(-2,.2,-1.87),(-.9,2.1,-1.87),.05,'wood',4)
 box('door rail',(-.5,2.42,-1.82),(3.4,.06,.07),'rust')
 box('side patched sheet',(2.26,1.05,.1),(.03,1.8,1.4),'faded')

def shed_b():
 shell(4.4,3.4,2.65,'brick')
 gable(4.95,3.8,2.7,.95,True)
 for s in (-1,1):
  mesh('gable masonry',[(-2.2,2.65,s*1.7),(2.2,2.65,s*1.7),(0,3.57,s*1.7)],[(0,1,2)],'plaster')
 window(-1.4,-1.735,1.7,True)
 for y in (.3,.65,1,1.35,1.7,2.05,2.4):box('brick course',(1.5,y,-1.725),(1.15,.025,.018),'stone')
 box('old enamel plate',(1.35,2.25,-1.78),(.5,.30,.045),'teal')
 for x in (-2.1,2.1):box('plaster remnant',(x,1.6,-1.74),(.2,1.9,.03),'cream')

def abandoned_a():
 # Recessed main facade leaves room for a porch inside the same 10 x 8 m footprint.
 shell(8.6,5.6,2.85,'ochre')
 gable(9.5,6.2,2.9,1.55)
 for x in (-2.8,2.8):window(x,-2.835,1.75)
 for x in (-1.8,1.8):solid('porch post',(x,1.2,-3.6),(.14,2.4,.14),'wood')
 mesh('porch roof',[(-2.2,2.62,-2.7),(2.2,2.62,-2.7),(2.2,2.4,-3.85),(-2.2,2.4,-3.85)],[(0,1,2,3)],'rust')
 for x in (-1.7,1.7):box('porch low rail',(x,.8,-3.6),(.65,.09,.09),'woodlight')
 solid('chimney',(-2.5,3.9,1),(.6,2.2,.6),'brick')
 for i in range(12):box('exposed brick',(-4.05+(i%6)*.25,.3+(i//6)*.14,-2.83),(.23,.12,.02),'brick')
 for z in (-2.4,2.4):box('wall base stain',(4.31,.36,z),(.025,.7,.55),'faded')

def abandoned_b():
 shell(9,7,3,'stone')
 gable(9.7,7.7,3.05,1.6,True)
 for x in (-2.9,2.9):window(x,-3.535,1.8,True)
 mesh('broken gable',[(-4.5,3,-3.5),(4.5,3,-3.5),(2.1,3.4,-3.5),(0,4.6,-3.5),(-2.7,3.3,-3.5)],[(0,1,2,3,4)],'plaster')
 for x in (-4.3,4.3):box('burnt timber',(x,1.5,-3.55),(.12,3,.09),'dark')
 for x in (-3.8,-3.2,-2.6,2.5,3.1,3.7):
  box('remaining plaster',(x,2,-3.535),(.42,1.7,.02),'plaster')
 beam('fallen rafter',(-3.8,.14,.4),(-1.7,.14,2.7),.1,'dark',4)
 solid('broken chimney',(2.6,3.5,1.8),(.65,1.6,.65),'brick')

def rusty_barrel():
 cylinder('drum',(0,.45,0),.30,.9,'rust',16)
 for y in (.04,.27,.65,.88):cylinder('drum rib',(0,y,0),.313,.035,'metal',16)
 cylinder('lid',(0,.91,0),.29,.025,'faded',16)
 cylinder('filler cap',(.15,.936,.06),.046,.025,'dark',8)
 for x in (-.12,.02,.16):box('faded paint',(x,.50,-.285),(.075,.28,.02),'teal')

def tyre_stack():
 for i in range(3):
  bpy.ops.mesh.primitive_torus_add(major_segments=20,minor_segments=6,major_radius=.25,minor_radius=.095,location=xyz((.025*(i%2),.10+i*.19,0)))
  register(bpy.context.object,'discarded tyre','rubber')
  for j in range(10):
   a=j*math.tau/10
   ob=box('worn tread',(.025*(i%2)+math.sin(a)*.341,.10+i*.19,math.cos(a)*.341),(.05,.10,.022),'dark')
   ob.rotation_euler[2]=-a

def broken_bench():
 for x in (-.8,.8):
  box('bench foot',(x,.04,0),(.22,.08,.6),'rust')
  box('bench leg',(x,.27,0),(.08,.5,.08),'metal')
  box('back upright',(x,.70,.24),(.06,.8,.06),'rust')
 for z in (-.19,0,.19):box('seat plank',(0,.52,z),(2.1,.07,.15),'woodlight')
 box('remaining back plank',(0,.89,.25),(2.1,.15,.07),'wood')
 box('broken back plank',(-.58,1.08,.25),(.94,.13,.07),'woodlight')

def wooden_pallet():
 for x in (-.44,0,.44):box('pallet runner',(x,.075,0),(.13,.15,.8),'wood')
 for i in range(6):box('pallet deck',(0,.18,-.35+i*.14),(1.15,.06,.12),'woodlight' if i%2 else 'wood')
 for x in (-.44,.44):
  for z in (-.35,.35):box('nail',(x,.212,z),(.02,.008,.02),'metal')

def scrap_heap():
 for i in range(5):
  ob=box('bent rusty sheet',(-.4+i*.20,.06+i*.06,0),(.55,.035,.8),'rust' if i%2 else 'metal')
  ob.rotation_euler[2]=i*.36
 for x in (-.40,.1,.45):beam('scrap tube',(x,.1,-.45),(x+.13,.42,.4),.06,'rust',8)

def warning_sign():
 box('rusted sign post',(0,.85,0),(.065,1.7,.065),'rust')
 # Readable warning symbol, no invented gameplay information.
 mesh('triangular plate',[(-.40,1.3,-.05),(.40,1.3,-.05),(0,2,-.05)],[(0,1,2)],'red')
 mesh('warning field',[(-.31,1.35,-.055),(.31,1.35,-.055),(0,1.89,-.055)],[(0,1,2)],'cream')
 box('exclamation',(0,1.63,-.07),(.055,.22,.02),'dark')
 box('warning dot',(0,1.44,-.07),(.055,.055,.02),'dark')

stats={}
builders={'abandoned_a':abandoned_a,'abandoned_b':abandoned_b,'shed_a':shed_a,'shed_b':shed_b,
 'rusty_barrel':rusty_barrel,'tyre_stack':tyre_stack,'broken_bench':broken_bench,
 'wooden_pallet':wooden_pallet,'scrap_heap':scrap_heap,'warning_sign':warning_sign}
for name,builder in builders.items():
 collection(name);builder();bpy.context.view_layer.update()
 # All geometry rests on Y=0, including rotated scrap.
 bottom=min((ob.matrix_world@Vector(c)).z for ob in context['current'].objects for c in ob.bound_box)
 for ob in context['current'].objects:ob.location.z-=bottom
 bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')))
 visual=bake_export(name);visual.data.calc_loop_triangles()
 stats[name]={'triangles':len(visual.data.loop_triangles),'size_xyz':[visual.dimensions.x,visual.dimensions.z,visual.dimensions.y],
              'colliders':len(context['colliders'])}
 for ob in list(bpy.data.objects):bpy.data.objects.remove(ob,do_unlink=True)
(SOURCE/'geometry.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf8')
print('YARD_ASSETS',json.dumps(stats))
