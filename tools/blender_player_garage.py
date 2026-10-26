"""Attached assembly bay, separate from the small existing house workshop."""
from pathlib import Path
kit=Path(__file__).with_name('blender_player_house.py')
exec(compile(kit.read_text(encoding='utf8').split("begin('home_roof')")[0],str(kit),'exec'))
begin('home_garage')
roof=b('standing seam garage roof',(9,3.0,-1),(7.7,.12,9.6),steel)
roof.rotation_euler[1]=math.atan2(.3,7)
for x in (5.35,6.15,6.95,7.75,8.55,9.35,10.15,10.95,11.75,12.55):
    y=3.15-(x-5.5)*.3/7
    b('roof seam',(x,y+.075,-1),(.035,.035,9.6),rust)
for z in (-5.5,3.5):
    mesh('roof infill',[(5.5,2.8,z-.1),(12.5,2.8,z-.1),(12.5,2.85,z-.1),(5.5,3.15,z-.1),
        (5.5,2.8,z+.1),(12.5,2.8,z+.1),(12.5,2.85,z+.1),(5.5,3.15,z+.1)],
        [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],plaster)
b('gutter',(12.78,2.81,-1),(.13,.10,9.6),steel)
b('garage drain',(12.65,1.35,3.6),(.10,2.7,.10),steel)
for x in (7.1,10.9):b('gate frame',(x,1.4,-5.65),(.13,2.5,.15),wood)
b('gate lintel',(9,2.73,-5.65),(3.95,.16,.15),wood)
# Clear central working area; small parts can be laid on the second bench.
box('assembly bench top',(11.3,.9,2.7),(1.7,.12,.8),lightwood,.02)
for x in (10.58,12.02):
    for z in (2.4,3.0):b('bench leg',(x,.43,z),(.09,.86,.09),steel)
b('assembly shelf',(11.3,.25,2.7),(1.5,.06,.66),wood)
for x in (7,11):
    b('strip light housing',(x,2.65,-1),(.18,.08,1.2),steel)
    b('strip diffuser',(x,2.60,-1),(.13,.025,1.10),linen)
for x in (6.8,11.2):b('parking guide',(x,.013,-1),(.06,.008,5.8),enamel)
for x in (6.1,11.9):
    b('gate rust patch',(x,.35,-5.63),(.35,.18,.018),rust)
for ob in active_collection.objects:
    if ob.name.startswith(('assembly bench','bench leg','assembly shelf','parking guide')):
        ob.location.z+=.15
# Named action-free anchor nodes survive export alongside the joined mesh.
anchors={'GarageCarAnchor':(9,0,-1), 'PartSpot_0':(7,.4,2.5),
 'PartSpot_1':(11.3,1.04,2.7),'PartSpot_2':(6.2,.25,1.0),
 'PartSpot_3':(11.5,.2,.9),'PartSpot_4':(11.5,.2,-.9),
 'PartSpot_5':(6.2,.2,-1.4),'PartSpot_6':(10.8,1.04,2.7),'PartSpot_7':(6.3,.2,-3.3)}
anchors={name:(p[0],p[1]+.15,p[2]) for name,p in anchors.items()}
# Reuse established palette export, without rebuilding any other home asset.
tail=kit.read_text(encoding='utf8').split("bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'player_house.blend'))")[1]
tail=tail.replace("SOURCE/'geometry.json'","SOURCE/'garage_geometry.json'")
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'player_garage.blend'))
exec(compile(tail,str(kit),'exec'))
for name,p in anchors.items():
    ob=bpy.data.objects.new(name,None);active_collection.objects.link(ob);ob.location=xyz(p);ob.select_set(True)
# Save editable scene including the anchors and export the complete selection.
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'player_garage.blend'))
bpy.ops.export_scene.gltf(filepath=str(ASSETS/'home_garage.glb'),export_format='GLB',use_selection=True,
    export_yup=True,export_apply=True,export_animations=False,export_cameras=False,export_lights=False,export_all_vertex_colors=True)
