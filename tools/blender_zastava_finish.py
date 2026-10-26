"""Surface/detail stage executed by blender_zastava_modular.py in its namespace.
Deterministic 1024px atlas, four PBR surfaces, unchanged assembly coordinates.
No photographs are used as textures. Blender ships numpy.
"""
import numpy as np
sys.path.insert(0,str(Path(__file__).parent))
from gltf_vertex_palette import normalize_palette

active_collection=colls['hood']
for ob in list(active_collection.objects):
    if ob.name.startswith('bonnet outer stamping'):bpy.data.objects.remove(ob,do_unlink=True)
# Gentle transverse crown and two pressed creases, all boundary points unchanged.
xs=[-.669,-.51,-.47,0,.47,.51,.669]
zs=[-1.77,-1.69,-1.16,-.93]
pts=[]
for z in zs:
    t=(z+1.77)/.84
    for x in xs:
        crown=.020*(1-(x/.669)**2)*math.sin(math.pi*t)
        crease=.008 if abs(abs(x)-.47)<.001 else 0
        pts.append((x,.223+.119*t+crown+crease*math.sin(math.pi*t),z))
hood=mesh('crowned bonnet with pressed creases',pts,
    [(j*7+i,j*7+i+1,(j+1)*7+i+1,(j+1)*7+i) for j in range(3) for i in range(6)],paint)
solid=hood.modifiers.new('folded skin','SOLIDIFY');solid.thickness=.016
for face in hood.data.polygons:face.use_smooth=True
active_collection=body
# Period dashboard details, visible through the new glazing.
for ob in list(body.objects):
    if ob.name.startswith(('instrument needle','dashboard vent')):bpy.data.objects.remove(ob,do_unlink=True)
box('radio face',(0,.27,-.566),(.24,.075,.012),steel,0)
box('radio tuning scale',(0,.285,-.557),(.16,.014,.003),plate,0)
for x in (-.105,.105):cylinder('radio knob',(x,.26,-.548),.015,.018,rubber,'Z',8)
for x in (-.60,.57):
    cylinder('round dashboard vent',(x,.29,-.563),.043,.015,rubber,'Z',12)
    line('vent vane',[(x-.032,.29,-.551),(x+.032,.29,-.551)],chrome,.004)
line('glovebox seam',[(.19,.31,-.568),(.49,.31,-.568),(.49,.21,-.568),(.19,.21,-.568)],steel,.003,True)
box('glovebox latch',(.34,.24,-.558),(.055,.018,.014),chrome,0)
for x in (-.48,-.35,-.20):
    line('pedal stalk',[(x,-.14,-.72),(x,-.22,-.55)],steel,.009)
    box('pedal rubber',(x,-.23,-.55),(.055,.07,.018),rubber,.005)
box('rear view mirror frame',(0,.68,-.66),(.21,.075,.025),rubber,.008)
box('rear view mirror silver',(0,.68,-.643),(.19,.056,.004),chrome,0)
line('rear view mirror stalk',[(0,.76,-.60),(0,.69,-.66)],steel,.009)
for side,suffix in ((-1,'l'),(1,'r')):
    for row,zc in (('f',-.35),('r',.63)):
        active_collection=colls['door_'+row+suffix]
        line('inner opening lever',[(side*.69,.19,zc+.15),(side*.67,.19,zc+.23)],chrome,.008)
        for y in (-.10,-.20):line('door card stitched line',[(side*.696,y,zc-.24),(side*.696,y,zc+.24)],cloth_edge,.0025)
active_collection=body
# Body shut lines are actual dark, narrow seals, following moving panel boundaries.
for key in ('door_fl','door_fr','door_rl','door_rr'):
    active_collection=colls[key];side=-1 if key.endswith('l') else 1
    z0,z1=(-.848,.148) if '_f' in key else (.192,1.093)
    line('door leading weather seal',[(side*.786,.282,z0),(side*.788,-.20,z0),(side*.746,-.387,z0)],rubber,.0025)
    if '_f' in key:
        line('door trailing weather seal',[(side*.787,.282,z1),(side*.787,-.20,z1),(side*.746,-.387,z1)],rubber,.0025)

