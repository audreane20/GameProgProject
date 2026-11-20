# Builds the hospital monster: tall, thin, hunched, dark, with glowing eyes on a separate mesh.
# Blender coords: Z up, faces -Y, character's left = +X.
import bpy, bmesh, math, json, sys
from mathutils import Vector, Matrix

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else '/home/claude/kid/out'
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 30

COLORS = {
    'MonsterSkin':  ((0.025, 0.024, 0.03), 0.45),
    'MonsterClaw':  ((0.32, 0.29, 0.24), 0.5),
    'MonsterTeeth': ((0.55, 0.52, 0.44), 0.6),
    'MonsterMouth': ((0.08, 0.0, 0.0), 0.3),
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

BONES = {
    'Hips':  ((0, 0, 1.03), (0, 0, 1.2), None),
    'Spine': ((0, 0, 1.2), (0, 0, 1.4), 'Hips'),
    'Chest': ((0, 0, 1.4), (0, 0, 1.62), 'Spine'),
    'Neck':  ((0, 0, 1.62), (0, 0, 1.8), 'Chest'),
    'Head':  ((0, 0, 1.8), (0, 0, 2.08), 'Neck'),
}
for side, sx in (('Left', 1), ('Right', -1)):
    BONES.update({
        f'{side}UpperArm': ((0.22*sx, 0, 1.58), (0.28*sx, 0, 1.12), 'Chest'),
        f'{side}LowerArm': ((0.28*sx, 0, 1.12), (0.31*sx, 0, 0.66), f'{side}UpperArm'),
        f'{side}Hand':     ((0.31*sx, 0, 0.66), (0.32*sx, 0, 0.42), f'{side}LowerArm'),
        f'{side}UpperLeg': ((0.12*sx, 0, 1.03), (0.13*sx, 0, 0.56), 'Hips'),
        f'{side}LowerLeg': ((0.13*sx, 0, 0.56), (0.13*sx, 0, 0.10), f'{side}UpperLeg'),
        f'{side}Foot':     ((0.13*sx, 0, 0.10), (0.13*sx, -0.20, 0.02), f'{side}LowerLeg'),
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

parts = {'MonsterBody': [], 'MonsterEyes': []}

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

def limb(a, b, r1, r2, mat, bone, target='MonsterBody', seg=8, sy=1.0):
    a, b = Vector(a), Vector(b)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r1, radius2=r2, depth=(b - a).length)
    bmesh.ops.scale(bm, vec=(1, sy, 1), verts=bm.verts)
    rot = Vector((0, 0, 1)).rotation_difference((b - a).normalized()).to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation((a + b) / 2) @ rot, verts=bm.verts)
    finish(bm, mat, bone, target)

def ball(c, r, mat, bone, target='MonsterBody', scale=(1, 1, 1), u=12, v=8):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r)
    bmesh.ops.scale(bm, vec=scale, verts=bm.verts)
    bmesh.ops.translate(bm, vec=c, verts=bm.verts)
    finish(bm, mat, bone, target)

# torso: narrow pelvis, sunken belly, ribcage, spine ridge
ball((0, 0.01, 1.08), 0.13, 'MonsterSkin', 'Hips', scale=(1.1, 0.75, 0.7))
limb((0, 0.0, 1.15), (0, 0.0, 1.42), 0.09, 0.12, 'MonsterSkin', 'Spine', sy=0.7)
limb((0, 0.0, 1.38), (0, 0.0, 1.64), 0.15, 0.17, 'MonsterSkin', 'Chest', sy=0.62)
for i, z in enumerate((1.20, 1.28, 1.36)):
    ball((0, 0.075, z), 0.03, 'MonsterSkin', 'Spine', u=8, v=6)
for i, z in enumerate((1.44, 1.52, 1.60)):
    ball((0, 0.10, z), 0.034, 'MonsterSkin', 'Chest', u=8, v=6)
for sx in (1, -1):
    for z in (1.44, 1.50, 1.56):   # ribs showing through
        limb((0.03*sx, -0.095, z), (0.15*sx, -0.03, z - 0.03), 0.012, 0.009, 'MonsterSkin', 'Chest', seg=6)
    ball((0.2*sx, 0.0, 1.58), 0.06, 'MonsterSkin', 'Chest', scale=(1, 0.9, 0.8), u=10, v=7)  # bony shoulders
# neck and long head with an open jaw
limb((0, 0.0, 1.6), (0, -0.02, 1.84), 0.045, 0.04, 'MonsterSkin', 'Neck', seg=8)
ball((0, -0.06, 1.95), 0.13, 'MonsterSkin', 'Head', scale=(0.95, 1.45, 1.05), u=14, v=10)   # skull
ball((0, -0.17, 1.86), 0.08, 'MonsterSkin', 'Head', scale=(1.0, 1.25, 0.45), u=12, v=8)    # lower jaw
ball((0, -0.205, 1.89), 0.055, 'MonsterMouth', 'Head', scale=(1.2, 0.6, 0.55), u=10, v=7)   # mouth hole
for i in range(7):
    x = -0.06 + i * 0.02
    limb((x, -0.235, 1.915), (x, -0.24, 1.88), 0.007, 0.001, 'MonsterTeeth', 'Head', seg=5)
    limb((x, -0.225, 1.86), (x, -0.232, 1.89), 0.006, 0.001, 'MonsterTeeth', 'Head', seg=5)
