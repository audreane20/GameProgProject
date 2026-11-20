# The hospital creature, v2: one continuous emaciated body (skin modifier + subdivision + lumpy displacement),
# auto-weighted to the rig, with a hinged jaw, teeth, sunken glowing eyes and long finger claws.
# Animations are deliberately twitchy and uneven. Blender coords: Z up, faces -Y, left = +X.
import bpy, bmesh, math, json, sys, random
from mathutils import Vector, Matrix, noise

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else '/home/claude/kid/out'
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 30
random.seed(3)

COLORS = {
    'MonsterSkin':  ((0.03, 0.026, 0.03), 0.38),
    'MonsterClaw':  ((0.24, 0.21, 0.17), 0.45),
    'MonsterTeeth': ((0.42, 0.38, 0.3), 0.5),
    'MonsterMouth': ((0.05, 0.0, 0.0), 0.3),
    'MonsterEyes':  ((1.0, 0.05, 0.02), 0.8),
}
MATS = {}
for n, (c, rough) in COLORS.items():
    m = bpy.data.materials.new(n)
    m.diffuse_color = (*c, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*c, 1)
    bsdf.inputs['Roughness'].default_value = rough
    MATS[n] = m

# ---------------- skeleton ----------------
BONES = {
    'Hips':  ((0, 0, 1.03), (0, 0, 1.2), None),
    'Spine': ((0, 0, 1.2), (0, 0, 1.4), 'Hips'),
    'Chest': ((0, 0, 1.4), (0, 0, 1.62), 'Spine'),
    'Neck':  ((0, 0, 1.62), (0, -0.03, 1.82), 'Chest'),
    'Head':  ((0, -0.03, 1.82), (0, -0.03, 2.06), 'Neck'),
    'Jaw':   ((0, -0.07, 1.89), (0, -0.21, 1.83), 'Head'),
}
for side, sx in (('Left', 1), ('Right', -1)):
    BONES.update({
        f'{side}Shoulder': ((0.04*sx, 0, 1.6), (0.2*sx, 0, 1.58), 'Chest'),
        f'{side}UpperArm': ((0.2*sx, 0, 1.58), (0.28*sx, 0, 1.12), f'{side}Shoulder'),
        f'{side}LowerArm': ((0.28*sx, 0, 1.12), (0.31*sx, 0, 0.66), f'{side}UpperArm'),
        f'{side}Hand':     ((0.31*sx, 0, 0.66), (0.32*sx, -0.01, 0.52), f'{side}LowerArm'),
        f'{side}Fingers':  ((0.32*sx, -0.01, 0.52), (0.33*sx, -0.04, 0.28), f'{side}Hand'),
        f'{side}UpperLeg': ((0.12*sx, 0, 1.0), (0.13*sx, -0.015, 0.56), 'Hips'),
        f'{side}LowerLeg': ((0.13*sx, -0.015, 0.56), (0.13*sx, 0, 0.09), f'{side}UpperLeg'),
        f'{side}Foot':     ((0.13*sx, 0, 0.09), (0.13*sx, -0.20, 0.02), f'{side}LowerLeg'),
    })

arm_data = bpy.data.armatures.new('MonsterArmature')
arm = bpy.data.objects.new('Monster', arm_data)
scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
for name, (h, t, p) in BONES.items():
    b = arm_data.edit_bones.new(name)
    b.head, b.tail, b.roll = h, t, 0
for name, (h, t, p) in BONES.items():
    if p:
        arm_data.edit_bones[name].parent = arm_data.edit_bones[p]
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------- organic body from a skin-modifier graph ----------------
verts, radii, edges = [], [], []
def V(p, r):
    verts.append(p); radii.append(r); return len(verts) - 1
def E(a, b): edges.append((a, b))
def chain(points):
    ids = [V(p, r) for p, r in points]
    for a, b in zip(ids, ids[1:]): E(a, b)
    return ids

pelvis = V((0, 0.0, 1.02), 0.105)
spine = chain([((0, 0.02, 1.13), 0.075), ((0, 0.03, 1.24), 0.06),       # pinched, starved waist
               ((0, 0.02, 1.36), 0.11), ((0, 0.0, 1.48), 0.135),          # ribcage
               ((0, 0.0, 1.58), 0.12)])
