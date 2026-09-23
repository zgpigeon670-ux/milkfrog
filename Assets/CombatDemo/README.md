# Milkfrog Combat Demo

Unity 6000.6.2f1 / URP / Input System。所有新增资产位于 `Assets/CombatDemo`，没有修改原始 SampleScene、项目输入设置或构建场景列表。

已提供 [Windows 可执行版本](../../Builds/CombatDemo/CombatDemo.exe)，也可直接在 Unity 中运行以下场景。完整画面的独立程序性能对照仍待验证，详情见报告。

## 开始

1. 等待 Unity 导入脚本并完成编译。
2. 打开 **`Assets/CombatDemo/Scenes/CombatDemo_Animated.unity`**，点击 Play，再点击 Game View 使其获得输入焦点。
3. WASD 相对镜头移动；短按左键斩击，长按左键蓄力、松开突刺；空格＋方向垫步，无方向时后撤。右键按住格挡，命中前 0.15 秒内新按下可弹反。
4. R 重置本轮；F1 循环切换 Duel（完整交战）、Dummy（静止靶子）、Rhythm（固定节奏攻击）。模式切换会重置本轮。F2 开关完整调试面板。
5. 敌人架势崩溃后，等待自己恢复可操作状态，在正面 2m 内左键处决。

Game View 的英文文字避免依赖额外中文字体。正式战斗 UI 常驻：敌人生命在左上、敌人架势在上方中央；玩家生命在左下、玩家架势在下方中央。赤红生命条带延迟伤害尾条，金色架势条由中央向两边增长。F2 只开关调试面板、状态标签和地面预警框，不隐藏正式血条。

动画场景中玩家为青色、敌人为红色；镜头以玩家背后越肩取景，随玩家朝向平滑旋转并避障。F2 调试模式的地面矩形用金色/红色/棕色区分前摇/有效期/后摇。原胶囊场景 `CombatDemo.unity` 保留为回归基线，其角色本体按状态变色。选中角色，在 Scene View 开启 Gizmos 可检查刀刃轨迹和防御角度。

## 推荐测试流程

- **Dummy：** 左键打一次应只扣 10 HP；前摇、有效期、后摇期间重复点击无效。走到范围外挥空，再靠近攻击。角色不能相互穿过。
- **Rhythm：** 提前按住右键是 Block；放开后在敌人前摇将结束时按下是 Deflect。弹反窗口结束后持续按住不会刷新窗口。一直不防御会死亡；一直格挡最终可架势崩溃。
- **Duel：** 玩家攻击两次，各等后摇结束；第三次攻击被敌人弹反。敌人短延迟反击，玩家恢复后，在反击命中前重新按下右键弹反。稍等敌人硬直结束再攻击两次，使敌人架势崩溃，之后左键处决。
- 战斗结束按 R；连续重开或切换模式不应有残留攻击、血量、架势、计时器或格挡次数。

## 规则和状态转换

| 来源 | 合法操作或事件 | 结果 |
|---|---|---|
| Neutral / Guard | 玩家左键按下 | AttackPrepare；可行动且满足处决条件时立即处决 |
| AttackPrepare | 0.18 秒前松开 / 按住达到阈值 | 普通斩击 AttackStartup，抵扣已用前摇 / Charging |
| Charging | 松开左键 | 蓄力突刺 AttackStartup，固定攻击方向 |
| Neutral / Guard | AI 或离散攻击请求 | AttackStartup，固定攻击方向，退出防御 |
| Neutral / Guard / AttackPrepare / Charging | 空格 | Dodge，锁定方向；0.38 秒后恢复 |
| AttackPrepare / Charging | 右键 | 取消并进入 Guard；新按下可弹反 |
| Neutral / Guard | 防御按住 / 松开 | Guard / Neutral；合法新按下开启弹反窗口 |
| AttackStartup | 前摇结束 | AttackActive |
| AttackActive | 有效期结束 | AttackRecovery |
| AttackRecovery / HitStun / DeflectedStun | 计时结束 | 按住防御则 Guard，否则 Neutral；不补发弹反窗口 |
| 非死亡状态 | 普通命中 | 扣血及架势，通常进入 HitStun |
| AttackActive | 被弹反 | 攻击立即中断，增加自身架势，通常进入 DeflectedStun |
| 非死亡状态 | 架势达到上限 | PostureBroken；持续期间再受击不会续期 |
| PostureBroken | 2 秒结束 | 架势归零，恢复 Guard 或 Neutral |
| 任意可受击状态 | HP 归零 | Dead，优先于崩溃和其他硬直 |
| 玩家可行动、敌人崩溃 | 正面且距离不超过 2m 的攻击输入 | 独立处决检查，敌人 Dead |
| Dead | 重置 | 初始资源、位置、Neutral |

