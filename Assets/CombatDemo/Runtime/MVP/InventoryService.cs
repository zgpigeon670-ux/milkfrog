using System;
using System.Collections.Generic;

namespace Milkfrog.CombatDemo
{
    [Serializable] public sealed class InventoryEntry
    {
        public string itemId;
        public int quantity;
    }
    [Serializable] public sealed class InventoryData
    {
        public InventoryEntry[] entries = Array.Empty<InventoryEntry>();
        public string equippedItemId = "";
        public string[] quickSlots = { "", "", "" };
        public EquipmentData equipment = new EquipmentData();
    }

    // Owns item counts. World keyItems records historical acquisition, not consumable ownership.
    public sealed class InventoryService
    {
        readonly Dictionary<string, int> quantities = new Dictionary<string, int>(StringComparer.Ordinal);
        public event Action Changed;
        readonly string[] quickSlots = { "", "", "" };
        public EquipmentService Equipment { get; } = new EquipmentService();
        public InventoryService() { Equipment.Changed += () => Changed?.Invoke(); }
        public int ActiveQuickSlot { get; private set; }
        public string EquippedItemId => quickSlots[ActiveQuickSlot];
        public string QuickItem(int slot) => slot >= 0 && slot < 3 ? quickSlots[slot] : "";
        public bool SelectQuickSlot(int slot)
        {
            if (slot < 0 || slot >= 3 || ActiveQuickSlot == slot) return false;
            ActiveQuickSlot = slot; Changed?.Invoke(); return true;
        }
        public bool TryAssignQuickSlot(int slot, string id)
        {
            if (slot < 0 || slot >= 3 || !HasItem(id) || quickSlots[slot] == id) return false;
            for (int i = 0; i < 3; i++) if (quickSlots[i] == id) quickSlots[i] = "";
            quickSlots[slot] = id; Changed?.Invoke(); return true;
        }
        public bool ClearQuickSlot(int slot)
        {
            if (slot < 0 || slot >= 3 || quickSlots[slot].Length == 0) return false;
            quickSlots[slot] = ""; Changed?.Invoke(); return true;
        }
        public bool TryEquip(string id)
        {
            return TryAssignQuickSlot(ActiveQuickSlot, id);
        }
        public void Unequip() => ClearQuickSlot(ActiveQuickSlot);
        public int Quantity(string id) => id != null && quantities.TryGetValue(id, out int count) ? count : 0;
        public bool HasItem(string id) => Quantity(id) > 0;
        public bool TryAdd(string id, int amount)
        {
            if (string.IsNullOrWhiteSpace(id) || amount <= 0 || Quantity(id) > int.MaxValue - amount) return false;
            quantities[id] = Quantity(id) + amount; Changed?.Invoke(); return true;
        }
        public bool TryConsume(string id, int amount = 1)
        {
            if (amount <= 0 || Quantity(id) < amount) return false;
            int next = Quantity(id) - amount;
            if (next == 0) quantities.Remove(id); else quantities[id] = next;
            Changed?.Invoke(); return true;
        }
        public void Restore(InventoryData data)
        {
            if (!IsValid(data)) throw new ArgumentException("Invalid inventory data.", nameof(data));
            quantities.Clear();
            foreach (var entry in data.entries) quantities.Add(entry.itemId, entry.quantity);
            for (int i = 0; i < 3; i++) quickSlots[i] = data.quickSlots[i] ?? "";
            // Preserve source callers of the old single-slot data API. v6 files use quickSlots.
            if (Array.TrueForAll(quickSlots, string.IsNullOrEmpty) && !string.IsNullOrEmpty(data.equippedItemId)) quickSlots[0] = data.equippedItemId;
            Equipment.Restore(data.equipment);
            Changed?.Invoke();
        }
        public InventoryData Capture()
        {
            var ids = new List<string>(quantities.Keys); ids.Sort(StringComparer.Ordinal);
            var entries = new InventoryEntry[ids.Count];
            for (int i = 0; i < ids.Count; i++) entries[i] = new InventoryEntry { itemId = ids[i], quantity = quantities[ids[i]] };
            return new InventoryData { entries = entries, equippedItemId = quickSlots[0], quickSlots = (string[])quickSlots.Clone(), equipment = Equipment.Capture() };
        }
        public void WriteTo(PlayerSnapshot snapshot) => snapshot.inventory = Capture();
        public static bool IsValid(InventoryData data)
        {
            if (data?.entries == null) return false;
            if (data.quickSlots == null || data.quickSlots.Length != 3 || !EquipmentService.IsValid(data.equipment)) return false;
            var bindings = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in data.quickSlots)
                if (!string.IsNullOrEmpty(id) && (string.IsNullOrWhiteSpace(id) || !bindings.Add(id))) return false;
            if (data.equippedItemId != null && data.equippedItemId.Length > 0 && string.IsNullOrWhiteSpace(data.equippedItemId)) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in data.entries)
                if (entry == null || string.IsNullOrWhiteSpace(entry.itemId) || entry.quantity <= 0 || !ids.Add(entry.itemId)) return false;
            return true;
        }
    }
}
