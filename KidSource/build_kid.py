# Builds a low-poly rigged kid with animations and exports FBX for Unity.
# Blender coords: Z up, character faces -Y, character's left = +X.
import bpy, bmesh, math, json, sys
from mathutils import Vector, Matrix

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else '/home/claude/kid/out'
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 30

# ---------------- proportions ----------------
# Body (everything below the neck) is shrunk; the head keeps its size, so it reads as a young kid.
BZ, BX = 0.80, 0.90           # body height / width factors
NECK_TOP = 0.94
SHIFT = NECK_TOP * (1 - BZ)   # how far the head moves down
def TB(p): return (p[0] * BX, p[1] * BX, p[2] * BZ)
def TH(p): return (p[0], p[1], p[2] - SHIFT)

# ---------------- materials ----------------
COLORS = {
    'ChildGown':  (0.55, 0.74, 0.88),   # light-blue hospital pyjamas
    'ChildSkin':  (0.96, 0.80, 0.69),
    'ChildHair':  (0.23, 0.15, 0.09),
    'ChildEyes':  (0.05, 0.04, 0.04),
    'ChildShoes': (0.85, 0.85, 0.82),   # hospital slippers
}
MATS = {}
for n, c in COLORS.items():
    m = bpy.data.materials.new(n)
    m.diffuse_color = (*c, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*c, 1)
    bsdf.inputs['Roughness'].default_value = 0.7
    MATS[n] = m

# ---------------- skeleton ----------------
BONES = {  # name: (head, tail, parent)
    'Hips':          ((0, 0, 0.52), (0, 0, 0.62), None),
    'Spine':         ((0, 0, 0.62), (0, 0, 0.74), 'Hips'),
    'Chest':         ((0, 0, 0.74), (0, 0, 0.88), 'Spine'),
    'Neck':          ((0, 0, 0.88), (0, 0, 0.94), 'Chest'),
    'Head':          ((0, 0, 0.94), (0, 0, 1.24), 'Neck'),
}
for side, sx in (('Left', 1), ('Right', -1)):
    BONES.update({
        f'{side}UpperArm': ((0.165*sx, 0, 0.865), (0.20*sx, 0, 0.69), 'Chest'),
        f'{side}LowerArm': ((0.20*sx, 0, 0.69), (0.215*sx, 0, 0.53), f'{side}UpperArm'),
        f'{side}Hand':     ((0.215*sx, 0, 0.53), (0.22*sx, 0, 0.46), f'{side}LowerArm'),
        f'{side}UpperLeg': ((0.075*sx, 0, 0.52), (0.075*sx, 0, 0.29), 'Hips'),
        f'{side}LowerLeg': ((0.075*sx, 0, 0.29), (0.075*sx, 0, 0.075), f'{side}UpperLeg'),
        f'{side}Foot':     ((0.075*sx, 0, 0.075), (0.075*sx, -0.10, 0.025), f'{side}LowerLeg'),
    })

arm_data = bpy.data.armatures.new('KidArmature')
arm = bpy.data.objects.new('Kid', arm_data)
scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
for name, (h, t, p) in BONES.items():
    b = arm_data.edit_bones.new(name)
    T = TH if name == 'Head' else TB
    b.head, b.tail, b.roll = T(h), T(t), 0
for name, (h, t, p) in BONES.items():
    if p:
        arm_data.edit_bones[name].parent = arm_data.edit_bones[p]
        arm_data.edit_bones[name].use_connect = False
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------- mesh building ----------------
parts = {'KidBody': [], 'KidHead': []}

def finish(bm, mat, bone, target, smooth=True):
    me = bpy.data.meshes.new(bone)
    bm.to_mesh(me); bm.free()
    for poly in me.polygons:
        poly.use_smooth = smooth
    ob = bpy.data.objects.new(bone, me)
    scene.collection.objects.link(ob)
    ob.data.materials.append(MATS[mat])
    vg = ob.vertex_groups.new(name=bone)
    vg.add(list(range(len(me.vertices))), 1.0, 'REPLACE')
    parts[target].append(ob)