准备／蓄力可以用右键或空格取消；释放后的斩击和突刺不能主动取消，受击与弹反仍可中断。战斗没有反伤或霸体。同一攻击每个 CombatCore 最多命中一次，多个子 Collider 不会重复扣血。双方同一时间发起攻击时，本 Demo 固定先推进玩家，后推进敌人；不实现联网回滚或双向同时命中裁决。

## 参数与代码入口

动画场景的生命、架势恢复、防御、闪避和 AI 参数在 `Settings/AnimatedCombat.asset`；斩击与突刺的时序和伤害分别在 `Settings/PlayerSlash.asset`、`EnemySlash.asset`、`PlayerThrust.asset`。胶囊场景仍使用 `Settings/DefaultCombat.asset`。单次攻击使用开始时的规则快照；退出 Play 后编辑配置并重新进入，避免运行中修改共享资产。

默认：生命/最大架势 100；Hit 扣血 10、架势 +10；Block 架势 +20；Deflect 攻击者架势 +30。攻击前摇/有效期/后摇为玩家 0.25/0.10/0.35 秒、敌人 0.45/0.10/0.45 秒。防御总角度 120°，弹反窗口 0.15 秒，受击/被弹反硬直 0.30/0.20 秒，崩溃 2 秒。有效交互刷新双方恢复延迟，2 秒无交互后仅在 Neutral/Guard 每秒恢复 15 架势。

参数应保持合理非负值，生命和最大架势大于零。动画场景使用烘焙刀刃轨迹的扫掠胶囊检测，核心 2m 范围仅作为上限，不代表挥剑必定命中 2m 内所有目标。当前 AI 接敌距离为 1.25m；命中还要求真实刀刃接触、无墙体遮挡且高度差不超过 0.9m。处决使用水平距离、正面 120°、高度和视线检查。胶囊基线仍使用前方检测盒。

- `CombatCore`：唯一状态所有者；`RequestAttack`、`SetGuard`、`Tick`、`TryHit`、`TryDeathblow`、`Reset` 是行为入口。`ResolveHit` 和中断优先级集中在内部。
- `CombatActor`：CharacterController、朝向、Physics 范围检测和每个角色实例。
- `EnemyBrain`：只提交意图，使用核心事件维护格挡次数、主动攻击等待和待执行反击；不重复管理攻击阶段或硬直。
- `CombatDemoSession`：输入与模拟协调、胜负、模式和完整重置。使用最大 1/120 秒子步推进，避免低帧率漏过攻击阶段。
- `DemoInput`：独立的新 Input System ActionMap，不修改项目已有 Input Actions。
- `CombatActorView`、`CombatHud`、`CombatDebugHud`、`DemoCamera`：反馈、常驻战斗 UI、调试和镜头，不拥有战斗规则。

规则层不依赖 Animator。动画场景通过 Humanoid Animator + 手动 Playables 采样表现；伤害只由战斗有效期决定，动画回调不拥有状态。

## 自动化验证

Unity Test Runner 中执行 `Milkfrog.CombatDemo.EditTests` 和 `Milkfrog.CombatDemo.PlayTests`。PlayMode 测试会加载独立 Demo 场景并注入输入意图和模拟时间；新 Input System 的键鼠绑定另用虚拟设备检查。

