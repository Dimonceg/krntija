"""B1: detachable Zastava panels. Reuse exterior without rewriting original assets.
Run Blender --background --python tools/blender_zastava_modular.py.
Authoring coordinates: X, Y up, front -Z. Panel geometry is baked around its hinge.
"""
from pathlib import Path
source = Path(__file__).with_name('blender_zastava.py')
exec(compile(source.read_text(encoding='utf8').split('# A reusable wheel mesh')[0], str(source), 'exec'))
from mathutils import Matrix
import hashlib
import re

OUT = ROOT/'art/blender/zastava-modular'
OUT.mkdir(parents=True, exist_ok=True)
colls = {'shell': body}
for key in ('door_fl','door_fr','door_rl','door_rr','hood','trunk'):
    coll = bpy.data.collections.new(key)
    bpy.context.scene.collection.children.link(coll)
    colls[key] = coll

def move(ob, key):
    for c in list(ob.users_collection): c.objects.unlink(ob)
    colls[key].objects.link(ob)

def centre(ob):
    points = [ob.matrix_world @ Vector(p) for p in ob.bound_box]
    return sum(points, Vector())/8

# Route existing fittings to their moving panel. Discard the old closed greenhouse.
bpy.context.view_layer.update()
for ob in list(body.objects):
    name = ob.name
    if name.startswith(('roof and pillars','door vertical shut','rear door shut','door lower seam','bonnet seam','hatch edge')):
        bpy.data.objects.remove(ob, do_unlink=True)
        continue
    if name.startswith(('front door glass','rear door glass','front quarterlight','door handle','chrome door handle','mirror')):
        c = centre(ob); side = 'l' if c.x < 0 else 'r'
        row = 'r' if name.startswith('rear door') or -c.y > .3 else 'f'
        move(ob, 'door_'+row+side)
    elif name.startswith(('hatch window','hatch handle')):
        move(ob, 'trunk')

# Hollow the pressed lower shell, retaining its original wheel arches and shoulders.
shell.modifiers.clear()
def boolean(ob, cutter, operation='DIFFERENCE'):
    bpy.context.view_layer.objects.active = ob
    mod = ob.modifiers.new(operation, 'BOOLEAN'); mod.operation=operation
    mod.solver='EXACT'; mod.object=cutter
    bpy.ops.object.modifier_apply(modifier=mod.name)

for name, pos, size in [
    ('cabin cavity',(0,.50,.44),(1.40,1.70,2.60)),
    ('engine cavity',(0,.50,-1.35),(1.34,1.64,.84)),
]:
    cutter=box(name,pos,size,rubber,0); boolean(shell,cutter)
    bpy.data.objects.remove(cutter,do_unlink=True)

# Each lower door is cut out of the same stamping: closed edges match exactly.
for side, suffix in ((-1,'l'),(1,'r')):
    for row,z0,z1 in (('f',-.85,.15),('r',.19,1.095)):
        key='door_'+row+suffix
        cutter=box('panel cutting tool',(side*.79,.30,(z0+z1)/2),(.23,1.38,z1-z0),rubber,0)
        panel=shell.copy();panel.data=shell.data.copy();colls[key].objects.link(panel)
        panel.name=key+' pressed lower skin';boolean(panel,cutter,'INTERSECT');boolean(shell,cutter)
        bpy.data.objects.remove(cutter,do_unlink=True)

cloth=material('faded tobacco vinyl',(.17,.12,.075),0,.90)
cloth_edge=material('worn seat piping',(.28,.22,.14),0,.85)
steel=material('engine bay aged steel',(.19,.20,.17),.25,.75)
rust=material('oxidised seams',(.22,.085,.035),.1,.95)

def plate_mesh(name, points, mat, thickness=.018):
    ob=mesh(name,points,[tuple(range(len(points)))],mat)
    mod=ob.modifiers.new('folded panel thickness','SOLIDIFY');mod.thickness=thickness
    return ob

active_collection=body
# Thin roof follows the original crown, with real A/B/C pillars and open doorways.
roof_points=[(-.635,.802,-.43),(-.632,.837,-.29),(-.628,.837,.72),(-.640,.79,.88),
             (.640,.79,.88),(.628,.837,.72),(.632,.837,-.29),(.635,.802,-.43)]
