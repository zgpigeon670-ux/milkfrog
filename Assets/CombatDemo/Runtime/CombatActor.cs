using UnityEngine;

namespace Milkfrog.CombatDemo
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CombatActor : MonoBehaviour
    {
        public bool isPlayer;
        public CombatCore Core { get; private set; }
        public CombatActor Target { get; set; }
        public CharacterController Motor { get; private set; }
        public Vector3 AttackForward { get; private set; }
        readonly Collider[] contacts = new Collider[32];
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        public void Initialize(CombatTuning tuning)
        {
            Motor = GetComponent<CharacterController>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            Core = new CombatCore(tuning);
            Core.ActiveSample += SampleAttack;
            Core.StateChanged += OnStateChanged;
            AttackForward = transform.forward;
        }

        void OnStateChanged(CombatState previous, CombatState next)
        {
            if (next == CombatState.AttackStartup) AttackForward = transform.forward;
        }

        public void ResetActor()
        {
            Motor.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            Motor.enabled = true;
            AttackForward = transform.forward;
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
            if (!Core.CanAct || Target == null) return;
            Vector3 forward = Target.transform.position - transform.position;
            forward.y = 0;
            if (forward.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(forward);
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
            if (isPlayer && Target != null && Core.TryDeathblow(Target.Core, true, DistanceToTarget,
                IsInFront(transform.forward, Target.transform.position - transform.position, 120))) return true;
            return Core.RequestAttack();
        }

        void SampleAttack()
        {
            var tuning = Core.Tuning;
            Vector3 center = transform.position + Vector3.up + AttackForward * (tuning.range * .5f);
            Vector3 half = new Vector3(tuning.attackHalfWidth, .9f, tuning.range * .5f);
            Quaternion rotation = Quaternion.LookRotation(AttackForward);
            int count = Physics.OverlapBoxNonAlloc(center, half, contacts, rotation, ~0, QueryTriggerInteraction.Ignore);
            // Overflow must not silently hide a target if extra geometry is added later.
            Collider[] found = count == contacts.Length ? Physics.OverlapBox(center, half, rotation, ~0,
                QueryTriggerInteraction.Ignore) : contacts;
            if (found != contacts) count = found.Length;
            for (int i = 0; i < count && Core.State == CombatState.AttackActive; i++)
            {
                if (!found[i].TryGetComponent<CombatActor>(out var other))
                    other = found[i].GetComponentInParent<CombatActor>();
                if (other == null || other == this || other.Core == null) continue;
                Vector3 offset = other.transform.position - transform.position;
                if (Vector3.ProjectOnPlane(offset, Vector3.up).magnitude > tuning.range ||
                    !IsInFront(AttackForward, offset, 120)) continue;
                Core.TryHit(other.Core, IsInFront(other.transform.forward, -offset, other.Core.Tuning.guardAngle));
            }
        }

        void OnDrawGizmosSelected()
        {
            float range = Core == null ? 2 : Core.Tuning.range;
            float width = Core == null ? .65f : Core.Tuning.attackHalfWidth;
            Vector3 forward = Core != null && Core.IsAttacking ? AttackForward : transform.forward;
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up + forward * range * .5f,
                Quaternion.LookRotation(forward), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(width * 2, 1.8f, range));
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.cyan;
            float angle = Core == null ? 120 : Core.Tuning.guardAngle;
            for (int sign = -1; sign <= 1; sign += 2)
                Gizmos.DrawRay(transform.position + Vector3.up, Quaternion.AngleAxis(angle * .5f * sign,
                    Vector3.up) * transform.forward * range);
        }
    }
}
