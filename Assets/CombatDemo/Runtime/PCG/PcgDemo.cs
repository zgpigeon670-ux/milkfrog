using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Milkfrog.CombatDemo.PCG
{
    public sealed class PcgDemo : MonoBehaviour
    {
        public WfcConfig config;
        public CombatDemoSettings combat;
        public EnemyDefinition enemyDefinition;
        public GameObject playerPrefab, enemyPrefab;
        public DemoCamera cameraRig;
        public Camera overviewCamera;
        public CombatFeedback feedback;
        public Material floorMaterial, wallMaterial, startMaterial, goalMaterial, encounterMaterial;
        public bool manualSimulation;
        public WfcResult Map { get; private set; }
        public CombatActor Player { get; private set; }
        public CombatActor[] Enemies { get; private set; } = Array.Empty<CombatActor>();
        public bool Observing { get; private set; }
        public bool Paused { get; private set; }
        public bool Generating => solveSteps != null;
        public bool Completed { get; private set; }
        public string Message { get; private set; } = "Preparing WFC...";
        public WfcStep LastStep { get; private set; }
        public int SelectedSeed { get; private set; }
        public int RemainingEnemies { get { int n = 0; foreach (var e in Enemies) if (e.Core.State != CombatState.Dead) n++; return n; } }
        public bool[] Engaged { get; private set; } = Array.Empty<bool>();
        public Vector3[] EnemyHomes { get; private set; } = Array.Empty<Vector3>();
        readonly List<CombatAnimationPresenter> animations = new List<CombatAnimationPresenter>();
        readonly List<CombatDemoSettings> ownedSettings = new List<CombatDemoSettings>();
        EnemyBrain[] brains = Array.Empty<EnemyBrain>();
        DemoInput input;
        WfcSolver solver;
        IEnumerator<WfcStep> solveSteps;
        GameObject generated;
        CombatActor locked;
        Vector2 movement;
        bool attackNeedsRelease = true, guardNeedsRelease = true, ignoreLook = true;
        bool autoPlay = true;
        float playbackRate = 15, playbackClock, deathTime;
        Font font;
        string seedText;
        GUIStyle titleStyle, textStyle, smallStyle, buttonStyle;

        void Awake()
        {
            font = Resources.Load<Font>("NotoSansSC-VF");
            input = new DemoInput("<Mouse>/middleButton");
            BeginGeneration(config.seed);
        }
        public void BeginGeneration(int seed)
        {
            solveSteps?.Dispose(); ClearInput(); SelectedSeed = seed; seedText = seed.ToString();
            solver = new WfcSolver(config, seed); solveSteps = solver.Steps().GetEnumerator();
            Message = "WFC 正在求解 / solving"; playbackClock = 0; LastStep = null;
            UpdateCursor();
        }
        public void FinishGeneration()
        {
            while (Generating) AdvanceGeneration();
        }
        public void AdvanceGeneration()
        {
            if (solveSteps == null) return;
            if (solveSteps.MoveNext())
            {
                LastStep = solveSteps.Current;
                if (LastStep.kind != WfcStepKind.Complete && LastStep.kind != WfcStepKind.Failed) return;
            }
            solveSteps.Dispose(); solveSteps = null;
            if (solver.Result.success)
            {
                try { Commit(solver.Result); Message = "从青色起点出发，寻找绿色出口 / Reach the green exit"; }
                catch (Exception error) { Message = "Collision validation failed: " + error.Message; Debug.LogException(error); }
            }
            else Message = solver.Result.error;
            ClearInput(); UpdateCursor();
        }
        public bool SpawnClear(Vector3 feet)
        {
            return Physics.Raycast(feet + Vector3.up, Vector3.down, out var ground, 1.25f, ~0, QueryTriggerInteraction.Ignore) &&
                Mathf.Abs(ground.point.y) < .03f && !Physics.CheckCapsule(feet + Vector3.up * .45f, feet + Vector3.up * 1.6f, .41f, ~0, QueryTriggerInteraction.Ignore);
        }
        void Commit(WfcResult map)
        {
            GameObject candidate = null;
            if (generated != null) generated.SetActive(false);
            try
            {
                candidate = PcgGeometry.Build(config, map, transform, floorMaterial, wallMaterial, startMaterial, goalMaterial, encounterMaterial);
                Physics.SyncTransforms();
                var spawn = PcgGeometry.Center(config, map.start) + Vector3.up * .02f;
                if (!SpawnClear(spawn)) throw new InvalidOperationException("Player spawn has no floor or insufficient clearance.");
                foreach (int cell in map.encounters) if (!SpawnClear(PcgGeometry.Center(config, cell) + new Vector3(0, .02f, 1))) throw new InvalidOperationException("Enemy spawn obstructed.");
                candidate.SetActive(false);
                var player = Instantiate(playerPrefab, spawn, Quaternion.identity, candidate.transform).GetComponent<CombatActor>();
                player.enforceFactions = true; player.autoFaceGuardAttacks = true; player.Initialize(Clone(combat.player));
                var enemies = new CombatActor[map.encounters.Length]; var nextBrains = new EnemyBrain[enemies.Length]; var homes = new Vector3[enemies.Length];
                var settings = new List<CombatDemoSettings>();
                for (int i = 0; i < enemies.Length; i++)
                {
                    homes[i] = PcgGeometry.Center(config, map.encounters[i]) + new Vector3(0, .02f, 1);
                    var instance = Instantiate(enemyPrefab, homes[i], Quaternion.Euler(0, 180, 0), candidate.transform);
                    // Existing EnemyController belongs to MvpWorld; this session owns these actors instead.
                    foreach (var controller in instance.GetComponents<EnemyController>()) controller.enabled = false;
                    var actor = enemies[i] = instance.GetComponent<CombatActor>(); actor.isPlayer = false; actor.enforceFactions = true;
                    actor.lightAttack = enemyDefinition.slash; actor.Initialize(Clone(enemyDefinition.tuning)); actor.Target = player;
                    var brainConfig = ScriptableObject.CreateInstance<CombatDemoSettings>(); settings.Add(brainConfig);
                    brainConfig.enemySpeed = enemyDefinition.speed; brainConfig.enemyWait = enemyDefinition.wait;
                    brainConfig.counterDelay = enemyDefinition.counterDelay; brainConfig.blocksBeforeDeflect = enemyDefinition.blocksBeforeDeflect;
                    nextBrains[i] = new EnemyBrain(actor, brainConfig, unchecked(map.stats.seed + i)); nextBrains[i].Reset(EnemyMode.Duel);
                }
                if (generated != null) Destroy(generated);
                foreach (var setting in ownedSettings) Destroy(setting); ownedSettings.Clear(); ownedSettings.AddRange(settings);
                generated = candidate; Player = player; Enemies = enemies; brains = nextBrains; EnemyHomes = homes; Engaged = new bool[enemies.Length];
                Map = map; Completed = false; deathTime = 0; locked = null;
                animations.Clear(); animations.AddRange(candidate.GetComponentsInChildren<CombatAnimationPresenter>(true));
                foreach (var animation in animations) animation.Initialize();
                foreach (var view in candidate.GetComponentsInChildren<CombatActorView>(true)) { view.viewCamera = cameraRig.GetComponent<Camera>(); view.diagnosticsOnly = true; }
                foreach (var label in candidate.GetComponentsInChildren<TextMesh>(true)) label.gameObject.SetActive(false);
                Player.Core.HitResolved += OnHit;
                var actors = new List<CombatActor> { Player }; actors.AddRange(Enemies); feedback.actors = actors.ToArray();
                feedback.ResolveActor = core => { foreach (var actor in feedback.actors) if (actor.Core == core) return actor; return null; };
                feedback.ResetFeedback();
                candidate.SetActive(true); Physics.SyncTransforms();
                cameraRig.player = Player.transform; cameraRig.enemy = null; cameraRig.lockOn = false;
                cameraRig.yaw = 0; cameraRig.pitch = 16; cameraRig.ResetImpulse(); cameraRig.Step(1);
            }
            catch
            {
                if (candidate != null) { candidate.SetActive(false); Destroy(candidate); }
                if (generated != null) generated.SetActive(true);
                throw;
            }
        }
        static CombatTuning Clone(CombatTuning tuning) => JsonUtility.FromJson<CombatTuning>(JsonUtility.ToJson(tuning));
        void OnHit(HitEvent hit)
        {
            CombatActor attacker = hit.Attacker == Player.Core ? Player : null;
            if (attacker == null) foreach (var e in Enemies) if (e.Core == hit.Attacker) { attacker = e; break; }
            feedback.OnContact(new CombatContact(hit, attacker != null && attacker.HasContactPoint ? attacker.LastContactPoint : Player.transform.position + Vector3.up));
        }
        void Update()
        {
            if (manualSimulation) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) SetPaused(!Paused);
                if (keyboard.f3Key.wasPressedThisFrame) SetObserving(!Observing);
                if (!Paused && keyboard.rKey.wasPressedThisFrame) BeginGeneration(SelectedSeed);
                if (!Paused && keyboard.nKey.wasPressedThisFrame) BeginGeneration(unchecked(SelectedSeed + 1));
                if (Observing && keyboard.f4Key.wasPressedThisFrame) { autoPlay = false; AdvanceGeneration(); }
            }
            if (Generating)
            {
                if (!Observing)
                {
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    while (Generating && clock.Elapsed.TotalMilliseconds < 4) AdvanceGeneration();
                }
                else if (autoPlay && !Paused)
                {
                    playbackClock += Time.unscaledDeltaTime * playbackRate;
                    int budget = Mathf.Min((int)playbackClock, 120); playbackClock -= budget;
                    for (int i = 0; i < budget && Generating; i++) AdvanceGeneration();
                }
                return;
            }
            if (Paused || Observing || Player == null) return;
            if (Player.Core.State == CombatState.Dead || Player.transform.position.y < -3)
            {
                deathTime += Time.unscaledDeltaTime;
                foreach (var animation in animations) animation.AdvanceVisual(Time.unscaledDeltaTime);
                if (deathTime > 1.5f) BeginGeneration(SelectedSeed);
                return;
            }
            if (ignoreLook) ignoreLook = false; else cameraRig.Look(input.Look);
            SubmitInput(input.Snapshot); Simulate(Time.unscaledDeltaTime);
        }
        Vector3 Direction(Vector2 value)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cameraRig.transform.forward, Vector3.up).normalized;
            return forward * value.y + Vector3.Cross(Vector3.up, forward) * value.x;
        }
        CombatActor ClosestTarget(float radius)
        {
            CombatActor target = null; float best = radius * radius;
            foreach (var enemy in Enemies)
            {
                float distance = (enemy.transform.position - Player.transform.position).sqrMagnitude;
                if (enemy.Core.State != CombatState.Dead && distance < best && Player.HasLineOfSight(enemy, enemy.transform.position + Vector3.up)) { best = distance; target = enemy; }
            }
            return target;
        }
        public void SubmitInput(CombatInputFrame frame)
        {
            if (Player == null || Generating || Paused || Observing || Player.Core.State == CombatState.Dead) return;
            movement = Vector2.ClampMagnitude(frame.move, 1);
            if (!frame.attackHeld) attackNeedsRelease = false;
            if (!frame.guardHeld) guardNeedsRelease = false;
            if (frame.lockPressed) locked = locked != null ? null : ClosestTarget(12);
            if (locked != null && (locked.Core.State == CombatState.Dead || (locked.transform.position - Player.transform.position).sqrMagnitude > 144)) locked = null;
            Player.Target = locked != null ? locked : ClosestTarget(Player.Core.Tuning.range + 1);
            bool guard = frame.guardHeld && !guardNeedsRelease;
            Player.Core.SetAttackHeld(frame.attackHeld);
            if (feedback.Clock.Remaining > 0) return;
            if (frame.dodgePressed) { Player.RequestDodge(Direction(movement)); attackNeedsRelease = frame.attackHeld; }
            if (frame.jumpPressed) Player.TryJump();
            Player.Core.SetGuard(guard, frame.guardPressed && !guardNeedsRelease);
            if (guard) { attackNeedsRelease = frame.attackHeld; return; }
            if (Player.Core.IsPreparing && (!frame.attackHeld || frame.attackReleased)) Player.Core.ReleaseAttack();
            else if (frame.attackPressed && !attackNeedsRelease)
            {
                if (Player.Target != null) Player.FaceTarget();
                Player.BeginPlayerAttack(); attackNeedsRelease = frame.attackHeld;
            }
        }
        public void Simulate(float dt)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
            if (Player == null || Generating || Paused || Observing || Player.Core.State == CombatState.Dead) return;
            while (dt > 0)
            {
                float step = Mathf.Min(dt, 1f / 120); dt -= step; feedback.TickReal(step); step = feedback.Clock.Consume(step); if (step <= 0) continue;
                Vector3 direction = Direction(movement);
                if (direction.sqrMagnitude > .001f && Player.Core.CanAct && !Player.Core.IsPreparing) Player.transform.rotation = Quaternion.LookRotation(direction);
                Player.Move(direction, combat.playerSpeed, step);
                for (int i = 0; i < Enemies.Length; i++)
                {
                    var enemy = Enemies[i]; if (enemy.Core.State == CombatState.Dead) { enemy.Motor.enabled = false; continue; }
                    bool engage = PcgGeometry.Cell(config, Player.transform.position) == Map.encounters[i] && enemy.HasLineOfSight(Player, Player.transform.position + Vector3.up);
                    if (engage) { Engaged[i] = true; brains[i].Tick(step); }
                    else
                    {
                        if (Engaged[i]) { enemy.Core.RestoreVitals(enemy.Core.Health, enemy.Core.Posture); brains[i].Reset(EnemyMode.Duel); }
                        Engaged[i] = false; enemy.Core.SetGuard(false, false);
                        var delta = EnemyHomes[i] - enemy.transform.position; delta.y = 0;
                        if (delta.sqrMagnitude > .01f && enemy.Core.CanAct) enemy.transform.rotation = Quaternion.LookRotation(delta);
                        enemy.Move(delta.normalized, Mathf.Min(enemyDefinition.speed, delta.magnitude / step), step);
                    }
                    // Keep the simple straight-line brain inside its own convex room, even during a dodge/attack.
                    Vector3 center = PcgGeometry.Center(config, Map.encounters[i]); float bound = config.cellSize * .5f - .85f;
                    Vector3 p = enemy.transform.position;
                    Vector3 confined = new Vector3(Mathf.Clamp(p.x, center.x - bound, center.x + bound), p.y, Mathf.Clamp(p.z, center.z - bound, center.z + bound));
                    if ((p - confined).sqrMagnitude > .000001f) enemy.Motor.Move(confined - p);
                }
                Physics.SyncTransforms(); Player.Core.Tick(step);
                foreach (var enemy in Enemies) if (enemy.Core.State != CombatState.Dead) enemy.Core.Tick(step);
                foreach (var animation in animations) animation.AdvanceVisual(step);
                if (Player.Core.State == CombatState.Dead) break;
                if (PcgGeometry.Cell(config, Player.transform.position) == Map.goal) Completed = true;
            }
        }
        public void ClearInput()
        {
            movement = Vector2.zero; attackNeedsRelease = guardNeedsRelease = ignoreLook = true; locked = null;
            if (Player == null || Player.Core == null) return;
            Player.Target = null; Player.Core.SetAttackHeld(false); Player.Core.SetGuard(false, false); Player.Core.CancelPreparation();
            Player.GetComponent<CombatAnimationPresenter>()?.ResetVisuals();
            feedback?.ResetFeedback();
        }
        public void SetPaused(bool value) { Paused = value; ClearInput(); UpdateCursor(); }
        public void SetObserving(bool value)
        {
            Observing = value; ClearInput(); cameraRig.Suspended = value;
            cameraRig.GetComponent<Camera>().enabled = !value; overviewCamera.gameObject.SetActive(value); UpdateCursor();
        }
        void UpdateCursor()
        {
            bool free = Paused || Observing || Generating || Player == null;
            cameraRig.Suspended = free; Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = free;
        }
        void OnApplicationFocus(bool focus) { if (!focus) SetPaused(true); }
        void OnDestroy()
        {
            input?.Dispose(); solveSteps?.Dispose(); foreach (var setting in ownedSettings) Destroy(setting);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        void Styles()
        {
            if (textStyle != null) return;
            textStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, wordWrap = true, normal = { textColor = new Color(.86f, .91f, .93f) } };
            titleStyle = new GUIStyle(textStyle) { fontSize = 27, fontStyle = FontStyle.Bold };
            smallStyle = new GUIStyle(textStyle) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 15, fixedHeight = 34 };
        }
        static void Fill(Rect rect, Color color) { var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
        void OnGUI()
        {
            Styles(); float scale = Mathf.Max(.65f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1)); float width = Screen.width / scale, height = Screen.height / scale;
            Fill(new Rect(20, 20, 320, Observing || Paused || Map == null ? height - 40 : 237), new Color(.035f, .055f, .075f, .94f));
            GUILayout.BeginArea(new Rect(38, 32, 284, height - 64));
            GUILayout.Label("MILKFROG / WFC", titleStyle); GUILayout.Label("程序化白盒 · 实验场", textStyle); GUILayout.Space(12);
            GUILayout.Label("SEED  " + SelectedSeed + (Generating ? "  / SOLVING" : ""), textStyle);
            if (Map != null)
            {
                var s = Map.stats;
                GUILayout.Label($"房间 {s.rooms}  ·  可走格 {s.walkable}\n最短路径 {s.shortestPath * config.cellSize:0} m  ·  环路 {s.loops}\n求解 {s.milliseconds:0.0} ms  ·  尝试 {s.attempts}\n小怪 {RemainingEnemies}/{Enemies.Length}  ·  HP {Player.Core.Health:0}", textStyle);
            }
            if (Observing || Paused || Map == null)
            {
                GUILayout.Space(12); GUILayout.Label(Message ?? "", textStyle);
                GUILayout.Space(8); seedText = GUILayout.TextField(seedText ?? SelectedSeed.ToString(), 12);
                if (GUILayout.Button("使用此种子生成", buttonStyle)) { if (int.TryParse(seedText, out int seed)) BeginGeneration(seed); else Message = "请输入有效的 32 位整数种子。"; }
                if (GUILayout.Button("N  /  下一个种子", buttonStyle)) BeginGeneration(unchecked(SelectedSeed + 1));
                if (GUILayout.Button("R  /  重建当前种子", buttonStyle)) BeginGeneration(SelectedSeed);
                if (GUILayout.Button(Observing ? "F3  /  返回跑图" : "F3  /  俯视观察", buttonStyle)) SetObserving(!Observing);
                if (Observing)
                {
                    GUILayout.Space(10);
                    GUILayout.Label(LastStep == null ? "等待求解" : $"{LastStep.kind}  ·  格 {LastStep.cell}\n尝试 {LastStep.attempt}  ·  回溯 {solver?.Result.stats.backtracks ?? 0}", textStyle);
                    autoPlay = GUILayout.Toggle(autoPlay, " 连续播放");
                    GUILayout.Label($"播放速度 {playbackRate:0} steps/s", textStyle); playbackRate = GUILayout.HorizontalSlider(playbackRate, 1, 120);
                    if (GUILayout.Button("F4  /  单步", buttonStyle)) { autoPlay = false; AdvanceGeneration(); }
                }
                if (GUILayout.Button("Esc  /  " + (Paused ? "继续" : "暂停"), buttonStyle)) SetPaused(!Paused);
                if (GUILayout.Button("退出试玩", buttonStyle)) Application.Quit();
            }
            GUILayout.EndArea();
            Fill(new Rect(20, height - 68, width - 40, 48), new Color(.035f, .055f, .075f, .94f));
            GUI.Label(new Rect(35, height - 60, width - 70, 38), "WASD 移动 · 鼠标镜头 · Space 跳跃 · Shift 垫步 · 左键攻击 · 右键防御 · 中键选敌 · Esc 面板 · F3 观察 · R 重建 · N 换图", textStyle);
            if (Completed && !Observing) { Fill(new Rect(width * .5f - 210, 30, 420, 68), new Color(.06f, .28f, .2f, .95f)); GUI.Label(new Rect(width * .5f - 190, 44, 390, 42), "跑图完成 / EXIT REACHED", titleStyle); }
            if (Generating && !Observing) GUI.Label(new Rect(width * .5f - 130, height * .5f, 420, 40), "WFC / GENERATING...", titleStyle);
            if (Observing) DrawWave(width, height);
            else if (Map != null) DrawMinimap(width);
        }
        void DrawWave(float width, float height)
        {
            Fill(new Rect(360, 20, width - 380, height - 110), new Color(.055f, .085f, .11f));
            float size = Mathf.Min((width - 410) / config.width, (height - 250) / config.height), x0 = 380, y0 = 105;
            GUI.Label(new Rect(x0, 32, width - x0 - 30, 50), "OBSERVATION / 候选集合 → 坍塌 → 传播", titleStyle);
            for (int i = 0; i < config.width * config.height; i++)
            {
                ulong domain = LastStep?.domains != null ? LastStep.domains[i] : Map != null ? 1UL << Map.tiles[i] : 0;
                int n = WfcSolver.Bits(domain), tile = WfcSolver.Tile(domain);
                Color color = n == 1 ? config.modules[tile].ports == 0 ? new Color(.1f, .13f, .16f) : config.modules[tile].room ? new Color(.35f, .62f, .68f) : new Color(.64f, .71f, .73f) : new Color(.19f, .28f, .36f);
                if (LastStep != null && i == LastStep.cell) color = new Color(1, .63f, .25f);
                var rect = new Rect(x0 + i % config.width * size, y0 + (config.height - 1 - i / config.width) * size, size - 3, size - 3);
                Fill(rect, color);
                if (n == 1 && config.modules[tile].ports != 0)
                {
                    int ports = config.modules[tile].ports;
                    for (int d = 0; d < 4; d++) if ((ports & (1 << d)) != 0)
                    {
                        Vector2 center = rect.center; float bar = size * .13f;
                        Fill(new Rect(center.x - bar * .5f + (d == 3 ? -size * .45f : 0), center.y - bar * .5f + (d == 0 ? -size * .45f : 0), d % 2 == 1 ? size * .45f + bar : bar, d % 2 == 0 ? size * .45f + bar : bar), new Color(.07f, .15f, .19f));
                    }
                }
                if (n != 1) GUI.Label(rect, n == 0 ? "×" : n.ToString(), smallStyle);
                else if (config.modules[tile].room) GUI.Label(rect, Map != null && i == Map.start ? "S" : Map != null && i == Map.goal ? "E" : "R", smallStyle);
            }
            GUI.Label(new Rect(x0, y0 + size * config.height + 8, width - x0 - 20, 46), "数字 = 候选数量 · 浅灰 = 走廊 · 青色 = 房间 · 橙色 = 当前选择 · 深色 = 空格\n主通路与连通性为额外约束；WFC 决定剩余模块与连接。", textStyle);
        }
        void DrawMinimap(float width)
        {
            float size = 15, x = width - config.width * size - 35, y = 35;
            Fill(new Rect(x - 10, y - 10, config.width * size + 20, config.height * size + 20), new Color(.035f, .055f, .075f, .94f));
            for (int i = 0; i < Map.tiles.Length; i++)
            {
                var m = config.modules[Map.tiles[i]]; if (m.ports == 0) continue;
                Color color = i == Map.start ? startMaterial.color : i == Map.goal ? goalMaterial.color : m.room ? new Color(.6f, .7f, .72f) : new Color(.3f, .4f, .45f);
                Fill(new Rect(x + i % config.width * size, y + (config.height - 1 - i / config.width) * size, size - 2, size - 2), color);
            }
            int cell = PcgGeometry.Cell(config, Player.transform.position);
            if (cell >= 0) Fill(new Rect(x + cell % config.width * size + 4, y + (config.height - 1 - cell / config.width) * size + 4, 6, 6), Color.yellow);
        }
    }
}
