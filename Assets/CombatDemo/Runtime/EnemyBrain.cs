using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class EnemyBrain
    {
        readonly CombatActor actor;
        readonly CombatDemoSettings settings;
        public EnemyMode Mode { get; private set; }
        public int Blocks { get; private set; }
        public int PatternIndex { get; private set; }
        public AttackPattern NextPattern { get; private set; } = AttackPattern.Slash;
        public string Decision { get; private set; } = "Wait";
        public float WaitRemaining => Mathf.Max(0, settings.enemyWait - waited);
        float waited, counterRemaining = -1;
        System.Random random;
        bool previousAttackWasHeavy;

        public EnemyBrain(CombatActor actor, CombatDemoSettings settings, int? randomSeed = null)
        {
            this.actor = actor;
            this.settings = settings;
            random = randomSeed.HasValue ? new System.Random(randomSeed.Value) : new System.Random();
            actor.Core.DeflectPolicy = () => Mode == EnemyMode.Duel && Blocks >= settings.blocksBeforeDeflect;
            actor.Core.HitResolved += OnHit;
            actor.Core.StateChanged += OnState;
        }

        public void Reset(EnemyMode mode)
        {
            Mode = mode;
            Blocks = 0;
            PatternIndex = 0;
            previousAttackWasHeavy = false;
            NextPattern = AttackPattern.Slash;
            waited = 0;
            counterRemaining = -1;
            Decision = mode.ToString();
        }

        public void SetRandomSeed(int seed) { random = new System.Random(seed); previousAttackWasHeavy = false; }

        void OnState(CombatState previous, CombatState next)
        {
            if (next == CombatState.HitStun || next == CombatState.DeflectedStun ||
                next == CombatState.PostureBroken || next == CombatState.Dead)
                counterRemaining = -1;
        }

        void OnHit(HitEvent hit)
        {
            waited = 0;
            if (hit.Defender == actor.Core)
            {
                if (hit.Result == HitResult.Block) Blocks++;
                if (hit.Result == HitResult.Hit) Blocks = 0;
                if (hit.Result == HitResult.Deflect)
                {
                    Blocks = 0;
                    counterRemaining = settings.counterDelay;
                }
            }
            else if (hit.Result == HitResult.Deflect) Blocks = 0;
        }

        public void Tick(float dt)
        {
            if (actor.Core.State == CombatState.Dead || actor.Target.Core.State == CombatState.Dead)
            { Decision = "Finished"; return; }
            if (!actor.Core.CanAct) { Decision = actor.Core.State.ToString(); return; }
            if (Mode == EnemyMode.Dummy)
            { actor.Core.SetGuard(false, false); Decision = "Training dummy"; return; }
            actor.FaceTarget();
            bool near = actor.DistanceToTarget <= actor.EngageDistance;
            if (counterRemaining >= 0)
            {
                counterRemaining -= dt;
                Decision = "Counter pending";
                if (counterRemaining <= 0)
                {
                    counterRemaining = -1;
                    BeginSelectedAttack(AttackPattern.Slash);
                    Decision = "Counterattack";
                }
                return;
            }
            if (!near)
            {
                actor.Core.SetGuard(false, false);
                actor.Move(actor.Target.transform.position - actor.transform.position, settings.enemySpeed, dt);
                waited = 0;
                Decision = "Approach";
                return;
            }
            actor.Core.SetGuard(Mode == EnemyMode.Duel, false);
            if (!settings.randomAttacks) NextPattern = ChoosePattern();
            Decision = (Mode == EnemyMode.Duel ? (Blocks >= settings.blocksBeforeDeflect ? "Deflect ready" : "Guard / wait") : "Rhythm / wait") +
                (settings.randomAttacks ? " / random" : " / " + NextPattern);
            waited += dt;
            if (waited >= settings.enemyWait)
            {
                waited = 0;
                BeginSelectedAttack(settings.randomAttacks ? ChoosePattern() : NextPattern);
                Decision = "Attack " + actor.Core.ActiveAttack.Kind;
            }
        }

        AttackPattern ChoosePattern()
        {
            if (settings.randomAttacks)
            {
                if (previousAttackWasHeavy) return AttackPattern.Slash;
                float slash = Mathf.Max(0, settings.slashWeight), slow = Mathf.Max(0, settings.slowWeight), perilous = Mathf.Max(0, settings.perilousWeight);
                float total = slash + slow + perilous;
                if (total <= 0) return AttackPattern.Slash;
                double roll = random.NextDouble() * total;
                return roll < slash ? AttackPattern.Slash : roll < slash + slow ? AttackPattern.Slow : AttackPattern.Perilous;
            }
            var pattern = settings.enemyPattern;
            if (pattern == null || pattern.Length == 0 || Mode == EnemyMode.Duel && !settings.usePatternInDuel) return AttackPattern.Slash;
            return pattern[PatternIndex % pattern.Length];
        }

        void BeginSelectedAttack(AttackPattern pattern)
        {
            if (pattern == AttackPattern.Slow && actor.slowAttack == null ||
                pattern == AttackPattern.Perilous && actor.perilousAttack == null)
                pattern = AttackPattern.Slash;
            var definition = actor.AttackFor(pattern);
            bool defined = definition != null && actor.Core.RequestDefinedAttack(definition.rules);
            if (!defined) { pattern = AttackPattern.Slash; if (!actor.Core.RequestAttack()) return; }
            NextPattern = pattern;
            previousAttackWasHeavy = pattern == AttackPattern.Slow || pattern == AttackPattern.Perilous;
            if (!settings.randomAttacks && defined && (Mode != EnemyMode.Duel || settings.usePatternInDuel) && settings.enemyPattern != null && settings.enemyPattern.Length > 0)
                PatternIndex = (PatternIndex + 1) % settings.enemyPattern.Length;
        }
    }
}
