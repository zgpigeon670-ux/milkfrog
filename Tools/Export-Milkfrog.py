import bpy, os
rig=bpy.data.objects['metarig'];scene=bpy.context.scene
previous_action=rig.animation_data.action;previous_frame=scene.frame_current
rig.animation_data.use_nla=False;rig.animation_data.action=bpy.data.actions['idle'];scene.frame_set(0)
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT')
for n in ['metarig','sword','tripo_node_02f756cc']:bpy.data.objects[n].select_set(True)
bpy.context.view_layer.objects.active=rig
rig.animation_data.use_nla=True;rig.animation_data.action=None
path=r'E:\UNITY\PROJECTS\milkfrog\Builds\AnimationRepair\milkfrog.fbx'
# Put the unit conversion in FBX metadata, rather than a 100x armature transform.
# This keeps the Generic prefab's existing 0.85 model scale valid.
bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_force_startend_keying=True,bake_anim_step=1.0,bake_anim_simplify_factor=0.0,path_mode='AUTO',embed_textures=False)
rig.animation_data.use_nla=False;rig.animation_data.action=previous_action;scene.frame_set(previous_frame);bpy.ops.object.mode_set(mode='POSE')
bpy.ops.wm.save_as_mainfile(filepath=r'C:\Users\pigeon\Desktop\nailong\milkfrog.blend')
result={'export':path,'bytes':os.path.getsize(path),'actions':len(bpy.data.actions)}
