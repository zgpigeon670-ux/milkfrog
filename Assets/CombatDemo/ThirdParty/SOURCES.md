# 第三方来源、固定版本与许可

本清单区分实际使用与研究参考。没有把任一 GitHub 仓库直接视作已在本项目验证的完整框架。

## 实际使用

| 内容 | 来源 / 固定版本 | 原始位置 | 本项目改动 | 许可 |
|---|---|---|---|---|
| Noto Sans SC 可变字体 | [Google Fonts 官方仓库](https://github.com/google/fonts/tree/b5efa9c32e8f9b63005f5cdb1ad5527a77d2cd04/ofl/notosanssc)，提交 `b5efa9c32e8f9b63005f5cdb1ad5527a77d2cd04` | `ofl/notosanssc/NotoSansSC[wght].ttf` | 原样放入 `Runtime/MVP/Resources/NotoSansSC-VF.ttf`，供危招 HUD 显示“危”和“闪避”；文件名仅为 Unity 资源路径调整，没有修改字形或字体内部名称 | SIL Open Font License 1.1，完整文本见 [NotoSansSC/OFL.txt](NotoSansSC/OFL.txt) |
| Mannequin 与 43 个动画 | [Quaternius 官方 Universal Animation Library](https://quaternius.itch.io/universal-animation-library)，免费 Standard 下载 upload 17958403 | 下载包 `Universal Animation Library[Standard]/Unity/UAL1_Standard.fbx` | Humanoid 导入、烘焙轴向、锁定根位移、循环标志；模型挂载本项目几何剑 | [作者随包 CC0 声明](Quaternius/License.txt) |
| 动画导入适配 | [firstkindgamer/QuaterniusUnityUtils](https://github.com/firstkindgamer/QuaterniusUnityUtils/tree/62a4f8e790330e089488a6bd66df2734c28a8796) | `QuaterniusUtils.cs` | 限定本项目 FBX、发现实际 `Armature/root`、检查 Avatar、立即重新导入；没有复制旧骨架路径 | [Unlicense](Licenses/QuaterniusUnityUtils-Unlicense.txt) |
| 性能采样 | [DohunWi/3D-Action](https://github.com/DohunWi/3D-Action/blob/da73b5be7ffa7216c48e196c4a69fe3ff66c6c65/3D_game/Assets/Scripts/Manager/PerformanceLogger.cs) | `3D_game/Assets/Scripts/Manager/PerformanceLogger.cs` | 提取帧时收集、nearest-rank p95 与 GC ProfilerRecorder 到 `CombatPerformanceSampler`；移除单例、跨场景对象、自动修改帧率、其他系统依赖 | [MIT / Copyright 2026 Dohun](Licenses/Dohun-MIT.txt) |

对应适配脚本：`Editor/QuaterniusAnimationImport.cs`、`Runtime/CombatPerformanceSampler.cs`。性能测试工具仅在明确执行验证命令时临时设 60 FPS、关闭 VSync；采样器和正常场景不调整全局帧率。

固定文件校验：

- Noto Sans SC 原始可变 TTF SHA-256：`A3041811A78C361B1DE50F953C805E0244951C21C5BD412F7232EF0D899AF0DA`
- 官方 ZIP SHA-256：`CC73FC4E495B82958207316596317A3F40B9FA38065BDE1027937452DA537724`
- 使用的 FBX SHA-256：`21B32D912DA3CB93426D974FB945E86F5B2E86970ACD2CE89905E0FBF9F1DCC2`
- 下载包未使用 `_RM.fbx`；未从第三方镜像导入动画。

`Animations/Guard.anim`、`Parry.anim`、`Deflected.anim`、`Broken.anim` 是从 CC0 动作采样并调整 Humanoid 肌肉曲线的姿势适配：格挡/弹反基于 Sword_Idle，后仰基于 Hit_Head，失衡基于 Crouch_Idle_Loop。不是作者提供的专用弹反或双人处决成品。其余必要片段直接引用 FBX 子资源。剑、池化闪光、短音效由本项目代码生成，不含外来模型、录音或音乐。

第三阶段新增 `Animations/Directional` 下的六个后退／侧移走跑片段、四个垫步、蓄力和突刺片段：在上述 CC0 Mannequin / Sword_Idle 基础上，由本项目 `CombatMotionAuthoring` 通过腿部／手臂逆运动学和 Humanoid 肌肉关键帧制作。不是额外下载的动画或只狼资产；没有增加外部代码或素材许可证依赖。

## 只作研究参考，未复制代码或资源

- [DragonSouls-Unity3D](https://github.com/btuhany/DragonSouls-Unity3D/tree/f54824255517801d5d3443848e1e4275d8d5066d)：武器启停、命中表现、镜头职责划分；未引入旧 Cinemachine 或第三方资产。
- [gltf-universal-animation-library](https://github.com/J-Ponzo/gltf-universal-animation-library/tree/e24c23cf2a1323488a3faa226ea7ea21f644b73e)：核对动作清单，正式资源采用上述作者原包。
- [Sigil Combat](https://github.com/forestlii/sigil-combat)：后续能力系统参考，本轮不引入 GAS。
- [Sekiro-Like-Combat-and-Traversal-System](https://github.com/Louaios/Sekiro-Like-Combat-and-Traversal-System)：仅功能研究，未找到明确许可证，因此没有复制代码或资产。

各项目根许可证不被用来推断其所有模型、音乐和动画的许可。

