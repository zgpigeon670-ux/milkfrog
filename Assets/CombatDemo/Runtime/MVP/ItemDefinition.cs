using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public enum ItemCategory { Consumable, Quest, Equipment }
    public enum EquipmentSlot { Head, Body, Legs, Shoes, Weapon }
    public enum ItemEffectType { Recovery, TemporaryAttack }
    public enum ItemUseContext { Backpack, QuickSlot }

    [CreateAssetMenu(menuName = "MILKFROG/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string stableId;
        public string displayName;
        public ItemCategory category;
        public ItemEffectType effectType;
        public Sprite icon;
        [TextArea] public string description;
        [Min(0)] public float restoreHealth;
        [Min(0)] public float reducePosture;
        [Min(1)] public float attackDamageMultiplier = 1;
        [Min(0)] public float attackBuffDuration;
        public EquipmentSlot equipmentSlot;
        [Min(0)] public float weight, armorDefense, guardDefense, moveSpeedBonus;
        [Min(0)] public float weaponDamage = 10;
        public GameObject visualPrefab;
        public bool EquipmentValid => category == ItemCategory.Equipment && System.Enum.IsDefined(typeof(EquipmentSlot), equipmentSlot) &&
            Finite(weight) && weight >= 0 && Finite(armorDefense) && armorDefense >= 0 && Finite(guardDefense) && guardDefense >= 0 &&
            Finite(moveSpeedBonus) && moveSpeedBonus >= 0 && Finite(weaponDamage) && weaponDamage > 0 &&
            restoreHealth == 0 && reducePosture == 0 && attackDamageMultiplier == 1 && attackBuffDuration == 0 && effectType == ItemEffectType.Recovery &&
            (equipmentSlot == EquipmentSlot.Weapon ? armorDefense == 0 && moveSpeedBonus == 0 : guardDefense == 0) &&
            (equipmentSlot == EquipmentSlot.Shoes || moveSpeedBonus == 0) && (equipmentSlot != EquipmentSlot.Shoes || armorDefense == 0) &&
            (equipmentSlot == EquipmentSlot.Weapon || visualPrefab != null);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public bool EffectsValid => Finite(restoreHealth) && restoreHealth >= 0 && Finite(reducePosture) && reducePosture >= 0 &&
            Finite(attackDamageMultiplier) && attackDamageMultiplier >= 1 && Finite(attackBuffDuration) && attackBuffDuration >= 0 &&
            (effectType == ItemEffectType.Recovery ? attackDamageMultiplier == 1 && attackBuffDuration == 0 :
             effectType == ItemEffectType.TemporaryAttack && restoreHealth == 0 && reducePosture == 0 && attackDamageMultiplier > 1 && attackBuffDuration > 0);
        public bool Usable => category == ItemCategory.Consumable && EffectsValid &&
            (effectType == ItemEffectType.TemporaryAttack || restoreHealth > 0 || reducePosture > 0);
        public string EffectLabel => category == ItemCategory.Equipment ?
            $"重量 {weight:0.#} · 护具防御 {armorDefense:0.#}\n" + (equipmentSlot == EquipmentSlot.Weapon ? $"基础伤害 {weaponDamage:0.#} · 格挡防御 {guardDefense:0.#}" : $"移动加速 +{moveSpeedBonus * 100:0}%") :
            category == ItemCategory.Quest ? "任务物品 · 不会消耗" :
            effectType == ItemEffectType.TemporaryAttack ? $"生命伤害 +{(attackDamageMultiplier - 1) * 100:0}% · {attackBuffDuration:0} 秒\n不叠加，再次使用刷新时长" :
            restoreHealth > 0 && reducePosture > 0 ? $"恢复生命 {restoreHealth:0} / 降低架势 {reducePosture:0}" :
            restoreHealth > 0 ? $"恢复生命 {restoreHealth:0}" : $"降低架势 {reducePosture:0}";
    }
}
