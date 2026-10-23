# 奶蛙 WFC 白盒 Demo

- Unity：打开 `Assets/CombatDemo/Scenes/PCG_Whitebox.unity`，点击 Play。
- Windows：运行 `Builds/Milkfrog-PCG/MilkfrogPCG.exe`；移动试玩包时请复制整个目录。
- 综述：[WFC 实践综述](WFC实践综述.md)。
- 验证：[测试与交付记录](../../Assets/CombatDemo/Validation/PCG/Report.md)。
- 配置：`Assets/CombatDemo/Settings/PCG/WhiteboxWFC.asset`。

| 操作 | 功能 |
|---|---|
| WASD / 鼠标 | 移动 / 过肩镜头 |
| Space / 左 Shift | 跳跃 / 垫步 |
| 左键 / 右键 | 攻击、按住蓄力 / 防御、弹反 |
| 鼠标中键 | 选择或取消可见敌人目标 |
| Esc | 暂停、种子输入与生成面板 |
| R / N | 重建当前种子 / 生成下一个种子 |
| F3 | 跑图与俯视观察切换 |
| F4 | 停止自动播放并前进一步 |

观察坍塌过程：按 F3，再按 R。候选数量、选择、传播和回溯会逐步显示；左侧可切换连续播放与播放速度。按 F3 返回跑图时，未完成的生成会快速求解后应用。

青色为起点，绿色为出口，橙色为两个遭遇房间；到达出口即完成跑图，不要求清怪。死亡、跌落或重建会恢复同一种子和全部遭遇。实验场景不接入主线磁盘存档。

生成方法是“主通路约束＋WFC＋全局连通验收”。小怪使用现有直线 AI，在各自房间内交战和返回，不具备跨房间寻路。

关闭正在使用本工程的 Unity 编辑器后，可在工程根目录运行：

```powershell
.\Tools\Validate-Pcg.ps1 -Stage All
```

支持 `EditMode`、`PlayMode`、`Benchmark`、`Capture`、`Build` 单独执行。算法离线检查使用 `.\Tools\Test-PcgOffline.ps1`，需要 .NET 10 SDK；离线结果不包含 Unity 碰撞验收。
