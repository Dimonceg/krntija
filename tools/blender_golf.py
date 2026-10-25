"""Original five-door Golf II art, chassis-relative metres, front -Z.
Blender --background --python tools/blender_golf.py
Uses only the reusable authoring helpers at the start of blender_zastava.py.
"""
from pathlib import Path
helper=Path(__file__).with_name('blender_zastava.py').read_text(encoding='utf8')
exec(compile(helper.split('# Overall length with bumpers')[0],str(Path(__file__).with_name('blender_zastava.py')),'exec'))
bpy.context.preferences.filepaths.save_version=0
paint.diffuse_color=(.32,.075,.045,1)
paint.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=paint.diffuse_color
paint.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.48
AXLES=(-1.2375,1.2375);WHEEL_Y=-.295;TRACK=1.42;RADIUS=.30

# Pressed shell with curved shoulders and genuinely cut wheel apertures.
stations=[(-1.91,.73,.19),(-1.77,.81,.23),(-1.24,.8325,.27),(-.84,.825,.30),
          (.28,.827,.30),(1.24,.824,.30),(1.75,.79,.27),(1.90,.74,.23)]
vertices=[]
for z,w,h in stations:
 vertices.extend([(0,h+.013,z),(w-.045,h,z),(w,h-.04,z),(w,-.22,z),
                  (w-.03,-.425,z),(0,-.435,z),(-w+.03,-.425,z),(-w,-.22,z),(-w,h-.04,z),(-w+.045,h,z)])
faces=[]
for j in range(len(stations)-1):
 for i in range(10):faces.append((j*10+i,j*10+(i+1)%10,(j+1)*10+(i+1)%10,(j+1)*10+i))
faces.extend([tuple(reversed(range(10))),tuple((len(stations)-1)*10+i for i in range(10))])
shell=mesh('Golf stamped body',vertices,faces,paint)
for z in AXLES:
 cutter=cylinder('temporary arch cutter',(0,WHEEL_Y,z),.359,2.1,rubber,vertices=24)
 bpy.context.view_layer.objects.active=shell
 modifier=shell.modifiers.new('wheel opening','BOOLEAN');modifier.operation='DIFFERENCE';modifier.solver='EXACT';modifier.object=cutter
 bpy.ops.object.modifier_apply(modifier=modifier.name);bpy.data.objects.remove(cutter,do_unlink=True)
bevel(shell,.009,1)

# Painted greenhouse: longer roof and upright hatch distinguish Mk2 from Golf I.
cab=[(-.88,.77,.30),(-.43,.652,.79),(-.28,.65,.82),(.94,.65,.82),(1.10,.67,.77),(1.78,.765,.28)]
v=[]
for z,w,h in cab:v.extend([(-.778,.275,z),(-w,h-.012,z),(-w*.82,h,z),(w*.82,h,z),(w,h-.012,z),(.778,.275,z)])
f=[]
for j in range(len(cab)-1):
 for i in range(5):f.append((j*6+i,j*6+i+1,(j+1)*6+i+1,(j+1)*6+i))
f.extend([tuple(reversed(range(6))),tuple((len(cab)-1)*6+i for i in range(6))])
mesh('roof and five door pillars',v,f,paint,.008)
def sx(y):return .778-(y-.275)*.245
def window(name,coords,side):
 pts=[(side*(sx(y)+.009),y,z) for z,y in coords]
 mesh(name,pts,[tuple(range(len(pts)))],glass)
 line(name+' seal',pts,rubber,.009,True)
