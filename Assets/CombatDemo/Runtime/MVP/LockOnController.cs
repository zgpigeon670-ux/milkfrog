using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class LockOnController
    {
        readonly MvpWorld world;
        public EnemyController Target { get; private set; }
        float obscured;
        public LockOnController(MvpWorld owner) { world = owner; }
        public void Toggle()
        {
            if (world.Director.State == EncounterState.BossCombat) return;
            if (Target != null) { Clear(); return; }
            EnemyController best = null; float score = float.PositiveInfinity, distance = float.PositiveInfinity;
            foreach (var candidate in world.enemies)
            {
                if (candidate.IsBoss || !candidate.Alive) continue;
                float d = Vector3.Distance(world.player.transform.position, candidate.transform.position);
                Vector3 p = world.gameplayCamera.WorldToViewportPoint(candidate.transform.position + Vector3.up * 1.2f);
                if (d > 20 || p.z <= 0 || p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1 || !world.Visible(candidate.Actor)) continue;
                // Pixel distance, not viewport distance: preserve ranking on wide displays.
                float s = new Vector2((p.x - .5f) * world.gameplayCamera.pixelWidth, (p.y - .5f) * world.gameplayCamera.pixelHeight).sqrMagnitude;
                if (s < score || (Mathf.Approximately(s, score) && (d < distance ||
                    (Mathf.Approximately(d, distance) && string.CompareOrdinal(candidate.stableId, best?.stableId) < 0))))
                { best = candidate; score = s; distance = d; }
            }
            if (best != null) Force(best);
        }
        public void Force(EnemyController target)
        {
            Target = target; obscured = 0; world.player.Target = target.Actor;
            // A mob lock is a selection aid; only the boss owns the camera.
            world.cameraRig.enemy = target.IsBoss ? target.transform : null;
            world.cameraRig.lockOn = target.IsBoss;
        }
        public void Clear()
        {
            Target = null; obscured = 0; world.player.Target = null;
            if (world.cameraRig.lockOn) world.cameraRig.ReleaseLock();
            else world.cameraRig.enemy = null;
        }
        public void Tick(float dt)
        {
            if (Target == null) return;
            if (!Target.Alive) { Clear(); return; }
            if (world.Director.State == EncounterState.BossCombat) return;
            obscured = world.Visible(Target.Actor) ? 0 : obscured + dt;
            if (Vector3.Distance(world.player.transform.position, Target.transform.position) > 25 || obscured >= 1) Clear();
        }
    }
}
