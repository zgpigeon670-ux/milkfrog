"""Create editable SVG figures and PNG companions from actual WFC output."""
from pathlib import Path
import csv
import json
import html
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Docs/PCG/Figures"
OUT.mkdir(parents=True, exist_ok=True)
FONT = "C:/Windows/Fonts/msyh.ttc"


def figure(name, title, subtitle, width=1440, height=680):
    image = Image.new("RGB", (width, height), "#0e1a24")
    draw = ImageDraw.Draw(image)
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">', f'<rect width="{width}" height="{height}" fill="#0e1a24"/>']

    def rect(x, y, w, h, color):
        draw.rectangle((x, y, x + w, y + h), fill=color)
        svg.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{color}"/>')

    def text(x, y, value, size=22, color="#e0e9eb"):
        draw.text((x, y), value, font=ImageFont.truetype(FONT, size), fill=color)
        svg.append(f'<text x="{x}" y="{y + size}" fill="{color}" font-size="{size}" font-family="Microsoft YaHei,sans-serif">{html.escape(value)}</text>')

    text(40, 26, title, 34)
    text(40, 83, subtitle, 20, "#91a6b2")

    def save():
        image.save(OUT / f"{name}.png")
        (OUT / f"{name}.svg").write_text("\n".join(svg + ["</svg>"]), encoding="utf-8")

    return rect, text, save


rect, text, save = figure("modules", "模块与接口 / SIMPLE TILED MODEL", "N=1 · E=2 · S=4 · W=8；每个开口须与相邻格的反向开口一致。", height=660)
for col, (mask, label) in enumerate([(0, "空格"), (5, "直道"), (3, "转角"), (7, "丁字"), (15, "十字"), (1, "尽端")]):
    x, y, side = 45 + col * 230, 190, 150
    rect(x, y, side, side, "#233744")
    if mask:
        rect(x+57, y+57, 36, 36, "#c1ced0")
        for d, bit in enumerate([1,2,4,8]):
            if mask & bit:
                values=[(57,0,36,75),(75,57,75,36),(57,75,36,75),(0,57,75,36)][d]
                rect(x+values[0], y+values[1], values[2], values[3], "#c1ced0")
    text(x, y+178, label, 25)
    text(x, y+219, f"接口掩码 {mask}", 18, "#91a6b2")
text(45, 505, "走廊：3 m 净宽；房间：8×8 m；墙高：3 m；默认网格：10×10。", 24)
text(45, 550, "5 类走廊原型旋转去重 = 15 种状态；同接口的房间状态 15 种；加 1 种空格，共 31 种。", 22)
save()

maps = json.loads((ROOT / "Assets/CombatDemo/Validation/PCG/maps-offline.json").read_text(encoding="utf-8"))
# Match the actual order in WfcConfig.Defaults: Empty, then corridor/room pairs by rotation.
modules = [(0,False)]
for base in [1,5,3,7,15]:
    seen=set(); mask=base
    for rotation in range(4):
        if mask not in seen:
            seen.add(mask);modules.extend([(mask,False),(mask,True)])
        mask=((mask<<1)&15)|((mask>>3)&1)
rect,text,save=figure("seed-comparison","同一种子，两种约束 / SEED 18","左：主通路＋WFC＋可能连通检查；右：移除主通路和全局检查后的第 32 次局部解。",height=880)
for col,mode in enumerate(["hybrid","unconstrained"]):
    item=next(m for m in maps if m["mode"]==mode and m["seed"]==18)
    x0,y0,size=100+col*700,175,49
    for cell,tile in enumerate(item["tiles"]):
        mask,room=modules[tile];x=x0+cell%10*size;y=y0+(9-cell//10)*size
        color="#233744" if mask==0 else "#68a1ad" if room else "#bbc9cd"
        if cell==item["start"]:color="#2fc3dc"
        if cell==item["goal"]:color="#52c78a"
        rect(x,y,size-3,size-3,color)
        if mask:
            for d,bit in enumerate([1,2,4,8]):
                if mask&bit:
                    vx,vy,vw,vh=[(21,0,5,25),(23,21,24,5),(21,23,5,24),(0,21,25,5)][d]
                    rect(x+vx,y+vy,vw,vh,"#0e1a24")
    text(x0,700,"HYBRID / 通过整图验收" if mode=="hybrid" else "WFC ONLY / 局部合法，整图不连通",23)
    text(x0,744,"71 / 71 格可达 · 14 房间 · 6 环路" if mode=="hybrid" else "从自动起点可达 5 / 72 格",20,"#91a6b2")
text(100,815,"这是约束消融对照，不是不同 WFC 库的性能排名；两侧模块、权重及重试上限相同。",20)
save()

rows=list(csv.DictReader((ROOT/"Assets/CombatDemo/Validation/PCG/benchmark.csv").open(encoding="utf-8-sig")))
rect,text,save=figure("benchmark","100 个固定种子 / UNITY EDITOR BENCHMARK","种子 0–99；每种模式最多 32 次尝试；耗时仅计求解，不包含几何实例化与渲染。",height=620)
for idx,mode in enumerate(["hybrid","unconstrained"]):
    group=[r for r in rows if r["mode"]==mode]
    passed=sum(int(r["success"]) for r in group);local=sum(int(r["localSolutions"])>0 for r in group)
    average=sum(float(r["milliseconds"]) for r in group)/len(group)
    y=185+idx*180
    text(45,y,"主通路＋WFC＋连通约束" if idx==0 else "取消主通路与连通约束",25)
    rect(465,y+5,600,32,"#233744");rect(465,y+5,6*passed,32,"#52c78a")
    text(1100,y,f"{passed}/100 整图通过",23)
    text(465,y+61,f"局部解成功 {local}/100  ·  每个种子平均累计求解 {average:.2f} ms",21,"#91a6b2")
text(45,540,"0/100 仅说明本模块集和验收标准下的结果，不能推导 WFC 无法生成可玩关卡。",22)
save()
print("Figures written:",OUT)
