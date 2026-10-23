"""Assemble a reviewable handoff from successful Unity/XML/player evidence."""
from pathlib import Path
import csv
import hashlib
import json
import re
import shutil
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / "Assets/CombatDemo/Validation/PCG"
BUILD = ROOT / "Builds/Milkfrog-PCG"
rows = list(csv.DictReader((EVIDENCE / "benchmark.csv").open(encoding="utf-8-sig")))
groups = {mode: [r for r in rows if r["mode"] == mode] for mode in ("hybrid", "unconstrained")}
edit = ET.parse(EVIDENCE / "EditMode.xml").getroot()
play = ET.parse(EVIDENCE / "PlayMode.xml").getroot()
build = json.loads((EVIDENCE / "build.json").read_text(encoding="utf-8-sig"))
smoke = json.loads((BUILD / "SmokeEvidence/smoke.json").read_text(encoding="utf-8-sig"))
assert edit.attrib["result"] == play.attrib["result"] == "Passed"
assert edit.attrib["failed"] == play.attrib["failed"] == "0"
assert build["result"] == "Succeeded" and smoke["passed"] and smoke["rendered"] and smoke["moved"]
assert all(r["success"] == "1" for r in groups["hybrid"])
maps = json.loads((EVIDENCE / "maps-offline.json").read_text(encoding="utf-8"))
offline_map = next(m for m in maps if m["mode"] == "hybrid" and m["seed"] == 18)
assert smoke["tiles"] == offline_map["tiles"], "Unity player / offline topology mismatch"

baseline = json.loads((ROOT / "Builds/PCGValidation/existing-dirty-files.json").read_text(encoding="utf-8-sig"))
for item in baseline:
    assert hashlib.sha256((ROOT / item["path"]).read_bytes()).hexdigest().upper() == item["hash"], item["path"]

source_files = list((ROOT / "Assets/CombatDemo/Runtime/PCG").glob("*.cs"))
source_files += list((ROOT / "Assets/CombatDemo/Editor").glob("Pcg*.cs"))
source_files += [ROOT / "Assets/CombatDemo/Tests/EditMode/WfcTests.cs", ROOT / "Assets/CombatDemo/Tests/PlayMode/PcgPlayTests.cs", ROOT / "Assets/CombatDemo/Scenes/PCG_Whitebox.unity"]
source_files += list((ROOT / "Assets/CombatDemo/Settings/PCG").glob("*.asset")) + list((ROOT / "Assets/CombatDemo/Settings/PCG").glob("*.mat"))
manifest = {"unity": smoke["unity"], "baselineDirtyFilesUnchanged": len(baseline), "windowsPlayerMatchesOfflineSeed18": True, "files": {str(p.relative_to(ROOT)).replace("\\", "/"): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(source_files)}}
for path in [BUILD / "MilkfrogPCG.exe", BUILD / "MilkfrogPCG_Data/Managed/Milkfrog.CombatDemo.dll"]:
    manifest["files"][str(path.relative_to(ROOT)).replace("\\", "/")] = hashlib.sha256(path.read_bytes()).hexdigest()
