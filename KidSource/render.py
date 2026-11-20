import bpy, math, sys
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath='/home/claude/kid/out/Kid.blend')
sc=bpy.context.scene; arm=bpy.data.objects['Kid']
sc.render.engine='CYCLES'; sc.cycles.samples=16; sc.cycles.device='CPU'
sc.render.resolution_x=320; sc.render.resolution_y=400
w=bpy.data.worlds.new('W'); w.use_nodes=True; w.node_tree.nodes['Background'].inputs[0].default_value=(0.6,0.6,0.65,1); w.node_tree.nodes['Background'].inputs[1].default_value=0.8; sc.world=w
l=bpy.data.objects.new('L',bpy.data.lights.new('L','SUN')); l.data.energy=3; l.rotation_euler=(math.radians(50),0,math.radians(-30)); sc.collection.objects.link(l)
bpy.ops.mesh.primitive_plane_add(size=6)
cam=bpy.data.objects.new('C',bpy.data.cameras.new('C')); sc.collection.objects.link(cam); sc.camera=cam; cam.data.lens=50
def look(pos,tgt):
    cam.location=pos; d=Vector(tgt)-Vector(pos); cam.rotation_euler=d.to_track_quat('-Z','Y').to_euler()
shots=[('Idle',0),('Walk',6),('Run',4),('Jump',16),('CrouchIdle',0),('CrouchWalk',8)]
for view,pos in (('front',(1.6,-3.0,1.0)),('side',(3.4,0,0.75))):
    for n,f in shots:
        arm.animation_data.action=bpy.data.actions[n]; sc.frame_set(f)
        look(pos,(0,0,0.6))
        sc.render.filepath=f'/home/claude/kid/r_{view}_{n}.png'; bpy.ops.render.render(write_still=True)
