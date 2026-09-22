using System;
using System.Collections.Generic;

namespace Milkfrog.CombatDemo
{
    public enum CombatState { Neutral, Guard, AttackStartup, AttackActive, AttackRecovery, HitStun, DeflectedStun, PostureBroken, Dead }
    public enum HitResult { Ignore, Hit, Block, Deflect, Deathblow }
    public enum EnemyMode { Dummy, Rhythm, Duel }

    [Serializable]
    public sealed class CombatTuning
    {
        public float maxHealth = 100, maxPosture = 100;
        public float startup = .25f, active = .10f, recovery = .35f;
        public float range = 2, attackHalfWidth = .65f, guardAngle = 120;
        public float deflectWindow = .15f, hitStun = .30f, deflectedStun = .20f;
        public float hitDamage = 10, hitPosture = 10, blockPosture = 20, deflectPosture = 30;
        public float brokenDuration = 2, postureRecoveryDelay = 2, postureRecoveryRate = 15;
    }

    public readonly struct HitEvent
    {
        public readonly CombatCore Attacker, Defender;
        public readonly HitResult Result;
        public HitEvent(CombatCore attacker, CombatCore defender, HitResult result)
        { Attacker = attacker; Defender = defender; Result = result; }
    }

    // This is the only owner of combat state. Neither input nor AI can assign State.
    public sealed class CombatCore
    {
        public CombatTuning Tuning { get; }
        public CombatState State { get; private set; }
        public float Health { get; private set; }
        public float Posture { get; private set; }
        public float Remaining { get; private set; }
        public float StateDuration { get; private set; }
        public float StateProgress => StateDuration <= 0 ? 0 : Math.Min(1, Math.Max(0, 1 - Remaining / StateDuration));
        public float DeflectRemaining { get; private set; }
        public int AttackId { get; private set; }
        public bool CanAct => State == CombatState.Neutral || State == CombatState.Guard;
        public bool IsAttacking => State == CombatState.AttackStartup || State == CombatState.AttackActive || State == CombatState.AttackRecovery;
        public bool DeflectOpen => State == CombatState.Guard && DeflectRemaining > 0;
        public event Action<CombatState, CombatState> StateChanged;
        public event Action<HitEvent> HitResolved;
        public event Action ActiveSample;
        // AI may select a parry opportunity, but cannot bypass the shared facing/Guard checks.
        public Func<bool> DeflectPolicy;
        readonly HashSet<CombatCore> hitTargets = new HashSet<CombatCore>();
        bool guardHeld;
        float sinceInteraction;

        public CombatCore(CombatTuning tuning) { Tuning = tuning; Reset(); }

        public void Reset()
        {
            guardHeld = false;
            DeflectRemaining = 0;
            sinceInteraction = 0;
            AttackId = 0;
            hitTargets.Clear();
            Health = Tuning.maxHealth;
            Posture = 0;
            Change(CombatState.Neutral);
        }

        public void SetGuard(bool held, bool pressed)
        {
            bool freshPress = pressed && !guardHeld;
            guardHeld = held;
            if (!CanAct) return;
            if (!held) { if (State == CombatState.Guard) Change(CombatState.Neutral); return; }
            if (State != CombatState.Guard) Change(CombatState.Guard);
            if (freshPress) DeflectRemaining = Math.Max(0, Tuning.deflectWindow);
        }

        public bool RequestAttack()
        {
            if (!CanAct) return false;
            hitTargets.Clear();
            AttackId++;
            Change(CombatState.AttackStartup, Tuning.startup);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (State == CombatState.Dead) return;
            float left = deltaTime;
            // Consume phase boundaries instead of skipping Active on a slow frame.
            for (int transitions = 0; transitions < 12; transitions++)
            {
                if (CanAct) { AdvanceClock(left); return; }
                if (State == CombatState.AttackActive)
                {
                    ActiveSample?.Invoke();
                    if (State != CombatState.AttackActive) return;
                }
                float consumed = Math.Min(left, Remaining);
                AdvanceClock(consumed);
                Remaining = Math.Max(0, Remaining - consumed);
                left = Math.Max(0, left - consumed);
                if (Remaining > 0) return;
                switch (State)
                {
                    case CombatState.AttackStartup: Change(CombatState.AttackActive, Tuning.active); break;
                    case CombatState.AttackActive: Change(CombatState.AttackRecovery, Tuning.recovery); break;
                    case CombatState.PostureBroken: Posture = 0; ReturnToReady(); break;
                    case CombatState.Dead: return;
                    default: ReturnToReady(); break;
                }
                // Entering Active must sample even when exactly at the phase boundary.
                if (left <= 0 && State != CombatState.AttackActive) return;
            }
        }

