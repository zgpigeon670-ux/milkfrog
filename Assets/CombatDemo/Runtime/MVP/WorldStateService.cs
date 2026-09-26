using System;
using System.Collections.Generic;

namespace Milkfrog.CombatDemo
{
    public enum WorldEventKind { BonfireLit, ItemAcquired, DoorOpened, BossDefeated }
    [Serializable] public sealed class WorldStateData
    {
        public string[] collectedObjects = Array.Empty<string>();
        public string[] keyItems = Array.Empty<string>();
        public string[] openedDoors = Array.Empty<string>();
    }
    public sealed class WorldStateService
    {
        public const string KeyObjectId = "pickup-guardian-key", KeyItemId = "guardian-key";
        public const string GateId = "gate-guardian", BossId = "boss-guardian", QuestId = "guardian-trial";
        public event Action<WorldEventKind, string> Changed;
        readonly HashSet<string> collected = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> items = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> doors = new HashSet<string>(StringComparer.Ordinal);
        public bool IsCollected(string id) => collected.Contains(id);
        public bool HasItem(string id) => items.Contains(id);
        public bool IsDoorOpen(string id) => doors.Contains(id);
        public void Restore(WorldStateData data)
        {
            Fill(collected, data?.collectedObjects); Fill(items, data?.keyItems); Fill(doors, data?.openedDoors);
        }
        static void Fill(HashSet<string> target, string[] values)
        { target.Clear(); if (values != null) foreach (var value in values) if (!string.IsNullOrWhiteSpace(value)) target.Add(value); }
        static string[] Sorted(HashSet<string> values)
        { var result = new string[values.Count]; values.CopyTo(result); Array.Sort(result, StringComparer.Ordinal); return result; }
        public WorldStateData Capture() => new WorldStateData
        { collectedObjects = Sorted(collected), keyItems = Sorted(items), openedDoors = Sorted(doors) };
        public void Apply(WorldEventKind kind, string id, string itemId = null)
        {
            bool changed = true;
            if (kind == WorldEventKind.ItemAcquired) { changed = collected.Add(id); if (itemId != null) changed |= items.Add(itemId); }
            if (kind == WorldEventKind.DoorOpened) changed = doors.Add(id);
            if (changed) Changed?.Invoke(kind, id);
        }
        public void WriteTo(PlayerSnapshot snapshot) => snapshot.worldState = Capture();
    }
}
