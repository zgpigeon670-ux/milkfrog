# 第二阶段验证报告

日期：2026-09-22。交付场景：`Assets/CombatDemo/Scenes/CombatDemo_Animated.unity`。

## 已完成的实现

低模人形持剑对战、Humanoid / Playables 表现、攻击阶段采样、格挡/弹反/后仰/崩溃适配姿势、死亡与重置同步、池化闪光、自制短音效、局部 0.04 秒弹反顿帧、双方取景与 SphereCast 避障。世界标签缓存，HUD 文本 10 Hz，可用 F2 关闭调试面板。

原始胶囊场景保留，共用战斗核心；Animator 不决定伤害。默认挥剑有效段已按手骨轨迹校准为动画归一化时间 0.25–0.38，映射到原有 0.10 秒有效期，前后阶段分别重映射。命中仍由前方范围检测与核心去重结算。防御姿势经过同骨架肌肉曲线调整，使剑举在胸前。

来源与完整许可见 [SOURCES.md](../../ThirdParty/SOURCES.md)。官方 CC0 FBX、MIT 性能采样改编、Unlicense 导入改编均有单独记录和固定版本。

## 验证证据

| 层次 | 结果 | 证据与边界 |
|---|---|---|
| 代码与资源检查 | 完成 | 仅新增/改动 CombatDemo；固定来源、资源哈希与许可；保留胶囊场景和用户原有修改 |
| Unity 实际编译 | 通过 | Unity 6000.6.2f1 批处理构建、Test Runner 成功运行；不是仅静态检查 |
| Edit Mode | **22 / 22 通过** | [原始 XML](EditMode-results.xml)：原有 20 项 + 2 项局部时钟/动画时序测试 |
| Play Mode | **15 / 15 通过** | [原始 XML](PlayMode-results.xml)：原有 7 项 + 8 项动画、反馈、镜头与帧率回归 |
| Humanoid / 必要动画 | 通过 | Avatar isValid/isHuman；10 个必需动作都有 humanMotion；实际手骨随挥剑移动，死亡后头部落低；[43 个片段导入清单](animation-import.txt) |
| GPU 画面检查 | 完成 | 实际 Play Mode 截图，检查待机、防御、攻击、弹反、失衡；无 T Pose |
| Windows Development Build | 已构建并运行 | 独立进程性能实测结果见下节；与 Editor 数据分别列示 |
| 真人键鼠手感 | **待体验** | 自动化行为与虚拟输入设备测试不能替代人的距离感、节奏感、音量与镜头评价 |

新增 Play Mode 覆盖：

1. Avatar、动作齐全、手骨真实运动、模型根节点无漂移、Root Motion 关闭。
2. 冻结期间拒绝攻击和新弹反窗口，恢复只进入普通格挡，重置清空反馈。
3. 受击打断攻击后无残留伤害和拖尾。
4. 相机 SphereCast 遇测试墙体时保持在墙前。
5. 30 FPS 下完整“两次格挡→敌人弹反→反击→玩家弹反→崩溃→处决→重置”。
6. 同一闭环在 60 FPS 下通过。
7. 同一闭环在 120 FPS 下通过。
8. 顿帧恰好归零后的新防御输入不会被旧输入覆盖；卸载场景后 Time.timeScale 不变。

第 5–7 项是按对应帧步长驱动的 Unity 场景自动化测试，不表示真人分别试玩了三种锁帧模式。

## 运行截图

![待机与防御](CombatDemo_Animated.png)

![有效期与受击](CombatDemo_Animated_1.png)

![敌人弹反与局部顿帧](CombatDemo_Animated_2.png)

![架势崩溃](CombatDemo_Animated_3.png)

## 性能对照方法

- 同一升级场景、同一画质 PC、同一 1280×799 渲染分辨率、同一确定性 60 Hz 战斗脚本。
- 每次预热 10 秒，随后记录 60 秒。依次 uncached/cached，重复三组。Editor 与独立程序分开执行，未并行争用资源。
- uncached 重现标签逐帧/HUD 每次 OnGUI 重建文本；cached 使用状态/窗口变化时更新标签及 10 Hz HUD。模型、动画、反馈、战斗脚本保持一致。
- 测试工具显式锁定 60 FPS、VSync=0、后台运行、AudioListener 音量为零；实际音效处理仍存在。正常场景与采样器本身不改变这些全局设置。
- 平均帧时及 p95 来自 Stopwatch 实际帧间隔；GC 来自 `ProfilerRecorder("GC Allocated In Frame")`。GC 包含宿主和渲染系统，不能全归因于战斗代码。
- 硬件：Ryzen 9 7940H / RTX 4060 Laptop GPU。Editor 采样于 18:39 起，独立程序于 23:32 起；不处于同一时段，不排除其他后台任务的影响。Editor 与独立 Development 构建有不同开销；这不是 Release 构建峰值性能测试。

