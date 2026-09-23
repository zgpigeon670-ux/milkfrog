# 第三阶段交付与验证

日期：2026-09-23。Unity 6000.6.2f1，URP，新 Input System。场景为 `Assets/CombatDemo/Scenes/CombatDemo_Animated.unity`。

## 已实现

- 四向走／跑：前进沿用 CC0 动作；后退、左移、右移分别制作走／跑关键帧，斜向混合相邻方向。实际位移驱动步频，防御保留上身遮罩。
- 方向垫步：空格＋方向，无输入后撤；总长 0.38 秒，前 0.24 秒平滑移动最多 1.6m，无敌 `[0.06, 0.18)`。CharacterController 处理墙体和角色阻挡，不补偿被阻挡的距离。
- 短按斩击／蓄力突刺：按下即准备，0.18 秒前释放抵扣普通前摇；0.18 秒进入蓄力，0.80 秒满蓄后保持。松开释放，突刺阶段为 0.12／0.12／0.42 秒。
- 突刺按蓄力比例提供 10→22 生命伤害、10→28 命中架势、20→35 格挡架势。可格挡、可弹反，无霸体。攻击规则开始时取快照，使用独立烘焙刀刃轨迹。
- 准备与蓄力可防御／闪避取消，释放后不可主动取消；受击仍可中断。失焦、重置、死亡清理未完成输入，顿帧松键不补发突刺。
- 同帧意图优先级为重置、闪避、防御、攻击。保留供敌人及原测试使用的离散攻击接口。
- 增加蓄力进度提示，修正缩放 HUD 装饰偏移；保留背后跟随、血条、架势条和弹反闪白反馈。

所有新动作关闭 Root Motion。`Animations/Directional` 的六个方向走跑、四个垫步、蓄力与突刺共 12 个片段是项目适配制作，基于现有 CC0 骨架；没有下载只狼资产。来源详见 [SOURCES.md](../../ThirdParty/SOURCES.md)。

规则入口为 `CombatCore`；输入快照在 `DemoInput` / `CombatDemoSession`；位移与刀刃扫掠在 `CombatActor`；表现采样在 `CombatAnimationPresenter`。斩击／突刺配置为 `Settings/PlayerSlash.asset`、`EnemySlash.asset`、`PlayerThrust.asset`，闪避参数保留在 `AnimatedCombat.asset`。

## Unity 编译与自动化结果

使用独立验证副本 `%TEMP%/MilkfrogCombatValidation`，避免占用原项目。运行时、编辑器脚本、场景、配置、动画、测试及其 meta 与交付源项目逐文件校验一致；文档和证据随后补充。

| 检查 | 结果 | 证据 |
|---|---|---|
| Edit Mode | 38 / 38，通过 | [EditMode.xml](EditMode.xml) |
| Play Mode | 33 / 33，通过 | [PlayMode.xml](PlayMode.xml) |
| 原有回归 | 原 50 项保持通过，新加 21 项 | 两份 XML 中的测试明细 |
| Unity 脚本及 Windows Development 构建 | 完成，未出现 C# 编译错误或 shader error | [ExecutionEvidence.txt](ExecutionEvidence.txt) |
| 独立程序启动 | 交付目录的 EXE 已启动，Input System、D3D11、PhysX 初始化完成；启动日志未检出异常 | 同上；不等于真人试玩 |

新增测试覆盖方向权重与完整步态周期脚部运动、无 Root Motion 漂移；垫步无敌两端边界、无敌不消耗攻击去重、方向锁定、无输入后撤、墙体／角色阻挡、受击停止移动；准备阈值、满蓄保持、伤害快照、取消、失焦、冻结松键、输入优先级、重置；突刺格挡／弹反、隔墙不命中、刀刃表现与烘焙轨迹对齐及拖尾清理。

30／60／120 FPS 的自动模拟分别检查原攻防交换、满蓄突刺单次伤害和垫步位移。它们验证时间步一致性，不代表三个实际显示帧率的人工操作体验。所有测试详细断言以 XML 对应源码为准。

