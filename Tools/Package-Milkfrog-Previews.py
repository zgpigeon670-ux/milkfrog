from pathlib import Path
from PIL import Image, ImageDraw
import json

root = Path(__file__).resolve().parents[1] / 'Builds/AnimationRepair'
groups = {
    'Attacks': ['light','followup','thrust','charge','slow','perilous'],
    'Reactions': ['guard','parry','hit','deflected','broken','death'],
    'Dodge-Air': ['dodgeForward','dodgeBack','dodgeLeft','dodgeRight','jump','fall','land'],
    'Locomotion': ['walk','jog','walkBack','jogBack','walkLeft','jogLeft','walkRight','jogRight'],
}
frames = {}
(root/'BlenderGIF').mkdir(exist_ok=True)
for folder in sorted((root/'BlenderFrames').iterdir()):
    images = [Image.open(f).convert('RGB') for f in sorted(folder.glob('*.png'))]
    frames[folder.name] = images
    labeled = []
    for i,im in enumerate(images):
        page=Image.new('RGB',(320,348),'#202731');page.paste(im,(0,0))
        ImageDraw.Draw(page).text((10,327),f'{folder.name}  frame {i}',fill='white');labeled.append(page)
    durations = [42]*len(labeled)
    durations[-1] = 400
    labeled[0].save(root/'BlenderGIF'/f'{folder.name}.gif',save_all=True,append_images=labeled[1:],duration=durations,loop=0)
for label,names in groups.items():
    pages=[]
    for frame in range(48):
        sheet=Image.new('RGB',(320*4,348*2),'#202731');draw=ImageDraw.Draw(sheet)
        for i,name in enumerate(names):
            images=frames[name];idx=min(len(images)-1,int(frame/47*(len(images)-1)))
            x=(i%4)*320;y=(i//4)*348;sheet.paste(images[idx],(x,y));draw.text((x+10,y+327),name,fill='white')
        pages.append(sheet)
    pages[0].save(root/f'Blender-{label}.gif',save_all=True,append_images=pages[1:],duration=42,loop=0)
sheet=Image.new('RGB',(240*7,270*4),'#202731');draw=ImageDraw.Draw(sheet)
for i,name in enumerate(sorted(frames)):
    images=frames[name]; x=(i%7)*240;y=(i//7)*270
    idx=len(images)-1 if name=='death' else len(images)//2
    sheet.paste(images[idx].resize((240,240)),(x,y));draw.text((x+10,y+244),name,fill='white')
sheet.save(root/'Blender-Actions.png')
print(json.dumps({'actions':len(frames),'playback_frames':sum(map(len,frames.values()))}))