E(pelvis, spine[0])
neck = chain([((0, -0.01, 1.66), 0.045), ((0, -0.03, 1.77), 0.036), ((0, -0.045, 1.84), 0.045)])
E(spine[-1], neck[0])
skull = chain([((0, -0.04, 1.9), 0.085), ((0, -0.03, 1.97), 0.105), ((0, -0.05, 2.03), 0.09),
               ((0, -0.04, 2.08), 0.05)])
E(neck[-1], skull[0])
snout = chain([((0, -0.13, 1.95), 0.07), ((0, -0.2, 1.935), 0.05)])      # long face
E(skull[1], snout[0])
for sx in (1, -1):
    arm_ids = chain([((0.11*sx, 0.0, 1.6), 0.06), ((0.2*sx, 0.01, 1.58), 0.05),
                     ((0.25*sx, 0.0, 1.35), 0.032), ((0.28*sx, 0.0, 1.12), 0.04),     # knobby elbow
                     ((0.295*sx, 0.0, 0.9), 0.026), ((0.31*sx, 0.0, 0.67), 0.03),
                     ((0.318*sx, -0.008, 0.58), 0.04)])
    E(spine[-1], arm_ids[0])
    hand = arm_ids[-1]
    for i, dx in enumerate((-0.035, -0.012, 0.012, 0.035)):          # long bony fingers
        f = chain([((0.32*sx + dx * 0.6 * sx, -0.012 - abs(dx) * 0.3, 0.52), 0.013),
                   ((0.322*sx + dx * sx, -0.03, 0.42 + abs(dx)), 0.01),
                   ((0.325*sx + dx * 1.2 * sx, -0.05 + abs(dx) * 0.4, 0.31 + abs(dx) * 1.2), 0.004)])
        E(hand, f[0])
    leg = chain([((0.11*sx, 0.0, 0.97), 0.065), ((0.125*sx, -0.005, 0.78), 0.045),
                 ((0.13*sx, -0.02, 0.56), 0.05), ((0.13*sx, -0.005, 0.33), 0.03),       # bony knee
                 ((0.13*sx, 0.0, 0.1), 0.03), ((0.13*sx, -0.1, 0.035), 0.03)])
    E(pelvis, leg[0])
    for dx in (-0.03, 0, 0.03):
        t = chain([((0.13*sx + dx * 0.6, -0.17, 0.025), 0.016), ((0.13*sx + dx, -0.25, 0.008), 0.005)])
        E(leg[-1], t[0])

me = bpy.data.meshes.new('MonsterBody')
me.from_pydata(verts, edges, [])
body = bpy.data.objects.new('MonsterBody', me)
scene.collection.objects.link(body)
skin = body.modifiers.new('Skin', 'SKIN')
skin.branch_smoothing = 0.6
skin.use_smooth_shade = True
for i, sv in enumerate(me.skin_vertices[0].data):
    sv.radius = (radii[i], radii[i] * (0.8 if 0 < i < 6 else 1.0))
me.skin_vertices[0].data[pelvis].use_root = True
sub = body.modifiers.new('Subd', 'SUBSURF'); sub.levels = 2; sub.render_levels = 2
bpy.context.view_layer.objects.active = body
for mod in list(body.modifiers):
    bpy.ops.object.modifier_apply(modifier=mod.name)

# lumpy, sinewy skin: displace along normals with layered noise (stronger on the back and limbs)
bm = bmesh.new(); bm.from_mesh(body.data)
bm.normal_update()
for v in bm.verts:
    p = v.co
    n1 = noise.noise(p * 14.0)
    n2 = noise.noise(p * 40.0 + Vector((3, 1, 2)))
    ridge = abs(noise.noise(Vector((p.x * 6, p.y * 6, p.z * 22))))      # stretched, vein-like ridges
    v.co += v.normal * (0.006 * n1 + 0.0025 * n2 + 0.004 * ridge)
bm.to_mesh(body.data); bm.free()
for poly in body.data.polygons:
    poly.use_smooth = True
body.data.materials.append(MATS['MonsterSkin'])

# auto weights from the skeleton (bone heat)
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True); arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
empty_groups = [g.name for g in body.vertex_groups
                if not any(g.index in [x.group for x in v.groups] for v in body.data.vertices)]
