using System;
using System.Collections.Generic;

namespace Milkfrog.CombatDemo
{
    public enum CombatState { Neutral, Guard, AttackStartup, AttackActive, AttackRecovery, HitStun, DeflectedStun, PostureBroken, Dead, AttackPrepare, Charging, Dodge }
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
        public float dodgeDuration = .38f, dodgeMoveDuration = .24f, dodgeDistance = 1.6f;
        public float invulnerableStart = .08f, invulnerableEnd = .16f;
        public float prepareThreshold = .18f, fullChargeTime = .80f;
        public float recoveryCancel = .16f;
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
        public bool AutomaticPostureRecovery { get; set; } = true;
        public float Posture { get; private set; }
        public float Remaining { get; private set; }
        public float StateDuration { get; private set; }
        public float StateProgress => StateDuration <= 0 ? 0 : Math.Min(1, Math.Max(0, 1 - Remaining / StateDuration));
        public float DeflectRemaining { get; private set; }
        public int AttackId { get; private set; }
        public AttackSnapshot ActiveAttack { get; private set; }
        readonly AttackParameters lightDefinition, thrustDefinition, followupDefinition;
        float chargeElapsed;
        public float ChargeElapsed => chargeElapsed;
        public float ChargeRatio => IsPreparing ? Math.Min(1,Math.Max(0,(chargeElapsed-Tuning.prepareThreshold)/Math.Max(.001f,Tuning.fullChargeTime-Tuning.prepareThreshold))) : 0;
        public float ReleasedCharge { get; private set; }
        public bool CanAct => State == CombatState.Neutral || State == CombatState.Guard;
        public bool IsAttacking => State == CombatState.AttackStartup || State == CombatState.AttackActive || State == CombatState.AttackRecovery;
        public bool DeflectOpen => State == CombatState.Guard && DeflectRemaining > 0;
        public bool IsPreparing => State == CombatState.AttackPrepare || State == CombatState.Charging;
        public float DodgeElapsed => State == CombatState.Dodge ? StateDuration-Remaining : 0;
        public float DodgeProgress => State == CombatState.Dodge ? StateProgress : 0;
        public bool IsInvulnerable => State == CombatState.Dodge && DodgeElapsed >= Tuning.invulnerableStart-.000001f && DodgeElapsed < Tuning.invulnerableEnd-.000001f;
        public bool CanCancelRecovery => State == CombatState.AttackRecovery && StateProgress * StateDuration <= ActiveAttack.CancelWindow + .000001f;
        public float ComboRemaining { get; private set; }
        public bool ComboOpen => ComboRemaining > 0;
        public event Action<CombatState, CombatState> StateChanged;
        public event Action<HitEvent> HitResolved;
        public event Action ActiveSample;
        // Covers the complete active interval, including its endpoint on a slow frame.
        public event Action<float, float> ActiveInterval;
        public event Action<float, float> DodgeInterval;
        // AI may select a parry opportunity, but cannot bypass the shared facing/Guard checks.
        public Func<bool> DeflectPolicy;
        readonly HashSet<CombatCore> hitTargets = new HashSet<CombatCore>();
        bool guardHeld;
        float sinceInteraction;

        public CombatCore(CombatTuning tuning,AttackParameters light=null,AttackParameters thrust=null,AttackParameters followup=null)
        { Tuning = tuning; lightDefinition=light; thrustDefinition=thrust??AttackParameters.Thrust(); followupDefinition=followup; Reset(); }

        public void Reset()
        {
            guardHeld = false;
            DeflectRemaining = 0;
            sinceInteraction = 0;
            AttackId = 0;
            chargeElapsed=ReleasedCharge=0;
            ComboRemaining=0;
            ActiveAttack=lightDefinition!=null?lightDefinition.Snapshot(0):AttackSnapshot.Light(Tuning);
            hitTargets.Clear();
            Health = Tuning.maxHealth;
            Posture = 0;
            Change(CombatState.Neutral);
        }

