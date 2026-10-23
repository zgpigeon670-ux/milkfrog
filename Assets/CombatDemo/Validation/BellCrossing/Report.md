# 沉钟渡 · 交付与验证记录

日期：2026-10-19；Unity 6000.6.2f1。本记录区分新关卡检查、全工程回归、Windows Player 与人工游玩。

## 交付

- [独立 Unity 场景](../../Scenes/BellCrossing.unity)：16 个可编辑空间、17 条连接，7 名普通守卫与 1 名守钟者。
- [Windows 试玩入口](../../../../Builds/Milkfrog-BellCrossing/Milkfrog-BellCrossing.exe)：保留完整同目录。最终构建 Succeeded，构建报告大小 201,746,750 字节。
- [设计、路线与操作说明](../../../../Docs/BellCrossing/README.md)。
- 独立会话包含双线取印、双印北门、西线绞盘捷径、可选供药小祠、留火恢复与自动复活、最终封门战、主动敲钟与结局。

## 新关卡与构建结果

| 检查 | 实际结果 | 证据 |
|---|---|---|
| 新关卡 PlayMode | **13 / 13，通过** | [FocusedTests.xml](FocusedTests.xml) |
| 全工程 EditMode | **166 / 166，通过** | [EditMode.xml](EditMode.xml) |
| Windows 构建 | **Succeeded** | [build.json](build.json) |
| Windows Player | **通过，退出码 0** | [windows-smoke.json](windows-smoke.json) |
| 既有 20 个已修改文件保护 | **20 个 SHA-256 均与开始时一致** | [source-manifest.json](source-manifest.json) |
| 人工键鼠手感与完整通关 | **尚待人工试玩** | 不以自动化结果替代 |

新关卡检查覆盖：真实 CharacterController 双向走过全部 17 条连接；跑、跳、垫步无法越过未开启北门；两枚钟印及各自守卫清场条件；绞盘只从西侧开启；留火点与死亡进度；真实 Update 驱动的死亡/跌落自动复活；封门战开始、失败重置和胜利开门；主动敲钟与完全重开；暂停/地图输入清理及补药阻挡；可选供药的一次性领取；敌人返回所属房间；玩家与普通敌人真实攻击双方造成伤害；独立场景中不存在 MvpWorld 或 GameFlowController。

首次通行检查发现灰烬印台座占据中心直行路线；将钟印与供药移到侧龛后，全部连接的真实角色通行检查通过。

## 全工程 PlayMode 未全部通过

保留两次原始结果，不把新关卡检查通过写成整个工程全绿：

- [首轮 PlayMode-first.xml](PlayMode-first.xml)：**195 / 196**。既有 `BullTrialTests.MainlinePauseEntryAndReturnPreserveLivePositionInventoryAndSelectedSlot` 期望架势落在 16–17，实际约 15.9299。
- [复测 PlayMode.xml](PlayMode.xml)：**195 / 196**。上述黄牛测试通过，但既有 `QuickConsumableFlowTests.ReloadingTheLevelKeepsSavedSlotAndQuantityWithoutTheTransientBuff` 在准备道具时报告 `Could not write save`。
- 两轮中的本关 13 项均通过。失败位于既有试炼/道具模块，本次没有改动这些实现与断言；当前证据不足以确定上述失败的根因。

## 画面核对

以下为 Unity Editor 内的实际 GameView 截图，采用脚本摆放镜头和设置关卡阶段，**不是人工通关录像**。最终标题裁切已修正，中文目标、交互提示、地图和结局面板可读。

| 截图 | 内容 |
|---|---|
| [01](Screenshots/01.png) | 渡口出发、操作与目标 |
| [02](Screenshots/02.png) | 整体俯视几何 |
| [03](Screenshots/03.png) | 留火院与休息交互 |
| [04](Screenshots/04.png) | 西侧钟印侧龛 |
| [05](Screenshots/05.png) | 守钟者、封门与 Boss 界面 |
| [06](Screenshots/06.png) | 敲钟结局；其中 00:00 来自摆拍状态 |
| [07](Screenshots/07.png) | 路线、空间名称、未开启门与玩家位置 |

Windows 包以 `-bell-smoke` 隐藏启动，实际初始化图形设备，通过 URP 渲染请求检查 [角色相机](Windows/player.png) 与 [俯视相机](Windows/overview.png)，并验证角色位移、8 个敌人的初始化和脚本设置胜利后的取印/绞盘/敲钟交互。该离屏检查不包含 GUI；GUI 来自上面的 Editor 截图。

关卡状态检查中的部分胜利条件通过将敌人生命设为零来模拟；真实攻击伤害另有 PlayMode 检查。这些证据不能表述为玩家无作弊击败所有敌人并通关。

## 保存、范围与复现

本关正常运行仅使用内存进度，不创建主线世界、存档服务或磁盘存档。两条取印路线可以互换顺序；退出/重开清空本关进度。玩家移动、跳跃、垫步及攻击规则复用当前项目；最终 Boss 复用 Guardian 配置。场景是可玩灰盒，没有新增角色美术、配音或跨房间寻路。10–15 分钟是首次游玩的设计目标，尚非实测统计。

源码、复用依赖与最终产物哈希见 [source-manifest.json](source-manifest.json)。开始时已有的 20 个已修改文件全部保持相同哈希，主线构建列表也在其中；新增关卡通过显式单场景列表构建。

关闭同项目编辑器后，在工程根目录执行 `Tools/Validate-BellCrossing.ps1`，分别选择 `Create`、`FocusedTests`、`Capture`、`Build`、`Smoke`。`All` 额外运行全工程回归，遇到既有模块测试失败会停止。批处理将自身 TEMP/TMP 指向工程的 `.utmp/BellCrossing`，调用结束后恢复原环境变量；不写系统临时目录配置。

日志在 `Builds/BellCrossingValidation/`。截图属于摆拍证据，Windows 离屏渲染属于运行证据，测试 XML 属于自动化证据；完整键鼠体验与难度验收留待人工试玩。