print('WEIGHT GROUPS', len(body.vertex_groups), 'empty:', empty_groups)
# the jaw is a separate rigid piece: keep the skull skin on the head
if 'Jaw' in body.vertex_groups:
    jaw_g = body.vertex_groups['Jaw']; head_g = body.vertex_groups['Head']
    for v in body.data.vertices:
        for g in v.groups:
            if g.group == jaw_g.index and g.weight > 0:
                head_g.add([v.index], g.weight, 'ADD')
    body.vertex_groups.remove(jaw_g)

# ---------------- rigid parts: jaw, teeth, mouth, eyes ----------------
rigid = {'MonsterBody': [], 'MonsterEyes': []}
def finish(bmx, mat, bone, target='MonsterBody'):
    m = bpy.data.meshes.new(bone)
    bmx.to_mesh(m); bmx.free()
    for poly in m.polygons: poly.use_smooth = True
    ob = bpy.data.objects.new(bone, m)
    scene.collection.objects.link(ob)
    ob.data.materials.append(MATS[mat])
    vg = ob.vertex_groups.new(name=bone)
    vg.add(list(range(len(m.vertices))), 1.0, 'REPLACE')
    rigid[target].append(ob)
def ball(c, r, mat, bone, target='MonsterBody', scale=(1, 1, 1), u=12, v=8):
    bmx = bmesh.new()
    bmesh.ops.create_uvsphere(bmx, u_segments=u, v_segments=v, radius=r)
    bmesh.ops.scale(bmx, vec=scale, verts=bmx.verts)
    bmesh.ops.translate(bmx, vec=c, verts=bmx.verts)
    finish(bmx, mat, bone, target)
def cone(a, b, r1, r2, mat, bone, seg=6):
    a, b = Vector(a), Vector(b)
    bmx = bmesh.new()
    bmesh.ops.create_cone(bmx, cap_ends=True, segments=seg, radius1=r1, radius2=r2, depth=(b - a).length)
    rot = Vector((0, 0, 1)).rotation_difference((b - a).normalized()).to_matrix().to_4x4()
    bmesh.ops.transform(bmx, matrix=Matrix.Translation((a + b) / 2) @ rot, verts=bmx.verts)
    finish(bmx, mat, bone)

ball((0, -0.15, 1.875), 0.065, 'MonsterSkin', 'Jaw', scale=(0.95, 1.5, 0.42))       # lower jaw
ball((0, -0.16, 1.9), 0.05, 'MonsterMouth', 'Head', scale=(1.0, 1.4, 0.5))           # throat/mouth
for i in range(9):                                                                   # crooked teeth
    a = -0.6 + i * 0.15
    x, y = 0.05 * math.sin(a), -0.13 - 0.1 * math.cos(a) ** 2
    L = random.uniform(0.018, 0.035)
    cone((x, y, 1.915), (x + random.uniform(-0.006, 0.006), y - 0.004, 1.915 - L), 0.0065, 0.0006, 'MonsterTeeth', 'Head')
    L = random.uniform(0.014, 0.028)
    cone((x * 0.95, y + 0.008, 1.888), (x * 0.95, y + 0.004, 1.888 + L), 0.006, 0.0006, 'MonsterTeeth', 'Jaw')
for sx in (1, -1):
    ball((0.047*sx, -0.162, 1.985), 0.024, 'MonsterMouth', 'Head', scale=(1.15, 0.42, 0.8))   # sunken sockets
    ball((0.047*sx, -0.17, 1.984), 0.0125, 'MonsterEyes', 'Head', target='MonsterEyes', scale=(1.2, 0.8, 0.9), u=10, v=8)
    for i, dx in enumerate((-0.035, -0.012, 0.012, 0.035)):                             # claw tips
        tip = Vector((0.325*sx + dx * 1.2 * sx, -0.05 + abs(dx) * 0.4, 0.31 + abs(dx) * 1.2))
        cone(tip + Vector((0, 0, 0.03)), tip - Vector((0, 0.015, 0.05)), 0.007, 0.0008, 'MonsterClaw', f'{"Left" if sx > 0 else "Right"}Fingers')

meshes = [body]
for target, obs in rigid.items():
    bpy.ops.object.select_all(action='DESELECT')
    if target == 'MonsterBody':
        for o in obs: o.select_set(True)
        body.select_set(True)
        bpy.context.view_layer.objects.active = body
        bpy.ops.object.join()
    else:
        for o in obs: o.select_set(True)
        bpy.context.view_layer.objects.active = obs[0]
        bpy.ops.object.join()
        ob = bpy.context.active_object
        ob.name = ob.data.name = target
        ob.parent = arm
        mod = ob.modifiers.new('Armature', 'ARMATURE'); mod.object = arm
        meshes.append(ob)
