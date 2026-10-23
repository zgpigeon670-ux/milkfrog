"""Create the original yellow bull rig, weighted low-poly model and independent actions.
Run: E:/blender/blender.exe -b --factory-startup --python Tools/Author-YellowBull.py
"""
import bpy, math, os
from mathutils import Vector
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT=os.path.join(ROOT,'Assets','CombatDemo','Art','YellowBull');os.makedirs(OUT,exist_ok=True)
SOURCE=os.path.join(ROOT,'SourceArt','YellowBull');os.makedirs(SOURCE,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;scene.render.fps=24
materials={}
for name,color in {'GoldenCoat':(1,.66,.025,1),'CreamMuzzle':(1,.8,.28,1),'HornIvory':(.94,.88,.63,1),'HoofBrown':(.18,.075,.028,1),'EyeWhite':(.99,.97,.86,1),'EyeBlack':(.023,.017,.01,1),'NoseBrown':(.26,.115,.035,1)}.items():
 m=bpy.data.materials.new(name);m.diffuse_color=color;materials[name]=m
arm=bpy.data.armatures.new('YellowBullSkeleton');rig=bpy.data.objects.new('YellowBullRig',arm);scene.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
bones={
 'Root':((0,0,0),(0,0,.3),None),'Hips':((0,0,1.15),(0,0,1.6),'Root'),'Spine':((0,0,1.6),(0,0,2.28),'Hips'),
 'Neck':((0,0,2.28),(0,-.03,2.6),'Spine'),'Head':((0,-.03,2.6),(0,-.03,3.2),'Neck'),
 'Tail':((0,.49,1.35),(0,.92,.85),'Hips')}
for side,sign in [('L',1),('R',-1)]:
 bones.update({f'UpperArm.{side}':((sign*.58,0,2.26),(sign*.98,0,1.94),'Spine'),f'Forearm.{side}':((sign*.98,0,1.94),(sign*1.08,-.1,1.43),f'UpperArm.{side}'),f'Hand.{side}':((sign*1.08,-.1,1.43),(sign*1.08,-.13,1.23),f'Forearm.{side}'),f'Thigh.{side}':((sign*.36,0,1.2),(sign*.39,0,.72),'Hips'),f'Shin.{side}':((sign*.39,0,.72),(sign*.4,0,.2),f'Thigh.{side}'),f'Foot.{side}':((sign*.4,0,.2),(sign*.4,-.32,.1),f'Shin.{side}')})
for name,(head,tail,parent) in bones.items():
 b=arm.edit_bones.new(name);b.head=head;b.tail=tail
 if parent:b.parent=arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT');meshes=[]
def bind(obj,name,mat,bone):
 obj.name=name;obj.data.materials.append(materials[mat]);bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 group=obj.vertex_groups.new(name=bone);group.add(list(range(len(obj.data.vertices))),1,'REPLACE');mod=obj.modifiers.new('BullSkin','ARMATURE');mod.object=rig;obj.parent=rig;meshes.append(obj);obj.select_set(False);return obj
def ell(name,pos,size,mat,bone,sub=2):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub,radius=1,location=pos);obj=bpy.context.object;obj.scale=size;return bind(obj,name,mat,bone)
def segment(name,a,b,r1,r2,mat,bone):
 delta=Vector(b)-Vector(a);bpy.ops.mesh.primitive_cone_add(vertices=10,radius1=r1,radius2=r2,depth=delta.length,location=(Vector(a)+Vector(b))/2);obj=bpy.context.object;obj.rotation_euler=delta.to_track_quat('Z','Y').to_euler();return bind(obj,name,mat,bone)
