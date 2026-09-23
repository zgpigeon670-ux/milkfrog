using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class EnemyBrain
    {
        readonly CombatActor actor;
        readonly CombatDemoSettings settings;
        public EnemyMode Mode { get; private set; }
        public int Blocks { get; private set; }
        public string Decision { get; private set; } = "Wait";
        public float WaitRemaining => Mathf.Max(0, settings.enemyWait - waited);
        float waited, counterRemaining = -1;

        public EnemyBrain(CombatActor actor, CombatDemoSettings settings)
        {
            this.actor = actor;
            this.settings = settings;
            actor.Core.DeflectPolicy = () => Mode == EnemyMode.Duel && Blocks >= settings.blocksBeforeDeflect;
            actor.Core.HitResolved += OnHit;
            actor.Core.StateChanged += OnState;
        }

        public void Reset(EnemyMode mode)
        {
            Mode = mode;
            Blocks = 0;
            waited = 0;
            counterRemaining = -1;
            Decision = mode.ToString();
        }

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
                    actor.Core.RequestAttack();
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
            Decision = Mode == EnemyMode.Duel ? (Blocks >= settings.blocksBeforeDeflect ? "Deflect ready" : "Guard / wait") : "Rhythm / wait";
            waited += dt;
            if (waited >= settings.enemyWait)
            {
                waited = 0;
                actor.Core.RequestAttack();
                Decision = "Attack";
            }
        }
    }
}
