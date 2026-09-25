using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class DemoCamera : MonoBehaviour
    {
        public Transform player, enemy;
        public Vector3 offset = new Vector3(3, 5, -7);
        public float smooth = 8;
        public float verticalSmooth = 3.5f;
        public bool avoidObstacles;
        public bool followBehindPlayer;
        public bool lockOn = true;
        public bool freeOrbit;
        public float yaw, pitch = 8, sensitivity = .12f;
        public void Look(Vector2 delta)
        {
            if (!freeOrbit || lockOn) return;
            yaw += delta.x * sensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -20, 65);
        }
        public void ReleaseLock()
        {
            yaw = transform.eulerAngles.y;
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0, transform.eulerAngles.x), -20, 65);
            if (freeOrbit) heading = transform.rotation;
            lockOn = false; enemy = null;
        }
        [Range(0, .5f)] public float enemyFocusWeight = .2f;
        public float headingSmooth = 7;
        public float collisionRadius = .25f;
        public Vector3 orbitShoulder = new Vector3(.45f, .05f, 0);
        public float orbitDistance = 2.7f;
        public float collisionReturnSmooth = 6;
        public LayerMask obstacleMask = ~0;
        readonly RaycastHit[] hits = new RaycastHit[32];
        float impulse, phase;
        Vector3 stablePosition;
        Vector3 unoccludedPosition;
        float stableHeight;
        bool positioned;
        Quaternion heading;
        public void Impulse(float strength) => impulse = Mathf.Max(impulse, Mathf.Clamp(strength, 0, .15f));
        public void ResetImpulse() { impulse = phase = 0; positioned = false; }

        public Vector3 ConstrainPosition(Vector3 focus, Vector3 desired)
        {
            Vector3 delta = desired - focus;
            float distance = delta.magnitude;
            if (!avoidObstacles || distance < .001f) return desired;
            int count = Physics.SphereCastNonAlloc(focus, collisionRadius, delta / distance, hits, distance, obstacleMask, QueryTriggerInteraction.Ignore);
            float clear = distance;
            for (int i = 0; i < count; i++)
            {
                // The actors must never pull the camera into the fight.
                if (hits[i].collider.GetComponentInParent<CombatActor>() != null) continue;
                clear = Mathf.Min(clear, Mathf.Max(0, hits[i].distance - .05f));
            }
            return focus + delta.normalized * clear;
        }

        void LateUpdate() => Step(Time.unscaledDeltaTime);

        public void Step(float dt)
        {
            if (freeOrbit) { StepOrbit(dt); return; }
            if (player == null || enemy == null) return;
            Vector3 focus = Vector3.Lerp(player.position, enemy.position, followBehindPlayer ? enemyFocusWeight : .45f) + Vector3.up * 1.2f;
            var forward = Vector3.ProjectOnPlane(lockOn ? player.forward : heading * Vector3.forward, Vector3.up);
            var targetHeading = forward.sqrMagnitude > .001f ? Quaternion.LookRotation(forward) : heading;
            // Locked combat may orbit the facing. Free play keeps the rig from swinging with strafes.
            heading = !lockOn ? targetHeading : positioned ? Quaternion.Slerp(heading, targetHeading, 1 - Mathf.Exp(-Mathf.Min(headingSmooth, 4) * dt)) : targetHeading;
            float framing = avoidObstacles ? Mathf.Clamp(Vector3.Distance(player.position, enemy.position) / 4, 1, 1.6f) : 1;
            Vector3 desired = focus + (followBehindPlayer ? heading * offset : offset) * framing;
            if (!positioned) { stablePosition = desired; stableHeight = desired.y; positioned = true; }
            // Horizontal follow stays quick. Vertical motion, including jumps, eases in on its own.
            float horizontal = 1 - Mathf.Exp(-smooth * dt);
            stablePosition = new Vector3(
                Mathf.Lerp(stablePosition.x, desired.x, horizontal),
                stableHeight = Mathf.Lerp(stableHeight, desired.y, 1 - Mathf.Exp(-verticalSmooth * dt)),
                Mathf.Lerp(stablePosition.z, desired.z, horizontal));
            stablePosition = ConstrainPosition(focus, stablePosition);
            phase += dt * 28;
            Vector3 shake = Vector3.up * Mathf.Sin(phase) * impulse;
            transform.position = ConstrainPosition(focus, stablePosition + shake);
            transform.LookAt(focus);
            impulse = Mathf.Max(0, impulse - dt * .9f);
        }

        void StepOrbit(float dt)
        {
            if (player == null) return;
            Vector3 pivot = player.position + Vector3.up * 1.25f;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            bool bossLock = lockOn && enemy != null;
            float distance = orbitDistance;
            if (bossLock)
            {
                Vector3 delta = enemy.position - player.position; delta.y = 0;
                if (delta.sqrMagnitude > .001f) rotation = Quaternion.LookRotation(delta) * Quaternion.Euler(12, 0, 0);
                distance = Mathf.Clamp(orbitDistance + delta.magnitude * .25f, orbitDistance, 5.5f);
            }
            // Mouse orbit only. Character facing must not drag the camera sideways.
            heading = bossLock
                ? (positioned ? Quaternion.Slerp(heading, rotation, 1 - Mathf.Exp(-Mathf.Min(headingSmooth, 4) * dt)) : rotation)
                : rotation;
            Vector3 desired = pivot + heading * (orbitShoulder + Vector3.back * distance);
            if (!positioned) { unoccludedPosition = desired; stableHeight = desired.y; }
            float horizontal = 1 - Mathf.Exp(-smooth * dt);
            stableHeight = Mathf.Lerp(stableHeight, desired.y, 1 - Mathf.Exp(-verticalSmooth * dt));
            unoccludedPosition = new Vector3(
                Mathf.Lerp(unoccludedPosition.x, desired.x, horizontal),
                stableHeight,
                Mathf.Lerp(unoccludedPosition.z, desired.z, horizontal));
            Vector3 clearPosition = ConstrainPosition(pivot, unoccludedPosition);
            // Move into an obstruction immediately; ease back out after it clears.
            stablePosition = !positioned || (clearPosition - pivot).sqrMagnitude < (stablePosition - pivot).sqrMagnitude
                ? clearPosition
                : Vector3.Lerp(stablePosition, clearPosition, 1 - Mathf.Exp(-collisionReturnSmooth * dt));
            positioned = true;
            phase += dt * 28;
            transform.position = ConstrainPosition(pivot, stablePosition + Vector3.up * Mathf.Sin(phase) * impulse);
            if (bossLock)
            {
                Vector3 aim = Vector3.Lerp(player.position, enemy.position, .45f) + Vector3.up * 1.35f;
                transform.rotation = Quaternion.LookRotation(aim - transform.position);
            }
            else transform.rotation = heading;
            impulse = Mathf.Max(0, impulse - dt * .9f);
        }
    }
}
