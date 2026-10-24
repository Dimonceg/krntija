"""Original loot kit; GLBs and transparent inventory renders come from the same geometry.
blender --background --python tools/blender_loot.py [-- --only=jerry_can]
Authoring coordinates: x, height, z. Labels face +Z. Units: metres.
"""
import bpy, math, json, sys
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'game/assets/models/props'; ICONS=OUT/'icons'
SOURCE=ROOT/'art/blender/loot'; SHOTS=ROOT/'docs/screenshots/loot-art'
for path in (OUT,ICONS,SOURCE,SHOTS):path.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
palette={'olive':(.19,.24,.09),'green':(.08,.24,.17),'red':(.48,.035,.025),
 'cream':(.78,.73,.58),'white':(.86,.85,.78),'metal':(.38,.42,.43),
 'steel':(.19,.23,.25),'dark':(.023,.028,.03),'blue':(.055,.18,.29),
 'rubber':(.035,.038,.035),'rust':(.29,.105,.045),'bread':(.45,.21,.065),
 'crumb':(.72,.47,.21),'paper':(.64,.60,.41),'copper':(.49,.25,.10)}

def xyz(p):return (p[0],-p[2],p[1])
def material():
 m=bpy.data.materials.new('Loot vertex palette');m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.68
 vc=m.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color'
 m.node_tree.links.new(vc.outputs['Color'],bs.inputs['Base Color']);return m

def paint(ob,name,color):
 ob.name=name
 layer=ob.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
 for v in layer.data:v.color=(*palette[color],1)
 ob.data.materials.clear();ob.data.materials.append(surface);ob.select_set(False)
 return ob

def box(name,p,size,color,bevel=0):
 bpy.ops.mesh.primitive_cube_add(size=1,location=xyz(p));ob=bpy.context.object
 ob.dimensions=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=ob.modifiers.new('worn edge','BEVEL');mod.width=bevel;mod.segments=1
  bpy.ops.object.modifier_apply(modifier=mod.name)
 return paint(ob,name,color)

def cyl(name,p,r,h,color,n=16,top=None):
 bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r,radius2=r if top is None else top,depth=h,location=xyz(p))
 return paint(bpy.context.object,name,color)

def ring(name,p,r,t,color,vertical=False):
 bpy.ops.mesh.primitive_torus_add(major_segments=24,minor_segments=6,major_radius=r,minor_radius=t,location=xyz(p))
 ob=bpy.context.object
 if vertical:ob.rotation_euler[0]=math.pi/2
 return paint(ob,name,color)

def beam(name,a,b,r,color):
 d=Vector(xyz(b))-Vector(xyz(a));mid=(Vector(xyz(a))+Vector(xyz(b)))*.5
 bpy.ops.mesh.primitive_cylinder_add(vertices=8,radius=r,depth=d.length,location=mid)
 ob=bpy.context.object;ob.rotation_euler=d.to_track_quat('Z','Y').to_euler()
 return paint(ob,name,color)

def label(caption,p,size,color='cream',flat=False):
 curve=bpy.data.curves.new(caption,'FONT');curve.body=caption;curve.align_x='CENTER';curve.align_y='CENTER'
 curve.size=size;curve.resolution_u=1
 ob=bpy.data.objects.new(caption,curve);bpy.context.collection.objects.link(ob);ob.location=xyz(p)
 if not flat:ob.rotation_euler[0]=math.pi/2
 bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.convert(target='MESH')
 return paint(ob,caption,color)

def handle(width,y,z,color):
 for x in (-width/2,width/2):box('handle upright',(x,y-.025,z),(.023,.07,.03),color,.006)
 box('handle grip',(0,y+.014,z),(width+.023,.023,.03),color,.006)

def jerry_can():
 box('pressed steel tank',(0,.195,0),(.32,.39,.17),'olive',.025)
 box('bottom seam',(0,.025,0),(.326,.028,.176),'steel',.006)
 handle(.13,.465,0,'olive')
 cyl('filler neck',(-.105,.405,0),.036,.032,'steel')
 cyl('locking cap',(-.105,.429,0),.039,.018,'olive',12)
 for s in (-1,1):
  beam('pressed diagonal',(-.11,.11,.087*s),(.11,.30,.087*s),.009,'green')
  beam('pressed diagonal',(-.11,.30,.087*s),(.11,.11,.087*s),.009,'green')
 label('20 L',(0,.345,.088),.040)

def motor_oil():
 box('oil jug',(0,.12,0),(.17,.24,.095),'red',.022)
 box('jug shoulder',(-.025,.245,0),(.12,.05,.085),'red',.016)
 cyl('screw cap',(-.045,.282,0),.026,.035,'dark',12)
 box('label',(0,.13,.049),(.125,.14,.005),'cream',.005)
 label('MOTOR',(0,.165,.053),.024,'red');label('15W-40',(0,.113,.053),.021,'dark')
 box('label stripe',(0,.075,.054),(.108,.012,.002),'green')
 handle(.06,.235,-.015,'red')

