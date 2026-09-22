using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class DemoCamera : MonoBehaviour
    {
        public Transform player, enemy;
        public Vector3 offset = new Vector3(3, 5, -7);
        public float smooth = 8;
        public bool avoidObstacles;
        public float collisionRadius = .25f;
        public LayerMask obstacleMask = ~0;
        readonly RaycastHit[] hits = new RaycastHit[32];
        float impulse, phase;
        Vector3 stablePosition;
        bool positioned;
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

        void LateUpdate()
        {
            if (player == null || enemy == null) return;
            float dt = Time.unscaledDeltaTime;
            Vector3 focus = Vector3.Lerp(player.position, enemy.position, .45f) + Vector3.up * 1.1f;
            float framing = avoidObstacles ? Mathf.Clamp(Vector3.Distance(player.position, enemy.position) / 4, 1, 1.6f) : 1;
            Vector3 desired = focus + offset * framing;
            if (!positioned) { stablePosition = transform.position; positioned = true; }
            stablePosition = Vector3.Lerp(stablePosition, desired, 1 - Mathf.Exp(-smooth * dt));
            stablePosition = ConstrainPosition(focus, stablePosition);
            phase += dt * 110;
            Vector3 shake = new Vector3(Mathf.Sin(phase), Mathf.Cos(phase * 1.31f), 0) * impulse;
            transform.position = ConstrainPosition(focus, stablePosition + shake);
            transform.LookAt(focus);
            impulse = Mathf.Max(0, impulse - dt * .9f);
        }
    }
}
