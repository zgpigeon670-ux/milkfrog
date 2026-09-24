using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class BonfireCheckpoint : MonoBehaviour
    {
        public const string StartId = "camp-start";
        public const string BossApproachId = "camp-before-boss";
        public string stableId = StartId;
        public string displayName = "起点篝火";
        [Min(.1f)] public float interactionRadius = 2.5f;

        public bool InRange(Vector3 playerPosition) =>
            EnemyController.FlatDistance(playerPosition, transform.position) <= interactionRadius;

        public Vector3 RespawnPosition => transform.position + transform.forward * 1.6f;
        public float RespawnYaw => transform.eulerAngles.y;
    }
}