ell('PowerfulTorso',(0,.035,1.92),(.78,.48,.85),'GoldenCoat','Spine')
ell('Belly',(0,-.335,1.8),(.55,.22,.57),'CreamMuzzle','Spine')
ell('Pelvis',(0,.015,1.22),(.62,.43,.38),'GoldenCoat','Hips')
ell('Neck',(0,.01,2.5),(.43,.38,.43),'GoldenCoat','Neck')
ell('BroadHead',(0,-.12,2.94),(.59,.45,.57),'GoldenCoat','Head')
ell('WideMuzzle',(0,-.51,2.76),(.48,.27,.28),'CreamMuzzle','Head')
for sign,side in [(1,'L'),(-1,'R')]:
 ell('Nostril'+side,(sign*.23,-.758,2.8),(.095,.025,.06),'NoseBrown','Head')
 ell('EyeWhite'+side,(sign*.285,-.468,3.075),(.155,.055,.115),'EyeWhite','Head')
 ell('Pupil'+side,(sign*.285,-.52,3.07),(.057,.025,.07),'EyeBlack','Head')
 eyebrow=ell('Brow'+side,(sign*.29,-.47,3.208),(.18,.055,.044),'NoseBrown','Head');eyebrow.rotation_euler.y=sign*-.14
 ear=ell('Ear'+side,(sign*.63,-.015,3.02),(.25,.12,.105),'GoldenCoat','Head');ear.rotation_euler.y=sign*-.22
 a=(sign*.42,.03,3.25);b=(sign*.62,.05,3.44);d=(sign*.64,-.04,3.64)
 segment('HornBase'+side,a,b,.12,.08,'HornIvory','Head');segment('HornTip'+side,b,d,.08,.008,'HornIvory','Head')
 ell('Shoulder'+side,(sign*.67,.02,2.18),(.34,.33,.39),'GoldenCoat','UpperArm.'+side)
 segment('UpperArm'+side,(sign*.72,0,2.19),(sign*.98,0,1.91),.28,.23,'GoldenCoat','UpperArm.'+side)
 ell('Elbow'+side,(sign*.98,0,1.91),(.25,.26,.25),'GoldenCoat','Forearm.'+side)
 segment('Forearm'+side,(sign*.98,0,1.91),(sign*1.08,-.1,1.44),.24,.19,'GoldenCoat','Forearm.'+side)
 ell('HandHoof'+side,(sign*1.08,-.13,1.32),(.23,.21,.22),'HoofBrown','Hand.'+side)
 ell('Thigh'+side,(sign*.37,0,1.0),(.29,.32,.4),'GoldenCoat','Thigh.'+side)
 ell('Shin'+side,(sign*.39,-.01,.48),(.22,.24,.34),'GoldenCoat','Shin.'+side)
 for half in [-1,1]:ell('ClovenHoof'+side+str(half),(sign*.4+half*.105,-.15,.15),(.1,.3,.14),'HoofBrown','Foot.'+side)
segment('Tail',(0,.49,1.35),(0,.94,.85),.045,.035,'GoldenCoat','Tail');ell('TailTuft',(0,.94,.83),(.1,.1,.15),'HoofBrown','Tail')
segment('Smile',(-.22,-.74,2.66),(.22,-.74,2.66),.015,.015,'NoseBrown','Head')
# Join into a single weighted mesh, retaining named materials and bone groups.
bpy.ops.object.select_all(action='DESELECT')
for obj in meshes:obj.select_set(True)
bpy.context.view_layer.objects.active=meshes[0];bpy.ops.object.join();body=bpy.context.object;body.name='YellowBullMesh'
# Every action is authored on this independent 18-bone rig, never the old swordsman.
actions=[]
def pose(name,frame,rot=None,loc=None):
 p=rig.pose.bones[name];p.rotation_mode='XYZ'
 if rot is not None:p.rotation_euler=[math.radians(v) for v in rot];p.keyframe_insert('rotation_euler',frame=frame,group=name)
 if loc is not None:p.location=loc;p.keyframe_insert('location',frame=frame,group=name)
def neutral(frame):
 for name in bones:pose(name,frame,(0,0,0),(0,0,0))
def action(name,keys):
 a=bpy.data.actions.new(name);rig.animation_data_create();rig.animation_data.action=a
 for frame,changes in keys:
  neutral(frame)
  for bone,values in changes.items():
   if isinstance(values,dict):pose(bone,frame,values.get('rot'),values.get('loc'))
   else:pose(bone,frame,values)
 actions.append(a);return a
idle=[(1,{}),(13,{'Spine':{'rot':(1,0,0),'loc':(0,.015,0)},'Head':(-2,0,0),'Tail':(0,12,0)}),(25,{})];action('Bull_Idle',idle)
walk=[]
for frame in [1,7,13,19,25]:
 t=(frame-1)/24*math.tau;v=math.sin(t);changes={'Hips':{'rot':(0,3*v,0),'loc':(0,.025*abs(v),0)},'Head':(-3,0,-2*v)}
 for side,sg in [('L',1),('R',-1)]:changes.update({f'Thigh.{side}':(25*v*sg,0,0),f'Shin.{side}':(-18*max(0,-v*sg),0,0),f'UpperArm.{side}':(-15*v*sg,0,0)})
 walk.append((frame,changes))
