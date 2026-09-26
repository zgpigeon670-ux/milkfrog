# 通用交互、主线任务与世界状态验证

日期：2026-09-26。Unity 6000.6.2f1。新增功能在独立验证副本测试；没有使用 Computer Use、没有真人 PIE 或独立构建键鼠试玩，没有使用正式玩家存档。

## 实现

- 现有 MVP 场景增量加入守门钥匙和封印门、任务配置及引用。E 统一选择屏幕内、无遮挡、2.5m 内且高度差不超过 1.5m 的目标；只在安全脱战 Neutral 状态执行。选择依次比较画面中心距离、世界距离、稳定 ID，并在执行前重新验证当前提示对象。
- 主线为点亮守门篝火、取得钥匙、开门、击败 BOSS。钥匙可提前取得；门未开时从任何方向接近都不能开始 BOSS 战。任务不另发经验。HUD 显示当前目标，Esc → 任务 / 属性 → 任务进度查看全部步骤。
- 世界事实驱动任务计算；拾取、开门和篝火先写盘再生效。存档失败保留原状态；BOSS 结算失败保留运行时通关与奖励，通关页可重试保存，不重复发奖。恢复输入时清除未完成攻击请求。
- v3 保存拾取物、关键物品、门及任务进度。死亡、休息、快速传送和战斗经验写盘均保留世界进度。v1 / v2 迁移保留原有成长；旧通关档补齐守门篝火、钥匙、门和任务事实，不新增经验。v1 原有一次性 200 经验补偿规则保留。主档损坏可恢复备份。
- 编辑器检查重复／缺失稳定 ID、缺失任务目标；门的“需要钥匙”字段不算存在可拾取钥匙。重复执行升级前后场景 SHA-256 相同。场景仅新增两处 Prefab 实例与任务引用，已有对象布局未重建。
- 发现危突刺引用的共享轨迹与当前动画阶段标记不匹配，独立重烘焙 BossPerilous / EnemyPerilous 派生轨迹及引用，未改动动画片段或招式数值。

## 本轮验证

| 项目 | 实测结果 |
|---|---|
| 原始 Edit Mode 基线 | 58/65 通过，7 项蓄力测试失败。见 BaselineEdit.xml。 |
| 原始 Play Mode 基线 | 7/78 通过，71 项失败；大量用例在失效刀轨的初始化错误处终止。见 BaselinePlay.xml。 |
| 新增 Edit Mode | 10/10 通过：乱序任务、事件幂等、事实校验、v2 迁移、v3 损坏字段、世界备份和编辑器 ID 检查。 |
| 新增 Play Mode | 8/8 通过：完整四步流程、门禁、失败回滚、交互筛选、状态限制、经验写盘、BOSS 保存重试、跨首页读档。 |
| 完整 Edit Mode 回归 | 68/75 通过，仍有 7 项现有蓄力测试失败。见 EditMode.xml。 |
| 完整 Play Mode 回归 | 76/86 通过，仍有 10 项现有战斗测试失败。见 PlayMode.xml。 |
| 自动渲染 | 任务 HUD、交互提示和任务页在 640×360、1280×720、1280×800 生成截图；逐张检查，无裁切，640×360 文字偏小。 |
| Unity 编译与 Windows 构建 | 最终批处理构建成功，退出码 0；入口为首页。输出 `Builds/MilkfrogMVP-Phase9/MilkfrogMVP.exe`，附带 Noto Sans SC 许可。 |
| 源码一致性 | 当前项目与最终验证副本的全部 CombatDemo C#、程序集定义、场景、配置和 Prefab 文件哈希一致；代码与说明的 diff 空白检查通过。 |

构建日志：`Builds/QuestValidation/FinalBuild.log`。最终游戏程序集 `MilkfrogMVP_Data/Managed/Milkfrog.CombatDemo.dll` 的 SHA-256 为 `6108CC713AA626B25C4E20320E91AF60DB0D98FBD58759CCB2A59C9B8D8B1B24`。请保留整个 `MilkfrogMVP-Phase9` 文件夹运行，不能只复制 exe。此构建已生成，尚未进行真人独立程序试玩。