调试中修正了浮点边界导致的攻击阶段延后一小步和满蓄提示未切换问题；方向测试按完整周期验证脚部活动，避免只比较两个接近同相位的姿势。

## 实际渲染截图

截图来自 Unity Play Mode，手动推进会话时间后使用真实场景渲染，不是示意图。动作检查使用侧前方机位；UI 检查恢复玩家背后机位。方向／垫步图暂时隐藏敌人，便于观察腿部。

| 内容 | 截图 |
|---|---|
| 后退步态，四个采样时刻 | `Locomotion-0.png` 至 `Locomotion-3.png` |
| 左侧移，四个采样时刻 | `Locomotion-4.png` 至 `Locomotion-7.png` |
| 右侧移，四个采样时刻 | `Locomotion-8.png` 至 `Locomotion-11.png` |
| 部分蓄力／满蓄保持 | [部分](Actions-0.png)、[满蓄](Actions-1.png) |
| 突刺前摇／有效期／回收 | [前摇](Actions-2.png)、[有效期](Actions-3.png)、[回收](Actions-4.png) |
| 前／后／左／右垫步 | [前](Actions-5.png)、[后](Actions-6.png)、[左](Actions-7.png)、[右](Actions-8.png) |
| 小窗口 640×360 | [UI](Actions-9.png) |
| 16:9，1280×720 | [UI](Actions-10.png) |
| 16:10，1280×800 | [UI](Actions-11.png) |

已检查方向动作、满蓄提示、突刺和垫步姿势，以及上述三种尺寸的血条、架势条和操作提示。未见截图中的 T Pose 或 UI 边缘缺失；640×360 下文字较小。步频按生成片段的脚部实际行程校准，数值见 [stride-calibration.txt](stride-calibration.txt)。静态截图不能证明所有转向和斜向动作无滑步。

## 构建与保留内容

Windows 构建位于项目 `Builds/CombatDemo/CombatDemo.exe`，须连同同目录 Data 和依赖文件运行。已将验证副本最终构建复制到交付目录，并核对运行时 DLL SHA-256：

`6565E79B22D6B4EF4E1C5B815E79E8DCB0CE377CFD0CCA1D54E8D84E4442D52E`

原胶囊场景文件未改动，其 SHA-256 保持：

`7753B9A6CC9EBF2EBC15648C4B76120FC60E0C50A6174297B356B21F4B7DBFE0`

未修改原项目 Build Profile；未提交或推送 Git。新增资产与许可仍集中在 `Assets/CombatDemo`，可执行输出位于项目 `Builds`。

## 已知限制与待验收

- **原编辑器窗口的最大化与预览 Scale 待检查。** 自动化两次无法激活该窗口，因此停止 UI 操作；没有确认此前的 1.3× 是否恢复。批处理尝试最大化得到的图与 1280×800 图一致，不能作为真实最大化验收。试玩前将 Game View Scale 调到适合窗口的比例，通常为 1×。
- **真人操作与连续画面待验收。** 请在键鼠试玩中检查八方向、快速换向、移动防御、贴墙停止、蓄力转向和取消的连续手感。已交付关键帧动作与脚程校准，但不将其描述为动作捕捉质量，也不以自动化结果代替所有姿势／滑步的人工判断。
- **编辑器日志尚非零警告。** 构建／退出阶段记录了 `JobTempAlloc` 残留分配警告；截图批处理记录了 `UnityEditor.Search.SearchDatabase` 启动索引异常。它们没有阻止本次构建和截图，但尚未定位，不能声称通过内存泄漏检查。另有旧 Unity API 的 obsolete 提示。
- 本轮没有重做性能基准，不宣称性能提升；历史性能数字仍只对应当时版本。
- 不包含冲刺、跳跃、连招、体力或敌人闪避 AI。专用双人处决演出仍未制作。

## 试玩顺序

打开动画场景并 Play。先 F1 切 Dummy：WASD 检查四向，空格检查后撤与斜向，短按／长按左键检查斩击与突刺，蓄力中用右键或空格取消；再切 Rhythm 检查垫步无敌和弹反，最后 Duel 检查原完整攻守循环。R 重开，F2 显示完整调试信息。