        public void RestoreVitals(float health, float posture)
        {
            if (float.IsNaN(health) || float.IsInfinity(health) || float.IsNaN(posture) || float.IsInfinity(posture))
                throw new ArgumentOutOfRangeException(nameof(health));
            Reset();
            Health = Math.Min(Tuning.maxHealth, Math.Max(0, health));
            Posture = Math.Min(Tuning.maxPosture, Math.Max(0, posture));
            if (Health <= 0) Change(CombatState.Dead);
        }

        public void RecoverExploration(float dt, float healthRate = 5, float postureRate = 20)
        {
            if (dt < 0 || float.IsNaN(dt) || float.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            if (!CanAct) return;
            Health = Math.Min(Tuning.maxHealth, Health + Math.Max(0, healthRate) * dt);
            Posture = Math.Max(0, Posture - Math.Max(0, postureRate) * dt);
        }

        public void SetGuard(bool held, bool pressed)
        {
            bool freshPress = pressed && !guardHeld;
            guardHeld = held;
            if (held && IsPreparing) CancelPreparation();
            if (held) TryCancelRecovery();
            if (!CanAct) return;
            if (!held) { if (State == CombatState.Guard) Change(CombatState.Neutral); return; }
            if (State != CombatState.Guard) Change(CombatState.Guard);
            if (freshPress) DeflectRemaining = Math.Max(0, Tuning.deflectWindow);
        }

        public bool RequestAttack()
        {
            if (!CanAct) return false;
            BeginAttack(lightDefinition!=null?lightDefinition.Snapshot(0):AttackSnapshot.Light(Tuning),0);
            return true;
        }
        public bool RequestFollowup()
        {
            if (!ComboOpen || followupDefinition == null) return false;
            ComboRemaining = 0;
            BeginAttack(followupDefinition.Snapshot(0),0);
            return true;
        }
        public bool RequestDefinedAttack(AttackParameters definition)
        {
            if (!CanAct || definition == null) return false;
            BeginAttack(definition.Snapshot(0),0);
            return true;
        }
        public bool TryCancelRecovery()
        {
            if (!CanCancelRecovery) return false;
            ComboRemaining = 0;
            ReturnToReady();
            return true;
        }
        void BeginAttack(AttackSnapshot attack,float credit)
        {
            ActiveAttack=attack;
            hitTargets.Clear();
            AttackId++;
            Change(CombatState.AttackStartup,attack.Startup);
            Remaining=Math.Max(0,Remaining-credit);
        }
        public bool BeginPreparation()
        {
            if(!CanAct)return false;
            guardHeld=false; chargeElapsed=ReleasedCharge=0;
            ActiveAttack=lightDefinition!=null?lightDefinition.Snapshot(0):AttackSnapshot.Light(Tuning);
            Change(CombatState.AttackPrepare,Tuning.prepareThreshold);
            return true;
        }
        public bool ReleaseAttack()
        {
            if(!IsPreparing)return false;
            if(State==CombatState.AttackPrepare) BeginAttack(ActiveAttack,chargeElapsed);
            else {ReleasedCharge=ChargeRatio;BeginAttack((thrustDefinition??AttackParameters.Thrust()).Snapshot(ReleasedCharge),0);}
            return true;
        }
        public void CancelPreparation(){if(IsPreparing)ReturnToReady();}

        public bool RequestDodge()
        {
            if (!CanAct && !IsPreparing && !TryCancelRecovery()) return false;
            ComboRemaining = 0;
            Change(CombatState.Dodge,Tuning.dodgeDuration);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (State == CombatState.Dead) return;
            ComboRemaining = Math.Max(0, ComboRemaining - deltaTime);
            float left = deltaTime;
            // Consume phase boundaries instead of skipping Active on a slow frame.
            for (int transitions = 0; transitions < 12; transitions++)
            {
                if (CanAct) { AdvanceClock(left); return; }
                if (State==CombatState.Charging)
                {
                    chargeElapsed=Math.Min(Tuning.fullChargeTime,chargeElapsed+left);
                    if(Tuning.fullChargeTime-chargeElapsed<.000001f)chargeElapsed=Tuning.fullChargeTime;
                    Remaining=Math.Max(0,Tuning.fullChargeTime-chargeElapsed);
                    AdvanceClock(left);return;
                }
                if (State == CombatState.AttackActive)
                {
                    ActiveSample?.Invoke();
                    if (State != CombatState.AttackActive) return;
                }
                float consumed = Math.Min(left, Remaining);
                if(State==CombatState.AttackPrepare)chargeElapsed+=consumed;
                float activeFrom = StateProgress;
                float dodgeFrom = DodgeElapsed;
                AdvanceClock(consumed);
                Remaining = Math.Max(0, Remaining - consumed);
                if(Remaining < .000001f)Remaining=0;
                left = Math.Max(0, left - consumed);
                if (State == CombatState.Dodge) DodgeInterval?.Invoke(dodgeFrom,DodgeElapsed);
                if (State == CombatState.AttackActive)
                {
                    ActiveInterval?.Invoke(activeFrom, StateProgress);
                    if (State != CombatState.AttackActive) return;
                }
                if (Remaining > 0) return;
                switch (State)
                {
                    case CombatState.AttackPrepare: Change(CombatState.Charging,Math.Max(0,Tuning.fullChargeTime-Tuning.prepareThreshold)); break;
                    case CombatState.AttackStartup: Change(CombatState.AttackActive, ActiveAttack.Active); break;
                    case CombatState.AttackActive: Change(CombatState.AttackRecovery, ActiveAttack.Recovery); break;
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
            if (CanAct && AutomaticPostureRecovery)
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
            if(next!=CombatState.AttackPrepare && next!=CombatState.Charging)chargeElapsed=0;
            Remaining = Math.Max(0, duration);
            StateDuration = Remaining;
            DeflectRemaining = 0;
            if (next != CombatState.AttackActive && next != CombatState.AttackStartup) hitTargets.Clear();
            if (previous == CombatState.AttackRecovery && (next == CombatState.Neutral || next == CombatState.Guard) && lightDefinition != null && lightDefinition.CanComboFrom && ActiveAttack.Kind == AttackKind.Light)
                ComboRemaining = Math.Max(0, lightDefinition.comboWindow);
            else if (next != CombatState.Neutral && next != CombatState.Guard) ComboRemaining = 0;
            else if (previous != CombatState.AttackRecovery) ComboRemaining = 0;
            if (previous != next) StateChanged?.Invoke(previous, next);
        }

        public HitResult TryHit(CombatCore defender, bool defenderFacingAttacker)
        {
            if (State != CombatState.AttackActive || defender == null || defender == this ||
                defender.State == CombatState.Dead || defender.IsInvulnerable || !hitTargets.Add(defender)) return HitResult.Ignore;
            return ResolveHit(defender, defenderFacingAttacker);
        }

        HitResult ResolveHit(CombatCore defender, bool facing)
        {
            sinceInteraction = defender.sinceInteraction = 0;
            HitResult result;
            if (ActiveAttack.Response != AttackResponse.DodgeOnly && facing && defender.State == CombatState.Guard &&
                (defender.DeflectOpen || (defender.DeflectPolicy?.Invoke() ?? false)))
            {
                result = HitResult.Deflect;
                Posture = Math.Min(Tuning.maxPosture, Posture + ActiveAttack.DeflectPosture);
                InterruptWithPriority(CombatState.DeflectedStun, Tuning.deflectedStun);
            }
            else if (facing && defender.State == CombatState.Guard)
            {
                result = HitResult.Block;
                defender.Posture = Math.Min(defender.Tuning.maxPosture, defender.Posture + ActiveAttack.Block);
                if (defender.Posture >= defender.Tuning.maxPosture)
                    defender.InterruptWithPriority(CombatState.HitStun, defender.Tuning.hitStun);
            }
            else
            {
                result = HitResult.Hit;
                defender.Health = Math.Max(0, defender.Health - ActiveAttack.Damage);
                defender.Posture = Math.Min(defender.Tuning.maxPosture, defender.Posture + ActiveAttack.Posture);
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