def water():
 cyl('bottle body',(0,.115,0),.047,.22,'blue',16)
 cyl('bottle shoulder',(0,.236,0),.047,.035,'blue',16,.019)
 cyl('neck',(0,.267,0),.018,.028,'blue',12)
 cyl('blue cap',(0,.289,0),.023,.022,'cream',12)
 cyl('paper wrap',(0,.13,0),.048,.076,'cream',16)
 label('VODA',(0,.13,.049),.020,'blue')
 for h in (.035,.06,.195):ring('bottle rib',(0,h,0),.045,.003,'blue')

def bread():
 bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=1,location=xyz((0,.063,0)))
 ob=bpy.context.object;ob.scale=(.155,.073,.061);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 paint(ob,'crust','bread')
 for x in (-.08,-.025,.03,.085):
  beam('scored crust',(x-.012,.108,-.027),(x+.012,.108,.027),.007,'crumb')

def medkit():
 box('medical tin',(0,.09,0),(.27,.18,.12),'cream',.016)
 box('lid seam',(0,.13,0),(.273,.013,.123),'steel',.003)
 box('cross vertical',(0,.09,.062),(.026,.105,.005),'red')
 box('cross horizontal',(0,.09,.063),(.105,.026,.005),'red')
 handle(.10,.215,0,'steel')
 for x in (-.095,.095):box('clasp',(x,.13,.065),(.022,.043,.009),'metal',.003)

def duct_tape():
 # Hollow annular strip, so the cardboard core remains visible in the icon.
 ring('blue tape',(0,.041,0),.051,.024,'blue')
 ring('cardboard core',(0,.040,0),.030,.006,'paper')
 box('loose tape tab',(.063,.009,.025),(.052,.010,.034),'blue',.002)

def wrench():
 box('forged handle',(0,.014,0),(.026,.025,.24),'metal',.008)
 ring('box end',(0,.015,-.137),.029,.010,'metal')
 box('open jaw bridge',(0,.016,.12),(.075,.028,.033),'metal',.007)
 for x in (-.029,.029):box('jaw',(x,.016,.15),(.019,.028,.060),'metal',.006)
 label('17',(0,.030,0),.018,'steel',True)

def spark_plug():
 cyl('threaded base',(0,.028,0),.018,.055,'steel',12)
 for y in (.009,.017,.025,.033,.041):ring('thread',(0,y,0),.018,.002,'metal')
 cyl('hex nut',(0,.062,0),.025,.021,'metal',6)
 cyl('ceramic',(0,.104,0),.015,.07,'white',12)
 for y in (.087,.10,.113,.126):ring('ceramic rib',(0,y,0),.015,.003,'white')
 cyl('terminal',(0,.15,0),.009,.025,'metal',12)

def cassette():
 box('cassette shell',(0,.009,0),(.15,.018,.095),'dark',.006)
 box('paper label',(0,.019,-.009),(.126,.002,.056),'cream',.004)
 for x in (-.032,.032):
  cyl('reel hole',(x,.022,-.005),.015,.003,'dark',12)
  ring('reel rim',(x,.024,-.005),.010,.003,'white')
 box('tape window',(0,.023,-.005),(.026,.003,.012),'steel')
 label('BALKAN  90',(0,.023,-.032),.011,'red',True)
 for x in (-.065,.065):
  for z in (-.038,.038):cyl('screw',(x,.020,z),.0025,.002,'metal',8)

def cigarettes():
 box('paper packet',(0,.052,0),(.06,.104,.027),'cream',.002)
 box('red packet band',(0,.077,.014),(.061,.038,.002),'red')
 label('DRINA',(0,.077,.016),.012,'white')
 label('20',(0,.025,.015),.012,'dark')

def spare_tire():
 ring('tire carcass',(0,.09,0),.225,.085,'rubber')
 cyl('steel rim',(0,.07,0),.16,.12,'steel',24)
 cyl('hub recess',(0,.134,0),.062,.008,'dark',16)
 for k in range(5):
  a=k*math.tau/5
  cyl('rim vent',(math.cos(a)*.105,.134,math.sin(a)*.105),.025,.010,'dark',12)
  cyl('wheel bolt',(math.cos(a)*.043,.144,math.sin(a)*.043),.009,.014,'metal',6)
 for k in range(32):
  a=k*math.tau/32
  ob=box('tread block',(math.cos(a)*.294,.09,math.sin(a)*.294),(.042,.105,.027),'dark',.003)
  ob.rotation_euler[2]=-a

