# 战斗定向、镜头与 BOSS 招式验证

日期：2026-09-24。Unity 6000.6.2f1 / URP / 新 Input System。没有使用 Computer Use；真人 PIE 与键鼠手感验收由用户执行。

## 本轮结果

- 普通小怪锁定只保存目标并显示标记，不再强制人物或镜头转向；BOSS 战仍自动锁定、持续面向并跟随 BOSS。
- 合法攻击提交时按锁定目标、准星附近的范围内目标、最近的范围内目标、准星水平射线排序。短按、蓄力释放、连击和处决共用该方向；有效期内刀刃轨迹不再追踪，射程与遮挡规则没有放宽。F2 调试 HUD 显示上次索敌来源。
- MVP 玩家已处于 Guard 时，普通可格挡接触结算前自动转向攻击者。未进入 Guard、架势已被打崩或遭遇危突刺时不会凭空格挡。`DodgeOnly` 现真正跳过格挡与弹反；旧训练场 Rhythm 模式的危招也因此改为仅闪避。
- 自由过肩镜头使用独立鼠标轨道、右肩偏移、碰撞收缩和回位阻尼；BOSS 模式取景双方。参考 [Unity 官方 Third Person Follow 文档](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/com.unity.cinemachine/Documentation~/CinemachineThirdPersonFollow.md)，没有新增 Cinemachine 包或复制其源码。
- BOSS 默认快→慢→快→危循环，反击仍使用快刀；生命 750、最大架势 350、速度 3.2m/s、主动攻击等待 0.85s。三种招式使用独立配置，分别为 0.35s/28 伤害、0.72s/36 伤害、0.62s/42 伤害的前摇/生命伤害初值。动画表现层按招式播放相应片段并使用相应刀刃轨迹。危突刺前摇显示红色“危／闪避”，打断、暂停、死亡和读档后清除。
- HUD 中文字体为 Google Fonts 官方 Noto Sans SC，SIL OFL 1.1。来源、固定提交、文件 SHA-256 和许可见 [SOURCES.md](../../ThirdParty/SOURCES.md)；Windows 构建目录另附 `NotoSansSC-OFL.txt`，构建脚本今后会自动复制该许可。

## 实测边界

| 项目 | 本轮结果与证据 |
|---|---|
| 实施前 Edit Mode | 52/52 通过，[原始 XML](Baseline-EditMode.xml) |
| 实施前 Play Mode | 52/52 通过，[原始 XML](Baseline-PlayMode.xml) |
| 最终 Edit Mode | 54/54 通过，[原始 XML](EditMode.xml)；包含危招不可格挡与字体字形检查 |
| 最终 Play Mode | 61/61 通过，[原始 XML](PlayMode.xml)；包含侧向锁定挥刀实际命中、四级索敌、蓄力与连击定向、背后及多敌格挡、BOSS 危招实际接触、循环/动画/警示、墙体避障 |
| Windows 构建 | Unity 构建成功，入口为项目根目录 `Builds/MilkfrogMVP-Phase5/MilkfrogMVP.exe`；EXE SHA-256 `6833A3B986DA4ACF7EF8B009BAE7136A01BE3734071D9DDC57B3DD115830536A` |
| 真人 PIE／独立程序画面与手感 | **待用户验证**；本轮未启动新构建，也未以命令行测试替代人工画面结论 |

旧的 `Builds/MilkfrogMVP` 程序在首次构建尝试时仍有进程占用，Unity 报告 EXE 与多项依赖文件无法覆盖；该目录可能留下部分构建中间结果，不应再作为验收版本。没有关闭该进程；成功的新构建位于独立目录。构建命令支持仅该次进程设置 `MILKFROG_BUILD_OUTPUT` 来指定输出文件。构建完成后，自动生成的 URP/ProjectSettings 差异已恢复，项目 Build Profile 保持原状。

## 建议的最后试玩

1. 从 `Scenes/MainMenu.unity` 进入关卡，或打开新构建。中键锁定小怪后转动鼠标并 WASD 移动：镜头与站姿不应被锁定目标强迫转向；从侧面短按或蓄力攻击时，人物与挥剑应转向所选目标。
2. 靠近两只小怪，不锁定时分别把准星移向不同敌人；按住右键接受背后及不同方向的普通攻击，检查格挡、弹反和架势崩溃后的受击区别。
3. 靠近 BOSS 检查强制锁定和快→慢→快→危节奏。“危”应在突刺前摇可见；持续格挡不能化解危突刺，合时机垫步可以避开。死亡重试及胜利后检查提示是否清除。
4. 沿关卡边界移动并转动镜头，检查墙体避障与解锁后无突跳；在 640×360、1280×720、1280×800 和最大化窗口检查“危”字、血条及准星位置。

真人试玩若仍出现空刀，请记录敌人位置、是否锁定、F2 HUD 的 Aim 来源、当时的攻击阶段及是否有墙体遮挡；这些信息可区分索敌、几何刀轨和遮挡问题。
