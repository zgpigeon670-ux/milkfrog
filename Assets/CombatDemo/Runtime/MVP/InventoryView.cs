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
        public Text vitals, message, empty, itemName, itemCount, description, effect, statsText, comparisonText;
        public Image detailIcon;
        public ScrollRect scroll;
        public RectTransform content;
        public InventoryTile tileTemplate;
        public Button useButton, closeButton, equipButton, unequipButton, gearEquipButton, gearUnequipButton;
        public Button[] categoryButtons, quickAssignButtons, quickSlotButtons, equipmentButtons, skillButtons;
        public CharacterPreviewView preview;
        public int layoutVersion;
        public bool Configured => layoutVersion == 3 && vitals != null && message != null && empty != null && itemName != null && itemCount != null &&
            description != null && effect != null && statsText != null && comparisonText != null && detailIcon != null && scroll != null && scroll.viewport != null && content != null &&
            tileTemplate != null && tileTemplate.label != null && tileTemplate.count != null && tileTemplate.icon != null && tileTemplate.equippedBadge != null &&
            useButton != null && equipButton != null && unequipButton != null && gearEquipButton != null && gearUnequipButton != null && closeButton != null && preview != null && preview.image != null &&
            Valid(categoryButtons,4) && Valid(quickAssignButtons,3) && Valid(quickSlotButtons,3) && Valid(equipmentButtons,5) && Valid(skillButtons,3);
        static bool Valid(Button[] buttons,int count) => buttons != null && buttons.Length == count && Array.TrueForAll(buttons,b=>b!=null);
        public bool Visible => gameObject.activeSelf;
        readonly List<InventoryTile> tiles = new List<InventoryTile>();
        MvpWorld world;
        string selectedId;
        int category, slotFilter = -1;
        Coroutine selectionRoutine;
        float renderedScale = -1;
        public void Show(MvpWorld owner)
        {
            Detach(); world = owner; world.Inventory.Changed += Refresh; gameObject.SetActive(true);
            closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(world.Flow.CloseInventory);
            useButton.onClick.RemoveAllListeners(); useButton.onClick.AddListener(()=>{world.TryUseItem(selectedId);RefreshDetail();});
            gearEquipButton.onClick.RemoveAllListeners();gearEquipButton.onClick.AddListener(()=>{world.TryEquipGear(selectedId);RefreshDetail();});
            gearUnequipButton.onClick.RemoveAllListeners();gearUnequipButton.onClick.AddListener(()=>{var item=world.FindItem(selectedId);if(item!=null)world.TryUnequipGear(item.equipmentSlot);RefreshDetail();});
            unequipButton.onClick.RemoveAllListeners();unequipButton.onClick.AddListener(()=>world.TryClearQuickItem(world.Inventory.ActiveQuickSlot));
            for(int i=0;i<4;i++){int index=i;categoryButtons[i].onClick.RemoveAllListeners();categoryButtons[i].onClick.AddListener(()=>SetCategory(index));}
            for(int i=0;i<3;i++)
            {
                int index=i;quickAssignButtons[i].onClick.RemoveAllListeners();quickAssignButtons[i].onClick.AddListener(()=>world.TryAssignQuickItem(index,selectedId));
                quickSlotButtons[i].onClick.RemoveAllListeners();quickSlotButtons[i].onClick.AddListener(()=>{world.Inventory.SelectQuickSlot(index);SetCategory(0);SelectItem(world.Inventory.QuickItem(index));});
            }
            for(int i=0;i<5;i++){int index=i;equipmentButtons[i].onClick.RemoveAllListeners();equipmentButtons[i].onClick.AddListener(()=>FilterEquipment((EquipmentSlot)index));}
            preview.Show(world); Refresh();
        }
        public void Hide()
        {
            if(selectionRoutine!=null){StopCoroutine(selectionRoutine);selectionRoutine=null;}
            if(EventSystem.current!=null && EventSystem.current.currentSelectedGameObject!=null && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))EventSystem.current.SetSelectedGameObject(null);
            preview?.Release();Detach();gameObject.SetActive(false);
        }
        void Detach(){if(world!=null)world.Inventory.Changed-=Refresh;}
        void OnDestroy()=>Detach();
        public void SetCategory(int value){category=Mathf.Clamp(value,0,3);slotFilter=-1;selectedId=null;scroll.verticalNormalizedPosition=1;Refresh();}
        public void FilterEquipment(EquipmentSlot slot){category=1;slotFilter=(int)slot;selectedId=world.Inventory.Equipment.Get(slot);scroll.verticalNormalizedPosition=1;Refresh();}
        public void SelectItem(string id){selectedId=tiles.Exists(t=>t.itemId==id)?id:null;RefreshDetail();}
        public void Refresh()
        {
            if(world==null)return;
            if(selectionRoutine!=null){StopCoroutine(selectionRoutine);selectionRoutine=null;}
            var focused=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            int previousIndex=tiles.FindIndex(t=>t.itemId==selectedId);var previous=new Dictionary<string,InventoryTile>(StringComparer.Ordinal);
            foreach(var tile in tiles)previous.Add(tile.itemId,tile);tiles.Clear();
            foreach(var entry in world.Inventory.Capture().entries)
            {
                var definition=world.FindItem(entry.itemId);
                if(category>0 && (definition==null || definition.category!=(category==1?ItemCategory.Equipment:category==2?ItemCategory.Consumable:ItemCategory.Quest)))continue;
                if(slotFilter>=0 && (definition==null || (int)definition.equipmentSlot!=slotFilter))continue;
                string id=entry.itemId;bool exists=previous.TryGetValue(id,out var tile);
                if(exists)previous.Remove(id);
                else{tile=Instantiate(tileTemplate,content);var created=tile;tile.itemId=id;tile.OnSelected=()=>{SelectItem(id);EnsureVisible(created);};tile.button.onClick.AddListener(()=>{SelectItem(id);EnsureVisible(created);});}
                tile.gameObject.SetActive(true);tile.transform.SetAsLastSibling();tile.label.text=definition!=null?definition.displayName:entry.itemId;
                tile.count.text="×"+entry.quantity;tile.icon.sprite=definition?.icon;tile.icon.enabled=tile.icon.sprite!=null;tiles.Add(tile);
            }
            foreach(var tile in previous.Values){tile.gameObject.SetActive(false);Destroy(tile.gameObject);}
            empty.text=category==0?"背包空空如也\n靠近物品后按 E 拾取":"此分类暂无物品";empty.gameObject.SetActive(tiles.Count==0);
            if(!tiles.Exists(t=>t.itemId==selectedId))selectedId=tiles.Count>0?tiles[Mathf.Clamp(previousIndex,0,tiles.Count-1)].itemId:null;
            for(int i=0;i<categoryButtons.Length;i++)categoryButtons[i].GetComponent<Image>().color=i==category?SelectedColor:TileColor;
            RefreshDetail();preview.RefreshEquipment();
            var selectable=focused!=null?focused.GetComponent<Selectable>():null;
            if(focused!=null && focused.transform.IsChildOf(transform) && focused.activeInHierarchy && selectable!=null && selectable.IsInteractable())return;
            selectionRoutine=StartCoroutine(SelectNextFrame());
        }
        IEnumerator SelectNextFrame(){yield return null;Canvas.ForceUpdateCanvases();selectionRoutine=null;if(!Visible||EventSystem.current==null)yield break;var tile=tiles.Find(t=>t.itemId==selectedId);EventSystem.current.SetSelectedGameObject(tile!=null?tile.gameObject:categoryButtons[category].gameObject);if(tile!=null)EnsureVisible(tile);}
        void EnsureVisible(InventoryTile tile)
        {
            Canvas.ForceUpdateCanvases();var rect=(RectTransform)tile.transform;float overflow=content.rect.height-scroll.viewport.rect.height;if(overflow<=0)return;
            float top=-rect.anchoredPosition.y-rect.rect.height*.5f,bottom=top+rect.rect.height,offset=content.anchoredPosition.y;
            if(top<offset)scroll.verticalNormalizedPosition=1-Mathf.Clamp01(top/overflow);else if(bottom>offset+scroll.viewport.rect.height)scroll.verticalNormalizedPosition=1-Mathf.Clamp01((bottom-scroll.viewport.rect.height)/overflow);
        }
        void RefreshDetail()
        {
            var item=world.FindItem(selectedId);bool gear=item!=null && item.category==ItemCategory.Equipment;
            itemName.text=selectedId==null?"选择物品":item!=null?item.displayName:selectedId;
            itemCount.text=selectedId==null?"":"持有数量  "+world.Inventory.Quantity(selectedId);
            description.text=item!=null?item.description:selectedId==null?"选择物品查看说明，点击右侧槽位配置装备。":"物品配置缺失，数量与绑定已保留。";
            effect.text=item!=null?item.EffectLabel:"";detailIcon.sprite=item?.icon;detailIcon.enabled=detailIcon.sprite!=null;
            string reason=world.ItemUseBlockReason(item);useButton.gameObject.SetActive(!gear);useButton.interactable=selectedId!=null && reason==null;
            useButton.GetComponentInChildren<Text>().text=selectedId==null?"暂无物品":reason=="当前动作期间无法使用道具"?"动作中禁用":reason??"使用（消耗 1）";
            for(int i=0;i<3;i++)
            {
                quickAssignButtons[i].gameObject.SetActive(!gear);bool bound=selectedId!=null && world.Inventory.QuickItem(i)==selectedId;
                quickAssignButtons[i].interactable=!bound && world.ItemEquipBlockReason(item)==null;
                quickAssignButtons[i].GetComponentInChildren<Text>().text=bound?$"已在槽 {i+1}":$"放入槽 {i+1}";
            }
            gearEquipButton.gameObject.SetActive(gear);gearUnequipButton.gameObject.SetActive(gear);
            bool worn=gear && world.Inventory.Equipment.Get(item.equipmentSlot)==selectedId;
            gearEquipButton.interactable=gear && !worn && world.EquipmentBlockReason(item)==null;
            gearEquipButton.GetComponentInChildren<Text>().text=worn?"已穿戴":"穿戴";
            gearUnequipButton.interactable=worn && item.equipmentSlot!=EquipmentSlot.Weapon && world.EquipmentBlockReason()==null;
            gearUnequipButton.GetComponentInChildren<Text>().text=gear && item.equipmentSlot==EquipmentSlot.Weapon?"默认武器保留":"卸下";
            unequipButton.interactable=world.Inventory.EquippedItemId.Length>0;
            comparisonText.text="";
            if(gear && item.EquipmentValid)
            {
                var next=new InventoryService();next.Restore(world.Inventory.Capture());
                if(worn)next.Equipment.TryUnequip(item.equipmentSlot);else next.Equipment.TryEquip(item,true);
                var before=CharacterStatsCalculator.Calculate(world.Inventory,world.FindItem);var after=CharacterStatsCalculator.Calculate(next,world.FindItem);
                comparisonText.text=$"{(worn?"卸下后":"穿戴后")}：重量 {before.Weight:0.#} → {after.Weight:0.#}；移速 {world.settings.playerSpeed*before.MovementMultiplier:0.##} → {world.settings.playerSpeed*after.MovementMultiplier:0.##}\n减伤 {before.DamageReduction:P0} → {after.DamageReduction:P0}；武器伤害倍率 {before.WeaponDamageMultiplier:0.##} → {after.WeaponDamageMultiplier:0.##}";
            }
            foreach(var tile in tiles)
            {
                tile.GetComponent<Image>().color=tile.itemId==selectedId?SelectedColor:TileColor;string badge="";
                for(int i=0;i<3;i++)if(world.Inventory.QuickItem(i)==tile.itemId)badge=(i+1)+" Q";
                for(int i=0;i<5;i++)if(world.Inventory.Equipment.Get((EquipmentSlot)i)==tile.itemId)badge="穿戴";
                tile.equippedBadge.text=badge;tile.equippedBadge.gameObject.SetActive(badge.Length>0);
            }
            for(int i=0;i<5;i++)
            {
                var definition=world.FindItem(world.Inventory.Equipment.Get((EquipmentSlot)i));string name=SlotName((EquipmentSlot)i);
                equipmentButtons[i].GetComponentInChildren<Text>().text=name+"\n"+(definition!=null?(definition.stableId==EquipmentService.DefaultWeaponId?"练习剑":definition.displayName):string.IsNullOrEmpty(world.Inventory.Equipment.Get((EquipmentSlot)i))?"未穿戴":"配置缺失");
            }
            for(int i=0;i<3;i++)
            {
                string id=world.Inventory.QuickItem(i);var definition=world.FindItem(id);
                quickSlotButtons[i].GetComponent<Image>().color=i==world.Inventory.ActiveQuickSlot?SelectedColor:TileColor;
                quickSlotButtons[i].GetComponentInChildren<Text>().text=$"{i+1}  "+(id.Length==0?"空槽":definition!=null?definition.displayName:"配置缺失")+$"\n×{world.Inventory.Quantity(id)}";
                var icon=quickSlotButtons[i].transform.Find("Icon").GetComponent<Image>();icon.sprite=definition?.icon;icon.enabled=icon.sprite!=null;
            }
        }
        void LateUpdate()
        {
            if(world==null)return;float scale=GetComponent<Canvas>().scaleFactor;
            if(!Mathf.Approximately(renderedScale,scale)){renderedScale=scale;foreach(var text in GetComponentsInChildren<Text>(true))text.SetAllDirty();}
            var core=world.player.Core;var growth=world.Progression;var stats=world.EquipmentStats;
            vitals.text=$"生命 {core.Health:0}/{core.Tuning.maxHealth:0}  架势 {core.Posture:0}/{core.Tuning.maxPosture:0}  等级 {growth.Level}  经验 {growth.Experience}";
            float damage=growth.AttackMultiplier*stats.WeaponDamageMultiplier*core.AttackBuffMultiplier;
            var light=world.player.lightAttack.rules;var follow=world.player.followupAttack.rules;var thrust=world.player.thrustAttack.rules;
            statsText.text=$"成长：体魄 {growth.Vitality} / 定力 {growth.Resolve} / 攻击 {growth.Power} · 下次 {growth.NextCost}\n"+
                $"伤害：普通 {light.damage*damage:0.#} / 追击 {follow.damage*damage:0.#} / 蓄力 {thrust.damage*damage:0.#}–{thrust.maxDamage*damage:0.#}\n"+
                $"护具 {stats.ArmorDefense:0.#} · 减伤 {stats.DamageReduction:P0}   格挡 {stats.GuardDefense:0.#} · 减架势 {stats.GuardReduction:P0}\n"+
                $"重量 {stats.Weight:0.#} · 鞋加速 {stats.SpeedBonus:P0} · 移速 {world.settings.playerSpeed*stats.MovementMultiplier:0.##}"+
                (core.AttackBuffRemaining>0?$"\n战意 +{(core.AttackBuffMultiplier-1)*100:0}% · {Mathf.CeilToInt(core.AttackBuffRemaining)} 秒":"");
            message.text=string.IsNullOrEmpty(world.Flow.Message)?"Tab 关闭 / Esc 返回 · 方向键选择 / Enter 确认":world.Flow.Message;
        }
        static Color32 TileColor=>new Color32(43,52,64,255);
        static Color32 SelectedColor=>new Color32(89,72,48,255);
        public static string SlotName(EquipmentSlot slot)=>slot==EquipmentSlot.Head?"头部":slot==EquipmentSlot.Body?"身体":slot==EquipmentSlot.Legs?"腿部":slot==EquipmentSlot.Shoes?"鞋子":"武器";
        public static InventoryView BuildLayout(GameObject root,Font font)
        {
            var canvas=root.GetComponent<Canvas>()??root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1100;
            var scaler=root.GetComponent<CanvasScaler>()??root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            if(root.GetComponent<GraphicRaycaster>()==null)root.AddComponent<GraphicRaycaster>();var view=root.GetComponent<InventoryView>()??root.AddComponent<InventoryView>();
            view.layoutVersion=3;
            var shade=Rect("Backdrop",root.transform,new Vector2(1280,720),Vector2.zero);shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.sizeDelta=Vector2.zero;shade.gameObject.AddComponent<Image>().color=new Color32(0,0,0,180);
            var panel=Rect("Panel",root.transform,new Vector2(1200,660),Vector2.zero);panel.gameObject.AddComponent<Image>().color=new Color32(17,23,31,255);
            Label("Title",panel,font,"行囊与装备  /  MILKFROG",new Vector2(1100,40),new Vector2(0,302),26,true);
            view.categoryButtons=new Button[4];string[] categories={"全部","装备","消耗品","任务"};
            for(int i=0;i<4;i++)view.categoryButtons[i]=MakeButton("Category"+i,panel,font,categories[i],new Vector2(146,36),new Vector2(-500+i*160,253));
            var viewport=Rect("Viewport",panel,new Vector2(640,274),new Vector2(-260,93));viewport.gameObject.AddComponent<Image>().color=new Color32(23,30,40,255);viewport.gameObject.AddComponent<RectMask2D>();
            view.scroll=viewport.gameObject.AddComponent<ScrollRect>();view.scroll.viewport=viewport;view.scroll.horizontal=false;view.scroll.movementType=ScrollRect.MovementType.Clamped;
            var content=Rect("Content",viewport,Vector2.zero,Vector2.zero);content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
            var grid=content.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(142,116);grid.spacing=new Vector2(12,12);grid.padding=new RectOffset(12,12,12,12);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=4;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;view.content=content;view.scroll.content=content;
            view.empty=Label("Empty",viewport,font,"",new Vector2(590,130),Vector2.zero,22);
            var template=MakeButton("ItemTemplate",content,font,"",new Vector2(142,116),Vector2.zero);DestroyImmediate(template.GetComponentInChildren<Text>().gameObject);
            var tile=template.gameObject.AddComponent<InventoryTile>();tile.button=template;tile.icon=Rect("Icon",tile.transform,new Vector2(48,48),new Vector2(0,24)).gameObject.AddComponent<Image>();tile.icon.preserveAspect=true;tile.icon.raycastTarget=false;
            tile.label=Label("Name",tile.transform,font,"",new Vector2(132,28),new Vector2(0,-17),18);tile.count=Label("Count",tile.transform,font,"",new Vector2(132,24),new Vector2(0,-44),16);
            tile.equippedBadge=Label("Equipped",tile.transform,font,"",new Vector2(48,22),new Vector2(44,44),15,true);tile.gameObject.SetActive(false);view.tileTemplate=tile;
            var details=Rect("Details",panel,new Vector2(640,236),new Vector2(-260,-170));details.gameObject.AddComponent<Image>().color=new Color32(23,30,40,255);
            view.detailIcon=Rect("Icon",details,new Vector2(64,64),new Vector2(-272,57)).gameObject.AddComponent<Image>();view.detailIcon.preserveAspect=true;view.detailIcon.raycastTarget=false;
            view.itemName=Label("Name",details,font,"选择物品",new Vector2(340,40),new Vector2(-60,91),24,true);view.itemCount=Label("Count",details,font,"",new Vector2(170,30),new Vector2(208,91),18);
            var textViewport=Rect("DescriptionViewport",details,new Vector2(548,76),new Vector2(34,30));textViewport.gameObject.AddComponent<Image>().color=new Color(0,0,0,0);textViewport.gameObject.AddComponent<RectMask2D>();
                        var barRect=Rect("DetailScrollbar",details,new Vector2(10,76),new Vector2(309,30));barRect.gameObject.AddComponent<Image>().color=TileColor;
            var bar=barRect.gameObject.AddComponent<Scrollbar>();var handle=Rect("Handle",barRect,Vector2.zero,Vector2.zero);handle.gameObject.AddComponent<Image>().color=new Color32(175,143,86,255);bar.targetGraphic=handle.GetComponent<Image>();bar.handleRect=handle;bar.direction=Scrollbar.Direction.BottomToTop;
            var textScroll=textViewport.gameObject.AddComponent<ScrollRect>();textScroll.verticalScrollbar=bar;textScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;textScroll.viewport=textViewport;textScroll.horizontal=false;textScroll.movementType=ScrollRect.MovementType.Clamped;
            var textContent=Rect("TextContent",textViewport,Vector2.zero,Vector2.zero);textContent.anchorMin=new Vector2(0,1);textContent.anchorMax=new Vector2(1,1);textContent.pivot=new Vector2(.5f,1);
            var vertical=textContent.gameObject.AddComponent<VerticalLayoutGroup>();vertical.childControlHeight=true;vertical.childControlWidth=true;vertical.childForceExpandHeight=false;vertical.spacing=4;
            textContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;textScroll.content=textContent;
            view.description=Label("Description",textContent,font,"",new Vector2(548,48),Vector2.zero,18);view.description.alignment=TextAnchor.UpperLeft;
            view.effect=Label("Effect",textContent,font,"",new Vector2(548,44),Vector2.zero,18,true);view.effect.alignment=TextAnchor.UpperLeft;
            view.comparisonText=Label("Comparison",details,font,"",new Vector2(600,52),new Vector2(0,-40),16,true);view.comparisonText.alignment=TextAnchor.UpperLeft;
            view.useButton=MakeButton("Use",details,font,"使用",new Vector2(146,34),new Vector2(-240,-91));view.useButton.GetComponentInChildren<Text>().fontSize=16;
            view.quickAssignButtons=new Button[3];for(int i=0;i<3;i++)view.quickAssignButtons[i]=MakeButton("Assign"+i,details,font,"放入槽 "+(i+1),new Vector2(146,34),new Vector2(-80+i*160,-91));view.equipButton=view.quickAssignButtons[0];
            view.gearEquipButton=MakeButton("Wear",details,font,"穿戴",new Vector2(220,34),new Vector2(-140,-91));view.gearUnequipButton=MakeButton("RemoveGear",details,font,"卸下",new Vector2(220,34),new Vector2(100,-91));
            Label("CharacterTitle",panel,font,"角色装备",new Vector2(480,22),new Vector2(324,276),18,true);
            var portrait=Rect("Portrait",panel,new Vector2(264,252),new Vector2(325,135));var image=portrait.gameObject.AddComponent<RawImage>();view.preview=portrait.gameObject.AddComponent<CharacterPreviewView>();view.preview.image=image;
            view.equipmentButtons=new Button[5];var positions=new[]{new Vector2(125,213),new Vector2(125,125),new Vector2(125,37),new Vector2(518,169),new Vector2(518,81)};
            for(int i=0;i<5;i++){view.equipmentButtons[i]=MakeButton("Gear"+i,panel,font,SlotName((EquipmentSlot)i)+"\n未穿戴",new Vector2(86,74),positions[i]);view.equipmentButtons[i].GetComponentInChildren<Text>().fontSize=16;}
            view.vitals=Label("Vitals",panel,font,"",new Vector2(488,26),new Vector2(324,-9),16,true);
            view.statsText=Label("Stats",panel,font,"",new Vector2(488,118),new Vector2(324,-80),16);view.statsText.alignment=TextAnchor.UpperLeft;
            Label("QuickTitle",panel,font,"快捷消耗品 · 1 / 2 / 3 切换，Q 使用",new Vector2(488,22),new Vector2(324,-150),16,true);
            view.quickSlotButtons=new Button[3];view.skillButtons=new Button[3];
            for(int i=0;i<3;i++)
            {
                var button=MakeButton("QuickSlot"+i,panel,font,"空槽",new Vector2(142,54),new Vector2(164+i*160,-188));view.quickSlotButtons[i]=button;
                var label=button.GetComponentInChildren<Text>();label.fontSize=15;((RectTransform)label.transform).sizeDelta=new Vector2(98,50);((RectTransform)label.transform).anchoredPosition=new Vector2(19,0);
                var icon=Rect("Icon",button.transform,new Vector2(32,32),new Vector2(-51,0)).gameObject.AddComponent<Image>();icon.preserveAspect=true;icon.raycastTarget=false;
                view.skillButtons[i]=MakeButton("SkillSlot"+i,panel,font,$"技能 {i+1}\n未开放",new Vector2(142,48),new Vector2(164+i*160,-263));view.skillButtons[i].interactable=false;view.skillButtons[i].GetComponentInChildren<Text>().fontSize=15;
            }
            Label("SkillsTitle",panel,font,"预留技能",new Vector2(488,20),new Vector2(324,-227),15);
            view.message=Label("Message",panel,font,"",new Vector2(640,36),new Vector2(-260,-309),16);
            view.unequipButton=MakeButton("Unequip",panel,font,"清空当前消耗品槽",new Vector2(240,32),new Vector2(324,-310));view.unequipButton.GetComponentInChildren<Text>().fontSize=16;
            view.closeButton=MakeButton("Close",panel,font,"×",new Vector2(34,34),new Vector2(570,302));
            return view;
        }
        public static bool EnsureQuickSlotLayout(InventoryView view,Font font)
        {
            if(view.layoutVersion==3)
            {
                var handle=view.transform.Find("Panel/Details/DetailScrollbar/Handle") as RectTransform;
                if(handle!=null && handle.sizeDelta!=Vector2.zero){handle.sizeDelta=Vector2.zero;return true;}
                return false;
            }
            for(int i=view.transform.childCount-1;i>=0;i--)DestroyImmediate(view.transform.GetChild(i).gameObject);
            BuildLayout(view.gameObject,font);return true;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 position)
        {
            var obj=new GameObject(name,typeof(RectTransform));obj.transform.SetParent(parent,false);var rect=obj.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
        }
        static Text Label(string name,Transform parent,Font font,string text,Vector2 size,Vector2 position,int fontSize,bool gold=false)
        {
            var label=Rect(name,parent,size,position).gameObject.AddComponent<Text>();label.font=font;label.fontSize=fontSize;label.text=text;label.alignment=TextAnchor.MiddleCenter;label.color=gold?new Color32(235,190,111,255):new Color32(222,225,230,255);label.supportRichText=false;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;return label;
        }
        static Button MakeButton(string name,Transform parent,Font font,string text,Vector2 size,Vector2 position)
        {
            var rect=Rect(name,parent,size,position);var image=rect.gameObject.AddComponent<Image>();image.color=TileColor;var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;var colors=button.colors;colors.highlightedColor=new Color32(160,160,160,255);colors.selectedColor=new Color32(200,175,130,255);colors.disabledColor=new Color32(105,110,120,200);button.colors=colors;Label("Label",rect,font,text,size-new Vector2(12,4),Vector2.zero,18);return button;
        }
    }
}
