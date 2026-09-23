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
        public CombatFeedback feedback;
        public GameObject arena;
        public Vector3 spawn = new Vector3(0, .02f, -35);
        public EncounterDirector Director { get; private set; }
        public LockOnController Lock { get; private set; }
        public GameFlowController Flow { get; private set; }
        public bool manualSimulation;
        public bool BossDefeated { get; private set; }
        public int ContactCount { get; private set; }
        public readonly HashSet<string> Defeated = new HashSet<string>();
        public bool CanSave => Director.State == EncounterState.Exploration && player.Core.State == CombatState.Neutral;
        public bool Running => Flow != null && Flow.State == GameFlowState.Playing && !Flow.Paused;
        readonly Dictionary<CombatCore, CombatActor> actors = new Dictionary<CombatCore, CombatActor>();
        readonly List<CombatAnimationPresenter> animations = new List<CombatAnimationPresenter>();
        readonly RaycastHit[] sightHits = new RaycastHit[32];
        DemoInput input;
        Vector2 movement;
        bool needsRelease, pendingSave, frozenGuard, hasFrozenGuard;
        float saveClock;
        MvpHud hud;

        void Awake()
        {
            Flow = GetComponent<GameFlowController>();
            player.enforceFactions = true;
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
            cameraRig.freeOrbit = true; cameraRig.lockOn = false; cameraRig.enemy = null;
            hud = GetComponent<MvpHud>(); if (hud != null) hud.Read = HudData;
            input = new DemoInput("<Mouse>/middleButton");
        }
        void Register(CombatActor actor)
        {
            actors.Add(actor.Core, actor);
            // Every hit is emitted by both cores; only the player subscription forwards it.
            if (actor == player) actor.Core.HitResolved += OnHit;
        }
        public void Restore(PlayerSnapshot snapshot)
        {
            Director = new EncounterDirector(this); saveClock = 0; pendingSave = false;
            foreach (var enemy in enemies) enemy.ResetForLoad();
            Defeated.Clear(); BossDefeated = snapshot != null && snapshot.bossDefeated;
            if (snapshot?.defeatedEnemyIds != null) foreach (string id in snapshot.defeatedEnemyIds) Defeated.Add(id);
            Vector3 position = snapshot == null ? spawn : snapshot.position;
            if (Mathf.Abs(position.x) > 38 || Mathf.Abs(position.z) > 48 || position.y < -1 || position.y > 4 ||
                (!BossDefeated && enemies.Length > 0 && InBossTrigger(position))) position = spawn;
            position.y = .02f;
            player.Motor.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0, snapshot?.yaw ?? 0, 0));
            player.Motor.enabled = true;
            player.Core.RestoreVitals(snapshot?.health ?? settings.player.maxHealth, snapshot?.posture ?? 0);
            foreach (var enemy in enemies)
                if (Defeated.Contains(enemy.stableId) || (enemy.IsBoss && BossDefeated))
                { enemy.Actor.Core.RestoreVitals(0, 0); enemy.MarkDead(); enemy.gameObject.SetActive(false); }
            if (arena != null) arena.SetActive(false);
            Lock.Clear(); cameraRig.yaw = player.transform.eulerAngles.y; cameraRig.pitch = 12; cameraRig.ResetImpulse();
            ClearInput(); feedback.ResetFeedback();
            foreach (var animation in animations) if (animation.gameObject.activeInHierarchy) animation.ResetVisuals();
            Physics.SyncTransforms(); hud?.Refresh();
        }
        bool InBossTrigger(Vector3 position)
        {
            foreach (var enemy in enemies) if (enemy.IsBoss && !Defeated.Contains(enemy.stableId) &&
                EnemyController.FlatDistance(position, enemy.Home) <= enemy.definition.bossTriggerRadius) return true;
            return false;
        }
        public PlayerSnapshot Snapshot() => new PlayerSnapshot
        {
            position = player.transform.position, yaw = player.transform.eulerAngles.y,
            health = player.Core.Health, posture = player.Core.Posture,
            defeatedEnemyIds = new List<string>(Defeated).ToArray(), bossDefeated = BossDefeated
        };
        void Update()
        {
            if (manualSimulation || input == null) return;
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame && hud != null) hud.showDebug = !hud.showDebug;
            if (input.PausePressed && Flow.State == GameFlowState.Playing) { Flow.TogglePause(); return; }
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
        public void ClearInput()
        {
            movement = Vector2.zero; needsRelease = true; hasFrozenGuard = frozenGuard = false;
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
            if (feedback.Clock.Remaining > 0)
            {
                frozenGuard = frame.guardHeld; hasFrozenGuard = true;
                if (frame.attackPressed) needsRelease = true;
                if (frame.attackReleased || !frame.attackHeld) player.Core.CancelPreparation();
                return;
            }
            hasFrozenGuard = false;
            if (Lock.Target != null) player.FaceTarget();
            if (frame.dodgePressed && player.RequestDodge(MoveDirection()))
            { player.Core.SetGuard(frame.guardHeld, false); needsRelease = frame.attackHeld; return; }
            player.Core.SetGuard(frame.guardHeld, frame.guardPressed);
            if (frame.guardHeld) return;
            if (frame.attackPressed)
            {
                player.Target = ExecutionTarget();
                if (player.Core.ComboOpen && (player.Target == null || player.Target.Core.State != CombatState.PostureBroken) && player.Core.RequestFollowup())
                { needsRelease = true; return; }
                if (!needsRelease) { player.BeginPlayerAttack(); needsRelease = true; }
                player.Target = Lock.Target == null ? null : Lock.Target.Actor;
            }
            if (frame.attackReleased) player.Core.ReleaseAttack();
        }
        CombatActor ExecutionTarget()
        {
            CombatActor best = null; float distance = float.PositiveInfinity;
            foreach (var enemy in enemies)
            {
                var actor = enemy.Actor;
                if (!enemy.Alive || !actor.AcceptsDamage || actor.Core.State != CombatState.PostureBroken) continue;
                Vector3 offset = actor.transform.position - player.transform.position;
                float d = Vector3.ProjectOnPlane(offset, Vector3.up).magnitude;
                if (d > player.Core.Tuning.range || Mathf.Abs(offset.y) > player.maxTargetHeightDifference ||
                    !CombatActor.IsInFront(player.transform.forward, offset, 120) || !player.HasLineOfSight(actor, actor.transform.position + Vector3.up)) continue;
                if (Lock.Target == enemy) return actor;
                if (d < distance) { best = actor; distance = d; }
            }
            return best ?? Lock.Target?.Actor;
        }
        public void Simulate(float dt)
        {
            if (dt < 0 || float.IsNaN(dt) || float.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            if (!Running) return;
            float visual = 0;
            while (dt > .000001f && Running)
            {
                float step = Mathf.Min(dt, 1f / 120); dt -= step;
                feedback.TickReal(step); step = feedback.Clock.Consume(step); visual += step;
                if (step <= 0) continue;
                if (hasFrozenGuard) { player.Core.SetGuard(frozenGuard, false); hasFrozenGuard = false; }
                Director.Tick(step); Lock.Tick(step);
                Vector3 direction = MoveDirection();
                if (Lock.Target != null) { player.Target = Lock.Target.Actor; player.FaceTarget(); }
                else if (direction.sqrMagnitude > .001f && player.Core.CanAct)
                    player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, Quaternion.LookRotation(direction), 720 * step);
                player.Move(direction, settings.playerSpeed, step);
                foreach (var enemy in enemies) if (enemy.gameObject.activeInHierarchy) enemy.Tick(step);
                Physics.SyncTransforms();
                bool exploring = Director.State == EncounterState.Exploration;
                player.Core.AutomaticPostureRecovery = !exploring;
                player.Core.Tick(step);
                foreach (var enemy in enemies) if (enemy.gameObject.activeInHierarchy) enemy.Actor.Core.Tick(step);
                if (player.Core.State == CombatState.Dead) { Flow.PlayerDied(); break; }
                foreach (var enemy in enemies)
                {
                    if (!enemy.Alive && Defeated.Add(enemy.stableId))
                    {
                        enemy.MarkDead();
                        if (enemy.IsBoss)
                        {
                            BossDefeated = true; Director.FinishBoss();
                            player.Core.RestoreVitals(player.Core.Health, player.Core.Posture);
                            Flow.Won(); break;
                        }
                    }
                }
                if (!Running) break;
                if (exploring) player.Core.RecoverExploration(step);
                saveClock += step;
                if ((pendingSave || saveClock >= 15) && CanSave) { Flow.Save(); pendingSave = false; saveClock = 0; }
            }
            foreach (var animation in animations) if (animation.gameObject.activeInHierarchy) animation.AdvanceVisual(visual);
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
        MvpHudData HudData()
        {
            var core = player.Core;
            var data = new MvpHudData { visible = Flow.State == GameFlowState.Playing && !Flow.Paused,
                health = core.Health, maxHealth = core.Tuning.maxHealth, posture = core.Posture, maxPosture = core.Tuning.maxPosture,
                charge = core.ChargeRatio, charging = core.IsPreparing, camera = gameplayCamera,
                encounter = Director.State.ToString().ToUpperInvariant(), message = Flow.Message,
                debug = $"{core.State} | Enemies: {Director.Count} | Disengage: {Director.Remaining:F1}s | Contacts: {ContactCount}" };
            if (Lock.Target != null) data.lockPoint = Lock.Target.transform.position + Vector3.up * 1.35f;
            var bars = new List<MvpEnemyBar>();
            foreach (var enemy in enemies)
            {
                if (!enemy.Alive) continue;
                var c = enemy.Actor.Core;
                if (enemy.IsBoss)
                {
                    if (Director.State == EncounterState.BossCombat)
                    { data.bossName = enemy.definition.displayName; data.bossHealth = c.Health; data.bossMaxHealth = c.Tuning.maxHealth; data.bossPosture = c.Posture; data.bossMaxPosture = c.Tuning.maxPosture; }
                }
                else bars.Add(new MvpEnemyBar { name = enemy.definition.displayName, status = enemy.Activity + " / " + c.State, point = enemy.transform.position + Vector3.up * 2f,
                    health = c.Health, maxHealth = c.Tuning.maxHealth, posture = c.Posture, maxPosture = c.Tuning.maxPosture,
                    visible = Vector3.Distance(player.transform.position, enemy.transform.position) <= 20 && Visible(enemy.Actor) });
            }
            data.enemies = bars.ToArray(); return data;
        }
        void OnDestroy()
        {
            input?.Dispose(); if (player != null && player.Core != null) player.Core.HitResolved -= OnHit;
        }
    }
}
