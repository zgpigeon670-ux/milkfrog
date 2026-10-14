# 消耗品快捷装备与攻击增益

2026-10-13，Unity 6000.6.2f1。本地分支 `codex/quick-consumables`，基于 `aead761` 与已有 Tab／BOSS 镜头工作区。未推送或合并 GitHub，保留原有修改；`milkfrog.slnx` 和已有 `Standalone-Startup.log.meta` 未纳入本次实现。

## 已实现

Tab 打开背包，选择消耗品后“装备到 Q”，Q 即时使用；页脚可卸下，任务钥匙无法装备。装备本身不消耗，数量为零仍保留选择，不自动换药；补充同类物品后可继续使用。右下 HUD 显示图标、名称、数量与按键，攻击增益独立显示倍率、剩余秒数及进度条。

| 配置 | 默认效果 | 一次性拾取 |
|---|---|---|
| `healing-medicine` 疗伤药 | 恢复 40 生命，上限截断 | 保留原位置与 ×3 |
| `calming-pill` 定心丸 | 降低 40 架势，下限零 | 保留原位置与 ×3 |
| `attack-tonic` 战意散 | 生命伤害 ×1.25，20 秒 | `(3, 0.02, -33)`，×3 |

`Settings/Items` 的 `ItemDefinition` 提供效果类型、恢复量、攻击倍率和持续时间；`Prefabs/MVP/pickup-attack-tonic.prefab` 提供拾取数量。无效数值、恢复与攻击混配会被配置检查拒绝。旧资产默认恢复类，不覆盖已有恢复数值。重复使用战意散消耗一份、刷新完整时长，倍率不叠加。

背包与 Q 共用 `MvpWorld.TryUseItem`，分别检查入口资格。只有 Neutral／Guard 可使用，Q 还要求运行中且无顿帧；按键不缓存，长按不连用，关闭菜单后须松开再按。该帧开始时的状态决定 Q 资格，防御取消蓄力或后摇不会让同帧用药通过。闪避优先于 Q；成功 Q 拦截同帧攻击，继续保持防御与原弹反窗口。

所有操作先写盘再生效；失败时数量、装备与恢复／增益均保持原状。战斗中更新最近安全档，保留安全位置、生命和架势；安全探索使用恢复药时保存恢复后的资源。既有原子写入与备份恢复继续使用。v5 在 `InventoryData` 增加 `equippedItemId`；v1–v4 沿原路径迁移，保留数量、世界与任务记录，快捷槽默认空，不赠送消耗品。死亡、休息、传送、奖励、通关与重新载入保留剩余数量及装备。

`CombatCore` 的药品倍率只缩放生命伤害，最终为基础伤害 × 成长倍率 × 药品倍率；架势、格挡及弹反奖励保持原逻辑。攻击提交时捕获快照，增益结束不修改已提交攻击。计时使用战斗模拟时间，暂停、背包、顿帧时冻结；死亡、角色重置、休息、传送、载入及通关重置时清除。恢复药、换药或卸下不会清除正在生效的增益，增益本身不入存档。

`UI/Inventory/InventoryView.prefab` 增加并排使用／装备按钮、网格 Q 标记和独立卸下按钮，保留鼠标与方向键／Enter 导航。动作禁用按钮文字缩短为“动作中禁用”。修复切换分辨率后旧 UGUI Text 缓存低分辨率字形的问题，缩放变化时重新生成字形。升级工具增量补建配置、Prefab、场景对象与引用；重复运行验证场景字节不变，不重建关卡。

## 自动验证

| 检查 | 基线 | 最终结果 |
|---|---|---|
| 完整 EditMode | 109/109 | **130/130 通过**，新增 21 项 |
| 完整 PlayMode | 38/111，73 失败 | **54/127 通过**，73 失败 |
| 新快捷消耗品 PlayMode | — | **16/16 通过** |
| 失败名单对照 | 73 个已有失败 | 差集为空，新增失败 0 |
| 自动渲染 | — | 640×360、1280×720、1920×1080，另含 4 种状态截图 |
| Windows Development 构建 | — | 成功；255 个源码／配置文件与副本一致，打包程序集 SHA-256 与编译结果一致，见 `Verification.json` |

EditMode 覆盖：恢复边界与无效使用、健康伤害与成长组合、蓄力／连击快照、到期不追溯、刷新不叠加、30／60／120 Hz 计时、恢复与清除、参数检查、装备与耗尽选择、v1–v4 迁移与重复加载、备份恢复及升级幂等性。既有恢复与数量边界用例继续执行。

PlayMode 覆盖：背包装备／卸下与键盘提交、真实 Q／Tab InputAction、长按与菜单隔离、疗伤药／定心丸有效与无效使用、战意散刷新及更换装备、HUD 立即更新、缺失配置与 ×0、全部禁用动作状态、同帧优先级、顿帧不缓存、暂停计时、保存失败回滚、新地面拾取失败／成功／一次性记录、战斗安全档资源保留、死亡快照恢复、休息、传送及实际重载关卡后的数量／装备一致性。