plate_mesh('roof stamping',roof_points,paint,.025)
plate_mesh('windscreen upper frame',[(-.635,.802,-.43),(.635,.802,-.43),
    (.592,.752,-.485),(-.592,.752,-.485)],paint,.02)
plate_mesh('windscreen cowl',[(-.741,.332,-.91),(.741,.332,-.91),
    (.691,.382,-.863),(-.691,.382,-.863)],paint,.02)
line('hatch hinge header',[(-.638,.79,.89),(.638,.79,.89)],paint,.018)
for side in (-1,1):
    line('A pillar',[(side*.741,.332,-.91),(side*.635,.802,-.43)],paint,.029)
    line('B pillar',[(side*.758,-.37,.17),(side*.743,.32,.17),(side*.634,.813,.17)],paint,.023)
    line('C pillar',[(side*.631,.813,.75),(side*.679,.60,1.07),(side*.748,.29,1.14)],paint,.025)
    plate_mesh('rear quarter frame',[(side*.677,.59,1.07),(side*.64,.79,.88),
        (side*.713,.307,1.67),(side*.744,.28,1.10)],paint,.025)
    # Fixed quarter glass is already part of the original exterior.
    box('door threshold',(side*.728,-.39,.10),(.065,.055,1.90),steel,0)
    box('footwell mat',(side*.34,-.337,-.35),(.52,.008,.61),rubber,0)
    line('sill worn seam',[(side*.764,-.37,-.80),(side*.764,-.37,.80)],rust,.006)

box('firewall',(0,-.02,-.895),(1.39,.66,.045),steel,0)
box('dashboard padded top',(0,.29,-.71),(1.34,.13,.27),rubber,.025)
box('dashboard instrument binnacle',(-.36,.34,-.62),(.42,.14,.12),steel,.015)
for x,r in ((-.45,.051),(-.32,.041)):
    cylinder('instrument dial',(x,.35,-.551),r,.006,plate,'Z',12)
    line('instrument needle',[(x,.35,-.546),(x+.015,.378,-.546)],rubber,.003)
for x in (.25,.55): box('dashboard vent',(x,.29,-.565),(.16,.05,.008),steel,0)
line('steering column',[(-.36,.18,-.66),(-.36,.32,-.43)],steel,.022)
steering=[(-.36+.16*math.cos(i*math.tau/20),.36+.14*math.sin(i*math.tau/20),-.40) for i in range(20)]
line('steering wheel',steering,rubber,.013,True)
for a in (0,2.1,4.2):line('steering spoke',[(-.36,.36,-.40),(-.36+.15*math.cos(a),.36+.13*math.sin(a),-.40)],chrome,.009)
for side in (-1,1):
    box('front seat cushion',(side*.34,-.13,.05),(.51,.17,.52),cloth,.045)
    back=box('front seat backrest',(side*.34,.13,.30),(.51,.51,.12),cloth,.035)
    back.rotation_euler.x=math.radians(-9)
    for x in (-.16,0,.16):
        line('seat stitched rib',[(side*.34+x,-.039,-.17),(side*.34+x,-.039,.22)],cloth_edge,.004)
        line('seat back stitched rib',[(side*.34+x,-.04,.227),(side*.34+x,.32,.284)],cloth_edge,.004)
box('rear bench cushion',(0,-.11,.97),(1.21,.18,.44),cloth,.035)
box('rear bench backrest',(0,.15,1.23),(1.21,.45,.13),cloth,.035)
box('transmission tunnel',(0,-.24,-.03),(.18,.16,1.55),rubber,.015)
line('gear lever',[(.04,-.17,-.21),(.04,.01,-.28)],steel,.012)
box('gear knob',(.04,.02,-.28),(.045,.05,.045),rubber,.01)
box('cargo floor',(0,-.31,1.52),(1.24,.022,.49),rubber,0)
# Visible mounting trays remain when all three mechanical parts are removed.
for x in (-.28,.28):box('engine mounting rail',(x,-.285,-1.30),(.06,.07,.62),steel,0)
box('battery tray',(-.49,-.17,-1.17),(.30,.025,.23),steel,0)
box('radiator lower crossmember',(0,-.28,-1.69),(.87,.05,.06),steel,0)
for side in (-1,1):
    cylinder('strut tower',(side*.57,-.10,-1.26),.105,.26,steel,'Y',12)
    cylinder('strut top nut',(side*.57,.04,-1.26),.027,.018,chrome,'Y',6)
    line('bay spot welded flange',[(side*.66,.20,-1.75),(side*.66,.28,-.96)],rust,.006)