| 环境 | 模式 | 三次平均帧时 ms | 三次 p95 的平均 ms | 平均 GC 字节/帧 |
|---|---|---:|---:|---:|
| Editor | cached | 16.6680 | 17.2266 | 175125.90 |
| Editor | uncached | 16.6682 | 17.2168 | 177450.54 |
| Standalone | cached | 16.6829 | 17.0035 | 392.57 |
| Standalone | uncached | 16.9727 | 16.9725 | 260.49 |

这里的 p95 汇总是三个独立 p95 的算术平均，不是合并全部帧后的 p95。

**可支持的结论：** Editor 内缓存使平均分配从 177450.54 降至 175125.90 字节/帧，减少 2324.64 字节，约 **1.31%**。平均帧时几乎相同，p95 未改善；60 FPS 上限也掩盖了吞吐差异，因此不能宣称帧率提升。

**独立程序的限制：** 使用隐藏窗口运行（Start-Process -WindowStyle Hidden），系统返回的可见主窗口句柄为 0。cached 的 GC 反而从 260.49 增至 392.57 字节/帧，不能证明优化有效。推测隐藏运行减少了 OnGUI 负载，而缓存的 Update 仍在工作；本轮未对 GUI 回调次数进行独立计数，因此不把这一推测当成已证实根因。其帧时也包含后台调度影响。该结果用于证明独立程序可持续运行完整自动战斗，**可见窗口下完整 GUI/渲染性能对照仍待验证**，不与 Editor 总分配直接比较。

原始数据：[Editor CSV](Performance-Editor.csv)、[Standalone CSV](Performance-Standalone.csv)、[Editor 环境](Performance-Editor.csv.environment.txt)、[Standalone 环境](Performance-Standalone.csv.environment.txt)。程序各执行六个 70 秒区间并自然完成退出，独立程序日志无脚本异常匹配；Development Player 退出时仍输出 Unity 原生分配诊断，不据此宣称整个引擎零泄漏。

## 可执行交付

[Windows Development 程序](../../../../Builds/CombatDemo/CombatDemo.exe) 位于项目 `Builds/CombatDemo`。直接双击使用键鼠；不带 `-combatBenchmark` 参数时不会自动战斗、静音或改测试帧率。必须保留同目录的 Data、MonoBleedingEdge 和 DLL。此目录遵循项目原有 .gitignore，不把构建二进制作为 Unity 资产导入。

[构建与 Editor 采样证据](Editor-build-and-benchmark-evidence.txt)、[独立程序运行证据](Standalone-runtime-evidence.txt)。交付的战斗程序集与实际采样的程序逐字节一致。

## 已修正的问题与已知限制

- 修复场景卸载时，反馈组件访问已经销毁的角色对象。
- 修复完全冻结的帧仍重混动画权重；现在冻结时保持采样姿势。
- 修复冻结结束边界的新防御输入被旧防御意图覆盖。
- 修正防御姿势中手臂朝后、文字面板遮挡世界标签，并校准挥剑有效段。
- 构建过程中出现过一次 Unity IL Post Processor 子进程启动失败；重新启动验证副本后成功完成编译和构建。
- Editor 截图/性能批处理启动时出现 Unity SearchDatabase 索引异常，退出时出现 JobTempAlloc 诊断。保留日志说明，不把宿主问题描述成 Demo 的零警告运行；帧率测试及场景测试结果以 XML 为准。独立程序日志另行检查。
- 已核对交付目录与验证副本的 111 个代码、场景、配置、模型及元数据文件，内容一致，见 `Delivery-checksums.json`。同步了自动生成但不一致的 19 个 `.meta`，避免脚本 GUID 引用丢失。原胶囊场景 SHA-256 保持 `7753B9A6CC9EBF2EBC15648C4B76120FC60E0C50A6174297B356B21F4B7DBFE0`。用户原有 URP.png 修改未动；Git 中当前 QualitySettings 的序列化版本升级和 .slnx 自动程序集列表也未回滚。
- 当前只做近战范围判定，反馈点取躯干中点；没有刀刃级连续碰撞、精密双人处决或正式 VFX。
- 专用防御/招架是简单的姿势适配，音效是合成短音。人的手感、视觉美术、镜头近墙极端夹角仍需实际试玩评价。
- 固定执行顺序仍是玩家先于敌人，不处理联网回滚和严格同时命中的双向裁决。


