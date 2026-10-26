"""Author the single skinned wolf and four in-place clips. No gameplay nodes.
Run: blender --background --python tools/blender_wolf.py
Logical coordinates: X right, Y up, -Z forward. Source: docs/WOLF_ART.md.
"""
from pathlib import Path
import math
import json
import bpy
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'game/assets/models/props/wolf.glb'
SOURCE = ROOT/'art/blender/life'
SOURCE.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for action in list(bpy.data.actions): bpy.data.actions.remove(action)

def vec(p): return Vector((p[0], -p[2], p[1]))
def logical(p): return Vector(p)

fur = (.29,.28,.245,1)
light = (.47,.45,.37,1)
dark = (.15,.16,.145,1)
black = (.022,.025,.022,1)
eye = (.63,.36,.09,1)
tooth = (.72,.69,.57,1)
vertices, faces, colors, weights = [], [], [], []

def part(points, polygons, color, bone):
    start = len(vertices)
    for point in points:
        vertices.append(vec(point))
        weights.append(bone(point) if callable(bone) else {bone:1.0})
    for polygon in polygons:
        faces.append(tuple(start+i for i in polygon)); colors.append(color)

def oval(center, radii, color, bone, segments=10, rings=6):
    points = [(center[0],center[1]+radii[1],center[2])]
    for j in range(1,rings):
        phi = math.pi*j/rings
        for i in range(segments):
            a = i*math.tau/segments
            points.append((center[0]+radii[0]*math.sin(phi)*math.cos(a),
                           center[1]+radii[1]*math.cos(phi),
                           center[2]+radii[2]*math.sin(phi)*math.sin(a)))
    points.append((center[0],center[1]-radii[1],center[2]))
    polys = [(0,1+i,1+(i+1)%segments) for i in range(segments)]
    for j in range(rings-2):
        for i in range(segments):
            a=1+j*segments+i; b=1+j*segments+(i+1)%segments
            polys.append((a,a+segments,b+segments,b))
    last=len(points)-1; base=1+(rings-2)*segments
    polys.extend((last,base+(i+1)%segments,base+i) for i in range(segments))
    part(points,polys,color,bone)

def taper(a,b,r1,r2,color,bone,segments=8):
    a,b=logical(a),logical(b); direction=(b-a).normalized()
    u=direction.cross(Vector((1,0,0))).normalized(); v=direction.cross(u)
    points=[]
    for p,r in [(a,r1),(b,r2)]:
        for i in range(segments):
            angle=i*math.tau/segments
            points.append(p+r*(u*math.cos(angle)+v*math.sin(angle)))
    polys=[tuple(reversed(range(segments))),tuple(range(segments,2*segments))]
    polys.extend((i,(i+1)%segments,(i+1)%segments+segments,i+segments) for i in range(segments))
    part(points,polys,color,bone)

# A permanent ground-level root; every deforming joint is below it.
rest = {}
def bone(name, head, tail, parent='root'):
    rest[name]=(logical(head),logical(tail),parent)
bone('root',(0,0,0),(0,.12,0),None)
bone('body',(0,.60,.10),(0,.60,-.10))
bone('chest',(0,.61,-.20),(0,.65,-.40),'body')
bone('hips',(0,.59,.25),(0,.59,.48),'body')
bone('neck',(0,.65,-.40),(0,.77,-.60),'chest')
bone('head',(0,.77,-.60),(0,.77,-.85),'neck')
bone('jaw',(0,.72,-.65),(0,.70,-.91),'head')
bone('tail_1',(0,.61,.55),(0,.43,.79),'hips')
bone('tail_2',(0,.43,.79),(0,.25,1.02),'tail_1')
for side,sign in [('L',-1),('R',1)]:
    x=.145*sign
    bone('ear_'+side,(sign*.085,.85,-.60),(sign*.112,1.015,-.56),'head')
    bone('fore_upper_'+side,(x,.63,-.35),(x,.34,-.30),'chest')
    bone('fore_lower_'+side,(x,.34,-.30),(x,.075,-.38),'fore_upper_'+side)
    bone('fore_paw_'+side,(x,.075,-.38),(x,.045,-.49),'fore_lower_'+side)
    bone('hind_upper_'+side,(x,.59,.40),(x,.35,.23),'hips')
    bone('hind_lower_'+side,(x,.35,.23),(x,.195,.50),'hind_upper_'+side)
    bone('hind_hock_'+side,(x,.195,.50),(x,.055,.43),'hind_lower_'+side)
    bone('hind_paw_'+side,(x,.055,.43),(x,.045,.32),'hind_hock_'+side)