for sx in (1, -1):
    ball((0.05*sx, -0.215, 1.975), 0.034, 'MonsterSkin', 'Head', scale=(1.2, 0.6, 0.8), u=8, v=6)  # brow
    ball((0.048*sx, -0.232, 1.955), 0.019, 'MonsterEyes', 'Head', target='MonsterEyes', u=10, v=8)
# arms: thin, very long, knobby joints and long claws
for side, sx in (('Left', 1), ('Right', -1)):
    limb((0.22*sx, 0, 1.58), (0.28*sx, 0, 1.12), 0.042, 0.032, 'MonsterSkin', f'{side}UpperArm')
    ball((0.28*sx, 0, 1.12), 0.04, 'MonsterSkin', f'{side}UpperArm', u=8, v=6)
    limb((0.28*sx, 0, 1.12), (0.31*sx, 0, 0.66), 0.032, 0.025, 'MonsterSkin', f'{side}LowerArm')
    ball((0.315*sx, -0.005, 0.6), 0.045, 'MonsterSkin', f'{side}Hand', scale=(0.6, 1, 1.3), u=8, v=6)
    for i, dx in enumerate((-0.03, -0.01, 0.01, 0.03)):
        top = (0.315*sx + dx*sx*0.5, -0.02 + abs(dx), 0.55)
        tip = (0.32*sx + dx*sx, -0.06 + abs(dx) * 0.5, 0.30 - abs(dx) * 1.5)
        limb(top, tip, 0.011, 0.002, 'MonsterClaw', f'{side}Hand', seg=5)
    # legs: thin with knobby knees and long feet
    limb((0.12*sx, 0, 1.05), (0.13*sx, 0, 0.56), 0.06, 0.04, 'MonsterSkin', f'{side}UpperLeg')
    ball((0.13*sx, -0.015, 0.56), 0.048, 'MonsterSkin', f'{side}UpperLeg', u=8, v=6)
    limb((0.13*sx, 0, 0.56), (0.13*sx, 0, 0.09), 0.04, 0.028, 'MonsterSkin', f'{side}LowerLeg')
    limb((0.13*sx, 0.02, 0.07), (0.13*sx, -0.17, 0.025), 0.035, 0.02, 'MonsterSkin', f'{side}Foot', sy=0.7)
    for dx in (-0.025, 0, 0.025):
        limb((0.13*sx + dx, -0.16, 0.025), (0.13*sx + dx * 1.4, -0.24, 0.006), 0.008, 0.002, 'MonsterClaw', f'{side}Foot', seg=5)

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
    apply_pose(pose)
    lo, _ = world_bounds()
    x, y, z = pose.get('hips_loc', (0, 0, 0))
    pose['hips_loc'] = (x, y - lo.z, z)
    return pose

def make_action(name, length, fn, step=2, fix=True):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = act
    frames = list(range(0, length + 1, step))
    if frames[-1] != length:
        frames.append(length)
    for f in frames:
        pose = fn(f / length)
        if fix:
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
def twitch(p, at, width=0.03, amount=1.0):
    d = abs(p - at)
    return amount * max(0.0, 1 - d / width)

def hunch(pose, lean=1.0):
    pose.setdefault('Spine.X', 0); pose.setdefault('Chest.X', 0); pose.setdefault('Neck.X', 0); pose.setdefault('Head.X', 0)
    pose['Spine.X'] += 18 * lean
    pose['Chest.X'] += 16 * lean
    pose['Neck.X'] += 8 * lean
    pose['Head.X'] -= 38 * lean
    return pose

def legs(pose, tL, tR, kL, kR, flat=0.8):
    pose['LeftUpperLeg.X'] = tL; pose['RightUpperLeg.X'] = tR
    pose['LeftLowerLeg.X'] = kL; pose['RightLowerLeg.X'] = kR
    pose['LeftFoot.X'] = -(tL + kL) * flat; pose['RightFoot.X'] = -(tR + kR) * flat

def idle(p):
    pose = hunch({'Chest.X': 2 * S(p), 'Head.Z': 25 * twitch(p, 0.3, 0.04) - 20 * twitch(p, 0.72, 0.03),
                  'Head.Y': 10 * S(p, 1, 0.2),
                  'LeftUpperArm.X': 4 * S(p, 1, 0.1), 'RightUpperArm.X': 4 * S(p, 1, 0.6),
                  'LeftLowerArm.X': -10, 'RightLowerArm.X': -10,
                  'LeftUpperArm.Y': 6, 'RightUpperArm.Y': -6})
    legs(pose, -8, -8, 16, 16)
    return pose

