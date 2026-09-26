using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Milkfrog.CombatDemo
{
    public sealed class MvpWorld : MonoBehaviour
    {
        public CombatDemoSettings settings;
        public CombatActor player;
        public EnemyController[] enemies;
        public Camera gameplayCamera;
        public DemoCamera cameraRig;
        [Range(0, .5f)] public float aimReticleRadius = .18f;
        public CombatFeedback feedback;
        public GameObject arena;
        public BonfireCheckpoint[] bonfires;
        public QuestDefinition quest;
        public WorldStateService WorldState { get; } = new WorldStateService();
        public QuestProgressService Quests { get; private set; }
        public InteractionController Interactions { get; private set; }
        WorldInteractable[] worldObjects;
        bool ownsQuest;
        public bool BossUnlocked => WorldState.IsDoorOpen(WorldStateService.GateId);
        public string InteractionBlockReason => !Running ? "当前无法交互" :
            feedback.Clock.Remaining > 0 ? "请稍候" : Director.State != EncounterState.Exploration ? "战斗中无法交互" :
            player.Core.State != CombatState.Neutral ? "请先结束当前动作" : null;
        public Vector3 spawn = new Vector3(0, .02f, -35);
        public EncounterDirector Director { get; private set; }
        public LockOnController Lock { get; private set; }
        public GameFlowController Flow { get; private set; }
        public bool manualSimulation;
        public bool BossDefeated { get; private set; }
        public PlayerProgression Progression { get; private set; } = new PlayerProgression();
        public string ActiveCheckpointId { get; private set; } = BonfireCheckpoint.StartId;
        public readonly HashSet<string> UnlockedCheckpointIds = new HashSet<string>(StringComparer.Ordinal);
        public int ContactCount { get; private set; }
        public string AimDecision { get; private set; } = "None";
        public readonly HashSet<string> Defeated = new HashSet<string>();
        public bool CanSave => Director.State == EncounterState.Exploration && player.Core.State == CombatState.Neutral;
        public bool Running => Flow != null && Flow.State == GameFlowState.Playing && !Flow.Paused;
        readonly Dictionary<CombatCore, CombatActor> actors = new Dictionary<CombatCore, CombatActor>();
        readonly List<CombatAnimationPresenter> animations = new List<CombatAnimationPresenter>();
        readonly RaycastHit[] sightHits = new RaycastHit[32];
        DemoInput input;
        Vector2 movement;
        bool needsRelease, attackQueued, pendingSave, frozenGuard, hasFrozenGuard;
        float saveClock, saveRetryClock;
        MvpHud hud;

        void Awake()
        {
            Flow = GetComponent<GameFlowController>();
            if (quest == null) { quest = ScriptableObject.CreateInstance<QuestDefinition>(); ownsQuest = true; }
            Quests = new QuestProgressService(quest);
            WorldState.Changed += OnWorldChanged;
            worldObjects = FindObjectsByType<WorldInteractable>();
            player.enforceFactions = true;
            player.autoFaceGuardAttacks = true;
            player.Initialize(JsonUtility.FromJson<CombatTuning>(JsonUtility.ToJson(settings.player)));
            Director = new EncounterDirector(this); Lock = new LockOnController(this);
            Register(player);
            foreach (var enemy in enemies) { enemy.Initialize(this); Register(enemy.Actor); }
            foreach (var actor in actors.Values)
            {
                var animation = actor.GetComponent<CombatAnimationPresenter>();
                if (animation != null) { animation.Initialize(); animations.Add(animation); }
            }
            feedback.actors = new List<CombatActor>(actors.Values).ToArray();
            feedback.ResolveActor = core => actors.TryGetValue(core, out var actor) ? actor : null;
            cameraRig.freeOrbit = true; cameraRig.lockOn = false; cameraRig.enemy = null; cameraRig.pitch = 8; cameraRig.orbitDistance = 2.7f; cameraRig.orbitShoulder = new Vector3(.45f, .05f, 0);
            hud = GetComponent<MvpHud>();
            if (hud != null)
            {
                hud.Read = HudData;
                hud.ReadInteractionPrompt = () => Interactions?.Prompt;
                hud.ReadPerilousWarning = IsBossPerilousStartup;
                hud.ReadVisible = () => Flow.State == GameFlowState.Playing && !Flow.Paused;
            }
            input = new DemoInput("<Mouse>/middleButton");
            if (bonfires == null || bonfires.Length == 0) bonfires = FindObjectsByType<BonfireCheckpoint>();
            Interactions = new InteractionController(this);
        }
        void Register(CombatActor actor)
        {
            actors.Add(actor.Core, actor);
            // Every hit is emitted by both cores; only the player subscription forwards it.
            if (actor == player) actor.Core.HitResolved += OnHit;
        }
        public void Restore(PlayerSnapshot snapshot)
        {
            Director = new EncounterDirector(this); saveClock = saveRetryClock = 0; pendingSave = false;
            AimDecision = "None";
            foreach (var enemy in enemies) enemy.ResetForLoad();
            Defeated.Clear(); BossDefeated = snapshot != null && snapshot.bossDefeated;
            Progression.Restore(snapshot);
            WorldState.Restore(snapshot?.worldState);
            ActiveCheckpointId = FindBonfire(snapshot?.activeCheckpointId) != null ? snapshot.activeCheckpointId : BonfireCheckpoint.StartId;
            UnlockedCheckpointIds.Clear();
            UnlockedCheckpointIds.Add(BonfireCheckpoint.StartId);
            UnlockedCheckpointIds.Add(ActiveCheckpointId);
            if (snapshot?.unlockedCheckpointIds != null)
                foreach (string id in snapshot.unlockedCheckpointIds)
                    if (FindBonfire(id) != null) UnlockedCheckpointIds.Add(id);
            if (snapshot?.defeatedEnemyIds != null) foreach (string id in snapshot.defeatedEnemyIds) Defeated.Add(id);
            Vector3 position = snapshot == null ? spawn : snapshot.position;
            if (Mathf.Abs(position.x) > 38 || Mathf.Abs(position.z) > 48 || position.y < -1 || position.y > 4) position = spawn;
            else if (!BossDefeated && enemies.Length > 0 && InBossTrigger(position))
            {
                var camp = FindBonfire(ActiveCheckpointId);
                position = camp != null && !InBossTrigger(camp.RespawnPosition) ? camp.RespawnPosition : spawn;
            }
            position.y = .02f;
            player.Motor.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0, snapshot?.yaw ?? 0, 0));
            player.Motor.enabled = true;
            player.Core.Reset();
            ApplyProgression();
            player.Core.RestoreVitals(snapshot?.health ?? player.Core.Tuning.maxHealth, snapshot?.posture ?? 0);
            foreach (var enemy in enemies)
                if (Defeated.Contains(enemy.stableId) || (enemy.IsBoss && BossDefeated))
                { enemy.Actor.Core.RestoreVitals(0, 0); enemy.MarkDead(); enemy.gameObject.SetActive(false); }
            if (arena != null) arena.SetActive(false);
            Lock.Clear(); cameraRig.yaw = player.transform.eulerAngles.y; cameraRig.pitch = 8; cameraRig.ResetImpulse();
            ClearInput(); feedback.ResetFeedback();
            foreach (var animation in animations) if (animation.gameObject.activeInHierarchy) animation.ResetVisuals();
            ApplyWorldObjects(); Physics.SyncTransforms(); Interactions.Refresh(); hud?.Refresh();
        }
        bool InBossTrigger(Vector3 position)
        {
            foreach (var enemy in enemies) if (enemy.IsBoss && !Defeated.Contains(enemy.stableId) &&
                EnemyController.FlatDistance(position, enemy.Home) <= enemy.definition.bossTriggerRadius) return true;
            return false;
        }
        public PlayerSnapshot Snapshot()
        {
            var snapshot = new PlayerSnapshot
        {
            position = player.transform.position, yaw = player.transform.eulerAngles.y,
            health = player.Core.Health, posture = player.Core.Posture,
            defeatedEnemyIds = new List<string>(Defeated).ToArray(), bossDefeated = BossDefeated,
            activeCheckpointId = ActiveCheckpointId,
            unlockedCheckpointIds = SortedCheckpointIds(),
            experience = Progression.Experience, vitality = Progression.Vitality,
            resolve = Progression.Resolve, power = Progression.Power
        };
            MergeWorldProgress(snapshot); return snapshot;
        }
        public void MergeWorldProgress(PlayerSnapshot snapshot)
        { WorldState.WriteTo(snapshot); Quests.WriteTo(snapshot); }
        void OnWorldChanged(WorldEventKind kind, string id)
        { ApplyWorldObjects(); Flow.Notify("进度更新 · " + Quests.Objective(Snapshot())); hud?.Refresh(); }
        void ApplyWorldObjects()
        { foreach (var obj in worldObjects) if (obj != null) obj.ApplyState(this); }
        public bool TryWorldInteraction(WorldInteractable target)
        {
            if (InteractionBlockReason != null || target == null || !target.Available(this) || target.BlockReason(this) != null ||
                EnemyController.FlatDistance(player.transform.position, target.InteractionPoint) > target.Radius ||
                Mathf.Abs(player.transform.position.y - target.InteractionPoint.y) > 1.5f) return false;
            var proposed = Snapshot(); var next = new WorldStateService(); next.Restore(proposed.worldState);
            var kind = target.kind == WorldInteractionKind.KeyPickup ? WorldEventKind.ItemAcquired : WorldEventKind.DoorOpened;
            next.Apply(kind, target.stableId, target.itemId); next.WriteTo(proposed); Quests.WriteTo(proposed);
            if (!Flow.Store.TrySave(proposed)) { Flow.Notify("保存失败，请重试。" + Flow.Store.LastMessage); return false; }
            WorldState.Apply(kind, target.stableId, target.itemId);
            return true;
        }
        public BonfireCheckpoint FindBonfire(string id)
        {
            if (bonfires == null) return null;
            foreach (var bonfire in bonfires) if (bonfire != null && bonfire.stableId == id) return bonfire;
            return null;
        }
        public bool IsBonfireUnlocked(string id) => id != null && UnlockedCheckpointIds.Contains(id);
        string[] SortedCheckpointIds(string additionalId = null)
        {
            var ids = new HashSet<string>(UnlockedCheckpointIds, StringComparer.Ordinal);
            ids.Add(BonfireCheckpoint.StartId);
            if (!string.IsNullOrEmpty(additionalId)) ids.Add(additionalId);
            var result = new string[ids.Count]; ids.CopyTo(result); Array.Sort(result, StringComparer.Ordinal);
            return result;
        }
        void ApplyProgression()
        {
            player.Core.ApplyGrowth(settings.player.maxHealth + 10 * Progression.Vitality,
                settings.player.maxPosture + 10 * Progression.Resolve, Progression.AttackMultiplier);
        }
        public bool TryRest(BonfireCheckpoint bonfire)
        {
            if (InteractionBlockReason != null || bonfire == null || !bonfire.InRange(player.transform.position)) return false;
            var save = Snapshot();
            save.activeCheckpointId = bonfire.stableId;
            save.unlockedCheckpointIds = SortedCheckpointIds(bonfire.stableId);
            Quests.WriteTo(save);
            save.health = player.Core.Tuning.maxHealth; save.posture = 0;
            save.defeatedEnemyIds = BossDefeatedIds();
            if (!Flow.Store.TrySave(save)) { Flow.Notify(Flow.Store.LastMessage); return false; }
            ActiveCheckpointId = bonfire.stableId;
            if (UnlockedCheckpointIds.Add(bonfire.stableId)) WorldState.Apply(WorldEventKind.BonfireLit, bonfire.stableId);
            ResetAfterBonfireUse();
            Flow.OpenBonfire(bonfire);
            return true;
        }
        public bool TryFastTravel(BonfireCheckpoint destination)
        {
            if (Flow == null || Flow.State != GameFlowState.Playing || !Flow.Paused ||
                Flow.ActiveBonfire == null || !Flow.ActiveBonfire.InRange(player.transform.position) ||
                !CanSave || destination == null || destination.stableId == ActiveCheckpointId ||
                !IsBonfireUnlocked(destination.stableId) ||
                (!BossDefeated && InBossTrigger(destination.RespawnPosition))) return false;
            var save = Snapshot();
            save.position = destination.RespawnPosition; save.yaw = destination.RespawnYaw;
            save.health = player.Core.Tuning.maxHealth; save.posture = 0;
            save.activeCheckpointId = destination.stableId;
            save.defeatedEnemyIds = BossDefeatedIds();
            if (!Flow.Store.TrySave(save)) { Flow.Notify(Flow.Store.LastMessage); return false; }
            ActiveCheckpointId = destination.stableId;
            player.Motor.enabled = false;
            player.transform.SetPositionAndRotation(save.position, Quaternion.Euler(0, save.yaw, 0));
            player.Motor.enabled = true;
            ResetAfterBonfireUse();
            cameraRig.yaw = save.yaw; cameraRig.pitch = 12; cameraRig.ResetImpulse(); cameraRig.Step(1);
            Flow.OpenBonfire(destination);
            return true;
        }
        void ResetAfterBonfireUse()
        {
            foreach (var enemy in enemies)
                if (!enemy.IsBoss) enemy.ResetForLoad();
                else if (BossDefeated) enemy.gameObject.SetActive(false);
            Defeated.Clear();
            foreach (string id in BossDefeatedIds()) Defeated.Add(id);
            Director = new EncounterDirector(this); Lock.Clear();
            player.Core.RestoreVitals(player.Core.Tuning.maxHealth, 0);
            feedback.ResetFeedback(); ClearInput(); Physics.SyncTransforms();
            hud?.Refresh();
        }
        public bool TryUpgrade(GrowthStat stat)
        {
            if (!Flow.Paused || Flow.ActiveBonfire == null || !CanSave) return false;
            var proposed = Snapshot();
            var next = new PlayerProgression(); next.Restore(proposed);
            if (!next.TryUpgrade(stat)) return false;
            next.WriteTo(proposed);
            proposed.health = settings.player.maxHealth + 10 * next.Vitality;
            proposed.posture = 0;
            if (!Flow.Store.TrySave(proposed)) { Flow.Notify(Flow.Store.LastMessage); return false; }
            Progression.Restore(proposed); ApplyProgression(); player.Core.RestoreVitals(player.Core.Tuning.maxHealth, 0);
            hud?.Refresh(); return true;
        }
        public PlayerSnapshot DeathRespawnSnapshot()
        {
            if (!Flow.Store.TryLoad(out var safe)) safe = new PlayerSnapshot { position = spawn };
            var camp = FindBonfire(ActiveCheckpointId) ?? FindBonfire(BonfireCheckpoint.StartId);
            safe.position = camp != null ? camp.RespawnPosition : spawn;
            safe.yaw = camp != null ? camp.RespawnYaw : 0;
            safe.health = settings.player.maxHealth + 10 * Progression.Vitality; safe.posture = 0;
            safe.activeCheckpointId = ActiveCheckpointId; safe.bossDefeated = BossDefeated;
            safe.unlockedCheckpointIds = SortedCheckpointIds();
            safe.defeatedEnemyIds = BossDefeatedIds();
            Progression.WriteTo(safe); MergeWorldProgress(safe); return safe;
        }
        void PersistCombatRewards()
        {
            if (!Flow.Store.TryLoad(out var safe)) { Flow.Notify(Flow.Store.LastMessage); pendingSave = true; return; }
            Progression.WriteTo(safe);
            safe.bossDefeated = BossDefeated;
            if (BossDefeated) safe.defeatedEnemyIds = BossDefeatedIds();
            safe.unlockedCheckpointIds = SortedCheckpointIds();
            MergeWorldProgress(safe);
            if (!Flow.Store.TrySave(safe)) { Flow.Notify("进度尚未保存，请重试。" + Flow.Store.LastMessage); pendingSave = true; }
        }
        string[] BossDefeatedIds()
        {
            if (!BossDefeated) return Array.Empty<string>();
            foreach (var enemy in enemies) if (enemy.IsBoss) return new[] { enemy.stableId };
            return Array.Empty<string>();
        }
        void Update()
        {
            if (manualSimulation || input == null) return;
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame && hud != null) hud.showDebug = !hud.showDebug;
            if (input.PausePressed && Flow.State == GameFlowState.Playing) { Flow.TogglePause(); return; }
            if (Running && input.AttributesPressed && Director.State == EncounterState.Exploration)
            { Flow.OpenAttributes(); return; }
            // Use the previous rendered focus; TryInteract revalidates it before committing.
            if (Running && input.InteractPressed && Interactions.TryInteract()) { ClearInput(); return; }
            if (!Running)
            {
                if (Flow.State == GameFlowState.PlayerDead || Flow.State == GameFlowState.Victory)
                {
                    feedback.TickReal(Time.unscaledDeltaTime);
                    foreach (var animation in animations) if (animation.gameObject.activeInHierarchy) animation.AdvanceVisual(Time.unscaledDeltaTime);
                }
                return;
            }
            cameraRig.Look(input.Look); SubmitInput(input.Snapshot); Simulate(Time.unscaledDeltaTime);
        }
        void LateUpdate() { Interactions?.Refresh(); }
        public void ClearInput()
        {
            movement = Vector2.zero; attackQueued = false; needsRelease = true; hasFrozenGuard = frozenGuard = false;
            if (player.Core == null) return;
            player.Core.SetGuard(false, false); player.Core.CancelPreparation();
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused) { ClearInput(); if (Flow != null && Running) Flow.TogglePause(); }
        }
        Vector3 MoveDirection()
        {
            Vector3 forward = Vector3.ProjectOnPlane(gameplayCamera.transform.forward, Vector3.up).normalized;
            return forward * movement.y + Vector3.Cross(Vector3.up, forward) * movement.x;
        }
        public void SubmitInput(CombatInputFrame frame)
        {
            if (!Running) return;
            movement = Vector2.ClampMagnitude(frame.move, 1);
            if (frame.lockPressed) Lock.Toggle();
            if (!frame.attackHeld) needsRelease = false;
            if (frame.attackPressed) attackQueued = true;
            player.Core.SetAttackHeld(frame.attackHeld);
            if (feedback.Clock.Remaining > 0)
            {
                frozenGuard = frame.guardHeld; hasFrozenGuard = true;
                return;
            }
            hasFrozenGuard = false;
            if (frame.dodgePressed && player.RequestDodge(MoveDirection()))
            { attackQueued = false; player.Core.SetGuard(frame.guardHeld, false); needsRelease = frame.attackHeld; return; }
            if (frame.jumpPressed) player.TryJump();
            player.Core.SetGuard(frame.guardHeld, frame.guardPressed);
            if (frame.guardHeld) { attackQueued = false; return; }
            if (attackQueued && player.Core.CanAct)
            {
                FaceAttackAim(SelectAttackTarget());
                player.BeginPlayerAttack();
                attackQueued = false;
                needsRelease = true;
            }
            player.Target = Lock.Target == null ? null : Lock.Target.Actor;
        }
        public CombatActor SelectAttackTarget()
        {
            if (Lock.Target != null && Lock.Target.Alive && Lock.Target.Actor.AcceptsDamage)
            { AimDecision = "Locked: " + Lock.Target.stableId; return Lock.Target.Actor; }
            EnemyController reticle = null, nearest = null;
            float reticleScore = float.PositiveInfinity, reticleDistance = float.PositiveInfinity;
            float nearestDistance = float.PositiveInfinity;
            float radius = Mathf.Min(gameplayCamera.pixelWidth, gameplayCamera.pixelHeight) * Mathf.Clamp01(aimReticleRadius);
            float radiusSquared = radius * radius;
            foreach (var enemy in enemies)
            {
                var actor = enemy.Actor;
                if (!enemy.Alive || !actor.AcceptsDamage) continue;
                Vector3 offset = actor.transform.position - player.transform.position;
                float d = Vector3.ProjectOnPlane(offset, Vector3.up).magnitude;
                if (d > player.Core.Tuning.range || Mathf.Abs(offset.y) > player.maxTargetHeightDifference ||
                    !player.HasLineOfSight(actor, actor.transform.position + Vector3.up * 1.1f)) continue;
                if (d < nearestDistance || Mathf.Approximately(d, nearestDistance) &&
                    string.CompareOrdinal(enemy.stableId, nearest?.stableId) < 0)
                { nearest = enemy; nearestDistance = d; }
                Vector3 viewport = gameplayCamera.WorldToViewportPoint(actor.transform.position + Vector3.up * 1.2f);
                if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1 || !Visible(actor)) continue;
                float dx = (viewport.x - .5f) * gameplayCamera.pixelWidth;
                float dy = (viewport.y - .5f) * gameplayCamera.pixelHeight;
                float score = dx * dx + dy * dy;
                if (score > radiusSquared) continue;
                if (score < reticleScore || Mathf.Approximately(score, reticleScore) &&
                    (d < reticleDistance || Mathf.Approximately(d, reticleDistance) &&
                    string.CompareOrdinal(enemy.stableId, reticle?.stableId) < 0))
                { reticle = enemy; reticleScore = score; reticleDistance = d; }
            }
            EnemyController selected = reticle ?? nearest;
            AimDecision = selected == null ? "Reticle" : (reticle != null ? "Reticle: " : "Nearest: ") + selected.stableId;
            return selected?.Actor;
        }
        void FaceAttackAim(CombatActor target)
        {
            Vector3 forward = target == null
                ? Vector3.ProjectOnPlane(gameplayCamera.ViewportPointToRay(new Vector3(.5f, .5f)).direction, Vector3.up)
                : Vector3.ProjectOnPlane(target.transform.position - player.transform.position, Vector3.up);
            if (forward.sqrMagnitude > .000001f) player.transform.rotation = Quaternion.LookRotation(forward);
            player.Target = target;
        }
        public void Simulate(float dt)
        {
            if (dt < 0 || float.IsNaN(dt) || float.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            if (!Running) return;
            while (dt > .000001f && Running)
            {
                float step = Mathf.Min(dt, 1f / 120); dt -= step;
                feedback.TickReal(step); step = feedback.Clock.Consume(step);
                if (step <= 0) continue;
                if (hasFrozenGuard) { player.Core.SetGuard(frozenGuard, false); hasFrozenGuard = false; }
                Director.Tick(step); Lock.Tick(step);
                Vector3 direction = MoveDirection();
                if (Lock.Target != null && Lock.Target.IsBoss) { player.Target = Lock.Target.Actor; player.FaceTarget(); }
                else if (direction.sqrMagnitude > .001f && player.Core.CanAct)
                    player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, Quaternion.LookRotation(direction), 720 * step);
                player.Move(direction, settings.playerSpeed, step);
                foreach (var enemy in enemies) if (enemy.gameObject.activeInHierarchy) enemy.Tick(step);
                Physics.SyncTransforms();
                bool exploring = Director.State == EncounterState.Exploration;
                player.Core.AutomaticPostureRecovery = !exploring;
                player.Core.Tick(step);
                foreach (var enemy in enemies) if (enemy.gameObject.activeInHierarchy) enemy.Actor.Core.Tick(step);
                // Sample every simulation slice so a blocked swing cannot skip its active/recovery pose.
                foreach (var animation in animations) if (animation.gameObject.activeInHierarchy) animation.AdvanceVisual(step);
                if (player.Core.State == CombatState.Dead) { Flow.PlayerDied(); break; }
                foreach (var enemy in enemies)
                {
                    if (!enemy.Alive && Defeated.Add(enemy.stableId))
                    {
                        enemy.MarkDead();
                        Progression.Grant(enemy.IsBoss ? 200 : 20);
                        if (enemy.IsBoss)
                        {
                            BossDefeated = true; WorldState.Apply(WorldEventKind.BossDefeated, enemy.stableId); Director.FinishBoss();
                            PersistCombatRewards();
                            player.Core.RestoreVitals(player.Core.Health, player.Core.Posture);
                            Flow.Won(); break;
                        }
                        PersistCombatRewards();
                    }
                }
                if (!Running) break;
                if (exploring) player.Core.RecoverExploration(step);
                saveClock += step;
                saveRetryClock = Mathf.Max(0, saveRetryClock - step);
                if ((pendingSave || saveClock >= 15) && CanSave && saveRetryClock <= 0)
                { pendingSave = !Flow.Save(); saveClock = 0; saveRetryClock = pendingSave ? 3 : 0; }
            }
        }
        public void RequestSave() { pendingSave = true; }
        void OnHit(HitEvent hit)
        {
            if (hit.Result == HitResult.Ignore) return;
            ContactCount++; Director.OnInteraction(hit);
            var attacker = actors[hit.Attacker]; var defender = actors[hit.Defender];
            Vector3 point = attacker.HasContactPoint ? attacker.LastContactPoint : (attacker.transform.position + defender.transform.position) * .5f + Vector3.up;
            feedback.OnContact(new CombatContact(hit, point));
        }
        public bool Visible(CombatActor actor)
        {
            Vector3 origin = gameplayCamera.transform.position, delta = actor.transform.position + Vector3.up * 1.2f - origin;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, sightHits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++)
            { var owner = sightHits[i].collider.GetComponentInParent<CombatActor>(); if (owner != actor && owner != player) return false; }
            return true;
        }
        bool IsBossPerilousStartup()
        {
            if (Director.State != EncounterState.BossCombat || Flow.State != GameFlowState.Playing || Flow.Paused) return false;
            foreach (var enemy in enemies)
                if (enemy.IsBoss && enemy.Alive && enemy.Actor.Core.State == CombatState.AttackStartup &&
                    enemy.Actor.Core.ActiveAttack.Kind == AttackKind.Perilous) return true;
            return false;
        }
        MvpHudData HudData()
        {
            var core = player.Core;

            var data = new MvpHudData { visible = Flow.State == GameFlowState.Playing && !Flow.Paused,
                health = core.Health, maxHealth = core.Tuning.maxHealth, posture = core.Posture, maxPosture = core.Tuning.maxPosture,
                charge = core.ChargeRatio, charging = core.IsPreparing, camera = gameplayCamera,
                level = Progression.Level, experience = Progression.Experience, nextLevelCost = Progression.NextCost,
                vitality = Progression.Vitality, resolve = Progression.Resolve, power = Progression.Power,
                attackMultiplier = Progression.AttackMultiplier,
                bonfirePrompt = Interactions?.Prompt,
                objective = Quests.Objective(Snapshot()),
                encounter = Director.State.ToString().ToUpperInvariant(), message = Flow.Message,
                debug = $"{core.State} | Enemies: {Director.Count} | Disengage: {Director.Remaining:F1}s | Contacts: {ContactCount} | Aim: {AimDecision}" };
            if (Lock.Target != null) data.lockPoint = Lock.Target.transform.position + Vector3.up * 1.35f;
            var bars = new List<MvpEnemyBar>();
            foreach (var enemy in enemies)
            {
                if (!enemy.Alive) continue;
                var c = enemy.Actor.Core;
                if (enemy.IsBoss)
                {
                    if (Director.State == EncounterState.BossCombat)
                    {
                        data.bossName = enemy.definition.displayName; data.bossHealth = c.Health;
                        data.bossMaxHealth = c.Tuning.maxHealth; data.bossPosture = c.Posture;
                        data.bossMaxPosture = c.Tuning.maxPosture;
                        data.perilousWarning = IsBossPerilousStartup();
                    }
                }
                else bars.Add(new MvpEnemyBar { name = enemy.definition.displayName, status = enemy.Activity + " / " + c.State, point = enemy.transform.position + Vector3.up * 2f,
                    health = c.Health, maxHealth = c.Tuning.maxHealth, posture = c.Posture, maxPosture = c.Tuning.maxPosture,
                    visible = Vector3.Distance(player.transform.position, enemy.transform.position) <= 20 && Visible(enemy.Actor) });
            }
            data.enemies = bars.ToArray(); return data;
        }
        void OnDestroy()
        {
            WorldState.Changed -= OnWorldChanged; if (ownsQuest) Destroy(quest);
            input?.Dispose(); if (player != null && player.Core != null) player.Core.HitResolved -= OnHit;
        }
    }
}