def torso_weights(p):
    z=p[2]
    if z<-.1:
        t=min(1,max(0,(-z-.1)/.30)); return {'body':1-t,'chest':t}
    t=min(1,max(0,(z-.1)/.35)); return {'body':1-t,'hips':t}

# Continuous tapered ribcage, with dark guard hairs across the saddle.
sections=[(-.56,.59,.065,.09),(-.42,.59,.16,.16),(-.20,.58,.19,.17),
          (.06,.58,.18,.16),(.27,.60,.135,.12),(.44,.59,.17,.14),(.60,.58,.06,.09)]
points=[]
for z,y,rx,ry in sections:
    for i in range(12):
        a=i*math.tau/12;points.append((rx*math.cos(a),y+ry*math.sin(a),z))
for j in range(len(sections)-1):
    for i in range(12):
        a=j*12+i;b=j*12+(i+1)%12
        part([points[k] for k in (a,b,b+12,a+12)],[(0,1,2,3)],
             dark if 1<=i<=4 else light if 7<=i<=10 else fur,torso_weights)
part(points[:12],[tuple(reversed(range(12)))],fur,torso_weights)
part(points[-12:],[tuple(range(12))],fur,torso_weights)
oval((0,.59,-.40),(.16,.16,.18),light,'chest')
taper((0,.65,-.40),(0,.79,-.60),.145,.105,fur,'neck',10)
oval((0,.79,-.65),(.115,.115,.155),fur,'head')
oval((0,.755,-.76),(.102,.075,.115),light,'head')
taper((0,.77,-.74),(0,.755,-.965),.073,.043,light,'head',8)
oval((0,.754,-.974),(.047,.037,.033),black,'head',8,4)
oval((0,.708,-.82),(.066,.022,.125),black,'jaw',8,4)
oval((0,.694,-.81),(.068,.03,.122),light,'jaw',8,4)
for side,sign in [('L',-1),('R',1)]:
    oval((sign*.098,.816,-.726),(.022,.021,.030),black,'head',8,4)
    oval((sign*.113,.819,-.739),(.008,.010,.015),eye,'head',8,4)
    oval((sign*.119,.820,-.744),(.004,.008,.007),black,'head',6,4)
    ear=[(sign*.045,.85,-.63),(sign*.14,.85,-.59),(sign*.112,1.015,-.56),
         (sign*.055,.85,-.53),(sign*.145,.85,-.51)]
    part(ear,[(0,1,2),(1,4,2),(4,3,2),(3,0,2),(0,3,4,1)],fur,'ear_'+side)
    part([(sign*.067,.875,-.625),(sign*.128,.875,-.60),(sign*.111,.98,-.574)],[(0,1,2)],dark,'ear_'+side)
    part([(sign*.085,.77,-.64),(sign*.185,.70,-.47),(sign*.095,.68,-.64)],[(0,1,2)],light,'head')
    # Short canines; hidden inside the closed mouth, visible during the bite.
    taper((sign*.046,.722,-.88),(sign*.044,.695,-.884),.010,.002,tooth,'head',5)
    x=sign*.145
    for prefix,r1,r2,col in [('fore_upper_',.073,.045,fur),('fore_lower_',.043,.030,light),
                             ('hind_upper_',.10,.061,fur),('hind_lower_',.060,.033,fur),
                             ('hind_hock_',.034,.025,light)]:
        a,b,_=rest[prefix+side];taper(a,b,r1,r2,col,prefix+side)
    oval((x,.58,.40),(.095,.15,.13),fur,'hind_upper_'+side,8,5)
    oval((x,.34,-.30),(.046,.052,.048),fur,'fore_lower_'+side,8,4)
    oval((x,.195,.50),(.037,.044,.041),fur,'hind_hock_'+side,8,4)
    for name,y,z in [('fore_paw_',.045,-.42),('hind_paw_',.045,.39)]:
        oval((x,y,z),(.057,.045,.095),light,name+side,8,4)
        for dx in (-.026,0,.026):
            oval((x+dx,.027,z-.080),(.009,.013,.020),dark,name+side,6,4)
taper((0,.61,.55),(0,.43,.79),.085,.072,fur,'tail_1',10)
taper((0,.43,.79),(0,.25,1.02),.074,.016,dark,'tail_2',10)