(EVIDENCE / "source-manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
shutil.copyfile(BUILD / "SmokeEvidence/smoke.json", EVIDENCE / "windows-smoke.json")
for src, dest in [("player.png", "windows-player.png"), ("observation.png", "windows-overview.png")]:
    shutil.copyfile(BUILD / "SmokeEvidence" / src, ROOT / "Docs/PCG/Figures" / dest)

summary = f"最终回归：EditMode **{edit.attrib['passed']}/{edit.attrib['total']}**、带图形设备的 PlayMode **{play.attrib['passed']}/{play.attrib['total']}**，均无失败及跳过；Windows 构建成功。独立 Windows Player 隐藏启动后，种子 18 生成、角色位移、两个小怪及两台相机的 URP 离屏渲染检查通过，进程以 0 退出。离屏画面不包含 GUI；本文中的界面截图来自 Unity Editor。人工键鼠手感尚未作为验收证据。"
report_path = ROOT / "Docs/PCG/WFC实践综述.md"
article = report_path.read_text(encoding="utf-8")
for mode, prefix in [("hybrid", "HYBRID"), ("unconstrained", "BASELINE")]:
    times = [float(r["milliseconds"]) for r in groups[mode]]
    article = article.replace("{{" + prefix + "_MS}}", f"{sum(times)/len(times):.2f}")
    article = article.replace("{{" + prefix + "_MAX}}", f"{max(times):.2f}")
article = article.replace("{{VALIDATION_SUMMARY}}", summary)
assert "{{" not in article
report_path.write_text(article, encoding="utf-8")

readme = """奶蛙 WFC 白盒 Demo / Milkfrog PCG

运行 MilkfrogPCG.exe。移动或分享时，请复制整个 Milkfrog-PCG 文件夹。
WASD 移动，鼠标镜头，Space 跳跃，左 Shift 垫步，左键攻击/蓄力，右键防御/弹反，中键选敌。
Esc 暂停与种子面板；R 重建当前种子；N 下一个种子；F3 观察；F4 停止播放并单步。
观看坍塌：F3 → R。青色起点、绿色出口、橙色遭遇房间；到达出口即可完成，不要求清怪。
死亡或跌落会重置同一地图及小怪。本独立场景不写主线存档。
采用主通路约束＋WFC＋全局连通检查；小怪仅在所在房间内交战，不跨房间寻路。
完整综述位于 Unity 工程 Docs/PCG/WFC实践综述.md。
"""
(BUILD / "README.txt").write_text(readme, encoding="utf-8-sig")

report = f"""# PCG 实现与交付验证记录

日期：2026-10-18；Unity {smoke['unity']}。全部为本机实际执行结果。

{summary}

## 数据与入口

- [Unity 场景](../../Scenes/PCG_Whitebox.unity)；[配置资产](../../Settings/PCG/WhiteboxWFC.asset)。
- [Windows 试玩包入口](../../../../Builds/Milkfrog-PCG/MilkfrogPCG.exe)，必须保留完整目录。
- [操作说明](../../../../Docs/PCG/README.md)；[实践综述](../../../../Docs/PCG/WFC实践综述.md)。
- [EditMode XML](EditMode.xml)；[PlayMode XML](PlayMode.xml)；[批量生成 CSV](benchmark.csv)。
- [构建结果](build.json)；[Windows Player 检查](windows-smoke.json)；[源码与构建哈希](source-manifest.json)。
- [离线检查](offline-checks.json)仅验证同源算法，不替代 Unity 物理检查。

## 验证范围

固定种子 0–99：混合方案 100/100 通过全部验收；取消主通路和可能连通约束后，100/100 种子均得到局部解，但 0/100 通过整图验收。每种模式最多尝试 32 次，每次最多回溯 2048 次。计时不包含几何构建与渲染。

PCG PlayMode 检查包含：三个种子的完整最短路线、默认地图每条开放连接、12 段关闭墙的跑/跳/垫步阻挡及镜头避障、房间内遭遇和返回、双方攻击伤害、按住蓄力、暂停和观察输入清理、失败时保留旧图，以及真实 Update 驱动的死亡与跌落自动重置。纯几何检查临时关闭小怪碰撞，战斗检查保留。

Windows 检查使用最终生成的 exe 与 Managed 程序集，实际初始化 Direct3D 11 / NVIDIA GeForce RTX 4060 Laptop GPU。为了避免隐藏窗口的黑色后备缓冲区影响截图，通过 URP StandardRequest 把两台相机渲染到纹理，并验证像素有变化；这证明三维内容渲染成功，不等同于人工体验完整界面。GUI 与不同观察状态通过 Editor 截图核对。Windows 种子 18 的完整模块数组还与离线输出逐项一致。

## 截图索引

| 文件 | 内容 |
|---|---|
| [01](Screenshots/01.png) | 默认种子第三人称跑图 |
| [02](Screenshots/02.png) | 默认种子整体白盒 |
| [03](Screenshots/03.png) | 完成后的模块观察 |
| [04](Screenshots/04.png) | 房间内遭遇 |
| [05](Screenshots/05.png) | 候选与传播中间状态 |
| [06](Screenshots/06.png) | 种子 42 的整体白盒 |
| [07](Screenshots/07.png) | 到达出口 |
| [Windows 角色相机](../../../../Docs/PCG/Figures/windows-player.png) | 独立包离屏三维渲染，不含 GUI |
| [Windows 俯视相机](../../../../Docs/PCG/Figures/windows-overview.png) | 独立包离屏三维渲染，不含 GUI |

## 保存边界与限制

本次新增 PCG 运行模块、配置、独立场景、编辑器工具、测试、综述与证据。原有 {len(baseline)} 个已修改文件的 SHA-256 与开始时逐一相同；原有未跟踪的黄牛内容保留。未改动主线构建列表或磁盘存档格式，Windows 构建通过显式单场景列表完成。

这里的回归结果与本机截图不是人工键鼠手感验收。当前是无高差白盒，敌人不具备跨房间寻路；固定端点和单调主路使最短路线长度缺少变化。两组消融同时改变多项约束，不足以区分各项约束的独立贡献。

## 复现

关闭同项目编辑器后，在工程根目录执行 `Tools/Validate-Pcg.ps1 -Stage All`。独立包的可选自动检查参数为 `-pcg-smoke`，会将结果写入包内 SmokeEvidence 并自行退出；正常运行不写验证文件。日志保存在 `Builds/PCGValidation/`。
"""
(EVIDENCE / "Report.md").write_text(report, encoding="utf-8")
for md in [report_path, EVIDENCE / "Report.md", ROOT / "Docs/PCG/README.md"]:
    for target in re.findall(r"!?\[[^\]]*\]\(([^)]+)\)", md.read_text(encoding="utf-8")):
        if not target.startswith(("http:", "https:", "#")):
            assert (md.parent / target).resolve().exists(), f"Broken link: {md}: {target}"
print("Evidence complete. Chinese article characters:", len(re.findall(r"[\u4e00-\u9fff]", article)))
