using System;
using UnityEngine;

namespace Milkfrog.CombatDemo
{
    [Serializable] public sealed class EquipmentData
    {
        public string[] slots = { "", "", "", "", "" };
    }

    public sealed class EquipmentService
    {
        public const string DefaultWeaponId = "practice-sword";
        readonly string[] slots = { "", "", "", "", "" };
        public event Action Changed;
        public string Get(EquipmentSlot slot) => slots[(int)slot];
        public bool TryEquip(ItemDefinition item, bool owned)
        {
            if (!owned || item == null || !item.EquipmentValid || string.IsNullOrWhiteSpace(item.stableId) || Get(item.equipmentSlot) == item.stableId) return false;
            slots[(int)item.equipmentSlot] = item.stableId; Changed?.Invoke(); return true;
        }
        public bool TryUnequip(EquipmentSlot slot)
        {
            if (!Enum.IsDefined(typeof(EquipmentSlot), slot) || slot == EquipmentSlot.Weapon || Get(slot).Length == 0) return false;
            slots[(int)slot] = ""; Changed?.Invoke(); return true;
        }
        public void Restore(EquipmentData data)
        {
            if (!IsValid(data)) throw new ArgumentException("Invalid equipment data.");
            for (int i = 0; i < slots.Length; i++) slots[i] = data.slots[i] ?? "";
        }
        public EquipmentData Capture() => new EquipmentData { slots = (string[])slots.Clone() };
        public static bool IsValid(EquipmentData data)
        {
            if (data?.slots == null || data.slots.Length != 5) return false;
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var id in data.slots)
                if (!string.IsNullOrEmpty(id) && (string.IsNullOrWhiteSpace(id) || !ids.Add(id))) return false;
            return true;
        }
        public static void EnsureDefaultWeapon(InventoryData data)
        {
            var bag = new InventoryService(); bag.Restore(data);
            if (!bag.HasItem(DefaultWeaponId)) bag.TryAdd(DefaultWeaponId, 1);
            var equipment = bag.Equipment.Capture();
            if (string.IsNullOrEmpty(equipment.slots[(int)EquipmentSlot.Weapon])) equipment.slots[(int)EquipmentSlot.Weapon] = DefaultWeaponId;
            bag.Equipment.Restore(equipment); var result = bag.Capture();
            data.entries = result.entries; data.equipment = result.equipment;
        }
    }

    public readonly struct EquipmentStats
    {
        public readonly float Weight, ArmorDefense, GuardDefense, SpeedBonus, MovementMultiplier, WeaponDamageMultiplier, DamageReduction, GuardReduction;
        public EquipmentStats(float weight, float armor, float guard, float speedBonus, float weaponDamage)
        {
            Weight = weight; ArmorDefense = armor; GuardDefense = guard; SpeedBonus = speedBonus;
            MovementMultiplier = Mathf.Clamp(1 - weight * .02f + speedBonus, .4f, 1.5f);
            WeaponDamageMultiplier = weaponDamage / 10f;
            DamageReduction = Mathf.Min(.8f, armor / (100 + armor)); GuardReduction = Mathf.Min(.8f, guard / (100 + guard));
        }
    }
    public static class CharacterStatsCalculator
    {
        public static EquipmentStats Calculate(InventoryService inventory, Func<string, ItemDefinition> resolve)
        {
            float weight = 0, armor = 0, guard = 0, speed = 0, damage = 10;
            for (int i = 0; i < 5; i++)
            {
                var item = resolve(inventory.Equipment.Get((EquipmentSlot)i));
                if (item == null || !inventory.HasItem(item.stableId) || !item.EquipmentValid || (int)item.equipmentSlot != i) continue;
                if (i != (int)EquipmentSlot.Shoes) weight += item.weight;
                if (i < 3) armor += item.armorDefense;
                if (i == (int)EquipmentSlot.Shoes) speed += item.moveSpeedBonus;
                if (i == (int)EquipmentSlot.Weapon) { guard = item.guardDefense; damage = item.weaponDamage; }
            }
            return new EquipmentStats(weight, armor, guard, speed, damage);
        }
    }
}
