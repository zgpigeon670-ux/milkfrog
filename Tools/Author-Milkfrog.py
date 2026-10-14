"""Run in the connected Blender through MCP; preserves authored idle/walk."""
import bpy, math, json, hashlib, os
from mathutils import Vector, Quaternion, Euler, Matrix

OUT = r'E:\UNITY\PROJECTS\milkfrog\Builds\AnimationRepair'
rig = bpy.data.objects['metarig']
scene = bpy.context.scene

def curves(a):
    return [f for l in a.layers for s in l.strips if hasattr(s, 'channelbags') for b in s.channelbags for f in b.fcurves]

def signature(a):
    data = {'range':list(a.frame_range), 'curves':[{'path':f.data_path,'index':f.array_index,'extrapolation':f.extrapolation,'keys':[{'co':list(k.co),'left':list(k.handle_left),'right':list(k.handle_right),'interpolation':k.interpolation,'left_type':k.handle_left_type,'right_type':k.handle_right_type} for k in f.keyframe_points]} for f in curves(a)]}
    return hashlib.sha256(json.dumps(data,sort_keys=True).encode()).hexdigest()

rig.animation_data.use_nla = False
rig.animation_data.action = bpy.data.actions['idle']
scene.frame_set(0)
bpy.context.view_layer.update()
BASE = {p.name: {'loc':p.location.copy(), 'rot':p.rotation_quaternion.copy() if p.rotation_mode=='QUATERNION' else p.rotation_euler.to_quaternion(), 'scale':p.scale.copy(), 'mode':p.rotation_mode} for p in rig.pose.bones}
SWORD_REL = rig.pose.bones['hand.R'].matrix.inverted() @ rig.matrix_world.inverted() @ bpy.data.objects['sword'].matrix_world
PROTECTED = json.load(open(os.path.join(OUT, 'protected-actions.json')))

def reset():
    for p in rig.pose.bones:
        b=BASE[p.name]; p.location=b['loc']; p.scale=b['scale']
        if p.rotation_mode=='QUATERNION': p.rotation_quaternion=b['rot']
        else: p.rotation_euler=b['rot'].to_euler(p.rotation_mode)
    bpy.context.view_layer.update()

def delta(name, degrees):
    p=rig.pose.bones[name]
    q=BASE[name]['rot'] @ Euler(tuple(math.radians(v) for v in degrees), 'XYZ').to_quaternion()
    if p.rotation_mode=='QUATERNION': p.rotation_quaternion=q
    else:p.rotation_euler=q.to_euler(p.rotation_mode)

def world_rotation(p, q):
    parent=p.parent
    basis=(parent.matrix @ parent.bone.matrix_local.inverted() @ p.bone.matrix_local) if parent else p.bone.matrix_local
    rotation=basis.to_quaternion().inverted() @ q
    if p.rotation_mode=='QUATERNION': p.rotation_quaternion=rotation
    else:p.rotation_euler=rotation.to_euler(p.rotation_mode)
    bpy.context.view_layer.update()

def point_bone(p, target):
    direction=Vector(target)-p.head
    if direction.length < .00001:return
    current=p.matrix.to_quaternion()
    world_rotation(p, (current @ Vector((0,1,0))).rotation_difference(direction.normalized()) @ current)

def limb(upper, lower, end, target, pole):
    u=rig.pose.bones[upper]; l=rig.pose.bones[lower]; e=rig.pose.bones[end]
    origin=u.head.copy(); v=Vector(target)-origin
    a=(l.head-u.head).length; b=(e.head-l.head).length
    d=max(abs(a-b)+.002,min(v.length,a+b-.002)); axis=v.normalized()
    pole=Vector(pole)-origin; normal=pole-axis*pole.dot(axis)
    if normal.length<.001:normal=axis.cross(Vector((0,0,1)))
    normal.normalize()
    along=(a*a+d*d-b*b)/(2*d); h=math.sqrt(max(0,a*a-along*along))
    elbow=origin+axis*along+normal*h
    point_bone(u,elbow);point_bone(l,origin+axis*d)

