using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public enum ItemCategory { Consumable, Quest }

    [CreateAssetMenu(menuName = "MILKFROG/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string stableId;
        public string displayName;
        public ItemCategory category;
        public Sprite icon;
        [TextArea] public string description;
        [Min(0)] public float restoreHealth;
        [Min(0)] public float reducePosture;
        public bool Usable => category == ItemCategory.Consumable && (restoreHealth > 0 || reducePosture > 0);
        public string EffectLabel => category == ItemCategory.Quest ? "任务物品 · 不会消耗" :
            $"恢复生命 {restoreHealth:0} / 降低架势 {reducePosture:0}";
    }
}