body = bpy.data.objects['MonsterBody']
meshes[0] = body
print('BODY VERTS', len(body.data.vertices))

# ---------------- animation ----------------
import numpy as np
pb = arm.pose.bones
for b in pb:
    b.rotation_mode = 'XYZ'
deps = bpy.context.evaluated_depsgraph_get()

def reset():
    for b in pb:
        b.rotation_euler = (0, 0, 0); b.location = (0, 0, 0)

def apply_pose(pose):
    reset()
    for key, val in pose.items():
        if key == 'hips_loc':
            pb['Hips'].location = val; continue
        bone, axis = key.split('.')
        e = list(pb[bone].rotation_euler)
        e['XYZ'.index(axis)] += math.radians(val)
        pb[bone].rotation_euler = e
    bpy.context.view_layer.update()

def lowest_z():
    deps.update()
    lo = 1e9
    for ob in meshes:
        ev = ob.evaluated_get(deps)
        m = ev.to_mesh()
        co = np.empty(len(m.vertices) * 3); m.vertices.foreach_get('co', co)
        co = co.reshape(-1, 3)
        mw = np.array(ev.matrix_world)
        z = co @ mw[2, :3] + mw[2, 3]
        lo = min(lo, float(z.min()))
        ev.to_mesh_clear()
    return lo

def ground_fix(pose):
    apply_pose(pose)
    x, y, z = pose.get('hips_loc', (0, 0, 0))
    pose['hips_loc'] = (x, y - lowest_z(), z)
    return pose

# find which local axis lifts the shoulder and which opens the jaw (sign-safe)
def tail_z(bone, axis, deg):
    apply_pose({f'{bone}.{axis}': deg})
    return (arm.matrix_world @ pb[bone].tail).z
base = tail_z('LeftShoulder', 'X', 0)
SHRUG = max(('X', 1), ('X', -1), ('Z', 1), ('Z', -1), key=lambda a: tail_z('LeftShoulder', a[0], 20 * a[1]) - base)
SHRUG_R = max(('X', 1), ('X', -1), ('Z', 1), ('Z', -1), key=lambda a: tail_z('RightShoulder', a[0], 20 * a[1]) - tail_z('RightShoulder', 'X', 0))
JAW = 1 if tail_z('Jaw', 'X', 20) < tail_z('Jaw', 'X', 0) else -1
print('SHRUG', SHRUG, SHRUG_R, 'JAW', JAW)

def make_action(name, length, fn, fix=True):
    act = bpy.data.actions.new(name); act.use_fake_user = True
    arm.animation_data_create(); arm.animation_data.action = act
    for f in range(0, length + 1):
        pose = fn(f / length)
        if fix: pose = ground_fix(pose)
        apply_pose(pose)
        for b in pb: b.keyframe_insert('rotation_euler', frame=f)
        pb['Hips'].keyframe_insert('location', frame=f)
    act.frame_range = (0, length)

S = lambda p, k=1, o=0: math.sin(2 * math.pi * (p * k + o))
C = lambda p, k=1, o=0: math.cos(2 * math.pi * (p * k + o))
pos = lambda v: max(0.0, v)
def snap(p, at, hold=0.06, rise=0.012, fall=0.05):
    """A sudden jerk: snaps in over ~1 frame, holds, then eases back."""
    d = (p - at) % 1.0
    if d < rise: return d / rise
    if d < rise + hold: return 1.0
    if d < rise + hold + fall: return 1.0 - (d - rise - hold) / fall
    return 0.0
def tremble(p, seed, amp, speed=40):
    # loops cleanly: noise sampled on a circle
    a = 2 * math.pi * p
    return amp * noise.noise(Vector((math.cos(a) * speed / 6.28, math.sin(a) * speed / 6.28, seed)))

def add(pose, key, v): pose[key] = pose.get(key, 0) + v
def hunch(pose, lean=1.0):
    add(pose, 'Spine.X', 22 * lean); add(pose, 'Chest.X', 18 * lean)
    add(pose, 'Neck.X', 10 * lean); add(pose, 'Head.X', -48 * lean)
    return pose