命令行运行应使用没有被其他 Unity 实例打开的项目副本：

```powershell
& 'E:\UNITY\EDITOR\6000.6.2f1\Editor\Unity.exe' -batchmode -nographics -projectPath '<验证项目>' -runTests -testPlatform EditMode -testResults '<EditMode.xml>' -logFile '<EditMode.log>'
& 'E:\UNITY\EDITOR\6000.6.2f1\Editor\Unity.exe' -batchmode -projectPath '<验证项目>' -runTests -testPlatform PlayMode -testResults '<PlayMode.xml>' -logFile '<PlayMode.log>'
```

`-runTests` 不附加 `-quit`，由 Test Runner 完成后退出。测试结果见 `Validation.md`；自动化 PlayMode 验证不等于人工手感验收。

场景生成工具为 `Tools > Combat Demo > Create Missing Demo Scene`，已有场景不会被覆盖。在交互式编辑器使用生成工具前，应先保存当前未命名场景。正常使用直接打开已交付场景即可，无需再次生成。


## 第二阶段：动画、反馈与镜头

- 原胶囊场景保持独立；动画场景共用原有核心。必要动作来自 Quaternius CC0 Standard，实际导入 43 个片段，默认使用持剑待机、步行、慢跑、挥剑、受击、死亡。防御/招架/后仰/崩溃采用本项目适配的肌肉曲线姿势。
- `Animations/HumanoidCombat.asset` 配置动作、混合时间和攻击有效期在动画中的起止比例。动画阶段跟随核心 `StateProgress`，双方不同前摇仍使用同一挥剑。Root Motion 关闭，模型根位移锁定，CharacterController 负责移动。
- `Combat Demo` 对象的 `CombatFeedback` 可独立关闭 hitStop、flashes、sound、cameraImpulse，并调整 volume、deflectFreeze。默认弹反顿帧 0.04 秒，连续请求取最大剩余时间，不相加。
- 顿帧使用会话局部时钟；双方战斗与动画暂停，镜头、声音、UI、闪光寿命继续走真实时间。不会修改 `Time.timeScale`。冻结期间攻击和新弹反按下不缓存，仍按住防御只在恢复后进入普通格挡；R 始终可重置。
- 闪光复用 8 组放射状火花；短音效由正弦包络合成。完美弹反使被弹反的攻击者全身泛白 0.20 秒并渐退，普通命中令受击者泛红；格挡只有较小金色碰撞火花。自制 `CombatFighter.shader` 通过 MaterialPropertyBlock 驱动，不逐次实例化材质。重置清理 shader、火花、声音与镜头冲击。
- 镜头使用玩家背后偏右的越肩取景，Inspector 可调整 `offset`、`headingSmooth` 和 `enemyFocusWeight`。保留 SphereCast 避障，镜头与 UI 在顿帧期间继续响应。
- 世界标签只在状态/弹反窗口改变时重写，调试文本最多 10 Hz；F2 控制调试内容。正式血条和架势条由独立 `CombatHud` 绘制，默认可见。
- 表现中断后停止旧攻击拖尾；攻击淡出保留最后采样姿势，避免回零抽动。移动防御使用上身遮罩，双腿继续按方向行走。处决仅组合挥剑和死亡，不是精密双人对位演出。
- 命中位置来自接触 Collider 的最近点。动画场景沿 `SwordBladeTrace.asset` 的 65 个刀刃姿势分段扫掠，覆盖大时间步跨过有效期的情况；不以当前渲染帧的骨骼位置决定规则。改动攻击片段、阶段标记、模型或剑尺寸后，应在验证副本执行 `Milkfrog.CombatDemo.Editor.CombatBladeTraceBaker.UpgradeScene` 重新烘焙，复制生成的轨迹资产与动画场景回来。该命令会将双方摆回默认训练位置；请先保存自行调整的场景。

## 构建、截图和性能复现

以下工具是批处理验证入口，应在单独项目副本运行，不关闭或占用正在编辑的原项目：