原 XML 在本目录：`BaselineEditMode.xml`、`BaselinePlayMode.xml`、`EditMode.xml`、`PlayMode.xml`、`QuickPlayMode.xml`。完整日志在 `Builds/QuickConsumablesValidation`。

## 既有问题与证据边界

71 项原 MVP 用例仍因 `MilkfrogSlash`、`MilkfrogThrust`、`MilkfrogFollowup` 的旧刀刃轨迹与当前动画不匹配而在初始化记录错误。另有 `CombatFeelSceneTests` 和 `PhaseThreeTests` 各一个原训练场命中失败；完整回归没有修改这些夹具，73 项失败名单与 Tab／BOSS 镜头基线完全一致。

新增关卡夹具仅用 `LogAssert.Expect` 条件登记上述三个准确的既有错误，重载关卡时同样登记；没有全局忽略日志，没有在生产代码中隐藏错误。16 项通过证明本次道具操作；伤害加成由战斗核心的真实 `TryHit` 与攻击快照验证。按已确认的范围，没有修复旧角色攻击轨迹，因此不能声称整关卡战斗已通过验收。

隔离验证项目为 `Builds/TabBossCameraValidation/Project`，复用暖缓存并同步本次源码。没有启动原项目编辑器，没有操作真实玩家存档，没有 Computer Use。测试使用独立临时存档，失败注入只阻断测试自己的目录。

## 画面检查

| 图片 | 检查 |
|---|---|
| `Screenshots/00.png`、`01.png`、`02.png` | 三档分辨率背包网格、中文、使用／装备、Q 标记、卸下与返回，无重叠 |
| `Screenshots/03.png`、`04.png`、`05.png` | 三档分辨率 HUD 图标、数量、Q 与独立增益倒计时，未覆盖生命／架势条和底部提示 |
| `Screenshots/06.png` | 未装备，提示 Tab 装备；独立增益继续显示 |
| `Screenshots/07.png` | 装备耗尽保留 ×0，禁用表现 |
| `Screenshots/08.png` | 配置缺失明确提示，无自动换药 |
| `Screenshots/09.png` | 动作中打开背包，使用禁用，文字完整 |

截图为固定验证状态的自动渲染，并已逐张查看。它们不代表真人键鼠手感或完整 BOSS 试玩验收。

## 复现与试玩

在隔离副本执行 `Unity.exe -batchmode -nographics -projectPath <副本> -runTests -testPlatform EditMode -testResults <XML> -logFile <日志>`，再以 `PlayMode` 执行完整回归。加 `-testFilter Milkfrog.CombatDemo.Tests.QuickConsumableFlowTests` 可运行道具定向测试。完整 PlayMode 因上述旧问题退出码为 2，判断结果须同时读取 XML 与失败名单。

渲染：设置 `MILKFROG_QUICK_EVIDENCE` 为图片输出目录，执行 `-batchmode -projectPath <副本> -executeMethod Milkfrog.CombatDemo.Editor.QuickConsumableCapture.Run -logFile <日志>`，不要加 `-quit` 或 `-nographics`。构建：设置 `MILKFROG_BUILD_OUTPUT` 为完整 exe 路径，执行 `-batchmode -quit -projectPath <副本> -executeMethod Milkfrog.CombatDemo.Editor.MvpInventoryUpgrade.BuildPlayer -logFile <日志>`。仅在副本运行这些编辑器升级／渲染命令。

交付程序：`Builds/MilkfrogMVP-QuickConsumables/MilkfrogMVP.exe`；保留同目录的 Data、UnityPlayer.dll、MonoBleedingEdge 和字体许可证。可附加 `--mvp-save-dir <独立目录>` 隔离试玩存档。

真人试玩清单：

1. 出生点分别拾取三种药；Tab 打开背包，用鼠标和方向键／Enter 装备与卸下，确认任务钥匙不能装备。
2. 装备疗伤药，受伤后 Q 一次恢复 40；长按不重复、满血不消耗。装备定心丸，产生架势后 Q 降低 40，零架势不消耗。
3. 装备战意散，Q 后显示 +25%／20 秒；稍后再用刷新到 20 秒。换到恢复药或卸下，增益仍持续；用完显示 ×0，不换药。
4. 攻击、蓄力、闪避及受击中按 Q，动作结束不补用。Q＋闪避应闪避；可行动时 Q＋攻击应只用药。菜单内长按 Q，关闭后需松开再按。
5. 暂停与背包中倒计时冻结；休息、死亡、传送或重进后增益消失，数量与装备保留。重复到地面位置不再获得道具。
6. 检查各分辨率与窗口切换，确认中文清晰、网格滚动和详情按钮可操作。BOSS 期间镜头仍能自由转动，背包暂停镜头；旧攻击判定问题仍单独待修。

真人键鼠与完整流程验收由用户完成。
