using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class DemoCamera : MonoBehaviour
    {
        public Transform player, enemy;
        public Vector3 offset = new Vector3(3, 5, -7);
        public float smooth = 8;
        public bool avoidObstacles;
        public bool followBehindPlayer;
        public bool lockOn = true;
        public bool freeOrbit;
        public float yaw, pitch = 12, sensitivity = .12f;
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
            lockOn = false; enemy = null;
        }
        [Range(0, .5f)] public float enemyFocusWeight = .2f;
        public float headingSmooth = 7;
        public float collisionRadius = .25f;
        public LayerMask obstacleMask = ~0;
        readonly RaycastHit[] hits = new RaycastHit[32];
        float impulse, phase;
        Vector3 stablePosition;
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
            var forward = Vector3.ProjectOnPlane(player.forward, Vector3.up);
            var targetHeading = forward.sqrMagnitude > .001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
            heading = positioned ? Quaternion.Slerp(heading, targetHeading, 1 - Mathf.Exp(-headingSmooth * dt)) : targetHeading;
            float framing = avoidObstacles ? Mathf.Clamp(Vector3.Distance(player.position, enemy.position) / 4, 1, 1.6f) : 1;
            Vector3 desired = focus + (followBehindPlayer ? heading * offset : offset) * framing;
            if (!positioned) { stablePosition = desired; positioned = true; }
            stablePosition = Vector3.Lerp(stablePosition, desired, 1 - Mathf.Exp(-smooth * dt));
            stablePosition = ConstrainPosition(focus, stablePosition);
            phase += dt * 110;
            Vector3 shake = new Vector3(Mathf.Sin(phase), Mathf.Cos(phase * 1.31f), 0) * impulse;
            transform.position = ConstrainPosition(focus, stablePosition + shake);
            transform.LookAt(focus);
            impulse = Mathf.Max(0, impulse - dt * .9f);
        }

        void StepOrbit(float dt)
        {
            if (player == null) return;
            Vector3 focus = player.position + Vector3.up * 1.4f;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            if (lockOn && enemy != null)
            {
                Vector3 delta = enemy.position - player.position; delta.y = 0;
                if (delta.sqrMagnitude > .001f) rotation = Quaternion.LookRotation(delta) * Quaternion.Euler(12, 0, 0);
                focus = Vector3.Lerp(player.position, enemy.position, .18f) + Vector3.up * 1.4f;
            }
            heading = positioned ? Quaternion.Slerp(heading, rotation, 1 - Mathf.Exp(-headingSmooth * dt)) : rotation;
            Vector3 desired = focus + heading * new Vector3(.65f, .25f, -4.5f);
            stablePosition = positioned ? Vector3.Lerp(stablePosition, desired, 1 - Mathf.Exp(-smooth * dt)) : desired;
            positioned = true; stablePosition = ConstrainPosition(focus, stablePosition);
            phase += dt * 110;
            transform.position = ConstrainPosition(focus, stablePosition + transform.right * Mathf.Sin(phase) * impulse);
            transform.rotation = heading;
            if (lockOn && enemy != null) transform.LookAt(focus);
            impulse = Mathf.Max(0, impulse - dt * .9f);
        }
    }
}