action('Bull_Walk',walk)
action('Bull_Hit',[(1,{}),(5,{'Spine':(-12,0,0),'Head':(-10,0,0)}),(13,{})])
action('Bull_Death',[(1,{}),(10,{'Root':{'rot':(0,0,15),'loc':(0,-.3,0)},'Spine':(-25,0,0)}),(25,{'Root':{'rot':(85,0,0),'loc':(0,-.85,0)},'Head':(-20,0,0),'UpperArm.L':(20,0,-25),'UpperArm.R':(20,0,25)})])
action('Bull_Broken',[(1,{}),(9,{'Hips':{'loc':(0,-.5,0)},'Thigh.L':(35,0,0),'Thigh.R':(35,0,0),'Shin.L':(-65,0,0),'Shin.R':(-65,0,0),'Spine':(22,0,0),'Head':(25,0,0)}),(25,{'Hips':{'loc':(0,-.5,0)},'Thigh.L':(35,0,0),'Thigh.R':(35,0,0),'Shin.L':(-65,0,0),'Shin.R':(-65,0,0),'Spine':(22,0,0),'Head':(25,0,0)})])
action('Bull_Roar',[(1,{}),(9,{'Spine':(-14,0,0),'Head':(-25,0,0),'UpperArm.L':(-30,0,-60),'UpperArm.R':(-30,0,60)}),(17,{'Spine':(-10,0,0),'Head':(-35,0,0),'UpperArm.L':(-30,0,-55),'UpperArm.R':(-30,0,55)}),(25,{})])
action('Bull_Horn',[(1,{}),(10,{'Spine':(20,0,-30),'Head':(25,0,-12)}),(12,{'Spine':(32,0,32),'Head':(35,0,20)}),(16,{'Spine':(24,0,25),'Head':(20,0,15)}),(25,{})])
action('Bull_Kick',[(1,{}),(10,{'Spine':(15,0,0),'Thigh.R':(30,0,0),'Shin.R':(-60,0,0)}),(12,{'Spine':(28,0,0),'Thigh.R':(-75,0,0),'Shin.R':(-5,0,0)}),(16,{'Spine':(20,0,0),'Thigh.R':(-65,0,0)}),(25,{})])
action('Bull_Charge',[(1,{}),(10,{'Spine':(42,0,0),'Head':(25,0,0),'UpperArm.L':(-35,0,-12),'UpperArm.R':(-35,0,12),'Thigh.L':(20,0,0),'Thigh.R':(-20,0,0)}),(12,{'Spine':(48,0,0),'Head':(28,0,0),'Thigh.L':(-28,0,0),'Thigh.R':(28,0,0)}),(16,{'Spine':(45,0,0),'Head':(28,0,0),'Thigh.L':(28,0,0),'Thigh.R':(-28,0,0)}),(25,{})])
action('Bull_Slam',[(1,{}),(10,{'Spine':(-14,0,0),'UpperArm.L':(-150,0,-12),'UpperArm.R':(-150,0,12),'Forearm.L':(-15,0,0),'Forearm.R':(-15,0,0)}),(12,{'Hips':{'loc':(0,-.24,0)},'Spine':(45,0,0),'Head':(15,0,0),'UpperArm.L':(-55,0,-8),'UpperArm.R':(-55,0,8),'Forearm.L':(25,0,0),'Forearm.R':(25,0,0)}),(16,{'Hips':{'loc':(0,-.24,0)},'Spine':(42,0,0),'UpperArm.L':(-52,0,-8),'UpperArm.R':(-52,0,8)}),(25,{})])
action('Bull_Wave',[(1,{}),(10,{'Thigh.L':(65,0,-10),'Shin.L':(-70,0,0),'Spine':(-8,0,0),'UpperArm.L':(-25,0,-30),'UpperArm.R':(-25,0,30)}),(12,{'Hips':{'loc':(0,-.17,0)},'Spine':(12,0,0),'Thigh.L':(-8,0,0)}),(16,{'Hips':{'loc':(0,-.1,0)},'Spine':(7,0,0)}),(25,{})])
rig.animation_data.action=None
for a in actions:
 t=rig.animation_data.nla_tracks.new();t.name=a.name;strip=t.strips.new(a.name,1,a);strip.action_frame_start=a.frame_range[0];strip.action_frame_end=a.frame_range[1]
scene.frame_start=1;scene.frame_end=25;scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'YellowBull.blend'))
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'YellowBull.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0.0)
print('YELLOW_BULL_EXPORT',len(bones),'bones',len(actions),'actions',len(body.data.vertices),'vertices')