data=bpy.data.meshes.new('WolfMesh');data.from_pydata(vertices,[],faces);data.update()
mesh=bpy.data.objects.new('WolfMesh',data);bpy.context.collection.objects.link(mesh)
bpy.context.view_layer.objects.active=mesh;mesh.select_set(True)
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
palette=data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
for polygon,color in zip(data.polygons,colors):
    for index in polygon.loop_indices:palette.data[index].color=color
mat=bpy.data.materials.new('Wolf grey brown fur');mat.use_nodes=True
shader=mat.node_tree.nodes['Principled BSDF'];shader.inputs['Roughness'].default_value=.95
vc=mat.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color'
mat.node_tree.links.new(vc.outputs['Color'],shader.inputs['Base Color']);data.materials.append(mat)
for name in rest:mesh.vertex_groups.new(name=name)
for index,groups in enumerate(weights):
    for name,weight in groups.items():
        if weight>0:mesh.vertex_groups[name].add([index],weight,'REPLACE')

arm=bpy.data.armatures.new('WolfSkeleton');rig=bpy.data.objects.new('WolfRig',arm)
bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig
mesh.select_set(False);rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for name,(head,tail,parent) in rest.items():
    b=arm.edit_bones.new(name);b.head=vec(head);b.tail=vec(tail)
    if parent:b.parent=arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
mesh.parent=rig;mod=mesh.modifiers.new('Wolf skin','ARMATURE');mod.object=rig
rig.animation_data_create()
for b in rig.pose.bones:b.rotation_mode='QUATERNION'

def set_segment(name,head,tail,scale=1):
    """Absolute rig-space pose. Parents are assigned before children."""
    h0,t0,_=rest[name];h,t=vec(head),vec(tail)
    rotation=(vec(t0)-vec(h0)).rotation_difference(t-h).to_matrix().to_4x4()
    basis=arm.bones[name].matrix_local.copy();basis.translation=Vector((0,0,0))
    length_scale=(t-h).length/(vec(t0)-vec(h0)).length
    rig.pose.bones[name].matrix=Matrix.Translation(h)@rotation@basis@Matrix.Diagonal((scale,length_scale,scale,1))
    bpy.context.view_layer.update()

def knee_between(a,b,l1,l2,bend):
    delta=b-a;distance=min(delta.length,l1+l2-.0001);axis=delta.normalized()
    along=(l1*l1-l2*l2+distance*distance)/(2*distance)
    height=math.sqrt(max(0,l1*l1-along*along))
    pole=Vector((0,0,bend));pole=(pole-axis*pole.dot(axis)).normalized()
    return a+axis*along+pole*height

def foot_cycle(phase,stride,lift,stance):
    phase%=1
    if phase<stance:return -stride/2+stride*phase/stance,0
    u=(phase-stance)/(1-stance)
    return stride/2-stride*u,lift*math.sin(math.pi*u)

