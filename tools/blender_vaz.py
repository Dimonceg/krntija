"""Original Krntija VAZ-2106: four round lamps, sedan body, separate wheel.

blender --background --python tools/blender_vaz.py
Coordinates are chassis-relative metres, front -Z, Y up. See VAZ_MODEL.md.
Only the authoring prelude of blender_zastava is executed, never its model.
"""
from pathlib import Path

helper_path = Path(__file__).with_name('blender_zastava.py')
exec(compile(helper_path.read_text(encoding='utf8').split('# Overall length with bumpers')[0], str(helper_path), 'exec'))
bpy.context.preferences.filepaths.save_version = 0
AXLES = (-1.212, 1.212)
WHEEL_Y = -.295
TRACK = 1.365
RADIUS = .31  # Existing CarSpec tyre radius; preserve the drivetrain tuning.
paint.diffuse_color = (.53, .45, .30, 1)
paint.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = paint.diffuse_color
paint.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .56
rust = material('09 - exposed oxidised steel', (.23, .10, .045), .05, .9)
dirt = material('10 - stained lower enamel', (.31, .28, .20), .05, .85)

# Long horizontal bonnet, pinched corners, pressed shoulder and lower sill.
stations = [(-2.005,.735,.23),(-1.91,.789,.27),(-1.212,.8055,.29),
            (-.70,.8055,.31),(.32,.8055,.31),(1.212,.801,.30),
            (1.91,.785,.27),(2.005,.735,.235)]
verts = []
for z, w, h in stations:
    verts.extend([(0,h+.012,z),(w-.035,h,z),(w,h-.035,z),(w,-.23,z),
                  (w-.03,-.42,z),(0,-.43,z),(-w+.03,-.42,z),
                  (-w,-.23,z),(-w,h-.035,z),(-w+.035,h,z)])
faces = []
for j in range(len(stations)-1):
    for i in range(10):
        faces.append((j*10+i,j*10+(i+1)%10,(j+1)*10+(i+1)%10,(j+1)*10+i))
faces.extend([tuple(reversed(range(10))),tuple((len(stations)-1)*10+i for i in range(10))])
shell = mesh('stamped sedan shell', verts, faces, paint)
for z in AXLES:
    cutter = cylinder('temporary arch cutter',(0,WHEEL_Y,z),.363,2.0,rubber,vertices=24)
    bpy.context.view_layer.objects.active = shell
    mod = shell.modifiers.new('wheel aperture','BOOLEAN')
    mod.operation = 'DIFFERENCE'; mod.solver = 'EXACT'; mod.object = cutter
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter,do_unlink=True)
bevel(shell,.007,1)

# Upright glasshouse with a short rear window and a separate boot deck.
cab = [(-.76,.754,.31),(-.34,.642,.797),(-.22,.639,.827),
       (.67,.639,.827),(.81,.657,.79),(1.29,.755,.30)]
verts = []
for z,w,h in cab:
    verts.extend([(-.765,.285,z),(-w,h-.012,z),(-w*.82,h,z),
                  (w*.82,h,z),(w,h-.012,z),(.765,.285,z)])
faces = []
for j in range(len(cab)-1):
    for i in range(5):
        faces.append((j*6+i,j*6+i+1,(j+1)*6+i+1,(j+1)*6+i))
faces.extend([tuple(reversed(range(6))),tuple((len(cab)-1)*6+i for i in range(6))])
mesh('roof and painted pillars',verts,faces,paint,.009)

def sx(y):
    return .765-(y-.285)*.241

def side_window(name, coords, side):
    points = [(side*(sx(y)+.012),y,z) for z,y in coords]
    mesh(name,points,[tuple(range(len(points)))],glass)
    line(name+' black seal',points,rubber,.010,True)
    line(name+' bright surround',[(x+side*.006,y,z) for x,y,z in points],chrome,.0035,True)

