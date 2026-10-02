using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class InventoryPickup : MonoBehaviour, IInteractable
    {
        public string stableId;
        public ItemDefinition item;
        [Min(1)] public int quantity = 3;
        [Min(.1f)] public float interactionRadius = 2.5f;
        public GameObject visual;
        public string StableId => stableId;
        public Transform Root => transform;
        public Vector3 InteractionPoint => transform.position + Vector3.up;
        public float Radius => interactionRadius;
        public bool Available(MvpWorld world) => item != null && !world.WorldState.IsCollected(stableId);
        public string Label(MvpWorld world) => $"拾取 {item.displayName} ×{quantity}";
        public string BlockReason(MvpWorld world) => Available(world) ? null : "已经拾取";
        public bool Interact(MvpWorld world) => world.TryPickup(this);
        public void ApplyState(MvpWorld world) { if (visual != null) visual.SetActive(Available(world)); }
    }
}
