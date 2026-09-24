using UnityEngine;

namespace Milkfrog.CombatDemo
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CombatActor : MonoBehaviour
    {
        public bool isPlayer;
        public bool enforceFactions;
        // Used by the MVP player only. Training actors retain directional guard rules.
        public bool autoFaceGuardAttacks;
        public bool AcceptsDamage { get; set; } = true;
        public CombatBladeTrace bladeTrace;
        public CombatAttackDefinition lightAttack, thrustAttack, followupAttack, slowAttack, perilousAttack;
        public CombatAttackDefinition CurrentAttackDefinition => AttackFor(Core == null ? AttackKind.Light : Core.ActiveAttack.Kind);
        public CombatAttackDefinition AttackFor(AttackKind kind)
        {
            switch (kind)
            {
                case AttackKind.Thrust: return thrustAttack != null ? thrustAttack : lightAttack;
                case AttackKind.Followup: return followupAttack != null ? followupAttack : lightAttack;
                case AttackKind.Slow: return slowAttack != null ? slowAttack : lightAttack;
                case AttackKind.Perilous: return perilousAttack != null ? perilousAttack : lightAttack;
                default: return lightAttack;
            }
        }
        public CombatAttackDefinition AttackFor(AttackPattern pattern)
        {
            switch (pattern)
            {
                case AttackPattern.Slow: return slowAttack != null ? slowAttack : lightAttack;
                case AttackPattern.Perilous: return perilousAttack != null ? perilousAttack : lightAttack;
                default: return lightAttack;
            }
        }
        CombatBladeTrace CurrentTrace => CurrentAttackDefinition != null ? CurrentAttackDefinition.trace : bladeTrace;
        public LayerMask combatMask = ~0;
        public float maxTargetHeightDifference = .9f;
        public CombatCore Core { get; private set; }
        public CombatActor Target { get; set; }
        public CharacterController Motor { get; private set; }
        public Vector3 AttackForward { get; private set; }
        public Vector3 DodgeDirection { get; private set; }
        public Vector3 LastContactPoint { get; private set; }
        public bool HasContactPoint { get; private set; }
        public float EngageDistance => bladeTrace != null ? Mathf.Min(Core.Tuning.range, bladeTrace.engageDistance) : Core.Tuning.range;
        readonly Collider[] contacts = new Collider[32];
        readonly RaycastHit[] occluders = new RaycastHit[32];
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        public void Initialize(CombatTuning tuning)
        {
            Motor = GetComponent<CharacterController>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            Core = new CombatCore(tuning,lightAttack!=null?lightAttack.rules:null,thrustAttack!=null?thrustAttack.rules:null,followupAttack!=null?followupAttack.rules:null);
            Core.ActiveSample += SampleAttack;
            Core.ActiveInterval += SweepBlade;
            Core.DodgeInterval += MoveDodge;
            Core.StateChanged += OnStateChanged;
            AttackForward = transform.forward;
            DodgeDirection = -transform.forward;
            if (bladeTrace != null && !bladeTrace.IsValid)
                Debug.LogError("Rebake the blade trace after changing its attack clip or phase markers.", this);
            foreach(var attack in new[]{lightAttack,thrustAttack,followupAttack,slowAttack,perilousAttack})
                if(attack!=null && !TraceMatches(attack))Debug.LogError("Rebake the trace for "+attack.name+" after changing its clip or phase markers.",this);
        }

        void OnStateChanged(CombatState previous, CombatState next)
        {
            if (next == CombatState.AttackStartup) { AttackForward = transform.forward; HasContactPoint = false; }
        }

        public void ResetActor()
        {
            Motor.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            Motor.enabled = true;
            AttackForward = transform.forward;
            HasContactPoint = false;
            DodgeDirection = -transform.forward;
            Core.Reset();
        }

        public void Move(Vector3 direction, float speed, float deltaTime)
        {
            if (!Core.CanAct) return;
            direction.y = 0;
            Motor.Move((Vector3.ClampMagnitude(direction, 1) * speed + Vector3.down * 2) * deltaTime);
        }

        public void FaceTarget()
        {
            if ((!Core.CanAct && !Core.IsPreparing) || Target == null) return;
            Vector3 forward = Target.transform.position - transform.position;
            forward.y = 0;
            if (forward.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(forward);
        }

        public bool RequestDodge(Vector3 direction)
        {
            if (!Core.RequestDodge()) return false;
            direction.y=0;
            DodgeDirection=direction.sqrMagnitude>.001f ? direction.normalized : -transform.forward;
            return true;
        }
        void MoveDodge(float from, float to)
        {
            float duration=Mathf.Max(.001f,Core.Tuning.dodgeMoveDuration);
            float a=Mathf.SmoothStep(0,1,Mathf.Clamp01(from/duration));
            float b=Mathf.SmoothStep(0,1,Mathf.Clamp01(to/duration));
            Motor.Move(DodgeDirection*((b-a)*Core.Tuning.dodgeDistance));
            Physics.SyncTransforms();
        }

        public float DistanceToTarget => Target == null ? float.PositiveInfinity :
            Vector3.ProjectOnPlane(Target.transform.position - transform.position, Vector3.up).magnitude;

        public static bool IsInFront(Vector3 forward, Vector3 offset, float fullAngle)
        {
            offset.y = forward.y = 0;
            return offset.sqrMagnitude < .000001f || Vector3.Dot(forward.normalized, offset.normalized) >=
                Mathf.Cos(fullAngle * .5f * Mathf.Deg2Rad);
        }

        public bool RequestPlayerAttack()
        {
            if (TryExecute())return true;
            return Core.RequestAttack();
        }
        public bool BeginPlayerAttack()
        {
            if(TryExecute())return true;
            return Core.BeginPreparation();
        }
        bool TryExecute()
        {
            if (isPlayer && Target != null && Core.CanAct && Target.Core.State == CombatState.PostureBroken &&
                ValidTarget(Target, transform.forward))
            {
                Vector3 point = Target.Motor.ClosestPoint(transform.position + Vector3.up * 1.1f);
                if (HasLineOfSight(Target, point))
                {
                    LastContactPoint = point; HasContactPoint = true;
                    if (Core.TryDeathblow(Target.Core, true, DistanceToTarget, true)) return true;
                }
            }
            return false;
        }

        bool ValidTarget(CombatActor other, Vector3 forward)
        {
            if (other == null || other == this || other.Core == null || other.Core.State == CombatState.Dead || !other.AcceptsDamage) return false;
            if (enforceFactions && isPlayer == other.isPlayer) return false;
            Vector3 delta = other.transform.position - transform.position;
            return Mathf.Abs(delta.y) <= maxTargetHeightDifference &&
                Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude <= Core.Tuning.range * Core.Tuning.range &&
                IsInFront(forward, delta, 120);
        }

        public bool HasLineOfSight(CombatActor other, Vector3 point)
        {
            Vector3 origin = transform.position + Vector3.up * 1.1f;
            Vector3 delta = point - origin;
            if (delta.sqrMagnitude < .000001f) return true;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, occluders, delta.magnitude, combatMask, QueryTriggerInteraction.Ignore);
            var found = count == occluders.Length ? Physics.RaycastAll(origin, delta.normalized, delta.magnitude, combatMask, QueryTriggerInteraction.Ignore) : occluders;
            if (found != occluders) count = found.Length;
            for (int i = 0; i < count; i++)
            {
                var owner = found[i].collider.GetComponentInParent<CombatActor>();
                if (owner != this && owner != other) return false;
            }
            return true;
        }

        void ResolveContacts(Collider[] found, int count, Vector3 bladeRoot, Vector3 bladeTip)
        {
            for (int i = 0; i < count && Core.State == CombatState.AttackActive; i++)
            {
                var other = found[i].GetComponentInParent<CombatActor>();
                if (!ValidTarget(other, AttackForward)) continue;
                Vector3 blade = bladeTip - bladeRoot;
                float along = blade.sqrMagnitude <= .000001f ? 0 : Mathf.Clamp01(Vector3.Dot(found[i].bounds.center - bladeRoot, blade) / blade.sqrMagnitude);
                Vector3 point = found[i].ClosestPoint(bladeRoot + blade * along);
                if (!HasLineOfSight(other, point)) continue;
                LastContactPoint = point; HasContactPoint = true;
                if (other.autoFaceGuardAttacks && other.Core.State == CombatState.Guard &&
                    Core.ActiveAttack.Response != AttackResponse.DodgeOnly)
                {
                    Vector3 incoming = transform.position - other.transform.position;
                    incoming.y = 0;
                    if (incoming.sqrMagnitude > .000001f)
                        other.transform.rotation = Quaternion.LookRotation(incoming);
                }
                Core.TryHit(other.Core, IsInFront(other.transform.forward, transform.position - other.transform.position, other.Core.Tuning.guardAngle));
            }
        }

        void SampleAttack()
        {
            if (CurrentTrace != null) return;
            var tuning = Core.Tuning;
            Vector3 center = transform.position + Vector3.up + AttackForward * (tuning.range * .5f);
            Vector3 half = new Vector3(tuning.attackHalfWidth, .9f, tuning.range * .5f);
            Quaternion rotation = Quaternion.LookRotation(AttackForward);
            int count = Physics.OverlapBoxNonAlloc(center, half, contacts, rotation, combatMask, QueryTriggerInteraction.Ignore);
            var found = count == contacts.Length ? Physics.OverlapBox(center, half, rotation, combatMask, QueryTriggerInteraction.Ignore) : contacts;
            ResolveContacts(found, found == contacts ? count : found.Length, center, center);
        }

        void SweepBlade(float from, float to)
        {
            var bladeTrace=CurrentTrace;
            if (bladeTrace == null || !bladeTrace.IsValid) return;
            if(CurrentAttackDefinition!=null && !TraceMatches(CurrentAttackDefinition))return;
            // Visit every baked knot and subdivide spatially: neither slow frames nor curved swings skip contact.
            int segments = bladeTrace.poses.Length - 1;
            BladePose previous = bladeTrace.Sample(from);
            CheckBlade(previous);
            while (from < to && Core.State == CombatState.AttackActive)
            {
                float next = Mathf.Min(to, (Mathf.Floor(from * segments + .0001f) + 1) / segments);
                if (next <= from) next = to;
                BladePose current = bladeTrace.Sample(next);
                float travel = Mathf.Max(Vector3.Distance(previous.root, current.root), Vector3.Distance(previous.tip, current.tip));
                int steps = Mathf.Max(1, Mathf.CeilToInt(travel / Mathf.Max(.01f, Mathf.Min(bladeTrace.spatialStep, bladeTrace.radius))));
                for (int i = 1; i <= steps && Core.State == CombatState.AttackActive; i++)
                    CheckBlade(BladePose.Lerp(previous, current, (float)i / steps));
                previous = current; from = next;
            }
        }
        static bool TraceMatches(CombatAttackDefinition definition) => definition.trace!=null && definition.trace.IsValid &&
            definition.trace.bakedClip==definition.clip && Mathf.Approximately(definition.trace.bakedStart,definition.activeStart) && Mathf.Approximately(definition.trace.bakedEnd,definition.activeEnd);

        void CheckBlade(BladePose pose)
        {
            var bladeTrace=CurrentTrace;
            if (Core.State != CombatState.AttackActive) return;
            Quaternion facing = Quaternion.LookRotation(AttackForward);
            Vector3 root = transform.position + facing * pose.root, tip = transform.position + facing * pose.tip;
            int count = Physics.OverlapCapsuleNonAlloc(root, tip, bladeTrace.radius, contacts, combatMask, QueryTriggerInteraction.Ignore);
            var found = count == contacts.Length ? Physics.OverlapCapsule(root, tip, bladeTrace.radius, combatMask, QueryTriggerInteraction.Ignore) : contacts;
            ResolveContacts(found, found == contacts ? count : found.Length, root, tip);
        }

        void OnDrawGizmosSelected()
        {
            float range = Core == null ? 2 : Core.Tuning.range;
            float width = Core == null ? .65f : Core.Tuning.attackHalfWidth;
            Vector3 forward = Core != null && Core.IsAttacking ? AttackForward : transform.forward;
            Quaternion facing = Quaternion.LookRotation(forward);
            if (bladeTrace != null && bladeTrace.poses != null)
            {
                Gizmos.color = new Color(1,.65f,.1f);
                foreach (var pose in bladeTrace.poses) Gizmos.DrawLine(transform.position + facing * pose.root, transform.position + facing * pose.tip);
                if (Core != null && Core.State == CombatState.AttackActive)
                {
                    var pose = bladeTrace.Sample(Core.StateProgress); Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(transform.position + facing * pose.tip, bladeTrace.radius);
                }
            }
            else
            {
                Gizmos.color = Color.yellow;
                Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up + forward * range * .5f, facing, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(width * 2, 1.8f, range)); Gizmos.matrix = Matrix4x4.identity;
            }
            Gizmos.color = Color.cyan;
            float angle = Core == null ? 120 : Core.Tuning.guardAngle;
            for (int sign = -1; sign <= 1; sign += 2)
                Gizmos.DrawRay(transform.position + Vector3.up, Quaternion.AngleAxis(angle * .5f * sign, Vector3.up) * transform.forward * range);
        }
    }
}