for side in (-1,1):
 window('front side glass',[(-.79,.34),(-.39,.755),(.20,.765),(.20,.34)],side)
 window('rear side glass',[(.30,.34),(.30,.765),(.94,.765),(1.39,.34)],side)
 line('rear fixed pane divider',[(side*(sx(y)+.021),y,z) for z,y in [(1.02,.34),(1.02,.68)]],rubber,.009)
 line('roof rain gutter',[(side*.66,.796,-.43),(side*.66,.83,-.28),(side*.66,.83,.94),(side*.68,.78,1.10)],rubber,.007)
 for z in (-.85,.245):line('door shut line',[(side*.824,.27,z),(side*.83,-.19,z),(side*.799,-.415,z)],rubber,.0025)
 line('rear door shut line',[(side*.823,.27,1.05),(side*.825,.12,.98),(side*.81,-.415,.77)],rubber,.0025)
 line('sill seam',[(side*.802,-.415,-.86),(side*.802,-.415,.76)],rubber,.003)
 box('sill trim',(side*.805,-.365,-.05),(.018,.065,1.62),rubber,.005)
 box('side rubbing strip',(side*.835,-.035,-.035),(.025,.065,1.70),rubber,.006)
 for z in (.08,.87):
  box('door handle recess',(side*.835,.19,z),(.02,.055,.17),rubber,.006)
  box('door handle',(side*.85,.196,z),(.03,.021,.125),chrome,.003)
 # Wheel-arch plastic extensions with a broad, flat cross section.
 for z in AXLES:
  pts=[];quads=[]
  for i in range(19):
   a=math.radians(-21+i*222/18)
   for r in (.36,.395):pts.append((side*.839,WHEEL_Y+r*math.sin(a),z+r*math.cos(a)))
  for i in range(18):quads.append((i*2,i*2+1,i*2+3,i*2+2))
  mesh('arch plastic extension',pts,quads,rubber)
 line('mirror mounting',[(side*.78,.34,-.72),(side*.91,.39,-.68)],rubber,.023)
 box('door mirror',(side*.94,.415,-.68),(.16,.10,.075),rubber,.018)
 box('mirror reflection',(side*.94,.417,-.637),(.126,.069,.008),glass,.006)
 box('wing repeater',(side*.837,.14,-1.62),(.018,.045,.10),amber,.004)

front=[(-.716,.35,-.852),(.716,.35,-.852),(.606,.784,-.46),(-.606,.784,-.46)]
rear=[(-.618,.73,1.17),(.618,.73,1.17),(.708,.345,1.72),(-.708,.345,1.72)]
for name,pts in [('windscreen',front),('rear hatch glass',rear)]:
 mesh(name,pts,[(0,1,2,3)],glass);line(name+' rubber seal',pts,rubber,.012,True)
for side in (-1,1):
 line('front wiper',[(side*.28,.348,-.872),(side*.20,.41,-.81),(side*.45,.45,-.77)],rubber,.005)
 line('bonnet shut line',[(side*.66,.213,-1.90),(side*.72,.255,-1.77),(side*.74,.295,-1.24),(side*.72,.324,-.90)],rubber,.0025)
line('rear wiper',[(0,.34,1.74),(.10,.49,1.53),(.36,.55,1.45)],rubber,.006)
line('hatch perimeter',[(-.72,.26,1.82),(-.71,-.17,1.91),(.71,-.17,1.91),(.72,.26,1.82)],rubber,.003)
for x in (-.42,.42):
 for i in range(6):box('cowl slot',(x+(i-2.5)*.03,.324,-.92),(.014,.003,.055),rubber,0)

# Two round headlights, black slatted grille and small bumpers: everyday CL trim.
box('grille black surround',(0,.055,-1.92),(1.47,.30,.04),rubber,.025)
for i in range(6):box('grille slat',(0,-.055+i*.043,-1.948),(1.36,.012,.012),chrome,0)
for side in (-1,1):
 cylinder('headlamp rubber seat',(side*.565,.055,-1.95),.143,.035,rubber,'Z',24)
 cylinder('headlamp rim',(side*.565,.055,-1.971),.130,.016,chrome,'Z',24)
 cylinder('round headlamp',(side*.565,.055,-1.984),.119,.012,lens,'Z',24)
 for dx in (-.06,-.02,.02,.06):
  h=math.sqrt(.112**2-dx**2)
  line('lens flute',[(side*.565+dx,.055-h,-1.992),(side*.565+dx,.055+h,-1.992)],plate,.0018)
 box('front indicator',(side*.64,-.225,-1.998),(.20,.075,.018),amber,.006)
 box('rear lamp base',(side*.566,.125,1.911),(.39,.21,.045),rubber,.008)
 box('rear red lens',(side*.566,.09,1.94),(.365,.12,.018),red,.004)
 box('rear amber lens',(side*.66,.19,1.94),(.17,.065,.018),amber,.003)
 box('reverse lamp',(side*.475,.19,1.94),(.16,.065,.018),lens,.003)
 for y in (.06,.10,.14):box('rear lens rib',(side*.566,y,1.952),(.35,.004,.005),red,0)
for z in (-1.925,1.925):
 box('black plastic bumper',(0,-.235,z),(1.69,.19,.135),rubber,.025)
 for side in (-1,1):box('bumper corner',(side*.814,-.235,z*.96),(.12,.19,.27),rubber,.025)
