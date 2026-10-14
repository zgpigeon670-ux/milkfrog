# Milkfrog 动作与 Player 修复记录

完成日期：2026-10-14。风格为夸张卡通，四向闪避采用蹲伏弹跳闪身。

## 文件与预览

- 可编辑源文件：[milkfrog.blend](C:/Users/pigeon/Desktop/nailong/milkfrog.blend)。打开后处于姿态模式，动作编辑器可选择全部 28 个动作。
- 原始状态备份：[milkfrog-before-animation-repair.blend](C:/Users/pigeon/Desktop/nailong/milkfrog-before-animation-repair.blend)，包含修改前尚未保存的内容。
- Unity 已替换的模型：[milkfrog.fbx](E:/UNITY/PROJECTS/milkfrog/Assets/CombatDemo/Animations/milkfrog/milkfrog.fbx)。原 GUID `3d37cc9d3e8361644ad876ca8759933b` 保留。
- 旧 FBX 与修改基线保存在 `E:/UNITY/PROJECTS/milkfrog/Builds/AnimationRepair/`。
- Windows 构建：[MilkfrogMVP.exe](E:/UNITY/PROJECTS/milkfrog/Builds/AnimationRepair/Windows/MilkfrogMVP.exe)。运行时需要同目录的 DLL 和数据文件。

![动作总览](E:/UNITY/PROJECTS/milkfrog/Assets/CombatDemo/Validation/AnimationRepair/Blender-Actions.png)

![攻击动作播放](E:/UNITY/PROJECTS/milkfrog/Assets/CombatDemo/Validation/AnimationRepair/Blender-Attacks.gif)

![移动动作播放](E:/UNITY/PROJECTS/milkfrog/Assets/CombatDemo/Validation/AnimationRepair/Blender-Locomotion.gif)

![Unity 游戏内表现](E:/UNITY/PROJECTS/milkfrog/Assets/CombatDemo/Validation/AnimationRepair/Unity-Gameplay.png)

## 修改内容

`walk` 与 `idle` 的 Blender 关键帧、曲线及帧范围原样保留。其余 26 个动作按完整身体姿态重新制作，保留骨架、蒙皮和模型；动作均为 24 FPS。

- 移动：`jog`、`jogBack`、`jogLeft`、`jogRight`、`walkBack`、`walkLeft`、`walkRight`。
- 攻击：`light`、`followup`、`thrust`、`slow`、`perilous`、`charge`。
- 防御与反应：`guard`、`parry`、`hit`、`deflected`、`broken`、`death`。
- 闪避与跳落：四向 `dodge`、`jump`、`fall`、`land`。

Unity 正确绑定跑步、方向移动、蓄力与失衡动作；修复 Generic 骨架防御遮罩、按实际闪避时长采样、处决表现被新动作打断、拖尾绑定与清除。角色模型重新居中，保持原 0.85 缩放；剑尖在倒地动作中避免穿入地面。

轻斩、直刺和追击分别使用实际剑身端点烘焙的 65 点轨迹，追击不再复用轻斩。运行时轨迹改为从角色原点变换，与烘焙和显示坐标一致，消除错误叠加碰撞体中心高度的偏移。训练角色的 Humanoid 烘焙路径保留兼容。

攻击数值、前后摇、移动速度、闪避距离及无敌窗口保持原规则。慢攻和危险攻击作为 FBX 动作提供，没有为 Player 增加新的操作或攻击规则。

## 验证与边界

- Unity 6000.6.2f1，在独立验证副本中执行。EditMode **132/132** 通过；最终 PlayMode **132/132** 通过。
- 验证全部动作导入、资源映射、循环设置、模型对齐与独立轨迹；实际剑身在 30/60/120 FPS 下与轨迹对齐，三种攻击各命中一次，墙体遮挡阻止伤害。
- 回归覆盖原项目的战斗、输入、移动、相机、菜单、道具、存档及世界流程；新增处决后闪避的表现中断检查。
- Windows 构建成功。此前已执行构建启动检查；图形与动作证据来自 Unity 编辑器中的真实 Play Mode 和直接动作渲染。
- Blender 完整播放预览与逐帧数据检查已完成，保留动作的 SHA-256 比较通过。原始 XML、日志、单动作 GIF 和截图在 `Builds/AnimationRepair`。
- 以上属于自动化验证与视觉检查；真人键鼠手感及完整通关体验仍需实际游玩确认。

## 后续修改动作时

通过 Blender MCP 使用 `Tools/Export-Milkfrog.py` 的 FBX 单位设置导出。动画名称、骨骼层级与原 FBX `.meta` 应保留。修改攻击姿态或有效区间后，在独立验证副本调用 `MilkfrogAnimationUpgrade.Run` 重新连接资源并烘焙三条轨迹，再验证实际剑身与碰撞一致。
