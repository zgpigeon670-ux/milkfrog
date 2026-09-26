using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public enum WorldInteractionKind { KeyPickup, SealedDoor }
    public sealed class WorldInteractable : MonoBehaviour, IInteractable
    {
        public string stableId;
        public string displayName;
        public WorldInteractionKind kind;
        public string itemId = WorldStateService.KeyItemId;
        public float interactionRadius = 2.5f;
        public GameObject visual;
        public string StableId => stableId;
        public Transform Root => transform;
        public Vector3 InteractionPoint => transform.position + Vector3.up;
        public float Radius => interactionRadius;
        public bool Available(MvpWorld world) => kind == WorldInteractionKind.KeyPickup ? !world.WorldState.IsCollected(stableId) : !world.WorldState.IsDoorOpen(stableId);
        public string Label(MvpWorld world) => (kind == WorldInteractionKind.KeyPickup ? "拾取 " : "开启 ") + displayName;
        public string BlockReason(MvpWorld world)
        {
            if (!Available(world)) return "已经完成";
            if (kind == WorldInteractionKind.SealedDoor)
            {
                if (!world.IsBonfireUnlocked(BonfireCheckpoint.BossApproachId)) return "请先点亮守门篝火";
                if (!world.WorldState.HasItem(itemId)) return "需要守门钥匙";
            }
            return null;
        }
        public bool Interact(MvpWorld world) => world.TryWorldInteraction(this);
        public void ApplyState(MvpWorld world)
        { if (visual != null) visual.SetActive(Available(world)); }
    }
}