box('front lower valance',(0,-.38,-1.82),(1.46,.12,.1),paint,.01)
box('rear hatch handle',(0,.025,1.938),(.24,.035,.035),rubber,.007)
for z in (-2.004,2.004):box('number plate',(0,-.235,z),(.40,.095,.012),plate,.004)
# Geometry-only emblem, created here rather than using a logo image.
cylinder('round grille badge',(0,.065,-1.963),.068,.018,chrome,'Z',20)
cylinder('badge inset',(0,.065,-1.976),.055,.008,rubber,'Z',20)
line('badge V',[(-.031,.098,-1.983),(0,.059,-1.983),(.031,.098,-1.983)],chrome,.005)
line('badge W',[(-.041,.057,-1.983),(-.025,.026,-1.983),(0,.050,-1.983),(.025,.026,-1.983),(.041,.057,-1.983)],chrome,.004)
def lettering(caption,p,size,back=False):
 curve=bpy.data.curves.new(caption,'FONT');curve.body=caption;curve.size=size;curve.align_x='CENTER';curve.align_y='CENTER';curve.resolution_u=1
 ob=bpy.data.objects.new(caption,curve);active_collection.objects.link(ob);ob.location=xyz(p)
 ob.rotation_euler=(math.pi/2,0,math.pi if back else 0);ob.data.materials.append(chrome)
lettering('GOLF',(-.55,.285,1.948),.055)
lettering('CL',(.57,.285,1.948),.045)
line('exhaust pipe',[(-.50,-.40,1.65),(-.50,-.43,1.98)],chrome,.027)

# Separate wheel, centred on its axle. Both faces modelled, so no mirroring needed.
active_collection=wheel
pts=[];faces=[]
profile=[(-.095,.17),(-.095,.255),(-.075,.293),(-.045,.30),(.045,.30),(.075,.293),(.095,.255),(.095,.17)]
for x,r in profile:
 for i in range(24):
  a=i*math.tau/24;pts.append((x,r*math.sin(a),r*math.cos(a)))
for j in range(len(profile)-1):
 for i in range(24):faces.append((j*24+i,j*24+(i+1)%24,(j+1)*24+(i+1)%24,(j+1)*24+i))
mesh('tyre carcass',pts,faces,rubber)
for side in (-1,1):
 cylinder('steel rim',(side*.087,0,0),.184,.018,chrome,vertices=24)
 cylinder('rim dish',(side*.102,0,0),.147,.014,rubber,vertices=24)
 cylinder('hubcap',(side*.113,0,0),.084,.025,chrome,vertices=16,edge=.004)
 for i in range(8):
  a=i*math.tau/8
  cylinder('rim ventilation',(side*.112,.126*math.sin(a),.126*math.cos(a)),.025,.008,chrome,vertices=8)
 for i in range(4):
  a=i*math.tau/4
  cylinder('wheel bolt',(side*.13,.055*math.sin(a),.055*math.cos(a)),.012,.008,rubber,vertices=6)

# Four surfaces maximum: vertex palette, rubber, glass, metal.
palette_mat=material('Golf colour palette',(.5,.5,.5),.1,.48)
bs=palette_mat.node_tree.nodes['Principled BSDF'];vc=palette_mat.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color'
palette_mat.node_tree.links.new(vc.outputs['Color'],bs.inputs['Base Color'])
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'golf_2.blend'))
for shared in (rubber,glass,chrome):
 vertex=shared.node_tree.nodes.new('ShaderNodeVertexColor');vertex.layer_name='Color'
 shared.node_tree.links.new(vertex.outputs['Color'],shared.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
stats={}
for name,coll in [('golf_2',body),('golf_2_wheel',wheel)]:
 bpy.ops.object.select_all(action='DESELECT')
 for ob in list(coll.objects):
  bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.convert(target='MESH')
  layer=ob.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
  original=ob.data.materials[0]
  for entry in layer.data:entry.color=original.diffuse_color
  replacement=original if original in (rubber,glass,chrome) else palette_mat
  ob.data.materials.clear();ob.data.materials.append(replacement)
  for polygon in ob.data.polygons:polygon.material_index=0
  ob.select_set(False)
 for ob in coll.objects:ob.select_set(True)
 bpy.context.view_layer.objects.active=list(coll.objects)[0];bpy.ops.object.join();merged=bpy.context.object;merged.name=name
 bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 merged.data.calc_loop_triangles();stats[name]={'triangles':len(merged.data.loop_triangles),'materials':len(merged.data.materials),'size_xyz':[merged.dimensions.x,merged.dimensions.z,merged.dimensions.y]}
 bpy.ops.export_scene.gltf(filepath=str(ASSETS/(name+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_apply=True,export_cameras=False,export_lights=False,export_all_vertex_colors=True)
(SOURCE/'golf_2_geometry.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf8')
print('GOLF_ASSETS',json.dumps(stats))