def arm(side, target, sword_dir=None):
    sign=1 if side=='L' else -1
    limb('upper_arm.'+side,'forearm.'+side,'hand.'+side,target,(sign*.8,.12,1.05))
    p=rig.pose.bones['hand.'+side]
    if sword_dir is not None:
        current=p.matrix.to_quaternion()
        blade=(p.matrix @ SWORD_REL).to_quaternion() @ Vector((0,0,-1))
        world_rotation(p,blade.rotation_difference(Vector(sword_dir).normalized()) @ current)

def feet(left=(.13,-.04,.085), right=(-.13,-.04,.085)):
    for side,target in [('L',left),('R',right)]:
        sign=1 if side=='L' else -1
        limb('thigh.'+side,'shin.'+side,'foot.'+side,target,(sign*.3,-1,.5))
        p=rig.pose.bones['foot.'+side]
        world_rotation(p,p.bone.matrix_local.to_quaternion())

def body(crouch=0, lean=0, twist=0, roll=0, bounce=0):
    # Local root translation changes silhouette, never adds planar root motion.
    p=rig.pose.bones['spine']
    p.location=BASE['spine']['loc']+p.bone.matrix_local.to_3x3().inverted() @ Vector((0,0,bounce-crouch))
    delta('spine.001',(lean*.35,twist*.35,roll*.4))
    delta('spine.002',(lean*.65,twist*.65,roll*.6))
    delta('spine.004',(-lean*.3,-twist*.25,-roll*.2))
    bpy.context.view_layer.update()

def ready(crouch=0,lean=0,twist=0,roll=0,bounce=0):
    body(crouch,lean,twist,roll,bounce)
    feet()
    arm('L',(.44,-.22,1.13-crouch+bounce))
    arm('R',(-.46,-.34,1.14-crouch+bounce),(-.22,-.91,.35))

def key_all(frame):
    for p in rig.pose.bones:
        p.keyframe_insert(data_path='location',frame=frame,group=p.name)
        p.keyframe_insert(data_path='rotation_quaternion' if p.rotation_mode=='QUATERNION' else 'rotation_euler',frame=frame,group=p.name)
        p.keyframe_insert(data_path='scale',frame=frame,group=p.name)

def author(name,length,frames,fn,loop=False):
    action=bpy.data.actions[name]
    for fc in list(curves(action)):
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    if fc in list(bag.fcurves):bag.fcurves.remove(fc)
    rig.animation_data.action=action
    action.use_fake_user=True
    for frame in frames:
        scene.frame_set(int(frame), subframe=frame-int(frame));reset();fn(frame/length if length else 0);key_all(frame)
    # Quaternion continuity and eased, non-overshooting transitions.
    for p in rig.pose.bones:
        if p.rotation_mode!='QUATERNION':continue
        fcs=sorted([f for f in curves(action) if f.data_path==p.path_from_id('rotation_quaternion')],key=lambda f:f.array_index)
        if len(fcs)!=4:continue
        previous=None
        for i in range(len(fcs[0].keyframe_points)):
            q=Quaternion(tuple(f.keyframe_points[i].co.y for f in fcs))
            if previous and previous.dot(q)<0:
                for f in fcs:f.keyframe_points[i].co.y *= -1
                q.negate()
            previous=q
    for f in curves(action):
        for k in f.keyframe_points:
            k.interpolation='BEZIER';k.handle_left_type='AUTO_CLAMPED';k.handle_right_type='AUTO_CLAMPED'
        f.update()
    action['milkfrog_polished']=True;action['loop']=loop
    return {'name':name,'range':list(action.frame_range),'keys':sum(len(f.keyframe_points) for f in curves(action))}