for side in (-1,1):
    side_window('front door glass',[(-.665,.35),(-.30,.761),(.18,.768),(.18,.35)],side)
    side_window('rear door glass',[(.27,.35),(.27,.768),(.67,.768),(1.095,.35)],side)
    line('quarterlight divider',[(side*(sx(y)+.024),y,z) for z,y in [(-.43,.35),(-.43,.62)]],chrome,.007)
    line('rear fixed pane divider',[(side*(sx(y)+.024),y,z) for z,y in [(.85,.35),(.85,.565)]],chrome,.005)
    line('roof rain gutter',[(side*.649,.802,-.34),(side*.648,.837,-.22),
                            (side*.648,.837,.67),(side*.666,.80,.81)],chrome,.005)
    for z in (-.73,.225):
        line('door shut line',[(side*.807,.277,z),(side*.807,-.22,z),(side*.777,-.415,z)],rubber,.0022)
    line('rear door shut line',[(side*.805,.276,1.12),(side*.806,.12,.98),
                                (side*.806,-.17,.84),(side*.778,-.415,.78)],rubber,.0022)
    line('door bottom seam',[(side*.781,-.40,-.725),(side*.781,-.40,.78)],rubber,.0025)
    # Narrow rubber/chrome rubbing strip, not a broad modern plastic cladding.
    for za,zb,x in [(-1.91,-1.62,.792),(-.80,.78,.811),(1.62,1.91,.790)]:
        box('side moulding rubber',(side*x,-.045,(za+zb)/2),(.014,.026,zb-za),rubber,.003)
        box('side moulding chrome',(side*(x+.008),-.04,(za+zb)/2),(.009,.009,zb-za),chrome,0)
    for z in (.055,.81):
        box('handle recess',(side*.808,.19,z),(.010,.04,.145),rubber,0)
        box('chrome door pull',(side*.822,.198,z),(.027,.019,.122),chrome,0)
    for z in AXLES:
        points = []; quads = []
        for i in range(17):
            a = math.radians(-20+i*220/16)
            for r in (.364,.379):
                points.append((side*.809,WHEEL_Y+r*math.sin(a),z+r*math.cos(a)))
        for i in range(16):quads.append((i*2,i*2+1,i*2+3,i*2+2))
        mesh('painted arch lip',points,quads,paint)
    line('mirror stem',[(side*.773,.35,-.60),(side*.88,.40,-.59)],chrome,.012)
    box('chrome mirror housing',(side*.91,.425,-.59),(.14,.09,.055),chrome,.012)
    box('mirror glass',(side*.91,.427,-.557),(.115,.066,.008),glass,.004)
    box('wing repeater frame',(side*.798,.16,-1.67),(.017,.05,.11),chrome,0)
    box('wing repeater lens',(side*.809,.16,-1.67),(.012,.035,.09),amber,0)
    # Small irregular corrosion islands below the doors. Geometry, no textures.
    for z,y,scale in [(-.57,-.25,1),(.60,-.20,.65),(-.20,-.30,.55)]:
        outline = [(-.13,-.015),(-.08,.022),(-.025,.012),(.018,.04),
                   (.10,.014),(.14,-.015),(.055,-.035),(-.06,-.026)]
        mesh('rust at lower door edge',[(side*.809,y+dy*scale,z+dz*scale) for dz,dy in outline],
             [tuple(range(len(outline)))],rust)
    mesh('road grime on sill',[(side*.798,-.30,-.76),(side*.786,-.39,-.76),
                              (side*.786,-.39,.76),(side*.798,-.30,.70)],[(0,1,2,3)],dirt)
    # Rear pillar ventilation, a visible detail specific to the classic sedan.
    for i in range(3):
        z = .96+i*.045; y = .60-i*.042
        line('rear pillar vent',[(side*(sx(y)+.022),y,z),
                                (side*(sx(y-.05)+.022),y-.05,z+.045)],rubber,.006)

front = [(-.70,.355,-.731),(.70,.355,-.731),(.597,.775,-.362),(-.597,.775,-.362)]
rear = [(-.608,.755,.85),(.608,.755,.85),(.70,.35,1.259),(-.70,.35,1.259)]
for name,points in [('windscreen',front),('rear windscreen',rear)]:
    mesh(name,points,[(0,1,2,3)],glass)
    line(name+' rubber',points,rubber,.012,True)
    # Chrome insert inside the old rubber gasket.
    line(name+' chrome',[(x,y+.003,z+(-.007 if name=='windscreen' else .007)) for x,y,z in points],chrome,.003,True)
