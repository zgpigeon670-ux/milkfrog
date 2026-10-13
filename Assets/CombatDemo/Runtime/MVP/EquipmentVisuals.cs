using System;
using System.Collections.Generic;
using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class EquipmentVisuals : MonoBehaviour
    {
        public Transform modelRoot;
        readonly Dictionary<EquipmentSlot, string> shown = new Dictionary<EquipmentSlot, string>();
        readonly Dictionary<EquipmentSlot, List<GameObject>> objects = new Dictionary<EquipmentSlot, List<GameObject>>();
        public int VisiblePartCount { get { int count = 0; foreach (var list in objects.Values) count += list.Count; return count; } }
        public void Apply(InventoryService bag, Func<string, ItemDefinition> resolve)
        {
            if (modelRoot == null) return;
            for (int i = 0; i < 4; i++)
            {
                var slot = (EquipmentSlot)i; string id = bag.Equipment.Get(slot); var item = resolve(id);
                if (item == null || !bag.HasItem(id) || !item.EquipmentValid || item.equipmentSlot != slot) id = "";
                if (shown.TryGetValue(slot, out var old) && old == id) continue;
                if (objects.TryGetValue(slot, out var previous)) foreach (var obj in previous) if (obj != null) { obj.SetActive(false); Destroy(obj); }
                var parts = new List<GameObject>(); objects[slot] = parts; shown[slot] = id;
                if (id.Length == 0 || item.visualPrefab == null) continue;
                var prefabParts = item.visualPrefab.GetComponentsInChildren<EquipmentVisualPart>(true);
                foreach (var part in prefabParts)
                {
                    Transform bone = null;
                    foreach (var candidate in modelRoot.GetComponentsInChildren<Transform>(true)) if (candidate.name == part.boneName) { bone = candidate; break; }
                    if (bone == null) { Debug.LogError("Missing equipment bone: " + part.boneName); continue; }
                    var instance = Instantiate(part.gameObject, bone, false); instance.name = "Equipped " + item.displayName;
                    foreach (var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = modelRoot.gameObject.layer;
                    parts.Add(instance);
                }
            }
        }
        void OnDestroy() { foreach (var list in objects.Values) foreach (var obj in list) if (obj != null) Destroy(obj); }
    }
}