FPS=30
clips={'idle':75,'walk':30,'run':18,'attack':24}
for clip,length in clips.items():
    action=bpy.data.actions.new(clip);rig.animation_data.action=action
    for frame in range(length+1):
        t=frame/length;phase=math.tau*t
        for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
        bob=.004*math.sin(phase)
        reach=0;pitch=0;turn=.12*math.sin(phase) if clip=='idle' else 0
        jaw_open=0
        if clip=='walk':bob=-.02+.008*(1-math.cos(phase*2))
        if clip=='run':bob=-.05+.020*(1-math.cos(phase*2))
        if clip=='attack':
            # Crouch -> short in-place lunge -> bite -> return to the rest pose.
            reach=math.sin(math.pi*min(1,max(0,(t-.12)/.66)))**2
            bob=-.035*math.sin(math.pi*min(1,t/.3)) if t<.3 else .035*reach
            pitch=-.08*reach
            jaw_open=.48*math.sin(math.pi*min(1,max(0,(t-.10)/.48)))
        shift=Vector((0,bob,0))
        for name in ('body','chest','hips'):
            h,tail,_=rest[name];set_segment(name,h+shift,tail+shift,1+.012*math.sin(phase) if name=='chest' and clip=='idle' else 1)
        neck_h=rest['neck'][0]+shift
        head_h=rest['head'][0]+shift+Vector((0,pitch,-.08*reach))
        set_segment('neck',neck_h,head_h)
        direction=Vector((math.sin(turn)*.25,-.025*reach,-math.cos(turn)*.25))
        set_segment('head',head_h,head_h+direction)
        head_rotation=vec((0,0,-1)).rotation_difference(vec(direction))
        # Explicit inverse conversion avoids Blender/Godot axis ambiguity.
        def hp(p):
            q=head_rotation@vec(logical(p)-rest['head'][0]);return head_h+Vector((q.x,q.z,-q.y))
        jh=hp(rest['jaw'][0]);jt=hp(rest['jaw'][1])+Vector((0,-.16*jaw_open,.05*jaw_open))
        set_segment('jaw',jh,jt)
        for side,sign in [('L',-1),('R',1)]:
            eh,et,_=rest['ear_'+side]
            set_segment('ear_'+side,hp(eh),hp(et)+Vector((sign*.012*math.sin(phase),0,.014*reach)))
        tail_h=rest['tail_1'][0]+shift
        tail_mid=rest['tail_1'][1]+shift+Vector((.035*math.sin(phase),.025*reach,0))
        tail_tip=rest['tail_2'][1]+shift+Vector((.07*math.sin(phase-.25),.05*reach,0))
        set_segment('tail_1',tail_h,tail_mid);set_segment('tail_2',tail_mid,tail_tip)
        for side,sign in [('L',-1),('R',1)]:
            for front in (True,False):
                prefix='fore' if front else 'hind'
                names=[prefix+'_upper_'+side,prefix+'_lower_'+side]
                hip=rest[names[0]][0]+shift
                ankle=rest[(prefix+'_lower_' if front else 'hind_hock_')+side][1].copy()
                if clip in ('walk','run'):
                    offset=({'foreL':0,'foreR':.5,'hindL':.75,'hindR':.25} if clip=='walk' else
                            {'foreL':.05,'foreR':.15,'hindL':.55,'hindR':.65})[prefix+side]
                    dz,dy=foot_cycle(t+offset,.23 if clip=='walk' else .42,.075 if clip=='walk' else .14,.62 if clip=='walk' else .38)
                    ankle+=Vector((0,dy,dz))
                if clip=='attack' and front:ankle+=Vector((0,.10*reach,-.15*reach))
                end=ankle if front else ankle+Vector((0,.14,.07))
                a,b,_=rest[names[0]];c,d,_=rest[names[1]]
                knee=knee_between(hip,end,(b-a).length,(d-c).length,1 if front else -1)
                set_segment(names[0],hip,knee);set_segment(names[1],knee,end)
                if not front:set_segment('hind_hock_'+side,end,ankle)
                set_segment(prefix+'_paw_'+side,ankle,ankle+rest[prefix+'_paw_'+side][1]-rest[prefix+'_paw_'+side][0])
        for pb in rig.pose.bones:
            if pb.name=='root':continue  # No animation track on the ground root.
            for prop in ('location','rotation_quaternion','scale'):
                pb.keyframe_insert(data_path=prop,frame=frame,group=pb.name)
    action.use_fake_user=True
    track=rig.animation_data.nla_tracks.new();track.name=clip
    strip=track.strips.new(clip,0,action);track.mute=True
rig.animation_data.action=None
for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
bpy.context.scene.render.fps=FPS;bpy.context.scene.frame_start=0;bpy.context.scene.frame_end=75
bpy.context.scene.frame_set(0);bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'wolf.blend'))
mesh.select_set(True);rig.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(OUT),export_format='GLB',use_selection=True,
    export_yup=True,export_animations=True,export_animation_mode='ACTIONS',
    export_frame_range=False,export_force_sampling=True,export_skins=True,
    export_cameras=False,export_lights=False,export_all_vertex_colors=True)
data.calc_loop_triangles()
stats={'triangles':len(data.loop_triangles),'materials':1,'bones':len(rest),
       'clips':{name:{'seconds':frames/FPS,'loop':name!='attack'} for name,frames in clips.items()},
       'withers_m':.75,'torso_length_m':1.16,'root_motion':False}
stats['size_x_y_z']=[max(v[i] for v in vertices)-min(v[i] for v in vertices) for i in (0,2,1)]
(SOURCE/'wolf_geometry.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf8')
catalog_path=SOURCE/'geometry.json'
catalog=json.loads(catalog_path.read_text(encoding='utf8')) if catalog_path.exists() else {}
catalog['wolf']={key:stats[key] for key in ('triangles','size_x_y_z')}
catalog_path.write_text(json.dumps(catalog,indent=2)+'\n',encoding='utf8')
print('WOLF_ASSET',json.dumps(stats))