for side in (-1,1):
    line('front wiper',[(side*.26,.352,-.754),(side*.20,.40,-.70),
                        (side*.42,.46,-.65)],rubber,.005)
    line('bonnet shut line',[(side*.675,.25,-1.99),(side*.72,.292,-1.91),
                            (side*.735,.314,-1.212),(side*.72,.334,-.78)],rubber,.0022)
    line('boot shut line',[(side*.71,.316,1.32),(side*.71,.283,1.90),
                          (side*.65,.26,1.985)],rubber,.0022)
    for i in range(7):
        box('bonnet ventilation',(side*(.22+i*.037),.333,-.80),(.015,.004,.07),rubber,0)
line('boot lower seam',[(-.66,.263,1.99),(.66,.263,1.99)],rubber,.002)

# Four small round lights in two black rectangular bezels; slim centre grille.
box('centre grille',(0,.047,-2.016),(.69,.235,.032),rubber,.012)
for i in range(5):box('grille horizontal slat',(0,-.044+i*.043,-2.038),(.65,.008,.009),chrome,0)
for x in (-.23,-.115,0,.115,.23):box('grille vertical rib',(x,.04,-2.044),(.007,.19,.007),rubber,0)
for side in (-1,1):
    box('twin headlight bezel',(side*.554,.047,-2.009),(.435,.245,.032),rubber,.012)
    for x in (.45,.655):
        cylinder('round headlight ring',(side*x,.047,-2.033),.094,.019,chrome,'Z',20)
        cylinder('headlight lens',(side*x,.047,-2.047),.083,.010,lens,'Z',20)
        for dx in (-.045,0,.045):
            h = math.sqrt(.078**2-dx**2)
            line('lens fluting',[(side*x+dx,.047-h,-2.054),(side*x+dx,.047+h,-2.054)],plate,.0016)
    box('front sidelight surround',(side*.575,-.135,-2.018),(.24,.065,.03),chrome,.004)
    box('front amber turn signal',(side*.633,-.135,-2.038),(.109,.047,.013),amber,0)
    box('front white sidelight',(side*.518,-.135,-2.038),(.105,.047,.013),lens,0)
    box('rear lamp chrome surround',(side*.55,.075,2.013),(.41,.20,.035),chrome,.006)
    box('rear lamp black gasket',(side*.55,.075,2.034),(.39,.18,.013),rubber,.004)
    box('rear outer indicator',(side*.672,.075,2.045),(.117,.158,.012),amber,0)
    box('rear stop lens',(side*.545,.075,2.045),(.115,.158,.012),red,0)
    box('rear reverse lens',(side*.42,.075,2.045),(.115,.158,.012),lens,0)
    for y in (.023,.065,.107):
        box('rear lens horizontal rib',(side*.55,y,2.054),(.37,.003,.004),rubber,0)

# Old chrome bumpers have black corner caps, a thin insert and two overriders.
for sign in (-1,1):
    z = sign*2.015
    box('chrome bumper',(0,-.245,z),(1.57,.105,.12),chrome,.015)
    box('bumper rubber insert',(0,-.253,sign*2.078),(1.49,.029,.01),rubber,.003)
    for side in (-1,1):
        box('bumper black end cap',(side*.785,-.245,sign*1.985),(.086,.106,.18),rubber,.012)
        box('bumper overrider',(side*.55,-.223,sign*2.027),(.052,.19,.108),rubber,.01)
box('front number plate',(0,-.245,-2.081),(.38,.09,.004),plate,.002)
box('rear number plate recess',(0,.025,2.016),(.40,.12,.022),rubber,.002)
box('rear number plate',(0,.025,2.032),(.37,.09,.007),plate,.002)
box('rear plate light',(0,.12,2.03),(.14,.025,.035),chrome,.004)
box('grille badge chrome',(0,.06,-2.047),(.055,.10,.012),chrome,.003)
box('grille badge inset',(0,.06,-2.056),(.039,.081,.008),red,.002)
line('boat badge',[(-.013,.04,-2.062),(.011,.04,-2.062),(.016,.048,-2.062),(-.014,.048,-2.062)],chrome,.0025)
line('badge sail',[(0,.05,-2.062),(0,.086,-2.062),(.012,.055,-2.062),(0,.055,-2.062)],chrome,.002)

def lettering(caption,p,size):
    curve = bpy.data.curves.new(caption,'FONT')
    curve.body = caption; curve.size = size; curve.align_x = 'CENTER'; curve.align_y = 'CENTER'; curve.resolution_u = 1
    ob = bpy.data.objects.new(caption,curve); active_collection.objects.link(ob)
    ob.location = xyz(p); ob.rotation_euler = (math.pi/2,0,0); ob.data.materials.append(chrome)

