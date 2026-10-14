"""Render complete action playback frames through Blender MCP."""
import bpy, os, json
from mathutils import Vector
OUT=r'E:\UNITY\PROJECTS\milkfrog\Builds\AnimationRepair'
scene=bpy.context.scene;rig=bpy.data.objects['metarig']
if 'milkfrog_preview_settings' not in scene:
    scene['milkfrog_preview_settings']=json.dumps({'engine':scene.render.engine,'resolution_x':scene.render.resolution_x,'resolution_y':scene.render.resolution_y,'resolution_percentage':scene.render.resolution_percentage,'filepath':scene.render.filepath,'world_color':list(scene.world.color)})
cam=bpy.data.objects.get('Animation QA Camera')
if cam is None:
    data=bpy.data.cameras.new('Animation QA Camera');cam=bpy.data.objects.new('Animation QA Camera',data);scene.collection.objects.link(cam)
cam.location=(3,-5,2.7);cam.rotation_euler=(Vector((0,-.1,.95))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=3.2
scene.camera=cam;scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=320;scene.render.resolution_y=320;scene.render.resolution_percentage=100
scene.display.shading.light='STUDIO';scene.display.shading.color_type='TEXTURE';scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True;scene.display.shading.background_type='WORLD';scene.world.color=(.13,.16,.20)
rig.animation_data.use_nla=False
for name in PREVIEW_ACTIONS:
    action=bpy.data.actions[name];rig.animation_data.action=action
    folder=os.path.join(OUT,'BlenderFrames',name);os.makedirs(folder,exist_ok=True)
    length=max(1,int(action.frame_range[1]))
    for frame in range(length+1):
        scene.frame_set(frame);scene.render.filepath=os.path.join(folder,f'{frame:03d}.png');bpy.ops.render.render(write_still=True)
result={'previewed':PREVIEW_ACTIONS}