# One atlas shared by all opaque surfaces. Left half: height-dependent body wear;
# upper right: vinyl grain; lower right: metal; small lower-right tile: dial face.
N=1024
y,x=np.mgrid[0:N,0:N].astype(np.float32);u=x/(N-1);v=y/(N-1)
rng=np.random.default_rng(101)
grain=rng.random((N,N),dtype=np.float32)
cloud=(np.sin(u*65+np.sin(v*17)*2)+np.sin(v*53+u*19)+np.sin(u*177+v*125))/6+.5
streak=(np.sin(u*340)+np.sin(u*871)*.4)*.5+.5
dirt=np.clip((.28-v)*2.4,0,.55)*(cloud*.55+.45)
speck=(grain>.965)*np.clip((.20-v)*4,0,.6)
base=np.ones((N,N,4),dtype=np.float32);base[:,:,3]=1
bodytone=.97-.025*cloud-.20*dirt-.08*streak*np.clip(.52-v,0,.4)
base[:,:,:3]=bodytone[:,:,None]
base[:,:,0]-=speck*.14;base[:,:,1]-=speck*.38;base[:,:,2]-=speck*.52
rough=np.ones((N,N,4),dtype=np.float32);rough[:,:,:3]=(.35+.16*cloud+.45*dirt)[:,:,None]
# Dense, tiny cracks in the upholstery; subtle brushed metal grain.
right=u>.50
vinyl=right & (v>.50)
value=.88+grain*.1-.11*(np.sin(u*1600)*np.sin(v*1600)>.85)
base[vinyl,:3]=np.repeat(value[vinyl,None],3,axis=1);rough[vinyl,:3]=.79
metal=right & (v<=.50)
value=.90+.07*grain+.025*np.sin(v*1400)
base[metal,:3]=np.repeat(value[metal,None],3,axis=1);rough[metal,:3]=.22+.12*grain[metal,None]
# Printed instrument markings, not geometry. UVs for each circular dial use this tile.
dx=(u-.875)/.12;dy=(v-.125)/.12;radius=np.sqrt(dx*dx+dy*dy)
ang=np.arctan2(dy,dx);ticks=(np.cos(ang*40)>.92)&(radius>.74)&(radius<.89)
major=(np.cos(ang*10)>.97)&(radius>.60)&(radius<.90)
dial=(u>.75)&(v<.25)
base[dial,:3]=.022
base[dial & (ticks|major),:3]=.86
needle=(abs(dy-dx*.70)<.025)&(dx<.04)&(dx>-.60)
base[dial & needle,:3]=(.8,.08,.025)
base[dial & (radius<.08),:3]=(.5,.3,.16);rough[dial,:3]=.45

def save_image(name,pixels):
    image=bpy.data.images.new(name,width=N,height=N,alpha=True)
    image.pixels.foreach_set(pixels.ravel());image.filepath_raw=str(OUT/(name+'.png'))
    image.file_format='PNG';image.save();image.pack();return image

albedo=save_image('zastava_surface_atlas',np.clip(base,0,1))
roughimage=save_image('zastava_roughness_atlas',np.clip(rough,0,1));roughimage.colorspace_settings.name='Non-Color'
surfaces=[]
for name,metallic,roughness in [('Paint',.12,.45),('Metal',.78,.25),('Interior',0,.8),('Glass',0,.12)]:
    mat=material('Zastava_'+name,(1,1,1),metallic,roughness)
    nt=mat.node_tree;bsdf=nt.nodes.get('Principled BSDF')
    if name=='Glass':
        mat.diffuse_color=(.64,.76,.75,.22)
        bsdf.inputs['Base Color'].default_value=(.64,.76,.75,1)
        bsdf.inputs['Alpha'].default_value=.22
        mat.surface_render_method='DITHERED';mat.use_backface_culling=False
    else:
        vc=nt.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color'
        tex=nt.nodes.new('ShaderNodeTexImage');tex.image=albedo
        multiply=nt.nodes.new('ShaderNodeMixRGB');multiply.blend_type='MULTIPLY';multiply.inputs[0].default_value=1
        nt.links.new(tex.outputs['Color'],multiply.inputs[1]);nt.links.new(vc.outputs['Color'],multiply.inputs[2])
        nt.links.new(multiply.outputs[0],bsdf.inputs['Base Color'])
        texr=nt.nodes.new('ShaderNodeTexImage');texr.image=roughimage
        separate=nt.nodes.new('ShaderNodeSeparateColor');nt.links.new(texr.outputs['Color'],separate.inputs[0])
        nt.links.new(separate.outputs['Green'],bsdf.inputs['Roughness'])
    surfaces.append(mat)

