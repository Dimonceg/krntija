"""Original abandoned Balkan places; metres, front -Z, one palette material.
Run Blender --background --python tools/blender_places.py.
Uses the established world kit's authoring/export helpers without rebuilding it.
"""
import sys
import runpy
import math
import json
from pathlib import Path

args=sys.argv[:]
sys.argv=['blender','--only=__helpers__']
kit=runpy.run_path(str(Path(__file__).with_name('blender_world.py')))
sys.argv=args
globals().update({k:kit[k] for k in ('bpy','box','mesh','beam','cylinder','collision','anchor','collection','bake_export','SOURCE','OUT','SHOTS','palette')})
palette['rust']=(.34,.16,.075)
palette['faded']=(.31,.32,.27)

def solid(name,p,size,color):
 box(name,p,size,color);collision(name,p,size)

def panels(w,d,h,rise,material='rust',damaged=False):
 # Broad sheet panels with raised seams. Missing sheets reveal rafters.
 for side in (-1,1):
  for j in range(8):
   z0=-d/2+j*d/8;z1=z0+d/8-.025
   if damaged and side==1 and j in (2,3):continue
   mesh('weathered roof sheet',[(0,h+rise,z0),(side*w/2,h,z0),
        (side*w/2,h,z1),(0,h+rise,z1)],[(0,1,2,3)],
        material if j%3 else 'faded')
   beam('standing seam',(0,h+rise+.025,z0),(side*w/2,h+.025,z0),.025,'metal',4)
 for z in (-d/2,d/2):
  for side in (-1,1):beam('bargeboard',(0,h+rise,z),(side*w/2,h,z),.06,'wood',4)
 beam('ridge flashing',(0,h+rise+.03,-d/2),(0,h+rise+.03,d/2),.075,'rust',6)

def shell(w,d,h,opening,color):
 # Open front, no invisible box across the entrance. Ground remains terrain.
 for x in (-w/2+.12,w/2-.12):solid('side wall',(x,h/2,0),(.24,h,d),color)
 solid('rear wall',(0,h/2,d/2-.12),(w,h,.24),color)
 wing=(w-opening)/2
 for side in (-1,1):solid('front pier',(side*(opening/2+wing/2),h/2,-d/2+.12),(wing,h,.24),color)
 solid('door lintel',(0,h-.22,-d/2+.12),(opening,.44,.24),color)

def well():
 # Square stone curb with a real dark opening and capstones.
 for x in (-.57,.57):solid('stone curb',(x,.43,0),(.30,.86,1.44),'stone')
 for z in (-.57,.57):solid('stone curb',(0,.43,z),(.84,.86,.30),'stone')
 for x in (-.58,.58):box('capstone',(x,.91,0),(.34,.16,1.5),'cream')
 for z in (-.58,.58):box('capstone',(0,.91,z),(.82,.16,.34),'cream')
 box('well depth',(0,.04,0),(.83,.02,.83),'dark')
 for x in (-.62,.62):
  box('timber upright',(x,1.38,.12),(.13,2.76,.14),'wood')
  beam('roof brace',(x,2.1,.12),(x,2.55,-.48),.045,'woodlight',4)
 beam('winch axle',(-.70,1.85,.12),(.70,1.85,.12),.10,'woodlight',10)
 beam('crank',(.70,1.85,.12),(.70,1.58,.12),.025,'rust',6)
 beam('crank handle',(.70,1.58,.12),(.74,1.58,-.10),.035,'wood',6)
 beam('rope',(0,1.83,.12),(0,.28,.12),.013,'hay',5)
 panels(1.48,1.48,2.55,.40)
 for y in (.23,.49,.73):
  for x in (-.40,0,.40):box('mortar seam',(x,y,-.726),(.36,.018,.012),'concrete')
 anchor('DrinkAnchor',(0,0,-1.12))

def shed():
 shell(4.5,3.5,2.65,1.4,'wood')
 panels(5,3.84,2.7,.75,damaged=True)
 for x in (-2.1,-1.7,-1.3,1.3,1.7,2.1):
  box('uneven boards',(x,1.3,-1.77),(.29,2.55,.065),'faded' if x>0 else 'woodlight')
 for z in (-1.5,-.75,0,.75,1.5):beam('exposed rafter',(-2.2,2.65,z),(0,3.42,z),.065,'wood',4)
 box('sliding door',(-1.43,1.2,-1.83),(1.32,2.4,.10),'teal')
 beam('door brace',(-2.0,.2,-1.91),(-.85,2.2,-1.91),.055,'woodlight',4)
 box('rusty track',(-.4,2.52,-1.9),(3.4,.06,.06),'rust')
 solid('work shelf',(1.7,.8,.6),(.65,1.6,1.5),'wood')