def walk(p):
    s, c = S(p), C(p)
    pose = hunch({'Hips.Y': 6 * s, 'Chest.Y': -8 * s, 'Chest.Z': 4 * s, 'Head.Z': 6 * S(p, 1, 0.25),
                  'LeftUpperArm.X': 16 * S(p, 1, 0.08), 'RightUpperArm.X': -16 * S(p, 1, 0.08),
                  'LeftLowerArm.X': -8 - 8 * pos(S(p, 1, 0.15)), 'RightLowerArm.X': -8 - 8 * pos(-S(p, 1, 0.15)),
                  'LeftUpperArm.Y': 5, 'RightUpperArm.Y': -5}, lean=1.1)
    legs(pose, -28 * s - 6, 28 * s - 6, 12 + 45 * pos(c), 12 + 45 * pos(-c))
    return pose

def run(p):
    s, c = S(p), C(p)
    pose = hunch({'Hips.Y': 10 * s, 'Chest.Y': -12 * s, 'Head.X': -10,
                  'LeftUpperArm.X': -55 + 35 * s, 'RightUpperArm.X': -55 - 35 * s,
                  'LeftLowerArm.X': -30, 'RightLowerArm.X': -30,
                  'LeftUpperArm.Y': 12, 'RightUpperArm.Y': -12}, lean=1.8)
    legs(pose, -52 * s - 10, 52 * s - 10, 25 + 85 * pos(c), 25 + 85 * pos(-c), flat=0.5)
    return pose

def stare(p):
    tilt = 18 * S(p, 1) + 22 * twitch(p, 0.55, 0.025)
    pose = hunch({'Head.Z': tilt, 'Head.X': 6, 'Neck.X': -4 + 3 * twitch(p, 0.2, 0.02),
                  'Chest.X': 1.5 * S(p, 2),
                  'LeftUpperArm.X': -18, 'RightUpperArm.X': -14,
                  'LeftLowerArm.X': -35 + 6 * twitch(p, 0.8, 0.03), 'RightLowerArm.X': -30,
                  'LeftUpperArm.Y': 14, 'RightUpperArm.Y': -14}, lean=0.8)
    legs(pose, -10, -6, 20, 16)
    return pose

def attack(p):
    k = min(1.0, p / 0.35); e = 0.5 - 0.5 * math.cos(math.pi * k)       # wind up
    k2 = max(0.0, min(1.0, (p - 0.35) / 0.25)); e2 = 0.5 - 0.5 * math.cos(math.pi * k2)  # lunge
    arms = -100 - 80 * e + 45 * e2          # raise claws high, then swing them forward at you
    pose = hunch({'Spine.X': 12 * e2, 'Chest.X': 10 * e2 - 12 * e, 'Head.X': 18 * e - 30 * e2,
                  'LeftUpperArm.X': arms, 'RightUpperArm.X': arms,
                  'LeftLowerArm.X': -40 + 30 * e2, 'RightLowerArm.X': -40 + 30 * e2,
                  'LeftUpperArm.Y': 25 - 15 * e2, 'RightUpperArm.Y': -25 + 15 * e2}, lean=1.0 + 0.6 * e2)
    legs(pose, -10 - 30 * e2, 10 - 20 * e2, 20 + 25 * e, 20 + 35 * e2, flat=0.8)
    return pose

for name, length, fn in (('Idle', 60, idle), ('Walk', 40, walk), ('Run', 20, run), ('Stare', 72, stare), ('Attack', 24, attack)):
    make_action(name, length, fn)

info = {}
for nm, fn in (('idle', idle), ('walk', walk)):
    apply_pose(ground_fix(fn(0)))
    lo, hi = world_bounds()
    eyes = meshes[1].evaluated_get(deps)
    info[nm] = {'height': round(hi.z - lo.z, 3)}
apply_pose(ground_fix(stare(0)))
ev = meshes[1].evaluated_get(deps); me = ev.to_mesh()
c = sum(((ev.matrix_world @ v.co) for v in me.vertices), Vector()) / len(me.vertices)
info['eyes_stare'] = [round(c.x, 3), round(c.y, 3), round(c.z, 3)]
ev.to_mesh_clear()
reset()
arm.animation_data.action = bpy.data.actions['Idle']
json.dump(info, open(f'{OUT}/monster_measure.json', 'w'))
print('MEASURE', info)

bpy.ops.wm.save_as_mainfile(filepath=f'{OUT}/Monster.blend')
bpy.ops.export_scene.fbx(
    filepath=f'{OUT}/Monster.fbx', use_selection=False, object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', armature_nodetype='NULL',
    use_armature_deform_only=True, primary_bone_axis='Y', secondary_bone_axis='X')
print('EXPORTED')