def limb(a, b, r1, r2, mat, bone, target='KidBody', seg=10, sy=1.0):
    T = TH if target == 'KidHead' else TB
    if target != 'KidHead':
        r1, r2 = r1 * BX, r2 * BX
    a, b = Vector(T(a)), Vector(T(b))
    bm = bmesh.new()
    d = (b - a).length
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r1, radius2=r2, depth=d)
    bmesh.ops.scale(bm, vec=(1, sy, 1), verts=bm.verts)
    rot = Vector((0, 0, 1)).rotation_difference((b - a).normalized()).to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation((a + b) / 2) @ rot, verts=bm.verts)
    finish(bm, mat, bone, target)

def ball(c, r, mat, bone, target='KidBody', scale=(1, 1, 1), u=14, v=9):
    if target == 'KidHead':
        c = TH(c)
    else:
        c, r = TB(c), r * BX
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r)
    bmesh.ops.scale(bm, vec=scale, verts=bm.verts)
    bmesh.ops.translate(bm, vec=c, verts=bm.verts)
    finish(bm, mat, bone, target)

def box(c, size, mat, bone, target='KidBody'):
    c = TB(c)
    size = (size[0] * BX, size[1] * BX, size[2] * BZ)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    bevel_geom = bm.edges[:]
    bmesh.ops.bevel(bm, geom=bevel_geom, offset=0.012, segments=2, affect='EDGES')
    bmesh.ops.translate(bm, vec=c, verts=bm.verts)
    finish(bm, mat, bone, target, smooth=True)

# torso (pyjama top + bottoms)
limb((0, 0, 0.45), (0, 0, 0.60), 0.135, 0.13, 'ChildGown', 'Hips', sy=0.8)
limb((0, 0, 0.58), (0, 0, 0.75), 0.13, 0.14, 'ChildGown', 'Spine', sy=0.78)
limb((0, 0, 0.73), (0, 0, 0.88), 0.14, 0.115, 'ChildGown', 'Chest', sy=0.78)
for sx in (1, -1):
    ball((0.135*sx, 0, 0.85), 0.06, 'ChildGown', 'Chest', scale=(1, 0.9, 0.9), u=10, v=7)
limb((0, 0, 0.86), (0, 0, 0.97), 0.045, 0.045, 'ChildSkin', 'Neck', seg=8)
# head (separate mesh so first person can hide it)
ball((0, 0, 1.06), 0.14, 'ChildSkin', 'Head', 'KidHead', scale=(1, 0.95, 1.02), u=16, v=11)
ball((0, 0.02, 1.09), 0.148, 'ChildHair', 'Head', 'KidHead', scale=(1.03, 1.0, 0.93), u=16, v=11)
for sx in (1, -1):
    ball((0.048*sx, -0.126, 1.055), 0.019, 'ChildEyes', 'Head', 'KidHead', scale=(1, 0.5, 1.2), u=8, v=6)
    ball((0.138*sx, 0.0, 1.04), 0.028, 'ChildSkin', 'Head', 'KidHead', scale=(0.5, 1, 1.2), u=8, v=6)  # ears
ball((0, -0.137, 1.02), 0.014, 'ChildSkin', 'Head', 'KidHead', u=8, v=6)  # nose
# arms and legs
for side, sx in (('Left', 1), ('Right', -1)):
    limb((0.165*sx, 0, 0.87), (0.19*sx, 0, 0.74), 0.052, 0.048, 'ChildGown', f'{side}UpperArm', seg=9)
    limb((0.188*sx, 0, 0.755), (0.20*sx, 0, 0.69), 0.038, 0.036, 'ChildSkin', f'{side}UpperArm', seg=8)
    limb((0.20*sx, 0, 0.70), (0.215*sx, 0, 0.535), 0.036, 0.031, 'ChildSkin', f'{side}LowerArm', seg=8)
    ball((0.218*sx, -0.005, 0.495), 0.038, 'ChildSkin', f'{side}Hand', scale=(0.75, 1, 1.15), u=10, v=7)
    limb((0.075*sx, 0, 0.53), (0.075*sx, 0, 0.28), 0.066, 0.056, 'ChildGown', f'{side}UpperLeg', seg=10)
    limb((0.075*sx, 0, 0.30), (0.075*sx, 0, 0.09), 0.054, 0.044, 'ChildGown', f'{side}LowerLeg', seg=10)
    limb((0.075*sx, 0, 0.11), (0.075*sx, 0, 0.055), 0.038, 0.038, 'ChildSkin', f'{side}LowerLeg', seg=8)
    box((0.075*sx, -0.03, 0.03), (0.08, 0.165, 0.06), 'ChildShoes', f'{side}Foot')