        void AdvanceClock(float dt)
        {
            float before = sinceInteraction;
            sinceInteraction += dt;
            DeflectRemaining = Math.Max(0, DeflectRemaining - dt);
            if (CanAct)
            {
                float recoverTime = Math.Max(0, sinceInteraction - Math.Max(before, Tuning.postureRecoveryDelay));
                Posture = Math.Max(0, Posture - recoverTime * Tuning.postureRecoveryRate);
            }
        }

        void ReturnToReady() => Change(guardHeld ? CombatState.Guard : CombatState.Neutral);

        void Change(CombatState next, float duration = 0)
        {
            var previous = State;
            State = next;
            Remaining = Math.Max(0, duration);
            StateDuration = Remaining;
            DeflectRemaining = 0;
            if (next != CombatState.AttackActive && next != CombatState.AttackStartup) hitTargets.Clear();
            if (previous != next) StateChanged?.Invoke(previous, next);
        }

        public HitResult TryHit(CombatCore defender, bool defenderFacingAttacker)
        {
            if (State != CombatState.AttackActive || defender == null || defender == this ||
                defender.State == CombatState.Dead || !hitTargets.Add(defender)) return HitResult.Ignore;
            return ResolveHit(defender, defenderFacingAttacker);
        }

        HitResult ResolveHit(CombatCore defender, bool facing)
        {
            sinceInteraction = defender.sinceInteraction = 0;
            HitResult result;
            if (facing && defender.State == CombatState.Guard &&
                (defender.DeflectOpen || (defender.DeflectPolicy?.Invoke() ?? false)))
            {
                result = HitResult.Deflect;
                Posture = Math.Min(Tuning.maxPosture, Posture + Tuning.deflectPosture);
                InterruptWithPriority(CombatState.DeflectedStun, Tuning.deflectedStun);
            }
            else if (facing && defender.State == CombatState.Guard)
            {
                result = HitResult.Block;
                defender.Posture = Math.Min(defender.Tuning.maxPosture, defender.Posture + Tuning.blockPosture);
                if (defender.Posture >= defender.Tuning.maxPosture)
                    defender.InterruptWithPriority(CombatState.HitStun, defender.Tuning.hitStun);
            }
            else
            {
                result = HitResult.Hit;
                defender.Health = Math.Max(0, defender.Health - Tuning.hitDamage);
                defender.Posture = Math.Min(defender.Tuning.maxPosture, defender.Posture + Tuning.hitPosture);
                defender.InterruptWithPriority(CombatState.HitStun, defender.Tuning.hitStun);
            }
            Emit(defender, result);
            return result;
        }

        void InterruptWithPriority(CombatState ordinary, float duration)
        {
            if (Health <= 0) { Change(CombatState.Dead); return; }
            // Further hits cannot renew an existing break or downgrade it to HitStun.
            if (State == CombatState.PostureBroken) return;
            if (Posture >= Tuning.maxPosture) { Change(CombatState.PostureBroken, Tuning.brokenDuration); return; }
            Change(ordinary, duration);
        }

        public bool TryDeathblow(CombatCore target, bool isPlayer, float distance, bool targetInFront)
        {
            if (!isPlayer || !CanAct || target == null || target == this ||
                target.State != CombatState.PostureBroken || distance > Tuning.range || !targetInFront) return false;
            target.Health = 0;
            target.Change(CombatState.Dead);
            Emit(target, HitResult.Deathblow);
            return true;
        }

        void Emit(CombatCore defender, HitResult result)
        {
            var hit = new HitEvent(this, defender, result);
            HitResolved?.Invoke(hit);
            defender.HitResolved?.Invoke(hit);
        }
    }
}
