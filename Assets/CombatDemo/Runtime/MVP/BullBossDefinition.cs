using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public enum BullMoveKind { Horn, Kick, Charge, Slam, Wave }
    [System.Serializable]
    public sealed class BullMove
    {
        public BullMoveKind kind;
        public AnimationClip clip;
        public AttackParameters attack = new AttackParameters();
        public float range = 3, halfWidth = 1, lockBeforeImpact = .25f;
        public float moveSpeed, cooldown, jumpClearance = .55f;
        [Range(0,180)] public float halfAngle = 65;
    }
    [CreateAssetMenu(menuName="Combat Demo/Yellow Bull")]
    public sealed class BullBossDefinition : ScriptableObject
    {
        public BullMove[] moves;
        public AnimationClip idle, walk, hit, death, broken, roar;
        public float speed = 2.2f, turnSpeed = 120, decisionDelay = .35f;
        public float arenaRadius = 18, phaseHealthRatio = .5f, roarDuration = 1.2f, waveRepeatGap = .65f;
        public BullMove Get(BullMoveKind kind) => System.Array.Find(moves, m => m.kind == kind);
        public bool IsValid => moves != null && moves.Length == 5 && idle != null && walk != null && death != null &&
            System.Array.TrueForAll(moves, m => m != null && m.clip != null && m.range > 0 && m.halfWidth > 0 &&
                m.attack != null && m.attack.startup > m.lockBeforeImpact && m.lockBeforeImpact >= 0 &&
                m.attack.active > 0 && m.attack.recovery > 0 && m.attack.damage >= 0) &&
            new System.Collections.Generic.HashSet<BullMoveKind>(System.Array.ConvertAll(moves,m=>m.kind)).Count == 5;
    }
    public static class BullHitGeometry
    {
        public const float ChargeFrontReach = 1.1f;
        public static float SegmentDistance(Vector3 point, Vector3 start, Vector3 end)
        {
            point.y = start.y = end.y = 0; var line = end-start;
            float t = line.sqrMagnitude < .000001f ? 0 : Mathf.Clamp01(Vector3.Dot(point-start,line)/line.sqrMagnitude);
            return Vector3.Distance(point,start+line*t);
        }
        public static bool Cone(Vector3 point, Vector3 origin, Vector3 forward, float range, float halfAngle, float radius)
        {
            var delta = Vector3.ProjectOnPlane(point-origin,Vector3.up);
            return delta.magnitude <= range+radius && (delta.magnitude <= radius || Vector3.Angle(forward,delta) <= halfAngle);
        }
        public static bool Ring(Vector3 point, Vector3 center, float from, float to, float width, float radius)
        {
            float d = EnemyController.FlatDistance(point,center);
            return d >= Mathf.Min(from,to)-width-radius && d <= Mathf.Max(from,to)+width+radius;
        }
    }
}