meshes = []
for target, obs in parts.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.join()
    ob = bpy.context.active_object
    ob.name = ob.data.name = target
    ob.parent = arm
    mod = ob.modifiers.new('Armature', 'ARMATURE')
    mod.object = arm
    meshes.append(ob)

# ---------------- animation ----------------
pb = arm.pose.bones
for b in pb:
    b.rotation_mode = 'XYZ'
deps = bpy.context.evaluated_depsgraph_get()

def reset():
    for b in pb:
        b.rotation_euler = (0, 0, 0)
        b.location = (0, 0, 0)

def apply_pose(pose):
    reset()
    for key, val in pose.items():
        if key == 'hips_loc':
            pb['Hips'].location = val
            continue
        bone, axis = key.split('.')
        e = list(pb[bone].rotation_euler)
        e['XYZ'.index(axis)] = math.radians(val)
        pb[bone].rotation_euler = e
    bpy.context.view_layer.update()

def world_bounds():
    deps.update()
    lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
    for ob in meshes:
        ev = ob.evaluated_get(deps)
        me = ev.to_mesh()
        for v in me.vertices:
            w = ev.matrix_world @ v.co
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
        ev.to_mesh_clear()
    return lo, hi

def ground_fix(pose):
    """Shift the hips up/down so the lowest point of the feet touches the floor."""
    apply_pose(pose)
    lo, _ = world_bounds()
    x, y, z = pose.get('hips_loc', (0, 0, 0))
    pose['hips_loc'] = (x, y - lo.z, z)   # Hips local Y = world up
    return pose

def make_action(name, length, pose_fn, step=2, fix_ground=True):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = act
    frames = list(range(0, length + 1, step))
    if frames[-1] != length:
        frames.append(length)
    for f in frames:
        pose = pose_fn(f / length)
        if fix_ground:
            pose = ground_fix(pose)
        apply_pose(pose)
        for b in pb:
            b.keyframe_insert('rotation_euler', frame=f)
        pb['Hips'].keyframe_insert('location', frame=f)
    act.frame_range = (0, length)
    return act

S = lambda p, k=1, o=0: math.sin(2 * math.pi * (p * k + o))
C = lambda p, k=1, o=0: math.cos(2 * math.pi * (p * k + o))
pos = lambda v: max(0.0, v)

def legs(pose, thighL, thighR, kneeL, kneeR, flat=0.85):
    pose['LeftUpperLeg.X'] = thighL;  pose['RightUpperLeg.X'] = thighR
    pose['LeftLowerLeg.X'] = kneeL;   pose['RightLowerLeg.X'] = kneeR
    pose['LeftFoot.X'] = -(thighL + kneeL) * flat
    pose['RightFoot.X'] = -(thighR + kneeR) * flat

def idle(p):
    pose = {'Chest.X': 1.5 * S(p), 'Spine.X': 1.0, 'Head.X': -1.5 * S(p, 1, 0.1) - 1,
            'Head.Y': 5 * S(p, 1, 0.25), 'Neck.Y': 2 * S(p, 1, 0.25),
            'LeftUpperArm.X': 2 * S(p, 1, 0.15), 'RightUpperArm.X': 2 * S(p, 1, 0.65),
            'LeftLowerArm.X': -8, 'RightLowerArm.X': -8,
            'LeftUpperArm.Y': 4, 'RightUpperArm.Y': -4}
    legs(pose, -2, -2, 4, 4)
    return pose

def walk(p):
    s, c = S(p), C(p)
    pose = {'Hips.Y': 5 * s, 'Chest.Y': -7 * s, 'Spine.X': 4, 'Head.X': -3, 'Head.Y': 2 * s,
            'LeftUpperArm.X': 22 * s, 'RightUpperArm.X': -22 * s,
            'LeftLowerArm.X': -15 - 15 * pos(-s), 'RightLowerArm.X': -15 - 15 * pos(s)}
    legs(pose, -26 * s, 26 * s, 6 + 42 * pos(c), 6 + 42 * pos(-c))
    return pose

