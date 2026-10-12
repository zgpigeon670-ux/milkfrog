using UnityEngine;

namespace Milkfrog.CombatDemo
{
    [RequireComponent(typeof(CombatActor))]
    public sealed class MilkfrogCombatDriver : MonoBehaviour
    {
        public CombatActor actor;
        public CombatAnimationPresenter presentation;
        public CombatTuning tuning = new CombatTuning();
        public float moveSpeed = 4f;

        DemoInput input;
        Vector2 movement;
        bool attackNeedsRelease;

        void Awake()
        {
            if (actor == null) actor = GetComponent<CombatActor>();
            if (presentation == null) presentation = GetComponent<CombatAnimationPresenter>();
            actor.Initialize(tuning);
            input = new DemoInput();
        }

        void Start() => presentation?.Initialize();

        void OnDestroy() => input?.Dispose();

        void Update()
        {
            var frame = input.Snapshot;
            if (frame.resetPressed) { actor.ResetActor(); presentation?.ResetVisuals(); return; }
            movement = Vector2.ClampMagnitude(frame.move, 1f);
            if (frame.attackReleased || !frame.attackHeld) attackNeedsRelease = false;
            bool attackPressed = frame.attackPressed && !attackNeedsRelease;
            var previous = actor.Core.State;
            actor.Core.SetAttackHeld(frame.attackHeld);
            if (frame.dodgePressed && actor.RequestDodge(Direction(movement)))
            {
                actor.Core.SetGuard(frame.guardHeld, false);
                attackNeedsRelease = frame.attackHeld;
                presentation?.AdvanceVisual(0, true);
                return;
            }
            if (frame.jumpPressed) actor.TryJump();
            actor.Core.SetGuard(frame.guardHeld, frame.guardPressed);
            if (frame.guardHeld) { attackNeedsRelease = frame.attackHeld; return; }
            if (actor.Core.IsPreparing && (frame.attackReleased || !frame.attackHeld)) actor.Core.ReleaseAttack();
            else if (attackPressed)
            {
                if (actor.Core.CanAct) actor.BeginPlayerAttack();
                attackNeedsRelease = frame.attackHeld;
            }
            if (actor.Core.State != previous) presentation?.AdvanceVisual(0, true);
        }

        void FixedUpdate()
        {
            float step = Time.fixedDeltaTime;
            actor.Move(Direction(movement), moveSpeed, step);
            actor.Core.Tick(step);
            presentation?.AdvanceVisual(step);
        }

        static Vector3 Direction(Vector2 move) => new Vector3(move.x, 0, move.y);
    }
}