def gait(t, direction, run):
    phase=2*math.pi*t; stride=.30 if run else .19; lift=.15 if run else .085
    bounce=(.055 if run else .025)*(1-math.cos(phase*2))
    body(.055 if run else .025,12 if run else 5,4*math.sin(phase),3*math.sin(phase),bounce)
    targets=[]
    for side,offset in [('L',0),('R',math.pi)]:
        p=phase+offset; s=math.sin(p)
        x=.16 if side=='L' else -.16
        targets.append((x+direction[0]*stride*s,-.035+direction[1]*stride*s,.085+lift*max(0,math.cos(p))))
    feet(*targets)
    arm('L',(.46,-.24+(.15 if run else .09)*math.sin(phase),1.12+bounce))
    arm('R',(-.46,-.36-.045*math.sin(phase),1.17+bounce),(-.15,-.90,.40))

def interpolate(t, points):
    if t<=points[0][0]:return points[0][1]
    for (a,va),(b,vb) in zip(points,points[1:]):
        if t<=b:
            u=(t-a)/(b-a);u=u*u*(3-2*u)
            return tuple(x+(y-x)*u for x,y in zip(va,vb))
    return points[-1][1]

# crouch, lean, twist, right-hand x/y/z, sword direction x/y/z
NEUTRAL=(.025,3,0,-.46,-.34,1.14,-.22,-.91,.35)
WINDUP=(.11,-12,-28,-.53,-.02,1.52,-.72,.50,.65)
FOLLOW_WIND=(.08,0,25,.08,-.54,1.31,.85,.10,.20)
THRUST_WIND=(.12,-7,-20,-.42,-.20,1.23,.12,-1,.03)
ATTACKS={
    'light':[(0,NEUTRAL),(.25,WINDUP),(.31,(.04,15,4,-.22,-.64,1.33,.12,-1,.04)),(.38,(.07,18,28,.14,-.56,1.17,.90,-.35,-.1)),(.62,(.05,8,18,.02,-.47,1.10,.86,-.10,-.25)),(1,NEUTRAL)],
    'followup':[(0,FOLLOW_WIND),(.25,(.09,-4,27,.04,-.55,1.36,.86,.05,.30)),(.31,(.04,12,0,-.25,-.62,1.28,-.10,-1,.08)),(.38,(.07,15,-28,-.59,-.35,1.10,-.87,-.45,-.12)),(.66,(.04,6,-13,-.56,-.23,1.07,-.71,-.40,-.20)),(1,NEUTRAL)],
    'thrust':[(0,THRUST_WIND),(.18181817,THRUST_WIND),(.25,(.04,18,-5,-.21,-.72,1.27,.03,-1,0)),(.36363634,(.04,19,0,-.13,-.76,1.26,0,-1,0)),(.64,(.06,8,-12,-.38,-.39,1.16,.08,-1,.05)),(1,NEUTRAL)],
    'slow':[(0,NEUTRAL),(.23,(.16,-15,-12,-.36,.04,1.72,.1,.22,1)),(.45,(.13,-10,-8,-.35,-.02,1.78,.05,.04,1)),(.57,(.07,22,3,-.23,-.72,1.32,0,-1,-.18)),(.68,(.14,28,8,-.20,-.51,.99,0,-.6,-.8)),(.82,(.12,12,4,-.26,-.36,1.05,.05,-.8,-.4)),(1,NEUTRAL)],
    'perilous':[(0,NEUTRAL),(.24,(.19,-12,-35,-.60,.10,1.43,-.8,.45,.25)),(.48,(.19,-10,-40,-.61,.13,1.48,-.9,.4,.18)),(.57,(.03,20,0,-.22,-.73,1.31,0,-1,.1)),(.68,(.12,20,35,.12,-.5,1.12,.9,-.3,-.16)),(.84,(.12,8,15,-.08,-.43,1.1,.7,-.5,-.1)),(1,NEUTRAL)],
    'charge':[(0,WINDUP),(.45,(.17,-10,-24,-.44,-.17,1.32,.2,-1,.15)),(1,THRUST_WIND)]
}

