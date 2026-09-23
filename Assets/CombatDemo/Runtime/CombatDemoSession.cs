using System;
using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class CombatDemoSession : MonoBehaviour
    {
        public CombatDemoSettings settings;
        public CombatActor player, enemy;
        public Camera gameplayCamera;
        public EnemyMode mode = EnemyMode.Duel;
        public CombatAnimationPresenter playerAnimation, enemyAnimation;
        public CombatFeedback feedback;
        public EnemyBrain Brain { get; private set; }
        public string LastHit { get; private set; } = "Ready";
        public bool Finished => player.Core.State == CombatState.Dead || enemy.Core.State == CombatState.Dead;
        [NonSerialized] public bool manualSimulation;
        DemoInput input;
        Vector2 movement;
        bool guardAfterFreeze, hasFrozenGuard;
        bool attackNeedsRelease;
        public event Action<HitEvent> CombatEvent;
        public event Action<CombatContact> ContactResolved;
        public event Action RoundReset;
        public bool IsFrozen => feedback != null && feedback.Clock.Remaining > 0;

        void Awake()
        {
            player.Initialize(settings.player);
            enemy.Initialize(settings.enemy);
            player.Target = enemy;
            enemy.Target = player;
            Brain = new EnemyBrain(enemy, settings);
            player.Core.HitResolved += OnHit;
            player.Core.StateChanged += (from, to) => Log("PLAYER " + from + " -> " + to);
            enemy.Core.StateChanged += (from, to) => Log("ENEMY " + from + " -> " + to);
            Brain.Reset(mode);
            if (playerAnimation != null) playerAnimation.Initialize();
            if (enemyAnimation != null) enemyAnimation.Initialize();
        }

        void OnEnable() => input = new DemoInput();
        void OnDisable() { input?.Dispose(); input = null; }
        void OnApplicationFocus(bool focused) { if (!focused) ClearInput(); }
        public void ClearInput()
        {
            movement=Vector2.zero;hasFrozenGuard=guardAfterFreeze=false;attackNeedsRelease=true;
            if(player==null || player.Core==null)return;
            player.Core.SetGuard(false,false);player.Core.CancelPreparation();
        }

        void Update()
        {
            if (manualSimulation || input == null) return;
            if (input.ModePressed) SetMode((EnemyMode)(((int)mode + 1) % 3));
            SubmitInput(input.Snapshot);
            Simulate(Time.unscaledDeltaTime);
        }

        public void SubmitInput(CombatInputFrame frame)
        {
            if(frame.resetPressed){ResetRound();return;}
            if(Finished)return;
            movement=Vector2.ClampMagnitude(frame.move,1);
            if(!frame.attackHeld)attackNeedsRelease=false;
            if(IsFrozen)
            {
                guardAfterFreeze=frame.guardHeld;hasFrozenGuard=true;
                if(frame.attackPressed)attackNeedsRelease=true;
                if(frame.attackReleased || !frame.attackHeld)player.Core.CancelPreparation();
                return;
            }
            hasFrozenGuard=false;
            player.FaceTarget();
            if(frame.dodgePressed && player.RequestDodge(CameraDirection(movement)))
            {player.Core.SetGuard(frame.guardHeld,false);attackNeedsRelease=frame.attackHeld;return;}
            player.Core.SetGuard(frame.guardHeld,frame.guardPressed);
            if(frame.guardHeld)return;
            if(frame.attackPressed && !attackNeedsRelease)
            {player.BeginPlayerAttack();attackNeedsRelease=true;}
            if(frame.attackReleased)player.Core.ReleaseAttack();
        }
        Vector3 CameraDirection(Vector2 direction)
        {
            Vector3 forward=Vector3.ProjectOnPlane(gameplayCamera.transform.forward,Vector3.up).normalized;
            return forward*direction.y+Vector3.Cross(Vector3.up,forward)*direction.x;
        }

        public void SubmitInput(Vector2 move, bool guardHeld, bool guardPressed, bool attackPressed)
        {
            if (Finished) return;
            movement = Vector2.ClampMagnitude(move, 1);
            if (IsFrozen) { guardAfterFreeze = guardHeld; hasFrozenGuard = true; return; }
            hasFrozenGuard = false;
            player.Core.SetGuard(guardHeld, guardPressed);
            player.FaceTarget();
            if (attackPressed) player.RequestPlayerAttack();
        }

        public void Simulate(float dt)
        {
            if (dt < 0 || float.IsNaN(dt) || float.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            // Small shared slices keep the defender window and attacker timing close under variable frame rates.
            float visualDelta = 0;
            while (dt > 0)
            {
                float step = Mathf.Min(dt, 1f / 120f);
                dt -= step;
                if (feedback != null)
                {
                    feedback.TickReal(step);
                    step = feedback.Clock.Consume(step);
                }
                visualDelta += step;
                if (step <= 0 || Finished) continue;
                if (hasFrozenGuard) { player.Core.SetGuard(guardAfterFreeze, false); hasFrozenGuard = false; }
                player.FaceTarget();
                Vector3 forward = Vector3.ProjectOnPlane(gameplayCamera.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                player.Move(forward * movement.y + right * movement.x, settings.playerSpeed, step);
                Brain.Tick(step);
                Physics.SyncTransforms();
                player.Core.Tick(step);
                if (!Finished) enemy.Core.Tick(step);
            }
            if (playerAnimation != null) playerAnimation.AdvanceVisual(visualDelta);
            if (enemyAnimation != null) enemyAnimation.AdvanceVisual(visualDelta);
        }

        public void ResetRound()
        {
            movement = Vector2.zero;
            hasFrozenGuard = guardAfterFreeze = false;
            attackNeedsRelease=false;
            player.ResetActor();
            enemy.ResetActor();
            Brain.Reset(mode);
            LastHit = "Round reset";
            Physics.SyncTransforms();
            if (playerAnimation != null) playerAnimation.ResetVisuals();
            if (enemyAnimation != null) enemyAnimation.ResetVisuals();
            RoundReset?.Invoke();
        }

        public void SetMode(EnemyMode next) { mode = next; ResetRound(); }

        void OnHit(HitEvent hit)
        {
            string attacker = hit.Attacker == player.Core ? "PLAYER" : "ENEMY";
            LastHit = attacker + " -> " + hit.Result.ToString().ToUpperInvariant();
            Log(LastHit);
            CombatEvent?.Invoke(hit);
            var attackerActor = hit.Attacker == player.Core ? player : enemy;
            Vector3 contactPoint = attackerActor.HasContactPoint ? attackerActor.LastContactPoint :
                Vector3.Lerp(player.transform.position, enemy.transform.position, .5f) + Vector3.up * 1.25f;
            ContactResolved?.Invoke(new CombatContact(hit, contactPoint));
        }

        void Log(string message) { if (settings.logEvents) Debug.Log("[CombatDemo] " + message, this); }
    }
}