def barn():
 shell(12.8,10.8,4.7,4.0,'wood')
 panels(13.8,11.8,4.75,2.6,damaged=True)
 for x in (-6,-5,-4,-3,3,4,5,6):
  box('faded red siding',(x,2.35,-5.43),(.72,4.55,.06),'red' if int(x)%2 else 'woodlight')
 for x in (-6.25,6.25):
  for z in (-5,0,5):box('stone footing',(x,.35,z),(.5,.7,.65),'stone')
 for z in (-5.2,0,5.2):
  beam('tie beam',(-6.2,4.6,z),(6.2,4.6,z),.12,'woodlight',4)
  for side in (-1,1):beam('roof truss',(side*6.2,4.6,z),(0,7.28,z),.12,'wood',4)
 for side in (-1,1):
  box('slid barn door',(side*3.1,2.05,-5.5),(2,4.1,.14),'red')
  for a,b in [((side*3.1-.85,.3,-5.6),(side*3.1+.85,3.8,-5.6)),
              ((side*3.1+.85,.3,-5.6),(side*3.1-.85,3.8,-5.6))]:beam('door cross brace',a,b,.065,'woodlight',4)
 box('sliding rail',(0,4.3,-5.55),(9,.09,.10),'rust')
 for x,z in [(-4,2),(-4,3.5),(4,3)]:solid('old hay bale',(x,.6,z),(1.6,1.2,1.25),'hay')
 mesh('front gable',[(-6.4,4.7,-5.4),(6.4,4.7,-5.4),(0,7.3,-5.4)],[(0,1,2)],'wood')
 box('loft vent',(0,5.65,-5.43),(1.1,.9,.03),'dark')
 for x in (-.4,-.2,0,.2,.4):box('vent louvre',(x,5.65,-5.46),(.055,.9,.04),'woodlight')

def abandoned():
 shell(9,7,3.0,1.5,'plaster')
 panels(9.8,7.8,3.05,1.8,'roof',True)
 for z in (-3.4,0,3.4):
  for side in (-1,1):beam('exposed roof timber',(side*4.5,3,z),(0,4.8,z),.075,'wood',4)
 for x in (-3,3):
  box('black broken window',(x,1.85,-3.53),(1.35,1.25,.035),'dark')
  for dx in (-.72,.72):box('window jamb',(x+dx,1.85,-3.58),(.12,1.5,.12),'cream')
  box('window sill',(x,1.17,-3.61),(1.65,.13,.27),'stone')
  for y in (1.55,1.93):
   ob=box('boarded window',(x,y,-3.65),(1.5,.16,.06),'woodlight');ob.rotation_euler[1]=.12
 for i in range(18):
  x=-4.25+(i%9)*.28;y=.25+(i//9)*.16
  box('exposed masonry',(x,y,-3.525),(.25,.13,.025),'brick')
 for x in (-4.3,4.3):box('water stain',(x,1.2,-3.53),(.18,2.2,.02),'faded')
 solid('broken chimney',(2.6,3.85,1.8),(.65,2.6,.65),'brick')
 box('fallen door',(2.5,.08,-1.6),(1.0,.12,2),'teal')
 for i in range(8):box('fallen tiles',(-3+i*.35,.04,.6+(i%3)*.27),(.3,.08,.22),'roof')
 mesh('remaining gable',[(-4.5,3,-3.5),(4.5,3,-3.5),(1,4.2,-3.5),(0,4.8,-3.5)],[(0,1,2,3)],'plaster')

stats={}
for name,fn in [('well',well),('shed',shed),('barn',barn),('abandoned',abandoned)]:
 collection(name);fn()
 bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')))
 visual=bake_export(name)
 visual.data.calc_loop_triangles()
 stats[name]={'triangles':len(visual.data.loop_triangles),'dimensions_xyz':list(visual.dimensions),'surfaces':1}
 for ob in list(bpy.data.objects):bpy.data.objects.remove(ob,do_unlink=True)
print('PLACE_ASSETS',json.dumps(stats))
