# 黄牛·踏地：模型与动作来源

本轮交付的 YellowBull 模型、18 根骨骼、7 种材质与 11 个动作均由 `Tools/Author-YellowBull.py` 独立生成，不包含奶蛙或旧剑士的网格、骨架、刀刃轨迹和动画。

- 可编辑模型与全部 Actions：`SourceArt/YellowBull/YellowBull.blend`。
- Unity 模型和动作：本目录 `YellowBull.fbx`，Generic 导入，独立 Avatar。
- 外形方向：按用户指定的“牛来”参考金黄色、短角、宽鼻和圆钝脸，使用本项目新建的低模几何。
- KayKit Character Animations 官方页：https://kaylousberg.itch.io/kaykit-character-animations ，页面标注 CC0，免费 FBX 可用于个人与商业项目。
- 检查样本：作者发布于 OpenGameArt 的 `kaykit_character_animations_1.2.zip`，https://opengameart.org/content/kaykit-character-animations 。下载与检查日志保留在 `Builds/BullValidation`。
- 该免费样本的骨架只有 Body、Head、双臂和双手挂点，缺少黄牛行走、后蹬、震脚所需的腿部骨骼。因此采用方案允许的自制动作路径，没有把该样本导入交付资源，也不把自制动作标为 KayKit 动作。

动作：Bull_Idle、Bull_Walk、Bull_Hit、Bull_Death、Bull_Broken、Bull_Roar、Bull_Horn、Bull_Kick、Bull_Charge、Bull_Slam、Bull_Wave。所有动作都在独立牛骨架上制作，战斗时间由模拟驱动，世界位移由角色碰撞控制器驱动，不依赖 Root Motion。

运行生成脚本会重建本目录 FBX 和 SourceArt 的源文件；手工修改源文件后应先另存备份。日常编辑器升级不会重新运行建模脚本，也不会覆盖已有招式数值或材质颜色。
