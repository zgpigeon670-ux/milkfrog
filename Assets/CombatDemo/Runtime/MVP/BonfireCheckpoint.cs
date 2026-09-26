using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class BonfireCheckpoint : MonoBehaviour, IInteractable
    {
        public const string StartId = "camp-start";
        public const string BossApproachId = "camp-before-boss";
        public string stableId = StartId;
        public string displayName = "起点篝火";
        [Min(.1f)] public float interactionRadius = 2.5f;

        public bool InRange(Vector3 playerPosition) =>
            EnemyController.FlatDistance(playerPosition, transform.position) <= interactionRadius;

        public string StableId => stableId;
        public Transform Root => transform;
        public Vector3 InteractionPoint => transform.position + Vector3.up * .5f;
        public float Radius => interactionRadius;
        public bool Available(MvpWorld world) => true;
        public string Label(MvpWorld world) => (world.IsBonfireUnlocked(stableId) ? "休息 · " : "点亮 · ") + displayName;
        public string BlockReason(MvpWorld world) => null;
        public bool Interact(MvpWorld world) => world.TryRest(this);
        public Vector3 RespawnPosition => transform.position + transform.forward * 1.6f;
        public float RespawnYaw => transform.eulerAngles.y;
    }
}