- `Milkfrog.CombatDemo.Editor.CombatDemoCapture.RunAnimated`：进入 Play Mode，等待着色器完成，记录背后视角与 UI、攻击有效期、玩家弹反导致敌人闪白、架势崩溃截图。
- `Milkfrog.CombatDemo.Editor.CombatBenchmarkBatch.BuildPlayer`：显式构建 Windows Development Player；场景顺序为动画场景、胶囊场景，不修改原 Build Profile。
- `Milkfrog.CombatDemo.Editor.CombatBenchmarkBatch.Run`：Editor 中运行六次采样，每次 10 秒预热 + 60 秒记录，交替 uncached/cached，三组对照。
- 在构建目录运行 `CombatDemo.exe -combatBenchmark -screen-width 1280 -screen-height 799 -screen-fullscreen 0`：独立程序同样六次采样。输出至工作目录 `Evidence`。

`CombatPerformanceSampler` 采集平均帧时、nearest-rank p95 和 `GC Allocated In Frame`。采样器本身不改帧率、不跨场景常驻。只有明确运行性能工具才临时设置 60 FPS、VSync=0、后台运行并将 AudioListener 音量置零，销毁时还原；Development Player 与 Editor 的数据分开列出。对照仅衡量本次标签/HUD 缓存，不能代表更换模型前后或整个游戏的收益。

最新改动与测试见 [第三阶段验证报告](Validation/Phase3/Report.md)。此前 [视角与判定验证](Validation/CombatFeel/Report.md) 和 [第二阶段验证报告](Validation/Animated/Report.md) 保留为历史记录，旧性能数字不代表当前版本。第三方素材来源见 [许可清单](ThirdParty/SOURCES.md)。

## 第三阶段操作与动画

- 前进沿用原走／跑动画，后退与左右侧移使用 `Animations/Directional` 内独立关键帧动画，不倒放或整体旋转前进动作。方向权重按角色局部实际速度混合，斜向使用相邻方向；步频按脚部实测行程校准。移动防御保留上身防御、下身步行。
- 空格垫步总计 0.38s，前 0.24s 平滑位移最多 1.6m；无敌区间为 `[0.06,0.18)` 秒。方向在开始时锁定，无方向后撤。墙体、敌人会阻挡位移，受击可打断，无连按缓存。
- 左键按下立即抬刀，0.18s 前松开为普通斩击，已经经过的准备时间抵扣 0.25s 前摇。按住超过阈值进入蓄力，0.80s 满蓄后保持，松开才突刺。
- 突刺释放后为 0.12s 前摇、0.12s 有效期、0.42s 后摇；生命伤害 10→22、命中架势 10→28、格挡架势 20→35。可以格挡或弹反，无霸体与自动破防。实际命中使用独立 `ThrustBladeTrace.asset`。
- 准备／蓄力期间不能移动，可以转向敌人；右键或空格取消。释放后方向锁定。受击、失焦、死亡、重置清除未完成蓄力；顿帧中松开左键会取消，不在恢复后补发。
- 同帧输入优先级：重置、闪避、防御、攻击。保留原离散 `SubmitInput` 重载供 AI、历史测试和性能脚本调用，真人键鼠走完整按下／按住／松开输入快照。
- 新动作是本项目制作的可编辑 Humanoid 肌肉关键帧，属于 Demo 适配，不是只狼提取资产或动作捕捉成品。`CombatMotionAuthoring.BuildLocomotion`、`BuildActions` 是验证副本中的显式重建工具；会覆盖本项目生成的动作或攻击默认配置，不应在手工微调后随意运行。
- Game View 预览请用适合窗口的 Scale，Free Aspect 通常使用 1×。放大预览会裁掉边缘，即使运行时 HUD 正常缩放也是如此。

## 本阶段边界

不包含冲刺、跳跃、连招、体力条、技能、装备、存档、网络、自由镜头、切换锁定、敌人闪避 AI、复杂 Boss 或付费资产。自动化验证不等于真人手感验收；动作过渡、音量、镜头距离和姿势仍适合在实际键鼠试玩后微调。正式项目的 Build Profile 保持原样。