**新增 18 项全部通过，但完整回归没有全绿。** 旧 Play Mode 基线大量用例被刀轨错误遮蔽，因此不能将本轮剩余 10 项行为失败声称为旧版本已经通过。现有蓄力输入、连击／定向、顿帧输入和训练场禁用 CharacterController 后继续移动等问题仍需单独修复；本轮没有为清空失败而删除测试或更改战斗动作规则。

初次复用旧临时副本遇到 Package Manager 缓存问题，那些启动失败不计入测试结果；有效结果来自 `Builds/QuestValidation`。截图期间 Unity Search 索引器出现非游戏逻辑异常，但截图脚本完成所有阶段。自动化模拟和固定状态截图不等于真人战斗手感验收。

## 待真人验收

1. 从首页开始新游戏，脱战后到守门篝火按 E，确认主线由 1/4 变为 2/4。
2. 去篝火东侧拿钥匙，开门前尝试从门侧绕到 BOSS，确认仍处于封印状态；开门后才触发战斗。
3. 拾取后和开门后分别返回首页再继续，确认物品不再出现、门保持开启、目标保持一致。
4. 休息、传送、死亡重试后再次检查世界进度；击败 BOSS，确认只有原有 200 经验并显示主线完成。
5. 检查最大化与小窗口下的文字、E 提示目标和菜单键鼠焦点。若旧战斗问题影响正常击败 BOSS，先修复报告中的战斗回归，再执行真人完整通关验收。

## 未通过用例明细

### Edit Mode 未通过用例

- `ChargeCoreTests.FullChargeHoldsAndSnapshotsDamageInsteadOfReadingMutableDefinition`：Expected: 78 /   But was:  100.0f
- `ChargeCoreTests.GuardAndDodgeCancelChargeButNotAReleasedSlash(False)`：Expected: AttackStartup /   But was:  AttackPrepare
- `ChargeCoreTests.HoldingPastTheWindupChargesWithoutReleasingEarly(0.25f,Light,False)`：Expected: False /   But was:  True
- `ChargeCoreTests.ReleasedThrustCannotBeCancelled`：Expected: 22 +/- 0.001d /   But was:  18.5161285f
- `ChargeCoreTests.ShortTapStartsTheSlashImmediately`：Expected: AttackStartup /   But was:  AttackPrepare
- `ChargeCoreTests.ThrustCanBeBlockedOrDeflected(False)`：Expected: Block /   But was:  Ignore
- `ChargeCoreTests.ThrustCanBeBlockedOrDeflected(True)`：Expected: Deflect /   But was:  Ignore

### Play Mode 未通过用例

- `CombatFeelSceneTests.PerilousDeflectFailsAndDodgeInvulnerabilityAvoidsIt`：Unhandled log message: '[Error] CharacterController.Move called on inactive controller'. Use UnityEngine.TestTools.LogAssert.Expect
- `CombatFeelSceneTests.RecoveryGuardCancelAndFollowupAreAvailable`：Unhandled log message: '[Error] CharacterController.Move called on inactive controller'. Use UnityEngine.TestTools.LogAssert.Expect
- `MvpFlowTests.ChargeReleaseReaimsAndKeepsBladeForwardAligned`：Expected: True /   But was:  False
- `MvpFlowTests.FollowupAndExecutionUseSelectedDirection`：Expected: Followup /   But was:  Light
- `MvpFlowTests.MiddleMouseBindingAndFreezeReleaseDoNotQueueAttack`：Expected: 0 /   But was:  1
- `MvpFlowTests.MobLockDoesNotSteerMovementOrCameraButAimsCommittedAttack`：Expected: True /   But was:  False
- `PhaseThreeTests.SnapshotPriorityFreezeReleaseFocusAndResetDoNotQueueAttacks`：Expected: 0 /   But was:  1
- `PhaseThreeTests.TapSlashesHoldThrustsAndShiftDodgesWhileSpaceJumps`：Unhandled log message: '[Error] CharacterController.Move called on inactive controller'. Use UnityEngine.TestTools.LogAssert.Expect
- `PhaseThreeTests.ThrustFollowsItsOwnBladeAndHitsOnceAtAllFrameRates`：Expected: 1 +/- 0.001d /   But was:  0.0f
- `PhaseThreeTests.ThrustVisualMatchesBakedTraceAndResetClearsTrail`：Expected: AttackActive /   But was:  Neutral