def attack_pose(t,name):
    v=interpolate(t,ATTACKS[name]);c,lean,twist,x,y,z,dx,dy,dz=v
    body(c,lean,twist);feet((.17,-.10,.085),(-.17,.025,.085))
    arm('R',(x,y,z),(dx,dy,dz));arm('L',(.35,-.35,1.15-c))

def guard(t,parry=False):
    accent=math.sin(math.pi*t) if parry else 0
    body(.08,5,-7+accent*14);feet((.16,-.07,.085),(-.16,.02,.085))
    arm('R',(-.24-accent*.1,-.54-accent*.10,1.38+accent*.1),(.60-accent*.2,-.35,.8))
    arm('L',(.30,-.36,1.31))

def reaction(t,name):
    if name=='broken':
        a=interpolate(t,[(0,(0,)),(.35,(1,)),(1,(1,))])[0]
        ready(.24*a,24*a,0,0);arm('R',(-.45,-.25,1.05-.18*a),(-.4,-.7,-.55));arm('L',(.42,-.25,1.03-.15*a))
    elif name=='death':
        a=interpolate(t,[(0,(0,)),(.18,(.25,)),(.64,(.94,)),(.82,(1,)),(1,(1,))])[0]
        # Collapse and settle on the side; root stays at the controller origin.
        body(.7*a,8*a,0,0)
        p=rig.pose.bones['spine']
        world_rotation(p,Quaternion((0,1,0),math.radians(-88*a)) @ p.bone.matrix_local.to_quaternion())
        delta('thigh.L',(-25*a,0,12*a));delta('thigh.R',(-18*a,0,-8*a))
        delta('shin.L',(48*a,0,0));delta('shin.R',(42*a,0,0))
        delta('upper_arm.L',(-10*a,0,-18*a));delta('upper_arm.R',(-8*a,0,16*a))
        bpy.context.view_layer.update()
        if a>.1:
            wrist=rig.pose.bones['hand.R'];current=wrist.matrix.to_quaternion()
            blade=(wrist.matrix @ SWORD_REL).to_quaternion() @ Vector((0,0,-1))
            direction=Vector((1,-.3,.03)).normalized()
            aim=blade.rotation_difference(direction) @ current
            best=None
            for roll in [0,45,-45,90,-90,180]:
                candidate=Quaternion(direction,math.radians(roll)) @ aim
                q=current.slerp(candidate,a)
                world_rotation(wrist,q)
                sword=bpy.data.objects['sword']
                floor=min((sword.matrix_world @ Vector(v)).z for v in sword.bound_box)
                score=max(0,.02-floor)*100 + current.rotation_difference(q).angle
                if best is None or score<best[0]:best=(score,q.copy())
            world_rotation(wrist,best[1])
        mesh=bpy.data.objects['tripo_node_02f756cc'].evaluated_get(bpy.context.evaluated_depsgraph_get())
        floor=min((mesh.matrix_world @ v.co).z for v in mesh.data.vertices)
        p.location += p.bone.matrix_local.to_3x3().inverted() @ Vector((0,0,max(0,.02-floor)))
    else:
        a=interpolate(t,[(0,(0,)),(.20,(1,)),(.48,(.7,)),(1,(0,))])[0]
        ready(.09*a,-20*a,(-15 if name=='deflected' else 9)*a)
        if name=='deflected':arm('R',(-.52,-.12,1.44),(-.6,.2,.8))
        else:arm('L',(.28,-.40,1.20-.06*a))

def dodge(t,direction):
    compression=interpolate(t,[(0,(.02,)),(.18,(.19,)),(.39,(.01,)),(.64,(.01,)),(.82,(.13,)),(1,(.02,))])[0]
    airborne=max(0,math.sin(math.pi*max(0,min(1,(t-.18)/.64))))
    ready(compression, direction[1]*-20*airborne,0,direction[0]*-15*airborne,.12*airborne)
    feet((.16+direction[0]*.04,-.035+direction[1]*.04,.085+.16*airborne),(-.16+direction[0]*.04,-.035+direction[1]*.04,.085+.16*airborne))
    arm('R',(-.37,-.30,1.19-compression+.12*airborne),(-.12,-.65,.75))