lettering('LADA',(-.52,.215,2.018),.043)
lettering('1600',(.52,.215,2.018),.04)
line('exhaust tailpipe',[(-.49,-.40,1.77),(-.49,-.445,2.06)],chrome,.022)

# One reusable narrow tyre with a pressed steel rim and domed chrome hubcaps.
active_collection = wheel
verts = []; faces = []
profile = [(-.0875,.17),(-.0875,.265),(-.067,.302),(-.04,RADIUS),
           (.04,RADIUS),(.067,.302),(.0875,.265),(.0875,.17)]
for x,r in profile:
    for i in range(24):
        a = i*math.tau/24; verts.append((x,r*math.sin(a),r*math.cos(a)))
for j in range(len(profile)-1):
    for i in range(24):faces.append((j*24+i,j*24+(i+1)%24,(j+1)*24+(i+1)%24,(j+1)*24+i))
mesh('narrow tyre carcass',verts,faces,rubber)
for side in (-1,1):
    cylinder('steel wheel rim',(side*.081,0,0),.177,.016,chrome,vertices=24)
    cylinder('pressed wheel dish',(side*.093,0,0),.149,.014,dirt,vertices=24)
    for i in range(8):
        a = i*math.tau/8
        cylinder('ventilation recess',(side*.103,.135*math.sin(a),.135*math.cos(a)),.017,.004,rubber,vertices=8)
    points = []; quads = []
    for x,r in [(.103,.112),(.119,.092),(.128,.04),(.13,0)]:
        for i in range(24):
            a = i*math.tau/24; points.append((side*x,r*math.sin(a),r*math.cos(a)))
    for j in range(3):
        for i in range(24):quads.append((j*24+i,j*24+(i+1)%24,(j+1)*24+(i+1)%24,(j+1)*24+i))
    cap = mesh('domed chrome hubcap',points,quads,chrome)
    for polygon in cap.data.polygons:polygon.use_smooth = True

# Keep editable parts in the .blend; export one mesh with a vertex palette.
palette_mat = material('VAZ colour palette',(.5,.5,.5),.12,.55)
vc = palette_mat.node_tree.nodes.new('ShaderNodeVertexColor'); vc.layer_name = 'Color'
palette_mat.node_tree.links.new(vc.outputs['Color'],palette_mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'vaz_2106.blend'))
for shared in (rubber,glass,chrome):
    vc = shared.node_tree.nodes.new('ShaderNodeVertexColor'); vc.layer_name = 'Color'
    shared.node_tree.links.new(vc.outputs['Color'],shared.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
stats = {}
for name,coll in [('vaz_2106',body),('vaz_2106_wheel',wheel)]:
    bpy.ops.object.select_all(action='DESELECT')
    for ob in list(coll.objects):
        bpy.context.view_layer.objects.active = ob; ob.select_set(True)
        bpy.ops.object.convert(target='MESH')
        original = ob.data.materials[0]
        layer = ob.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
        for entry in layer.data:entry.color = original.diffuse_color
        replacement = original if original in (rubber,glass,chrome) else palette_mat
        ob.data.materials.clear(); ob.data.materials.append(replacement)
        for polygon in ob.data.polygons:polygon.material_index = 0
        ob.select_set(False)
    for ob in coll.objects:ob.select_set(True)
    bpy.context.view_layer.objects.active = list(coll.objects)[0]; bpy.ops.object.join()
    merged = bpy.context.object; merged.name = name
    bpy.context.scene.cursor.location = (0,0,0); bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    merged.data.calc_loop_triangles()
    stats[name] = {'triangles':len(merged.data.loop_triangles),'materials':len(merged.data.materials),
                   'size_xyz':[merged.dimensions.x,merged.dimensions.z,merged.dimensions.y]}
    bpy.ops.export_scene.gltf(filepath=str(ASSETS/(name+'.glb')),export_format='GLB',use_selection=True,
                              export_yup=True,export_apply=True,export_cameras=False,export_lights=False,
                              export_all_vertex_colors=True)
(SOURCE/'vaz_2106_geometry.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf8')
print('VAZ_ASSETS',json.dumps(stats))
