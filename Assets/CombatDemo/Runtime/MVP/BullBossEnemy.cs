using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class BullBossEnemy : EnemyController, ICombatHitSampler
    {
        public BullBossDefinition bull;
        public BullPresentation presentation;
        public Vector3 arenaCenter;
        public override bool IsBoss => true;
        public BullMove CurrentMove { get; private set; }
        public bool PhaseTwo { get; private set; }
        public int PhaseChanges { get; private set; }
        public float RoarRemaining { get; private set; }
        public Vector3 LockedDirection { get; private set; }
        public Vector3 LockedPoint { get; private set; }
        public bool TargetLocked { get; private set; }
        public float KickCooldown { get; private set; }
        public float ChargeRemainingDistance => CurrentMove != null && CurrentMove.kind == BullMoveKind.Charge ? Mathf.Max(0,CurrentMove.range-chargeTravel) : 0;
        public Vector3 ChargeWarningEnd => ClampArena(transform.position+LockedDirection*ChargeRemainingDistance)+LockedDirection*BullHitGeometry.ChargeFrontReach;
        public string SlamHint => CurrentMove != null && CurrentMove.kind == BullMoveKind.Slam ? (TargetLocked ? "已锁点 · 离圈" : "危 · 落点追踪") : null;
        public int Repetitions { get; private set; }
        public float ActiveTime => Actor.Core.State == CombatState.AttackActive ? Actor.Core.StateProgress*Actor.Core.ActiveAttack.Active : 0;
        public bool Danger => CurrentMove != null && CurrentMove.attack.response == AttackResponse.DodgeOnly;
        float wait, chargeTravel;
        bool repeatCharge, ignoresPlayer;
        BullMoveKind last;
        bool hasLast;
        int decision;

        public override void Initialize(MvpWorld owner)
        {
            base.Initialize(owner); Actor.HitSampler = this; Actor.Core.StateChanged += Changed;
            ResetPresentation();
        }
        public override void ResetForLoad()
        {
            base.ResetForLoad(); transform.rotation = Quaternion.Euler(0,180,0); CurrentMove = null; PhaseTwo = false; PhaseChanges = 0; RoarRemaining = KickCooldown = wait = 0;
            repeatCharge = hasLast = TargetLocked = false; Repetitions = decision = 0; ResetPresentation();
        }
        void Changed(CombatState previous, CombatState next)
        {
            if (next != CombatState.AttackActive) RestoreCollision();
            if (next == CombatState.Dead || next == CombatState.PostureBroken || next == CombatState.DeflectedStun || next == CombatState.HitStun)
            { CurrentMove = null; repeatCharge = false; RoarRemaining = 0; presentation?.ClearTelegraph(); }
        }
        public override void Tick(float dt)
        {
            if (!Alive) { MarkDead(); return; }
            if (Activity != EnemyActivity.Combat) return;
            KickCooldown = Mathf.Max(0,KickCooldown-dt);
            if (CurrentMove != null)
            {
                if (Actor.Core.IsAttacking)
                {
                    if (Actor.Core.State == CombatState.AttackStartup && !TargetLocked)
                    {
                        TrackTarget();
                        if (Actor.Core.Remaining <= CurrentMove.lockBeforeImpact + dt) TargetLocked = true;
                    }
                    return;
                }
                CurrentMove = null; presentation?.ClearTelegraph();
                if (repeatCharge && Actor.Core.CanAct) { repeatCharge = false; StartMove(BullMoveKind.Charge,true); return; }
                wait = bull.decisionDelay;
            }
            if (!Actor.Core.CanAct) return;
            if (RoarRemaining > 0) { RoarRemaining = Mathf.Max(0,RoarRemaining-dt); return; }
            if (!PhaseTwo && Actor.Core.Health <= Actor.Core.Tuning.maxHealth*bull.phaseHealthRatio)
            { PhaseTwo = true; PhaseChanges++; hasLast = false; Repetitions = 0; RoarRemaining = bull.roarDuration; wait = bull.decisionDelay; return; }
            if (wait > 0) { wait -= dt; return; }
            var delta = Vector3.ProjectOnPlane(world.player.transform.position-transform.position,Vector3.up);
            float distance = delta.magnitude;
            bool rear = Vector3.Dot(transform.forward,delta.normalized) < -.35f;
            var choice = Choose(distance,rear);
            if (choice.HasValue) { TryStartMove(choice.Value); return; }
            if (distance > .1f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(delta),bull.turnSpeed*dt);
                Actor.Move(delta.normalized,bull.speed,dt);
            }
        }
        BullMoveKind? Choose(float distance, bool rear)
        {
            if (rear && distance <= bull.Get(BullMoveKind.Kick).range && KickCooldown <= 0 && CanRepeat(BullMoveKind.Kick)) return BullMoveKind.Kick;
            BullMoveKind[] choices = distance > 8 ? new[]{BullMoveKind.Charge} : distance > 3 ?
                new[]{BullMoveKind.Charge,BullMoveKind.Slam,BullMoveKind.Wave} : new[]{BullMoveKind.Horn,BullMoveKind.Slam,BullMoveKind.Wave};
            for(int i=0;i<choices.Length;i++)
            {
                var kind = choices[(decision+i)%choices.Length];
                if (CanRepeat(kind) && distance <= bull.Get(kind).range)
                { decision++; return kind; }
            }
            return null;
        }
        bool CanRepeat(BullMoveKind kind) => !hasLast || last != kind || Repetitions < 2;
        public bool TryStartMove(BullMoveKind kind)
        {
            if (!Alive || Activity != EnemyActivity.Combat || !Actor.Core.CanAct || CurrentMove != null || RoarRemaining > 0 || !CanRepeat(kind)) return false;
            if (kind == BullMoveKind.Kick)
            {
                var delta = world.player.transform.position-transform.position;
                if (KickCooldown > 0 || FlatDistance(world.player.transform.position,transform.position) > bull.Get(kind).range || Vector3.Dot(transform.forward,delta.normalized) >= -.35f) return false;
            }
            return StartMove(kind,false);
        }
        bool StartMove(BullMoveKind kind, bool followup)
        {
            var move = bull.Get(kind); var attack = JsonUtility.FromJson<AttackParameters>(JsonUtility.ToJson(move.attack));
            if (kind == BullMoveKind.Charge && PhaseTwo && !followup) { attack.recovery = .25f; repeatCharge = true; }
            if (kind == BullMoveKind.Wave && PhaseTwo) attack.active += bull.waveRepeatGap;
            CurrentMove = move; TargetLocked = false; chargeTravel = 0;
            if (kind != BullMoveKind.Kick) FacePlayer();
            TrackTarget();
            if (!Actor.Core.RequestDefinedAttack(attack)) { CurrentMove = null; return false; }
            Repetitions = hasLast && last == kind ? Repetitions+1 : 1;
            if (kind == BullMoveKind.Wave && PhaseTwo) Repetitions++;
            last = kind; hasLast = true;
            if (kind == BullMoveKind.Kick) KickCooldown = move.cooldown;
            presentation?.BeginMove(); return true;
        }
        void FacePlayer()
        {
            var delta = Vector3.ProjectOnPlane(world.player.transform.position-transform.position,Vector3.up);
            if (delta.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(delta);
        }
        void TrackTarget()
        {
            if (CurrentMove.kind == BullMoveKind.Charge) FacePlayer();
            LockedDirection = CurrentMove.kind == BullMoveKind.Kick ? -transform.forward : transform.forward;
            LockedPoint = CurrentMove.kind == BullMoveKind.Slam ? world.player.transform.position : transform.position;
            LockedPoint = ClampArena(LockedPoint); LockedPoint = new Vector3(LockedPoint.x,.035f,LockedPoint.z);
        }
        Vector3 ClampArena(Vector3 point)
        {
            var delta = Vector3.ProjectOnPlane(point-arenaCenter,Vector3.up);
            return delta.magnitude <= bull.arenaRadius-1.2f ? point : arenaCenter+delta.normalized*(bull.arenaRadius-1.2f)+Vector3.up*point.y;
        }
        public void Sample(float from, float to)
        {
            var move = CurrentMove; if (move == null || Actor.Core.State != CombatState.AttackActive) return;
            var target = world.player; var origin = transform.position; var end = origin;
            if (move.kind == BullMoveKind.Charge)
            {
                if (!ignoresPlayer) { Physics.IgnoreCollision(Actor.Motor,target.Motor,true); ignoresPlayer = true; }
                float travel = Mathf.Min(move.range-chargeTravel,(to-from)*Actor.Core.ActiveAttack.Active*move.moveSpeed);
                end = ClampArena(origin+LockedDirection*travel); Actor.Motor.Move(end-origin); end = transform.position; chargeTravel += FlatDistance(end,origin);
            }
            float radius = target.Motor.radius;
            var point = target.transform.position; bool hit = false;
            switch(move.kind)
            {
                case BullMoveKind.Horn: case BullMoveKind.Kick:
                    hit = BullHitGeometry.Cone(point,origin,LockedDirection,move.range,move.halfAngle,radius); break;
                case BullMoveKind.Charge:
                    hit = BullHitGeometry.SegmentDistance(point,origin,end+LockedDirection*BullHitGeometry.ChargeFrontReach) <= move.halfWidth+radius; break;
                case BullMoveKind.Slam: hit = FlatDistance(point,LockedPoint) <= move.range+radius; break;
                case BullMoveKind.Wave:
                    int waves = PhaseTwo ? 2 : 1;
                    for(int i=0;i<waves;i++)
                    {
                        float a = from*Actor.Core.ActiveAttack.Active-i*bull.waveRepeatGap, b = to*Actor.Core.ActiveAttack.Active-i*bull.waveRepeatGap;
                        if (b < 0 || a > move.attack.active || point.y-arenaCenter.y >= move.jumpClearance) continue;
                        if (BullHitGeometry.Ring(point,LockedPoint,Mathf.Clamp01(a/move.attack.active)*move.range,Mathf.Clamp01(b/move.attack.active)*move.range,move.halfWidth,radius))
                            Actor.ResolveCustomHit(target,point+Vector3.up*.4f,i+1);
                    }
                    return;
            }
            if (hit && Mathf.Abs(point.y-origin.y) < 2.8f) Actor.ResolveCustomHit(target,point+Vector3.up);
        }
        public void AdvancePresentation(float dt) => presentation?.Advance(dt);
        public void ResetPresentation() { if (presentation != null) presentation.Initialize(this); }
        void RestoreCollision() { if (ignoresPlayer && Actor != null && world != null) Physics.IgnoreCollision(Actor.Motor,world.player.Motor,false); ignoresPlayer = false; }
        void OnDisable() { RestoreCollision(); presentation?.ClearTelegraph(); }
    }
}