# Bake original colours and world-aligned UVs before rebasing each panel to its hinge.
for key,coll in colls.items():
    for ob in list(coll.objects):
        if ob.type=='EMPTY':continue
        bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
        for modifier in ob.modifiers:
            if modifier.type=='BEVEL':modifier.segments=1
        bpy.ops.object.convert(target='MESH')
        original=list(ob.data.materials)
        colors=ob.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
        uv=ob.data.uv_layers.active or ob.data.uv_layers.new(name='UVMap')
        groups=[]
        for face in ob.data.polygons:
            old=original[face.material_index]
            group=3 if old==glass and 'mirror' not in ob.name else (0 if old==paint else (1 if old in (chrome,steel,lens,amber,red) else 2))
            is_dial=ob.name.startswith('instrument dial')
            groups.append(group)
            normal=(ob.matrix_world.to_3x3()@face.normal).normalized()
            for li in face.loop_indices:
                local=ob.data.vertices[ob.data.loops[li].vertex_index].co
                w=ob.matrix_world@local; gx,gy,gz=w.x,w.z,-w.y
                color=old.diffuse_color[:]
                if group==3:color=(1,1,1,1)
                if is_dial:color=(1,1,1,1)
                colors.data[li].color=color
                if is_dial:
                    centre=ob.location;rad=.051 if '001' not in ob.name else .041
                    pu=.875+(w.x-centre.x)/rad*.12;pv=.125+(w.z-centre.z)/rad*.12
                elif group==0:
                    pu=.015+.47*(gz+2)/4
                    pv=.025+.94*(gy+.43)/1.30
                    if abs(normal.z)>.7:
                        pu=.015+.47*(gx+.80)/1.60;pv=.67+.28*(gz+2)/4
                else:
                    pu=.52+.21*((gz+2)/4);pv=(.53 if group==2 else .02)+.44*((gy+.43)/1.30)
                uv.data[li].uv=(pu,pv)
        ob.data.materials.clear()
        for m in surfaces:ob.data.materials.append(m)
        for face,group in zip(ob.data.polygons,groups):face.material_index=group

bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'zastava_modular.blend'))
stats={}
for key,coll in colls.items():
    bpy.ops.object.select_all(action='DESELECT')
    meshes=[o for o in coll.objects if o.type!='EMPTY']
    for ob in meshes:ob.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0];bpy.ops.object.join();ob=bpy.context.object
    # Join merges identical slots only after explicit remapping.
    remap=[surfaces.index(m) for m in ob.data.materials];indices=[remap[p.material_index] for p in ob.data.polygons]
    ob.data.materials.clear()
    for m in surfaces:ob.data.materials.append(m)
    for p,i in zip(ob.data.polygons,indices):p.material_index=i
    name='zastava_101_'+key;ob.name=name;pivot=SOCKETS.get(key,(0,0,0))
    bpy.context.scene.cursor.location=xyz(pivot);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');ob.location=(0,0,0)
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    dissolve=ob.modifiers.new('coplanar reduction','DECIMATE');dissolve.decimate_type='DISSOLVE'
    dissolve.angle_limit=math.radians(.5);dissolve.delimit={'MATERIAL','UV'}
    bpy.ops.object.modifier_apply(modifier=dissolve.name)
    ob.data.calc_loop_triangles();triangles=len(ob.data.loop_triangles)
    budget=2850 if key=='shell' else (440 if key.startswith('door') else (140 if key=='hood' else 230))
    if triangles>budget:
        dec=ob.modifiers.new('game budget','DECIMATE');dec.ratio=(budget-24)/triangles
        bpy.ops.object.modifier_apply(modifier=dec.name)
    ob.data.calc_loop_triangles();triangles=len(ob.data.loop_triangles)
    assert triangles<=budget,(key,triangles,budget)
    stats[key]={'triangles':triangles,'socket':list(pivot),'budget':budget,'materials':len(set(p.material_index for p in ob.data.polygons))}
    # Blender's exporter does not support arbitrary multiply graphs. Export base
    # texture + COLOR_0 directly: glTF multiplies these according to its standard.
    for mat in surfaces[:3]:
        nt=mat.node_tree;tex=next(n for n in nt.nodes if n.type=='TEX_IMAGE' and n.image==albedo)
        nt.links.new(tex.outputs['Color'],nt.nodes.get('Principled BSDF').inputs['Base Color'])
    for anchor in coll.objects:
        if anchor.type=='EMPTY':anchor.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(ASSETS/(name+'.glb')),export_format='GLB',use_selection=True,
        export_yup=True,export_apply=True,export_animations=False,export_cameras=False,export_lights=False,export_all_vertex_colors=True)
    normalize_palette(ASSETS/(name+'.glb'))
    imp=ASSETS/(name+'.glb.import')
    content=imp.read_text(encoding='utf8').replace('res://scripts/WorldPropImport.gd','res://assets/materials/zastava_import.gd')
    content=content.replace('gltf/embedded_image_handling=1','gltf/embedded_image_handling=2')
    imp.write_text(content,encoding='utf8',newline='\n')
assert sum(s['triangles'] for s in stats.values())<=5000
(OUT/'geometry.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf8')
print('ZASTAVA_DETAIL',json.dumps(stats))
