using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Milkfrog.CombatDemo
{
    public sealed class InventoryView : MonoBehaviour
    {
        public Text vitals, message, empty, itemName, itemCount, description, effect;
        public Image detailIcon;
        public ScrollRect scroll;
        public RectTransform content;
        public InventoryTile tileTemplate;
        public Button useButton, closeButton;
        public Button[] categoryButtons;
        public bool Configured => vitals != null && message != null && empty != null && itemName != null && itemCount != null &&
            description != null && effect != null && detailIcon != null && scroll != null && scroll.viewport != null && content != null &&
            tileTemplate != null && tileTemplate.label != null && tileTemplate.count != null && tileTemplate.icon != null &&
            tileTemplate.button != null && useButton != null && closeButton != null && categoryButtons != null &&
            categoryButtons.Length == 3 && Array.TrueForAll(categoryButtons, b => b != null);
        public bool Visible => gameObject.activeSelf;
        readonly List<InventoryTile> tiles = new List<InventoryTile>();
        MvpWorld world;
        string selectedId;
        int category;
        Coroutine selectionRoutine;

        public void Show(MvpWorld owner)
        {
            Detach(); world = owner; world.Inventory.Changed += Refresh;
            gameObject.SetActive(true);
            closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(world.Flow.CloseInventory);
            useButton.onClick.RemoveAllListeners(); useButton.onClick.AddListener(() => { world.TryUseItem(selectedId); RefreshDetail(); });
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                int index = i; categoryButtons[i].onClick.RemoveAllListeners();
                categoryButtons[i].onClick.AddListener(() => SetCategory(index));
            }
            Refresh();
        }
        public void Hide()
        {
            if (selectionRoutine != null) { StopCoroutine(selectionRoutine); selectionRoutine = null; }
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform)) EventSystem.current.SetSelectedGameObject(null);
            Detach(); gameObject.SetActive(false);
        }
        void Detach() { if (world != null) world.Inventory.Changed -= Refresh; }
        void OnDestroy() => Detach();
        public void SetCategory(int value)
        {
            category = Mathf.Clamp(value, 0, 2); selectedId = null;
            scroll.verticalNormalizedPosition = 1; Refresh();
        }
        public void SelectItem(string id)
        { selectedId = tiles.Exists(t => t.itemId == id) ? id : null; RefreshDetail(); }
        public void Refresh()
        {
            if (world == null) return;
            foreach (var tile in tiles) { tile.gameObject.SetActive(false); Destroy(tile.gameObject); }
            tiles.Clear();
            foreach (var entry in world.Inventory.Capture().entries)
            {
                var definition = world.FindItem(entry.itemId);
                if (category > 0 && (definition == null || (int)definition.category != category - 1)) continue;
                var tile = Instantiate(tileTemplate, content); tile.gameObject.SetActive(true);
                tile.label.text = definition != null ? definition.displayName : entry.itemId;
                tile.count.text = "×" + entry.quantity;
                tile.icon.sprite = definition?.icon; tile.icon.enabled = tile.icon.sprite != null;
                string id = entry.itemId; tile.OnSelected = () => { SelectItem(id); EnsureVisible(tile); };
                tile.button.onClick.AddListener(() => { SelectItem(id); EnsureVisible(tile); });
                tiles.Add(tile);
                tile.itemId = id;
            }
            empty.text = category == 0 ? "背包空空如也\n靠近物品后按 E 拾取" : "此分类暂无物品";
            empty.gameObject.SetActive(tiles.Count == 0);
            bool found = tiles.Exists(t => t.itemId == selectedId);
            if (!found) selectedId = tiles.Count > 0 ? tiles[0].itemId : null;
            for (int i = 0; i < categoryButtons.Length; i++)
                categoryButtons[i].GetComponent<Image>().color = i == category ? new Color32(89, 72, 48, 255) : new Color32(43, 52, 64, 255);
            RefreshDetail();
            if (selectionRoutine != null) StopCoroutine(selectionRoutine);
            selectionRoutine = StartCoroutine(SelectNextFrame());
        }
        IEnumerator SelectNextFrame()
        {
            yield return null; Canvas.ForceUpdateCanvases(); selectionRoutine = null;
            if (!Visible || EventSystem.current == null) yield break;
            var tile = tiles.Find(t => t.itemId == selectedId);
            EventSystem.current.SetSelectedGameObject(tile != null ? tile.gameObject : categoryButtons[category].gameObject);
        }
        void EnsureVisible(InventoryTile tile)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)tile.transform;
            float overflow = content.rect.height - scroll.viewport.rect.height;
            if (overflow <= 0) return;
            float top = -rect.anchoredPosition.y - rect.rect.height * .5f;
            float bottom = top + rect.rect.height;
            float offset = content.anchoredPosition.y;
            if (top < offset) scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01(top / overflow);
            else if (bottom > offset + scroll.viewport.rect.height)
                scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01((bottom - scroll.viewport.rect.height) / overflow);
        }
        void RefreshDetail()
        {
            var item = world.FindItem(selectedId);
            itemName.text = selectedId == null ? "选择物品" : item != null ? item.displayName : selectedId;
            itemCount.text = selectedId == null ? "" : "持有数量  " + world.Inventory.Quantity(selectedId);
            description.text = item != null ? item.description : selectedId == null ? "拾取后可查看说明与效果。" : "物品配置暂不可用，数量已保留。";
            effect.text = item != null ? item.EffectLabel : "";
            detailIcon.sprite = item?.icon; detailIcon.enabled = detailIcon.sprite != null;
            string reason = world.ItemUseBlockReason(item);
            useButton.interactable = selectedId != null && reason == null;
            useButton.GetComponentInChildren<Text>().text = selectedId == null ? "暂无物品" : reason ?? "使用（消耗 1 个）";
            foreach (var tile in tiles)
                tile.GetComponent<Image>().color = tile.itemId == selectedId ? new Color32(89, 72, 48, 255) : new Color32(43, 52, 64, 255);
        }
        void LateUpdate()
        {
            if (world == null) return;
            var core = world.player.Core;
            vitals.text = $"生命 {core.Health:0}/{core.Tuning.maxHealth:0}     架势 {core.Posture:0}/{core.Tuning.maxPosture:0}";
            message.text = string.IsNullOrEmpty(world.Flow.Message) ? "游戏已暂停 · B 关闭 / Esc 返回 · 方向键选择 / Enter 确认" : world.Flow.Message;
        }

        // Creates authored UI for the editor upgrade. Runtime uses the saved Prefab and its references.
        public static InventoryView BuildLayout(GameObject root, Font font)
        {
            var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1100;
            var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            root.AddComponent<GraphicRaycaster>(); var view = root.AddComponent<InventoryView>();
            var shade = Rect("Backdrop", root.transform, new Vector2(1280,720), Vector2.zero);
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.sizeDelta = Vector2.zero;
            shade.gameObject.AddComponent<Image>().color = new Color32(0,0,0,180);
            var panel = Rect("Panel", root.transform, new Vector2(1120,640), Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = new Color32(17,23,31,255);
            Label("Title", panel, font, "行囊  /  MILKFROG", new Vector2(1016,48), new Vector2(0,271), 30, true);
            view.vitals = Label("Vitals", panel, font, "", new Vector2(1016,36), new Vector2(0,222), 22);
            var left = Rect("Items", panel, new Vector2(580,410), new Vector2(-244,-22));
            view.categoryButtons = new Button[3];
            string[] categories = {"全部", "消耗品", "任务物品"};
            for (int i = 0; i < 3; i++) view.categoryButtons[i] = MakeButton("Category"+i,left,font,categories[i],new Vector2(176,42),new Vector2(-192+i*192,190));
            var viewport = Rect("Viewport",left,new Vector2(580,350),new Vector2(0,-24));
            viewport.gameObject.AddComponent<Image>().color = new Color32(23,30,40,255); viewport.gameObject.AddComponent<RectMask2D>();
            view.scroll = viewport.gameObject.AddComponent<ScrollRect>(); view.scroll.viewport = viewport;
            view.scroll.horizontal = false; view.scroll.movementType = ScrollRect.MovementType.Clamped;
            var content = Rect("Content",viewport,new Vector2(580,350),Vector2.zero);
            content.anchorMin = new Vector2(0,1); content.anchorMax = new Vector2(1,1); content.pivot = new Vector2(.5f,1);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(128,142); grid.spacing = new Vector2(12,12);
            grid.padding = new RectOffset(16,16,12,12); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            view.content = content; view.scroll.content = content;
            view.empty = Label("Empty",viewport,font,"",new Vector2(540,140),Vector2.zero,24);
            var tileButton = MakeButton("ItemTemplate",content,font,"",new Vector2(128,142),Vector2.zero);
            DestroyImmediate(tileButton.GetComponentInChildren<Text>().gameObject);
            var tile = tileButton.gameObject.AddComponent<InventoryTile>(); tile.button = tileButton;
            tile.icon = Rect("Icon",tile.transform,new Vector2(56,56),new Vector2(0,29)).gameObject.AddComponent<Image>();
            tile.icon.preserveAspect = true; tile.icon.raycastTarget = false;
            tile.label = Label("Name",tile.transform,font,"",new Vector2(120,32),new Vector2(0,-22),20);
            tile.count = Label("Count",tile.transform,font,"",new Vector2(120,26),new Vector2(0,-51),18);
            tile.gameObject.SetActive(false); view.tileTemplate = tile;
            var right = Rect("Details",panel,new Vector2(400,430),new Vector2(330,-13));
            right.gameObject.AddComponent<Image>().color = new Color32(23,30,40,255);
            view.itemName = Label("Name",right,font,"选择物品",new Vector2(352,46),new Vector2(0,173),28,true);
            view.detailIcon = Rect("Icon",right,new Vector2(72,72),new Vector2(0,106)).gameObject.AddComponent<Image>();
            view.detailIcon.preserveAspect = true; view.detailIcon.raycastTarget = false;
            view.itemCount = Label("Count",right,font,"",new Vector2(352,32),new Vector2(0,48),22);
            view.description = Label("Description",right,font,"",new Vector2(352,118),new Vector2(0,-31),22);
            view.description.alignment = TextAnchor.UpperLeft;
            view.effect = Label("Effect",right,font,"",new Vector2(352,58),new Vector2(0,-120),20);
            view.useButton = MakeButton("Use",right,font,"使用",new Vector2(352,52),new Vector2(0,-180));
            view.message = Label("Message",panel,font,"",new Vector2(850,60),new Vector2(-84,-280),19);
            view.closeButton = MakeButton("Close",panel,font,"返回",new Vector2(140,44),new Vector2(438,-278));
            return view;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var obj = new GameObject(name,typeof(RectTransform)); obj.transform.SetParent(parent,false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        static Text Label(string name, Transform parent, Font font, string text, Vector2 size, Vector2 position, int fontSize, bool gold = false)
        {
            var label = Rect(name,parent,size,position).gameObject.AddComponent<Text>(); label.font = font; label.fontSize = fontSize;
            label.text = text; label.alignment = TextAnchor.MiddleCenter; label.color = gold ? new Color32(235,190,111,255) : new Color32(222,225,230,255);
            label.supportRichText = false; label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }
        static Button MakeButton(string name, Transform parent, Font font, string text, Vector2 size, Vector2 position)
        {
            var rect = Rect(name,parent,size,position); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color32(43,52,64,255);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color32(160,160,160,255); colors.selectedColor = new Color32(200,175,130,255);
            colors.disabledColor = new Color32(105,110,120,200); button.colors = colors;
            Label("Label",rect,font,text,size-new Vector2(12,4),Vector2.zero,22); return button;
        }
    }
}
