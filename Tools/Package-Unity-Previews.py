from pathlib import Path
from PIL import Image, ImageDraw

root=Path(__file__).resolve().parents[1]/'Builds/AnimationRepair'
names=['idle','jog','walkBack','jogLeft','light','followup','thrust','charge','guard','parry','hit','broken','dodgeForward','dodgeLeft','jump','fall','land','death']
sheet=Image.new('RGB',(240*6,270*3),'#202731');draw=ImageDraw.Draw(sheet)
for i,name in enumerate(names):
    path=root/'Unity'/f'{name}-{4 if name=="death" else 2 if name!="idle" else 0}.png'
    image=Image.open(path).convert('RGB').resize((240,240));x=(i%6)*240;y=(i//6)*270;sheet.paste(image,(x,y));draw.text((x+10,y+245),name,fill='white')
sheet.save(root/'Unity-Actions.png')
shots=sorted((root/'Gameplay').glob('*.png')) if (root/'Gameplay').exists() else []
if shots:
    sheet=Image.new('RGB',(480*3,300*4),'#202731');draw=ImageDraw.Draw(sheet)
    for i,path in enumerate(shots):
        x=(i%3)*480;y=(i//3)*300;image=Image.open(path).convert('RGB');image.thumbnail((480,270));sheet.paste(image,(x,y));draw.text((x+10,y+275),path.stem,fill='white')
    sheet.save(root/'Unity-Gameplay.png')
