using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Milkfrog.CombatDemo
{
    public sealed class InventoryTile : MonoBehaviour, ISelectHandler
    {
        public Text label, count;
        public Image icon;
        public Button button;
        [NonSerialized] public string itemId;
        [NonSerialized] public Action OnSelected;
        public void OnSelect(BaseEventData eventData) => OnSelected?.Invoke();
    }
}