def shrug(pose, l, r):
    add(pose, f'LeftShoulder.{SHRUG[0]}', l * SHRUG[1]); add(pose, f'RightShoulder.{SHRUG_R[0]}', r * SHRUG_R[1])
def jaw(pose, deg): add(pose, 'Jaw.X', deg * JAW)
def legs(pose, tL, tR, kL, kR, flat=0.8):
    add(pose, 'LeftUpperLeg.X', tL); add(pose, 'RightUpperLeg.X', tR)
    add(pose, 'LeftLowerLeg.X', kL); add(pose, 'RightLowerLeg.X', kR)
    add(pose, 'LeftFoot.X', -(tL + kL) * flat); add(pose, 'RightFoot.X', -(tR + kR) * flat)

def idle(p):
    pose = hunch({})
    add(pose, 'Chest.X', 3 * S(p, 2))                                   # slow heavy breathing
    add(pose, 'Head.Y', 22 * S(p, 1) - 40 * snap(p, 0.68, 0.1))         # looks around... then snaps
    add(pose, 'Head.Z', 30 * snap(p, 0.33, 0.12) + tremble(p, 1, 3))
    add(pose, 'Hips.Z', 4 * S(p, 1, 0.2))
    shrug(pose, 10 * snap(p, 0.5, 0.04) + 4, 4 - 6 * snap(p, 0.85, 0.05))
    for side, o in (('Left', 0.1), ('Right', 0.6)):
        add(pose, f'{side}UpperArm.X', 4 * S(p, 1, o) + tremble(p, 2 + o, 2))
        add(pose, f'{side}LowerArm.X', -8)
        add(pose, f'{side}Fingers.X', -18 + 12 * S(p, 2, o) + tremble(p, 5 + o, 6, 60))
    add(pose, 'LeftUpperArm.Y', 6); add(pose, 'RightUpperArm.Y', -6)
    jaw(pose, 6 + 4 * S(p, 2) + 18 * snap(p, 0.33, 0.12))
    legs(pose, -10, -6, 20, 14)
    return pose

def walk(p):
    s, c = S(p), C(p)
    pose = hunch({}, 1.15)
    # good left leg takes a long stride, bad right leg barely lifts and drags
    legs(pose, -30 * s - 6, 12 * s - 4, 12 + 50 * pos(c), 26 + 8 * pos(-c), flat=0.8)
    add(pose, 'RightFoot.X', 18)                                          # toes dragging
    add(pose, 'Hips.Z', -9 * pos(-s) + 3)                                 # lurches onto the bad leg
    add(pose, 'Hips.Y', 7 * s); add(pose, 'Chest.Y', -9 * s); add(pose, 'Spine.Z', 6 * pos(-s))
    add(pose, 'Head.Z', 22 * S(p, 1, 0.3) - 38 * snap(p, 0.8, 0.07) + tremble(p, 7, 2))
    add(pose, 'Head.Y', 8 * S(p, 1, 0.1))
    add(pose, 'LeftUpperArm.X', 20 * S(p, 1, 0.12)); add(pose, 'LeftLowerArm.X', -10 - 10 * pos(S(p, 1, 0.2)))
    add(pose, 'RightUpperArm.X', -7 * S(p, 1, 0.15) - 45 * snap(p, 0.45, 0.05))   # limp arm that twitches
    add(pose, 'RightLowerArm.X', -5 - 30 * snap(p, 0.45, 0.05))
    add(pose, 'LeftUpperArm.Y', 6); add(pose, 'RightUpperArm.Y', -8)
    add(pose, 'LeftFingers.X', -20); add(pose, 'RightFingers.X', -10 + tremble(p, 9, 8, 70))
    shrug(pose, 5 + 4 * pos(s), 12 * pos(-s))
    jaw(pose, 10 + 6 * S(p, 2))
    return pose

