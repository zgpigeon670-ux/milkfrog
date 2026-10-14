using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;


namespace Milkfrog.CombatDemo.Tests
{
    public class EquipmentFlowTests
    {
        static readonly string[] GearIds={"practice-helmet","practice-armor","practice-leggings","light-shoes",EquipmentService.DefaultWeaponId};
        MvpWorld world;string directory;
        [UnitySetUp] public IEnumerator Load()
        {
            directory=Path.Combine(Path.GetTempPath(),"EquipmentFlow-"+Guid.NewGuid());GameFlowController.SaveDirectoryOverride=directory;
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/MVP_TestLevel.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();world.manualSimulation=true;if(world.Flow.Paused)world.Flow.TogglePause();world.feedback.hitStop=false;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("EquipmentCleanup-"+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);GameFlowController.SaveDirectoryOverride=null;
            if(Directory.Exists(directory))Directory.Delete(directory,true);else if(File.Exists(directory))File.Delete(directory);
        }
        void Prepare()
        {
            foreach(var id in GearIds.Take(4))world.Inventory.TryAdd(id,1);
            foreach(var id in new[]{"healing-medicine","calming-pill","attack-tonic"})world.Inventory.TryAdd(id,3);
            world.player.Core.RestoreVitals(24,60);Assert.That(world.Flow.Save(),Is.True);
        }
        void WearFull()
        {
            Prepare();world.Flow.OpenInventory();foreach(var id in GearIds.Take(4))Assert.That(world.TryEquipGear(id),Is.True,world.Flow.Message);
        }
        void Slots()
        {
            world.Flow.OpenInventory();for(int i=0;i<3;i++)Assert.That(world.TryAssignQuickItem(i,new[]{"healing-medicine","calming-pill","attack-tonic"}[i]),Is.True);
            world.Flow.CloseInventory();world.SubmitInput(new CombatInputFrame());
        }
        void Place(Vector3 point){world.player.Motor.enabled=false;world.player.transform.position=point;world.player.Motor.enabled=true;Physics.SyncTransforms();}
        MvpHudData Hud(){var hud=world.GetComponent<MvpHud>();hud.Refresh();return hud.Read();}
        [UnityTest] public IEnumerator EquipAndRemovePreserveResourcesAndShowRealArmorParts()
        {
            Prepare();world.player.Core.TryApplyAttackBuff(1.25f,20);world.Flow.OpenInventory();int before=world.player.Core.AttackId;
            foreach(var id in GearIds.Take(4))Assert.That(world.TryEquipGear(id),Is.True);
            Assert.That(world.player.Core.Health,Is.EqualTo(24));Assert.That(world.player.Core.Posture,Is.EqualTo(60));Assert.That(world.player.Core.AttackBuffRemaining,Is.EqualTo(20));Assert.That(world.player.Core.AttackId,Is.EqualTo(before));
            Assert.That(world.player.GetComponent<EquipmentVisuals>().VisiblePartCount,Is.EqualTo(6));Assert.That(world.EquipmentStats.Weight,Is.EqualTo(8));
            world.Flow.inventoryView.FilterEquipment(EquipmentSlot.Body);Assert.That(world.Flow.inventoryView.itemName.text,Is.EqualTo("练习胸甲"));Assert.That(world.Flow.inventoryView.comparisonText.text,Does.Contain("卸下后"));
            world.Flow.inventoryView.gearUnequipButton.onClick.Invoke();Assert.That(world.Inventory.Equipment.Get(EquipmentSlot.Body),Is.Empty);Assert.That(world.Inventory.Quantity("practice-armor"),Is.EqualTo(1));Assert.That(world.player.GetComponent<EquipmentVisuals>().VisiblePartCount,Is.EqualTo(5));
            Assert.That(world.TryUnequipGear(EquipmentSlot.Weapon),Is.False);Assert.That(world.Flow.Store.TryLoad(out var saved),Is.True);Assert.That(saved.inventory.equipment.slots[1],Is.Empty);yield return null;
        }
        [UnityTest] public IEnumerator CombatGuardActionsAndHitStopCannotGainEquipmentEligibilityFromInputCleanup()
        {
            Prepare();world.player.Core.SetGuard(true,true);world.Flow.OpenInventory();Assert.That(world.TryEquipGear("practice-helmet"),Is.False);world.Flow.CloseInventory();
            world.player.Core.BeginPreparation();world.player.Core.SetAttackHeld(true);world.Flow.OpenInventory();Assert.That(world.Flow.InventoryEquipmentAllowed,Is.False);Assert.That(world.TryEquipGear("practice-helmet"),Is.False);world.Flow.CloseInventory();world.player.Core.Reset();
            world.feedback.Clock.Request(.1f);world.Flow.OpenInventory();Assert.That(world.TryEquipGear("practice-helmet"),Is.False);world.Flow.CloseInventory();world.feedback.Clock.Reset();
            var enemy=world.enemies[0];Place(enemy.Home+Vector3.back*2);world.Director.Engage(enemy);world.Flow.OpenInventory();Assert.That(world.TryEquipGear("practice-helmet"),Is.False);Assert.That(world.TryAssignQuickItem(0,"healing-medicine"),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator FailedSaveDoesNotChangeEquipmentAppearanceStatsOrQuickBindings()
        {
            Prepare();Directory.Delete(directory,true);File.WriteAllText(directory,"blocks creation");world.Flow.OpenInventory();
            Assert.That(world.TryEquipGear("practice-helmet"),Is.False);Assert.That(world.Inventory.Equipment.Get(EquipmentSlot.Head),Is.Empty);Assert.That(world.player.GetComponent<EquipmentVisuals>().VisiblePartCount,Is.Zero);
            Assert.That(world.EquipmentStats.Weight,Is.EqualTo(2));Assert.That(world.TryAssignQuickItem(2,"attack-tonic"),Is.False);Assert.That(world.Inventory.QuickItem(2),Is.Empty);yield return null;
        }
        [UnityTest] public IEnumerator AllFourGroundGearPickupsHaveOneTimeSavedOwnership()
        {
            var pickups=UnityEngine.Object.FindObjectsByType<InventoryPickup>().Where(p=>p.item.category==ItemCategory.Equipment).OrderBy(p=>p.item.equipmentSlot).ToArray();Assert.That(pickups.Length,Is.EqualTo(4));
            foreach(var pickup in pickups)
            {
                Assert.That(pickup.quantity,Is.EqualTo(1));Place(pickup.Root.position+Vector3.back*1.8f);world.gameplayCamera.transform.position=pickup.InteractionPoint+new Vector3(4,2,-5);world.gameplayCamera.transform.LookAt(pickup.InteractionPoint);
                world.Interactions.Refresh();Assert.That(world.Interactions.Focus,Is.SameAs(pickup));Assert.That(world.Interactions.TryInteract(),Is.True,world.Flow.Message);Assert.That(world.Inventory.Quantity(pickup.item.stableId),Is.EqualTo(1));Assert.That(world.WorldState.IsCollected(pickup.stableId),Is.True);
            }
            Assert.That(world.Flow.Store.TryLoad(out var saved),Is.True);world.Restore(saved);Assert.That(pickups.All(p=>!p.Available(world)),Is.True);Assert.That(world.Inventory.Equipment.Get(EquipmentSlot.Head),Is.Empty);yield return null;
        }
        [UnityTest] public IEnumerator EquippedMovementAndShoesChangeActualHorizontalDisplacement()
        {
            WearFull();world.Flow.CloseInventory();Place(world.spawn);world.cameraRig.yaw=0;world.cameraRig.Step(1);Vector3 before=world.player.transform.position;
            world.SubmitInput(new CombatInputFrame{move=Vector2.up});world.Simulate(.5f);float moved=Vector3.ProjectOnPlane(world.player.transform.position-before,Vector3.up).magnitude;Assert.That(moved,Is.EqualTo(4*.94f*.5f).Within(.05));
            world.SubmitInput(new CombatInputFrame());world.Flow.OpenInventory();Assert.That(world.TryUnequipGear(EquipmentSlot.Shoes),Is.True);world.Flow.CloseInventory();Place(world.spawn);before=world.player.transform.position;
            world.SubmitInput(new CombatInputFrame{move=Vector2.up});world.Simulate(.5f);moved=Vector3.ProjectOnPlane(world.player.transform.position-before,Vector3.up).magnitude;Assert.That(moved,Is.EqualTo(4*.84f*.5f).Within(.05));yield return null;
        }
        [UnityTest] public IEnumerator ArmorAndWeaponDefenseApplyToActualSceneHitsAndBlocks()
        {
            WearFull();world.Flow.CloseInventory();var core=world.player.Core;var enemy=world.enemies[0].Actor.Core;core.RestoreVitals(100,0);enemy.Reset();enemy.RequestAttack();float damage=enemy.ActiveAttack.Damage;enemy.Tick(enemy.ActiveAttack.Startup);Assert.That(enemy.TryHit(core,false),Is.EqualTo(HitResult.Hit));Assert.That(core.Health,Is.EqualTo(100-damage*.8f).Within(.001));
            core.RestoreVitals(100,0);core.SetGuard(true,false);enemy.Reset();enemy.RequestAttack();float block=enemy.ActiveAttack.Block;enemy.Tick(enemy.ActiveAttack.Startup);Assert.That(enemy.TryHit(core,true),Is.EqualTo(HitResult.Block));Assert.That(core.Posture,Is.EqualTo(block*5/6).Within(.001));Assert.That(core.Health,Is.EqualTo(100));yield return null;
        }
        [UnityTest] public IEnumerator NumericSlotsSelectAndSameFrameQUsesTheNewSlotOnlyOnce()
        {
            Prepare();Slots();world.SubmitInput(new CombatInputFrame{slot2Pressed=true,slot2Held=true,itemPressed=true,itemHeld=true});Assert.That(world.Inventory.ActiveQuickSlot,Is.EqualTo(1));Assert.That(world.player.Core.Posture,Is.EqualTo(20));Assert.That(world.Inventory.Quantity("calming-pill"),Is.EqualTo(2));
            world.SubmitInput(new CombatInputFrame{slot2Held=true,itemHeld=true});Assert.That(world.Inventory.Quantity("calming-pill"),Is.EqualTo(2));world.SubmitInput(new CombatInputFrame());
            world.SubmitInput(new CombatInputFrame{slot1Pressed=true,slot2Pressed=true,slot3Pressed=true,slot1Held=true,slot2Held=true,slot3Held=true});Assert.That(world.Inventory.ActiveQuickSlot,Is.Zero);Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(3));
            Assert.That(Hud().quickSlots.Length,Is.EqualTo(3));Assert.That(Hud().quickSlots[1].count,Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator SlotSelectionWorksDuringAttackAndHitStopWithoutUsingOrBufferingQ()
        {
            Prepare();Slots();world.player.Core.RequestAttack();world.SubmitInput(new CombatInputFrame{slot3Pressed=true,slot3Held=true,itemPressed=true,itemHeld=true});Assert.That(world.Inventory.ActiveQuickSlot,Is.EqualTo(2));Assert.That(world.Inventory.Quantity("attack-tonic"),Is.EqualTo(3));world.player.Core.Tick(1);world.SubmitInput(new CombatInputFrame{itemHeld=true});Assert.That(world.player.Core.AttackBuffRemaining,Is.Zero);
            world.SubmitInput(new CombatInputFrame());world.feedback.Clock.Request(.1f);world.SubmitInput(new CombatInputFrame{slot2Pressed=true,slot2Held=true});Assert.That(world.Inventory.ActiveQuickSlot,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator MenuHeldNumericKeysNeedReleaseBeforeTheyCanChangeTheSlot()
        {
            Prepare();Slots();world.Flow.OpenInventory();world.SubmitInput(new CombatInputFrame{slot3Pressed=true,slot3Held=true});world.Flow.CloseInventory();world.SubmitInput(new CombatInputFrame{slot3Pressed=true,slot3Held=true});Assert.That(world.Inventory.ActiveQuickSlot,Is.Zero);
            world.SubmitInput(new CombatInputFrame());world.SubmitInput(new CombatInputFrame{slot3Pressed=true,slot3Held=true});Assert.That(world.Inventory.ActiveQuickSlot,Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator SlotButtonsMoveAndClearBindingsWithoutChangingTheInventory()
        {
            Prepare();world.Flow.OpenInventory();var view=world.Flow.inventoryView;view.SelectItem("healing-medicine");view.quickAssignButtons[2].onClick.Invoke();Assert.That(world.Inventory.QuickItem(2),Is.EqualTo("healing-medicine"));
            view.quickAssignButtons[0].onClick.Invoke();Assert.That(world.Inventory.QuickItem(2),Is.Empty);Assert.That(world.Inventory.QuickItem(0),Is.EqualTo("healing-medicine"));Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(3));
            view.quickSlotButtons[0].onClick.Invoke();view.unequipButton.onClick.Invoke();Assert.That(world.Inventory.QuickItem(0),Is.Empty);Assert.That(view.skillButtons.All(b=>!b.interactable),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator PreviewAnimatesAndRotatesIndependentlyAndReleasesEveryTime()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("This rendering test requires a graphics device.");
            WearFull();var view=world.Flow.inventoryView;Vector3 position=world.player.transform.position;Quaternion rotation=world.gameplayCamera.transform.rotation;float timer=world.player.Core.Remaining;
            Assert.That(view.preview.HasResources,Is.True);view.preview.OnDrag(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,delta=new Vector2(100,0)});Assert.That(view.preview.Yaw,Is.EqualTo(50));
            var texture=view.preview.image.texture;var rect=view.preview.image.rectTransform.rect;Assert.That((float)texture.width/texture.height,Is.EqualTo(rect.width/rect.height).Within(.003));
            yield return null;yield return null;Assert.That(Quaternion.Angle(view.preview.ModelRotation,Quaternion.Euler(0,50,0)),Is.LessThan(.01f));Assert.That(world.player.transform.position,Is.EqualTo(position));Assert.That(world.gameplayCamera.transform.rotation,Is.EqualTo(rotation));Assert.That(world.player.Core.Remaining,Is.EqualTo(timer));
            for(int i=0;i<3;i++){world.Flow.CloseInventory();Assert.That(view.preview.HasResources,Is.False);yield return null;Assert.That(UnityEngine.Object.FindObjectsByType<Camera>().Count(c=>c.name=="Preview Camera"),Is.Zero);world.Flow.OpenInventory();Assert.That(view.preview.HasResources,Is.True);}
        }
        [UnityTest] public IEnumerator DeathRestTravelAndReloadPreserveEquipmentAndAllBindings()
        {
            WearFull();world.Flow.CloseInventory();Slots();world.Inventory.SelectQuickSlot(2);world.SubmitInput(new CombatInputFrame{itemPressed=true,itemHeld=true});var saved=world.DeathRespawnSnapshot();world.Restore(saved);
            Assert.That(world.Inventory.ActiveQuickSlot,Is.EqualTo(2));Assert.That(world.Inventory.Quantity("attack-tonic"),Is.EqualTo(2));Assert.That(world.Inventory.Equipment.Get(EquipmentSlot.Body),Is.EqualTo("practice-armor"));Assert.That(world.player.Core.AttackBuffRemaining,Is.Zero);
            saved=world.Snapshot();saved.unlockedCheckpointIds=new[]{BonfireCheckpoint.StartId,BonfireCheckpoint.BossApproachId};world.Restore(saved);var start=world.FindBonfire(BonfireCheckpoint.StartId);Place(start.Root.position+Vector3.back);Assert.That(world.TryRest(start),Is.True);Assert.That(world.TryFastTravel(world.FindBonfire(BonfireCheckpoint.BossApproachId)),Is.True);
            Assert.That(world.Inventory.ActiveQuickSlot,Is.EqualTo(2));Assert.That(world.Flow.Store.TryLoad(out saved),Is.True);var previous=world;world.Flow.LoadLevel(saved);
            for(int i=0;i<120;i++){yield return null;var loaded=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();if(loaded==null||loaded==previous||loaded.Flow.Store==null)continue;world=loaded;world.manualSimulation=true;break;}
            Assert.That(world,Is.Not.SameAs(previous));Assert.That(world.Inventory.ActiveQuickSlot,Is.Zero);for(int i=0;i<3;i++)Assert.That(world.Inventory.QuickItem(i),Is.EqualTo(new[]{"healing-medicine","calming-pill","attack-tonic"}[i]));Assert.That(world.Inventory.Quantity("attack-tonic"),Is.EqualTo(2));Assert.That(world.EquipmentStats.Weight,Is.EqualTo(8));
        }
        [UnityTest] public IEnumerator CombatBindingWritesRetainTheSafePositionAndResources()
        {
            Prepare();Assert.That(world.Flow.Store.TryLoad(out var before),Is.True);var enemy=world.enemies[0];Place(enemy.Home+Vector3.back*2);world.Director.Engage(enemy);world.player.Core.RestoreVitals(10,90);world.Flow.OpenInventory();Assert.That(world.TryAssignQuickItem(2,"attack-tonic"),Is.True);
            Assert.That(world.Flow.Store.TryLoad(out var after),Is.True);Assert.That(after.position,Is.EqualTo(before.position));Assert.That(after.health,Is.EqualTo(before.health));Assert.That(after.posture,Is.EqualTo(before.posture));Assert.That(after.inventory.quickSlots[2],Is.EqualTo("attack-tonic"));yield return null;
        }
        [UnityTest] public IEnumerator RealKeyboardNumericBindingsAndQOperateTheThreeSlots()
        {
            Prepare();Slots();var settings=InputSystem.settings;var background=settings.backgroundBehavior;var editor=settings.editorInputBehaviorInPlayMode;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard=InputSystem.AddDevice<Keyboard>();var input=(DemoInput)typeof(MvpWorld).GetField("input",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(world);var map=(InputActionMap)typeof(DemoInput).GetField("map",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(input);map.devices=new InputDevice[]{keyboard};
            try
            {
                Dispatch(keyboard,new KeyboardState(Key.Digit2,Key.Q));Assert.That(world.Inventory.ActiveQuickSlot,Is.EqualTo(1));Assert.That(world.Inventory.Quantity("calming-pill"),Is.EqualTo(2));Dispatch(keyboard,new KeyboardState(Key.Digit2,Key.Q));Assert.That(world.Inventory.Quantity("calming-pill"),Is.EqualTo(2));
                Dispatch(keyboard,new KeyboardState());Dispatch(keyboard,new KeyboardState(Key.Digit1,Key.Digit3));Assert.That(world.Inventory.ActiveQuickSlot,Is.Zero);
                Dispatch(keyboard,new KeyboardState());Dispatch(keyboard,new KeyboardState(Key.Tab));Dispatch(keyboard,new KeyboardState(Key.Digit3));world.Flow.CloseInventory();Dispatch(keyboard,new KeyboardState(Key.Digit3));Assert.That(world.Inventory.ActiveQuickSlot,Is.Zero);Dispatch(keyboard,new KeyboardState());Dispatch(keyboard,new KeyboardState(Key.Digit3));Assert.That(world.Inventory.ActiveQuickSlot,Is.EqualTo(2));
            }
            finally{map.devices=null;InputSystem.RemoveDevice(keyboard);settings.backgroundBehavior=background;settings.editorInputBehaviorInPlayMode=editor;}
            yield return null;
        }
        void Dispatch(Keyboard device,KeyboardState state){InputSystem.QueueStateEvent(device,state);InputSystem.Update();world.manualSimulation=false;try{typeof(MvpWorld).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(world,null);}finally{world.manualSimulation=true;}}
    }
}