# Window frames and upholstered inside faces travel with their doors.
for side,suffix in ((-1,'l'),(1,'r')):
    for row, coords in [('f',[(-.85,.32),(-.40,.79),(.15,.80),(.15,.32)]),
                        ('r',[(.19,.32),(.19,.80),(.73,.80),(1.095,.58),(1.095,.32)])]:
        active_collection=colls['door_'+row+suffix]
        pts=[(side*(side_x(h)+.006),h,z) for z,h in coords]
        line('pressed door window frame',pts,paint,.018,True)
        zc=-.35 if row=='f' else .63
        box('door vinyl inner card',(side*.708,-.005,zc),(.016,.49,.79 if row=='f' else .64),cloth,.008)
        box('door armrest',(side*.674,.05,zc),(.07,.045,.26),rubber,.008)
        line('manual window crank',[(side*.69,.16,zc-.16),(side*.658,.115,zc-.12)],chrome,.008)
        for h in (-.21,.20):box('door hinge',(side*.724,h,-.84 if row=='f' else .20),(.055,.052,.035),steel,0)

active_collection=colls['hood']
hoodpts=[(-.669,.223,-1.77),(.669,.223,-1.77),(.669,.342,-.93),(-.669,.342,-.93)]
plate_mesh('bonnet outer stamping',hoodpts,paint,.022)
for x in (-.42,.42):line('bonnet underside strengthening rib',[(x,.196,-1.72),(x,.31,-.97)],steel,.014)
line('bonnet front rolled edge',[(-.65,.217,-1.77),(.65,.217,-1.77)],paint,.012)
active_collection=colls['trunk']
# Fastback tailgate, rather than a horizontal saloon boot lid. Window is a real opening.
outer=[(-.638,.785,.90),(.638,.785,.90),(.714,.319,1.67),(.65,.245,1.85),(-.65,.245,1.85),(-.714,.319,1.67)]
# Four strips around the existing rear glass, plus lower hatch apron.
for name,pts in [
    ('hatch top rail',[outer[0],outer[1],rear_glass[1],rear_glass[0]]),
    ('hatch right rail',[outer[1],outer[2],rear_glass[2],rear_glass[1]]),
    ('hatch left rail',[outer[5],outer[0],rear_glass[0],rear_glass[3]]),
    ('hatch lower panel',[rear_glass[3],rear_glass[2],outer[2],outer[3],outer[4],outer[5]])]:
    plate_mesh(name,pts,paint,.022)
line('hatch inner reinforcing rib',[(-.58,.30,1.64),(0,.30,1.64),(.58,.30,1.64)],steel,.014)

SOCKETS={'engine':(0,-.27,-1.31),'radiator':(0,-.26,-1.69),'battery':(-.49,-.155,-1.17),
    'hood':(0,.342,-.93),'trunk':(0,.785,.90),
    'door_fl':(-.74,-.03,-.85),'door_fr':(.74,-.03,-.85),
    'door_rl':(-.74,-.03,.19),'door_rr':(.74,-.03,.19),
    'wheel_fl':(-.65,-.295,-1.2245),'wheel_fr':(.65,-.295,-1.2245),
    'wheel_rl':(-.65,-.295,1.2245),'wheel_rr':(.65,-.295,1.2245)}
OPEN={'door_fl':('Y',-70),'door_fr':('Y',70),'door_rl':('Y',-70),'door_rr':('Y',70),
      'hood':('X',70),'trunk':('X',-65)}
C=Matrix(((1,0,0),(0,0,-1),(0,1,0)))
for key,pos in SOCKETS.items():
    anchor=bpy.data.objects.new('Socket_'+key,None);body.objects.link(anchor);anchor.location=xyz(pos)
    anchor.empty_display_size=.06
    if key in OPEN:
        axis,deg=OPEN[key];ob=bpy.data.objects.new('Open_'+key,None);body.objects.link(ob)
        ob.location=xyz(pos);ob.rotation_euler=(C @ Matrix.Rotation(math.radians(deg),3,axis) @ C.inverted()).to_euler()
        ob.empty_display_size=.06

finish=Path(__file__).with_name('blender_zastava_finish.py')
exec(compile(finish.read_text(encoding='utf8'),str(finish),'exec'))
