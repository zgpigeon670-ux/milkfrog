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
    }

    // Owns item counts. World keyItems records historical acquisition, not consumable ownership.
    public sealed class InventoryService
    {
        readonly Dictionary<string, int> quantities = new Dictionary<string, int>(StringComparer.Ordinal);
        public event Action Changed;
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
            Changed?.Invoke();
        }
        public InventoryData Capture()
        {
            var ids = new List<string>(quantities.Keys); ids.Sort(StringComparer.Ordinal);
            var entries = new InventoryEntry[ids.Count];
            for (int i = 0; i < ids.Count; i++) entries[i] = new InventoryEntry { itemId = ids[i], quantity = quantities[ids[i]] };
            return new InventoryData { entries = entries };
        }
        public void WriteTo(PlayerSnapshot snapshot) => snapshot.inventory = Capture();
        public static bool IsValid(InventoryData data)
        {
            if (data?.entries == null) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in data.entries)
                if (entry == null || string.IsNullOrWhiteSpace(entry.itemId) || entry.quantity <= 0 || !ids.Add(entry.itemId)) return false;
            return true;
        }
    }
}