def run(p):
    s, c = S(p), C(p)
    pose = {'Hips.Y': 8 * s, 'Chest.Y': -10 * s, 'Spine.X': 12, 'Chest.X': 4, 'Head.X': -12,
            'LeftUpperArm.X': 42 * s, 'RightUpperArm.X': -42 * s,
            'LeftLowerArm.X': -80, 'RightLowerArm.X': -80,
            'LeftUpperArm.Y': 6, 'RightUpperArm.Y': -6}
    legs(pose, -45 * s, 45 * s, 18 + 75 * pos(c), 18 + 75 * pos(-c), flat=0.6)
    return pose

def jump(p):
    k = min(1.0, p / 0.35)                      # quick move into the air pose, then hold
    e = 0.5 - 0.5 * math.cos(math.pi * k)
    wob = 0.03 * S(p, 2) * (1 - k * 0.5)
    pose = {'Spine.X': 6 * e, 'Head.X': -6 * e,
            'LeftUpperArm.X': -150 + 60 * e, 'RightUpperArm.X': -150 + 60 * e,
            'LeftUpperArm.Y': 15, 'RightUpperArm.Y': -15,
            'LeftLowerArm.X': -20 - 20 * e, 'RightLowerArm.X': -20 - 20 * e}
    legs(pose, -10 - 45 * e + wob, -10 - 35 * e, 10 + 65 * e, 10 + 75 * e, flat=0.4)
    pose['LeftFoot.X'] += 25 * (1 - e); pose['RightFoot.X'] += 25 * (1 - e)
    return pose

def crouch_base(pose):
    pose.update({'Spine.X': 40, 'Chest.X': 18, 'Neck.X': -18, 'Head.X': -26,
                 'LeftUpperArm.X': -28, 'RightUpperArm.X': -28,
                 'LeftLowerArm.X': -45, 'RightLowerArm.X': -45,
                 'LeftUpperArm.Y': 6, 'RightUpperArm.Y': -6})
    return pose

def crouch_idle(p):
    pose = crouch_base({})
    pose['Chest.X'] = 18 + 2 * S(p)
    pose['Head.Y'] = 8 * S(p, 1, 0.25)
    legs(pose, -88, -82, 134, 128, flat=1.0)
    return pose

def crouch_walk(p):
    s, c = S(p), C(p)
    pose = crouch_base({})
    pose['Hips.Y'] = 6 * s
    pose['LeftUpperArm.X'] = -28 + 10 * s; pose['RightUpperArm.X'] = -28 - 10 * s
    legs(pose, -85 - 16 * s, -85 + 16 * s, 128 + 14 * pos(c), 128 + 14 * pos(-c), flat=1.0)
    return pose

actions = [
    make_action('Idle', 60, idle),
    make_action('Walk', 24, walk),
    make_action('Run', 16, run),
    make_action('Jump', 20, jump, fix_ground=False),
    make_action('CrouchIdle', 40, crouch_idle),
    make_action('CrouchWalk', 30, crouch_walk),
]

# ---------------- measurements for the Unity controller ----------------
info = {}
for nm, fn in (('stand', idle), ('crouch', crouch_idle)):
    pose = ground_fix(fn(0.0))
    apply_pose(pose)
    lo, hi = world_bounds()
    hm = arm.matrix_world @ pb['Head'].matrix
    eye_world = hm @ Vector((0, 0.115, 0))
    info[nm] = {'height': round(hi.z - lo.z, 3), 'minZ': round(lo.z, 3),
                'eye_z': round(eye_world.z, 3), 'eye_y': round(eye_world.y, 3),
                'head_top': round(hi.z, 3)}
reset()
arm.animation_data.action = bpy.data.actions['Idle']
json.dump(info, open(f'{OUT}/measure.json', 'w'), indent=1)
print('MEASURE', info)

bpy.ops.wm.save_as_mainfile(filepath=f'{OUT}/Kid.blend')
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.export_scene.fbx(
    filepath=f'{OUT}/Kid.fbx', use_selection=False, object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', armature_nodetype='NULL',
    use_armature_deform_only=True, primary_bone_axis='Y', secondary_bone_axis='X')
print('EXPORTED')
