# 第一阶段验证记录（历史基线）

日期：2026-09-22。此文件保留第一阶段原始结果；升级后的当前结果见 [第二阶段报告](Validation/Animated/Report.md)。

## 环境与方式

- Unity 6000.6.2f1，项目现有 URP 17.6.0、Input System 1.20.0、Unity Test Framework 1.8.0。
- 为保留用户正在打开的 Unity 项目，使用 `C:\Users\pigeon\AppData\Local\Temp\MilkfrogCombatValidation` 独立副本编译、生成场景并执行测试。
- 交付场景、脚本和 `.meta` 来自该验证副本；同步后检查代码及场景文件 SHA-256 一致性。
- 没有关闭用户原编辑器、替换 SampleScene、修改项目输入/渲染设置或增加第三方依赖。

## 已通过

| 检查 | 结果 | 证据 |
|---|---|---|
| 战斗核心 Unity 编译 | 通过，批处理退出码 0 | 验证副本 `stage-core.log` |
| 全部运行时、编辑器和测试程序集编译 | 通过，无 C# 编译错误 | 场景生成及后续 Test Runner 成功执行 |
| 独立场景生成 | 通过 | 验证副本 `scene-final.log`，交付 `Scenes/CombatDemo.unity` |
| EditMode 核心规则测试 | 20 / 20 通过 | [原始 XML](Validation/EditMode-results.xml) |
| 最终 PlayMode 场景测试 | 7 / 7 通过 | [原始 XML](Validation/PlayMode-results.xml) |
| GPU 运行画面 | 已进入 Play Mode，等待 Shader 编译完成后截图 | [运行截图](Validation/CombatDemo.png) |

EditMode 覆盖：非法请求、攻击阶段及大时间步、命中去重、普通格挡、前后方向、弹反窗口前/中/后边界、持续按住不刷新、攻击中断、架势崩溃优先级、死亡优先级、崩溃不续期及恢复、架势恢复条件、处决限制和资源重置。

最终 7 项 PlayMode 测试：

1. 完整攻守交换 → 敌人崩溃 → 处决 → 连续五次重置。
2. 实际 Physics 检测对多个 Collider 去重；被打断的敌人攻击无残留伤害。
3. 玩家朝敌人移动受到 CharacterController 碰撞阻挡，保持在地面上，重置还原位置。
4. 提前防御是 Block、晚按防御是 Deflect、不防御可导致玩家死亡。
5. 模式切换清除待执行反击与计数。
6. 持续格挡导致玩家架势崩溃、禁止行动、随后恢复；敌人不执行处决。
7. 新 Input System 的 WASD、左右键、R、F1 绑定，以及按下/持续按住/释放语义。

## 测试期间修正

- 批处理启动时的未命名空场景不允许 Additive 新建；生成器仅在独立批处理副本使用 Single，交互式编辑器仍采用 Additive 并保留已有场景。
- 无焦点的批处理环境会过滤键鼠输入；输入测试暂时调整焦点策略并在 finally 中还原，不修改正式项目输入设置。
- 捕获画面时等待 Shader 编译完成，避免把青色编译占位画面当作正常渲染。
- 固定镜头增加侧偏、降低角色标签，并为 HUD 提供深色背景，减少信息遮挡；调整后 7 项 PlayMode 测试再次通过。
- 截图批处理启动时出现一次 `UnityEditor.Search.SearchDatabase` 索引异常，调用栈位于编辑器搜索初始化，未涉及 Demo 代码；截图正常生成，27 项测试的结果不受该截图会话影响。没有为此修改原项目的搜索设置。

## 明确未验证或未实现

- **未进行真人键鼠手感验收。** PlayMode 行为测试通过的是程序驱动的场景模拟；键鼠 ActionMap 使用虚拟设备单独验证。
- **未生成独立 Windows 可执行程序。** 当前交付目标为 Unity Editor 中打开场景后 Play。
- 不包含正式模型、动画、声音、特效、Root Motion、闪避、连招、网络、自由镜头或复杂 AI。
- 第一版同一模拟子步先推进玩家、再推进敌人，不处理严格同时命中的双向裁决；规则与调试优先于完整动作游戏手感。

推荐下一步由使用者在 Game View 中按 README 的三个模式各操作一轮，重点评价攻击距离、前摇可读性和弹反窗口，而不是重复静态代码检查。