def run(p):
    s, c = S(p), C(p)
    pose = hunch({}, 2.1)                                                 # torso almost flat: scrambling
    add(pose, 'Head.X', -30); add(pose, 'Neck.X', -20)
    add(pose, 'Head.Z', tremble(p, 11, 10, 30))
    add(pose, 'LeftUpperArm.X', -65 + 45 * s); add(pose, 'RightUpperArm.X', -65 - 45 * s)
    add(pose, 'LeftLowerArm.X', -10 - 20 * pos(-s)); add(pose, 'RightLowerArm.X', -10 - 20 * pos(s))
    add(pose, 'LeftUpperArm.Y', 14); add(pose, 'RightUpperArm.Y', -14)
    add(pose, 'LeftFingers.X', -35); add(pose, 'RightFingers.X', -35)
    legs(pose, -50 * S(p, 1, 0.5) - 15, -50 * S(p, 1, 0.65) - 15,
         30 + 80 * pos(C(p, 1, 0.5)), 30 + 80 * pos(C(p, 1, 0.65)), flat=0.5)
    shrug(pose, 10 * pos(s), 10 * pos(-s))
    jaw(pose, 32 + 6 * S(p, 2))
    return pose

def stare(p):
    pose = hunch({}, 0.85)
    # head slowly tilts over... further... then snaps upright and starts again
    tilt = 58 * min(1.0, p / 0.6) ** 1.5 if p < 0.62 else 58 * max(0.0, 1 - (p - 0.62) / 0.02)
    tilt = max(tilt, -12 * snap(p, 0.64, 0.05, 0.01, 0.3))
    add(pose, 'Head.Z', tilt + tremble(p, 13, 2.5, 50))
    add(pose, 'Neck.Z', tilt * 0.45)
    add(pose, 'Head.X', 8 + tremble(p, 14, 2, 50))
    add(pose, 'Chest.X', 1.5 * S(p, 3) + tremble(p, 15, 1, 80))
    shrug(pose, 14, 10)
    gape = 6 + 44 * min(1.0, p / 0.85) if p < 0.88 else 50 * max(0.0, 1 - (p - 0.88) / 0.04) + 6
    jaw(pose, gape)
    for side, o in (('Left', 0.0), ('Right', 0.37)):
        add(pose, f'{side}UpperArm.X', -22 + tremble(p, 16 + o, 3, 60))
        add(pose, f'{side}LowerArm.X', -38 + tremble(p, 18 + o, 4, 60))
        add(pose, f'{side}Fingers.X', -15 - 30 * (0.5 + 0.5 * S(p, 2, o)) + tremble(p, 20 + o, 6, 90))
    add(pose, 'LeftUpperArm.Y', 16); add(pose, 'RightUpperArm.Y', -16)
    legs(pose, -12, -6, 24, 16)
    return pose

def attack(p):
    k = min(1.0, p / 0.3); e = 0.5 - 0.5 * math.cos(math.pi * k)
    k2 = max(0.0, min(1.0, (p - 0.3) / 0.2)); e2 = 0.5 - 0.5 * math.cos(math.pi * k2)
    pose = hunch({}, 1.0 + 0.5 * e2)
    arms = -100 - 80 * e + 45 * e2
    for side in ('Left', 'Right'):
        add(pose, f'{side}UpperArm.X', arms)
        add(pose, f'{side}LowerArm.X', -40 + 30 * e2)
        add(pose, f'{side}Fingers.X', -50 * e2 + 20 * e)
    add(pose, 'LeftUpperArm.Y', 25 - 15 * e2); add(pose, 'RightUpperArm.Y', -25 + 15 * e2)
    add(pose, 'Head.X', 25 * e - 35 * e2); add(pose, 'Head.Z', tremble(p, 30, 8, 60))
    shrug(pose, 18 * e, 18 * e)
    jaw(pose, 10 + 60 * e)                                                # jaw unhinges
    legs(pose, -10 - 30 * e2, 10 - 20 * e2, 20 + 25 * e, 20 + 35 * e2)
    return pose

for name, length, fn in (('Idle', 90, idle), ('Walk', 48, walk), ('Run', 20, run),
                         ('Stare', 90, stare), ('Attack', 24, attack)):
    make_action(name, length, fn)
    print('ACTION', name)

reset()
apply_pose(ground_fix(idle(0.0)))
print('IDLE LOWEST', round(lowest_z(), 3))
reset()
arm.animation_data.action = bpy.data.actions['Idle']
bpy.ops.wm.save_as_mainfile(filepath=f'{OUT}/Monster.blend')
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.export_scene.fbx(
    filepath=f'{OUT}/Monster.fbx', use_selection=False, object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', armature_nodetype='NULL',
    use_armature_deform_only=True, primary_bone_axis='Y', secondary_bone_axis='X')
print('EXPORTED')