def air(t,name):
    if name=='jump':
        c=interpolate(t,[(0,(.03,)),(.22,(.17,)),(.45,(0,)),(1,(.02,))])[0]
        tuck=interpolate(t,[(0,(0,)),(.35,(.04,)),(.70,(.24,)),(1,(.16,))])[0]
        ready(c,8);feet((.17,-.04,.085+tuck),(-.17,-.04,.085+tuck))
    elif name=='fall':
        ready(.02,5,0,2*math.sin(2*math.pi*t));feet((.17,-.035,.11),(-.17,-.035,.11))
        arm('L',(.53,-.21,1.22));arm('R',(-.48,-.34,1.25),(-.2,-.7,.68))
    else:
        c=interpolate(t,[(0,(.03,)),(.3,(.21,)),(.55,(.16,)),(.8,(.06,)),(1,(.025,))])[0]
        ready(c,10*c/.21)

report=[]
for name,direction,run in [('jog',(0,-1),True),('jogBack',(0,1),True),('jogLeft',(1,0),True),('jogRight',(-1,0),True),('walkBack',(0,1),False),('walkLeft',(1,0),False),('walkRight',(-1,0),False)]:
    report.append(author(name,24,list(range(0,25,3)),lambda t,d=direction,r=run:gait(t,d,r),True))
lengths={'light':24,'followup':20,'thrust':24,'slow':36,'perilous':40,'charge':12}
for name,length in lengths.items():
    frames=sorted(set([0,length]+[round(t*length,5) for t,v in ATTACKS[name]]))
    report.append(author(name,length,frames,lambda t,n=name:attack_pose(t,n)))
report.append(author('guard',12,[0,6,12],lambda t:guard(t),True))
report.append(author('parry',4,[0,1,2,3,4],lambda t:guard(t,True)))
for name,length in [('hit',12),('deflected',16),('broken',24),('death',16)]:
    frames=list(range(length+1)) if name=='death' else [0,length*.18,length*.35,length*.48,length*.64,length*.82,length]
    report.append(author(name,length,frames,lambda t,n=name:reaction(t,n)))
for name,direction in [('dodgeForward',(0,-1)),('dodgeBack',(0,1)),('dodgeLeft',(1,0)),('dodgeRight',(-1,0))]:
    report.append(author(name,14,[0,2.52,5.46,8.96,11.48,14],lambda t,d=direction:dodge(t,d)))
for name,length in [('jump',12),('fall',12),('land',10)]:
    report.append(author(name,length,[0,length*.22,length*.3,length*.45,length*.55,length*.7,length*.8,length],lambda t,n=name:air(t,n),name=='fall'))

assert len(report)==26
for name,expected in PROTECTED.items():assert signature(bpy.data.actions[name])==expected,name+' was changed'
# Each strip is independently exportable. No HOLD from other actions during editing.
for track in rig.animation_data.nla_tracks:
    for strip in track.strips:
        strip.extrapolation='NOTHING'
        strip.action_frame_start=strip.action.frame_range[0]
        strip.action_frame_end=max(strip.action.frame_range[1],strip.action_frame_start+1)
rig.animation_data.use_nla=False
rig.animation_data.action=bpy.data.actions['light'];scene.frame_set(7)
scene.render.fps=24
with open(os.path.join(OUT,'authored-actions.json'),'w') as f:json.dump({'actions':report,'protected':PROTECTED,'fps':24},f,indent=2)
bpy.ops.wm.save_as_mainfile(filepath=r'C:\Users\pigeon\Desktop\nailong\milkfrog.blend')
result={'authored':len(report),'protected':PROTECTED,'filepath':bpy.data.filepath}