def map_item():
 for i in range(4):
  ob=box('folded map panel',(-.09+i*.06,.008+(i%2)*.008,0),(.061,.003,.18),'paper')
  ob.rotation_euler[1]=(.08 if i%2 else -.08)
 for x,z,w,d,c in [(-.065,-.035,.045,.055,'green'),(.05,.04,.055,.065,'olive'),(.07,-.05,.05,.03,'blue')]:
  box('map region',(x,.024,z),(w,.001,d),c)
 for a,b in [((-.10,.026,.05),(.08,.026,-.06)),((-.06,.027,-.07),(.09,.027,.06))]:beam('map road',a,b,.0025,'red')
 label('JADRAN',(0,.028,-.075),.018,'dark',True)

def radio_part():
 box('circuit board',(0,.006,0),(.14,.012,.105),'green',.003)
 for x in (-.045,-.015,.015,.045):box('copper trace',(x,.013,0),(.002,.001,.085),'copper')
 box('integrated circuit',(-.025,.022,0),(.045,.017,.025),'dark',.003)
 for x in (-.04,-.03,-.02,-.01):
  for z in (-.019,.019):box('chip pin',(x,.017,z),(.004,.005,.012),'metal')
 for x,z in [(.035,.025),(.04,-.025)]:
  cyl('capacitor',(x,.034,z),.013,.042,'blue',12);cyl('capacitor top',(x,.056,z),.011,.002,'metal',12)
 beam('resistor',(-.04,.023,.034),(.0,.023,.034),.005,'paper')

def scrap_metal():
 for x,z,a in [(-.035,0,.3),(.04,.015,-.5)]:
  ob=box('rusted offcut',(x,.012,z),(.10,.018,.22),'rust',.004);ob.rotation_euler[2]=a
 beam('bent pipe',(-.1,.045,-.07),(.05,.045,.06),.018,'steel')
 beam('bent pipe',(.05,.045,.06),(.08,.09,.09),.018,'steel')
 ring('old washer',(.04,.034,-.045),.027,.009,'metal')

def bottle_cap():
 cyl('crown cap',(0,.007,0),.019,.012,'red',20)
 for k in range(20):
  a=k*math.tau/20
  box('crimp',(math.cos(a)*.019,.005,math.sin(a)*.019),(.004,.009,.004),'metal')
 cyl('cap stamp',(0,.014,0),.012,.002,'cream',16)

builders={'jerry_can':jerry_can,'motor_oil':motor_oil,'water':water,'bread':bread,'medkit':medkit,
 'duct_tape':duct_tape,'wrench':wrench,'spark_plug':spark_plug,'cassette':cassette,'cigarettes':cigarettes,
 'spare_tire':spare_tire,'map':map_item,'radio_part':radio_part,'scrap_metal':scrap_metal,'bottle_cap':bottle_cap}
only=next((arg.split('=',1)[1] for arg in sys.argv if arg.startswith('--only=')),None)
stats=json.loads((SHOTS/'geometry.json').read_text()) if (SHOTS/'geometry.json').exists() else {}
for item,builder in builders.items():
 if only and only!=item:continue
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 surface=material();builder()
 bpy.context.view_layer.update()
 bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(item+'.blend')))
 bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=next(o for o in bpy.context.scene.objects if o.type=='MESH')
 bpy.ops.object.join();ob=bpy.context.object;ob.name='loot_'+item
 bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 ob.data.materials.clear();ob.data.materials.append(surface)
 for poly in ob.data.polygons:poly.material_index=0
 ob.data.calc_loop_triangles();stats[item]={'triangles':len(ob.data.loop_triangles),'surfaces':1}
 bpy.ops.export_scene.gltf(filepath=str(OUT/('loot_'+item+'.glb')),export_format='GLB',use_selection=True,
  export_yup=True,export_apply=True,export_cameras=False,export_lights=False,export_all_vertex_colors=True)
 # Render the exported mesh, framed per item, with identical view and lighting.
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
 scene.render.resolution_x=256;scene.render.resolution_y=256;scene.render.resolution_percentage=100
 scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
 scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.65,.7,.75,1)
 scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
 corners=[ob.matrix_world@Vector(c) for c in ob.bound_box]
 center=sum(corners,Vector())/8
 extent=max(ob.dimensions);distance=max(extent,0.05)
 for direction,power in [((-2, -3, 4),80),((3,1,2),50)]:
  data=bpy.data.lights.new('icon softbox','AREA');data.energy=power*distance*distance;data.size=distance*3
  light=bpy.data.objects.new('icon softbox',data);scene.collection.objects.link(light)
  light.location=center+Vector(direction)*distance
  light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
 data=bpy.data.cameras.new('Inventory');cam=bpy.data.objects.new('Inventory',data);scene.collection.objects.link(cam)
 cam.location=center+Vector((1.1,-1.7,1.25))*distance
 cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
 data.type='ORTHO';data.ortho_scale=distance*1.43;data.clip_start=.001;scene.camera=cam
 scene.render.filepath=str(ICONS/(item+'.png'));bpy.ops.render.render(write_still=True)
 print('LOOT_COMPLETE',item,stats[item],flush=True)
(SHOTS/'geometry.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf8')
