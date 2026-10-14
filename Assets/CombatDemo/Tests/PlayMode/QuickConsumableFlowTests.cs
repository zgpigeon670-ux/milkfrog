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
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Milkfrog.CombatDemo.Tests
{
    public class QuickConsumableFlowTests
    {
        MvpWorld world; string directory;
        [UnitySetUp] public IEnumerator Load()
        {
            directory=Path.Combine(Path.GetTempPath(),"QuickItems-"+Guid.NewGuid());GameFlowController.SaveDirectoryOverride=directory;
#if UNITY_EDITOR
            ExpectExistingTraceErrors();
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/MVP_TestLevel.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("MVP_TestLevel");
#endif
            yield return null;world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();world.manualSimulation=true;
            if(world.Flow.Paused)world.Flow.TogglePause();world.feedback.hitStop=false;
        }
        void ExpectExistingTraceErrors()
        {
#if UNITY_EDITOR
            // These three existing errors are explicitly expected, never globally suppressed.
            foreach(string name in new[]{"MilkfrogSlash","MilkfrogThrust","MilkfrogFollowup"})
            {
                var attack=AssetDatabase.LoadAssetAtPath<CombatAttackDefinition>("Assets/CombatDemo/Settings/"+name+".asset");
                if(attack!=null && (attack.trace==null || !attack.trace.IsValid || attack.trace.bakedClip!=attack.clip ||
                    !Mathf.Approximately(attack.trace.bakedStart,attack.activeStart) || !Mathf.Approximately(attack.trace.bakedEnd,attack.activeEnd)))
                    LogAssert.Expect(LogType.Error,"Rebake the trace for "+name+" after changing its clip or phase markers.");
            }
#endif
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var scene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("QuickCleanup-"+Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(scene);GameFlowController.SaveDirectoryOverride=null;
            if(Directory.Exists(directory))Directory.Delete(directory,true);else if(File.Exists(directory))File.Delete(directory);
        }
        void Prepare(string id="healing-medicine",int count=3)
        {
            world.Inventory.TryAdd(id,count);world.player.Core.RestoreVitals(20,60);Assert.That(world.Flow.Save(),Is.True);
            world.Flow.OpenInventory();Assert.That(world.TryEquipItem(id),Is.True,world.Flow.Message);world.Flow.CloseInventory();Release();
        }
        void Release()=>world.SubmitInput(new CombatInputFrame{itemReleased=true});
        void Press()=>world.SubmitInput(new CombatInputFrame{itemPressed=true,itemHeld=true});
        MvpHudData Hud()
        {var hud=world.GetComponent<MvpHud>();hud.Refresh();return hud.Read();}
        void Place(Vector3 position)
        {world.player.Motor.enabled=false;world.player.transform.position=position;world.player.Motor.enabled=true;Physics.SyncTransforms();}
        [UnityTest] public IEnumerator EquipUseAndUnequipButtonsPersistAndMarkTheGrid()
        {
            world.Inventory.TryAdd("healing-medicine",3);world.Inventory.TryAdd(WorldStateService.KeyItemId,1);world.Flow.OpenInventory();yield return null;
            var view=world.Flow.inventoryView;view.SelectItem("healing-medicine");
            EventSystem.current.SetSelectedGameObject(view.equipButton.gameObject);
            ExecuteEvents.Execute(view.equipButton.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
            Assert.That(world.Inventory.EquippedItemId,Is.EqualTo("healing-medicine"));Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(3));
            Assert.That(view.content.GetComponentsInChildren<InventoryTile>().First(t=>t.itemId=="healing-medicine").equippedBadge.gameObject.activeSelf,Is.True);
            view.SelectItem(WorldStateService.KeyItemId);Assert.That(view.equipButton.interactable,Is.False);Assert.That(world.TryEquipItem(WorldStateService.KeyItemId),Is.False);
            view.unequipButton.onClick.Invoke();Assert.That(world.Inventory.EquippedItemId,Is.Empty);
            Assert.That(world.Flow.Store.TryLoad(out var saved),Is.True);Assert.That(saved.inventory.equippedItemId,Is.Empty);
        }
        [UnityTest] public IEnumerator HealingQuickUseIsImmediateDoesNotRepeatAndFullHealthDoesNotConsume()
        {
            Prepare();Press();Assert.That(world.player.Core.Health,Is.EqualTo(60));Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(2));
            world.SubmitInput(new CombatInputFrame{itemHeld=true});Assert.That(world.player.Core.Health,Is.EqualTo(60));
            Release();Press();Assert.That(world.player.Core.Health,Is.EqualTo(100));Release();Press();
            Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(1));Assert.That(Hud().quickItemCount,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator CalmingQuickUsePreservesGuardAndZeroPostureDoesNotConsume()
        {
            Prepare("calming-pill");world.player.Core.SetGuard(true,true);float window=world.player.Core.DeflectRemaining;
            world.SubmitInput(new CombatInputFrame{itemPressed=true,itemHeld=true,guardHeld=true});
            Assert.That(world.player.Core.State,Is.EqualTo(CombatState.Guard));Assert.That(world.player.Core.DeflectRemaining,Is.EqualTo(window));Assert.That(world.player.Core.Posture,Is.EqualTo(20));
            Release();Press();Assert.That(world.player.Core.Posture,Is.Zero);Release();Press();Assert.That(world.Inventory.Quantity("calming-pill"),Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator TonicRefreshAndReplacingTheSlotDoNotStackOrRemoveBuff()
        {
            Prepare("attack-tonic");Press();Assert.That(world.player.Core.AttackBuffMultiplier,Is.EqualTo(1.25f));world.player.Core.Tick(6);Release();Press();
            Assert.That(world.player.Core.AttackBuffRemaining,Is.EqualTo(20));Assert.That(world.player.Core.AttackBuffMultiplier,Is.EqualTo(1.25f));
            world.Inventory.TryAdd("healing-medicine",2);world.Flow.OpenInventory();Assert.That(world.TryEquipItem("healing-medicine"),Is.True);world.Flow.CloseInventory();Release();Press();
            Assert.That(world.player.Core.Health,Is.EqualTo(60));Assert.That(world.player.Core.AttackBuffRemaining,Is.EqualTo(20));
            Assert.That(Hud().quickItemId,Is.EqualTo("healing-medicine"));Assert.That(Hud().attackBuffMultiplier,Is.EqualTo(1.25f));yield return null;
        }
        [UnityTest] public IEnumerator EmptySlotAndDepletedOrUnknownItemsDoNotConsumeOrAutoSwitch()
        {
            Assert.That(world.TryUseEquippedItem(),Is.False);Prepare("attack-tonic",1);Press();
            Assert.That(world.Inventory.EquippedItemId,Is.EqualTo("attack-tonic"));Assert.That(Hud().quickItemCount,Is.Zero);
            Release();Press();Assert.That(world.Inventory.Quantity("attack-tonic"),Is.Zero);
            world.Inventory.TryAdd("attack-tonic",1);Release();Press();Assert.That(world.Inventory.Quantity("attack-tonic"),Is.Zero);
            world.Inventory.Restore(new InventoryData{equippedItemId="unconfigured",entries=new[]{new InventoryEntry{itemId="unconfigured",quantity=2}}});
            Assert.That(world.TryUseEquippedItem(),Is.False);Assert.That(world.Inventory.Quantity("unconfigured"),Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator ActionAndHitStopPressesAreDiscardedInsteadOfQueued()
        {
            Prepare("attack-tonic");world.player.Core.RequestAttack();Press();Assert.That(world.Inventory.Quantity("attack-tonic"),Is.EqualTo(3));
            world.player.Core.Tick(1);world.SubmitInput(new CombatInputFrame{itemHeld=true});Assert.That(world.player.Core.AttackBuffRemaining,Is.Zero);
            Release();world.feedback.Clock.Request(.1f);Press();world.feedback.Clock.Reset();world.SubmitInput(new CombatInputFrame{itemHeld=true});
            Assert.That(world.player.Core.AttackBuffRemaining,Is.Zero);Release();Press();Assert.That(world.Inventory.Quantity("attack-tonic"),Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator DodgeWinsOverQuickUseAndSuccessfulQuickUseWinsOverAttack()
        {
            Prepare();world.SubmitInput(new CombatInputFrame{itemPressed=true,itemHeld=true,dodgePressed=true});Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(3));
            world.player.Core.Tick(1);Release();world.SubmitInput(new CombatInputFrame{itemPressed=true,itemHeld=true,attackPressed=true,attackHeld=true});
            Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(2));Assert.That(world.player.Core.IsPreparing,Is.False);Assert.That(world.player.Core.AttackId,Is.Zero);yield return null;
        }
        [UnityTest] public IEnumerator EveryForbiddenStateRejectsQIncludingSameFrameGuardCancellation()
        {
            Prepare("attack-tonic");var core=world.player.Core;
            foreach(var state in new[]{CombatState.AttackPrepare,CombatState.Charging,CombatState.AttackStartup,
                CombatState.AttackActive,CombatState.AttackRecovery,CombatState.Dodge,CombatState.HitStun,
                CombatState.DeflectedStun,CombatState.PostureBroken,CombatState.Dead})
            {
                core.RestoreVitals(state==CombatState.Dead?1:20,state==CombatState.PostureBroken?99:0);Release();
                if(state==CombatState.AttackPrepare||state==CombatState.Charging)
                {core.BeginPreparation();core.SetAttackHeld(true);if(state==CombatState.Charging)core.Tick(core.Tuning.prepareThreshold);}
                else if(state==CombatState.Dodge)core.RequestDodge();
                else if(state==CombatState.HitStun||state==CombatState.PostureBroken||state==CombatState.Dead)
                {
                    var enemy=world.enemies[0].Actor.Core;enemy.Reset();enemy.RequestAttack();enemy.Tick(enemy.ActiveAttack.Startup);enemy.TryHit(core,false);
                }
                else
                {
                    core.RequestAttack();if(state!=CombatState.AttackStartup)core.Tick(core.ActiveAttack.Startup);
                    if(state==CombatState.AttackRecovery)core.Tick(core.ActiveAttack.Active);
                    if(state==CombatState.DeflectedStun)
                    {var enemy=world.enemies[0].Actor.Core;enemy.Reset();enemy.SetGuard(true,true);core.TryHit(enemy,true);}
                }
                Assert.That(core.State,Is.EqualTo(state));
                world.SubmitInput(new CombatInputFrame{itemPressed=true,itemHeld=true,attackHeld=true,guardHeld=true,guardPressed=true});
                Assert.That(world.Inventory.Quantity("attack-tonic"),Is.EqualTo(3),state.ToString());
                Assert.That(core.AttackBuffRemaining,Is.Zero,state.ToString());
                world.SubmitInput(new CombatInputFrame{itemHeld=true});Assert.That(core.AttackBuffRemaining,Is.Zero);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator TonicGroundPickupIsOneTimeAndFailedSaveLeavesItVisible()
        {
            var pickup=UnityEngine.Object.FindObjectsByType<InventoryPickup>().Single(p=>p.item.stableId=="attack-tonic");
            Assert.That(pickup.transform.position,Is.EqualTo(new Vector3(3,.02f,-33)));Assert.That(pickup.quantity,Is.EqualTo(3));
            Place(pickup.Root.position+Vector3.back*1.8f);world.gameplayCamera.transform.SetPositionAndRotation(pickup.InteractionPoint+Vector3.back*5,Quaternion.identity);
            world.Interactions.Refresh();Assert.That(world.Interactions.Focus,Is.SameAs(pickup));
            Directory.Delete(directory,true);File.WriteAllText(directory,"blocks creation");
            Assert.That(world.Interactions.TryInteract(),Is.False);Assert.That(pickup.visual.activeSelf,Is.True);
            Assert.That(world.Inventory.Quantity("attack-tonic"),Is.Zero);Assert.That(world.WorldState.IsCollected(pickup.stableId),Is.False);
            File.Delete(directory);Assert.That(world.Interactions.TryInteract(),Is.True,world.Flow.Message);
            Assert.That(world.Inventory.Quantity("attack-tonic"),Is.EqualTo(3));Assert.That(pickup.visual.activeSelf,Is.False);
            Assert.That(world.Flow.Store.TryLoad(out var saved),Is.True);world.Restore(saved);
            Assert.That(world.WorldState.IsCollected(pickup.stableId),Is.True);Assert.That(pickup.Available(world),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator ReloadingTheLevelKeepsSavedSlotAndQuantityWithoutTheTransientBuff()
        {
            Prepare("attack-tonic");Press();Assert.That(world.Flow.Store.TryLoad(out var saved),Is.True);
            ExpectExistingTraceErrors();var previous=world;world.Flow.LoadLevel(saved);
            for(int i=0;i<120;i++)
            {
                yield return null;var loaded=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();
                if(loaded==null||loaded==previous||loaded.Flow.Store==null)continue;
                world=loaded;world.manualSimulation=true;break;
            }
            Assert.That(world,Is.Not.SameAs(previous));Assert.That(world.Inventory.EquippedItemId,Is.EqualTo("attack-tonic"));
            Assert.That(world.Inventory.Quantity("attack-tonic"),Is.EqualTo(2));Assert.That(world.player.Core.AttackBuffRemaining,Is.Zero);
        }
        [UnityTest] public IEnumerator MenuCloseRequiresReleasingAndPressingQAgain()
        {
            Prepare();world.Flow.OpenInventory();Press();world.Flow.CloseInventory();Press();
            Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(3));Release();Press();Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator BuffFreezesInInventoryAndHitStopAndClearsOnRest()
        {
            Prepare("attack-tonic");Press();world.Flow.OpenInventory();world.Simulate(5);Assert.That(world.player.Core.AttackBuffRemaining,Is.EqualTo(20));world.Flow.CloseInventory();
            world.feedback.Clock.Request(.1f);world.Simulate(.05f);Assert.That(world.player.Core.AttackBuffRemaining,Is.EqualTo(20));
            world.Simulate(1);Assert.That(world.player.Core.AttackBuffRemaining,Is.InRange(18.9f,19.2f));
            var camp=world.FindBonfire(BonfireCheckpoint.StartId);Place(camp.Root.position+Vector3.back);
            Assert.That(world.TryRest(camp),Is.True);Assert.That(world.player.Core.AttackBuffRemaining,Is.Zero);Assert.That(world.Inventory.EquippedItemId,Is.EqualTo("attack-tonic"));yield return null;
        }
        [UnityTest] public IEnumerator CombatConsumptionPreservesSafeVitalsPositionAndEquippedSelection()
        {
            Prepare("attack-tonic");Assert.That(world.Flow.Store.TryLoad(out var before),Is.True);
            var enemy=world.enemies[0];Place(enemy.Home+Vector3.back*2);world.Director.Engage(enemy);Assert.That(world.Director.State,Is.EqualTo(EncounterState.NormalCombat));
            world.player.Core.RestoreVitals(10,30);Press();Assert.That(world.Flow.Store.TryLoad(out var after),Is.True);
            Assert.That(after.position,Is.EqualTo(before.position));Assert.That(after.health,Is.EqualTo(before.health));Assert.That(after.posture,Is.EqualTo(before.posture));
            Assert.That(after.inventory.equippedItemId,Is.EqualTo("attack-tonic"));Assert.That(after.inventory.entries.First(e=>e.itemId=="attack-tonic").quantity,Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator FailedWriteRollsBackBothEquipmentAndEffects()
        {
            Prepare();world.Inventory.TryAdd("attack-tonic",3);Directory.Delete(directory,true);File.WriteAllText(directory,"blocks creation");
            world.Flow.OpenInventory();Assert.That(world.TryEquipItem("attack-tonic"),Is.False);Assert.That(world.Inventory.EquippedItemId,Is.EqualTo("healing-medicine"));world.Flow.CloseInventory();Release();Press();
            Assert.That(world.player.Core.Health,Is.EqualTo(20));Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(3));
            Assert.That(world.TryUseItem("attack-tonic",ItemUseContext.QuickSlot),Is.False);Assert.That(world.player.Core.AttackBuffRemaining,Is.Zero);yield return null;
        }
        [UnityTest] public IEnumerator RestoreDeathAndFastTravelKeepQuantityAndSelectionButClearBuff()
        {
            Prepare("attack-tonic");Press();var saved=world.DeathRespawnSnapshot();world.Restore(saved);
            Assert.That(world.Inventory.EquippedItemId,Is.EqualTo("attack-tonic"));Assert.That(world.Inventory.Quantity("attack-tonic"),Is.EqualTo(2));Assert.That(world.player.Core.AttackBuffRemaining,Is.Zero);
            saved=world.Snapshot();saved.unlockedCheckpointIds=new[]{BonfireCheckpoint.StartId,BonfireCheckpoint.BossApproachId};world.Restore(saved);
            var start=world.FindBonfire(BonfireCheckpoint.StartId);Place(start.Root.position+Vector3.back);Assert.That(world.TryRest(start),Is.True);
            world.player.Core.TryApplyAttackBuff(1.25f,20);Assert.That(world.TryFastTravel(world.FindBonfire(BonfireCheckpoint.BossApproachId)),Is.True);
            Assert.That(world.Inventory.Quantity("attack-tonic"),Is.EqualTo(2));Assert.That(world.Inventory.EquippedItemId,Is.EqualTo("attack-tonic"));Assert.That(world.player.Core.AttackBuffRemaining,Is.Zero);yield return null;
        }
        [UnityTest] public IEnumerator QBindingPressHoldReleaseAndTabMenusUseTheRealInputMap()
        {
            Prepare();var policy=InputSystem.settings;var background=policy.backgroundBehavior;policy.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            var editorPolicy=policy.editorInputBehaviorInPlayMode;policy.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            var keyboard=InputSystem.AddDevice<Keyboard>();
            var input=(DemoInput)typeof(MvpWorld).GetField("input",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(world);
            var map=(InputActionMap)typeof(DemoInput).GetField("map",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(input);map.devices=new InputDevice[]{keyboard};
            try
            {
                Dispatch(keyboard,new KeyboardState(Key.Q));Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(2));
                Dispatch(keyboard,new KeyboardState(Key.Q));Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(2));
                Dispatch(keyboard,new KeyboardState());Dispatch(keyboard,new KeyboardState(Key.Tab));Assert.That(world.Flow.InventoryOpen,Is.True);
                Dispatch(keyboard,new KeyboardState(Key.Q));Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(2));world.Flow.CloseInventory();
                Dispatch(keyboard,new KeyboardState(Key.Q));Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(2));
                Dispatch(keyboard,new KeyboardState());Dispatch(keyboard,new KeyboardState(Key.Q));Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(1));
            }
            finally
            {
                map.devices=null;InputSystem.RemoveDevice(keyboard);policy.backgroundBehavior=background;
#if UNITY_EDITOR
                policy.editorInputBehaviorInPlayMode=editorPolicy;
#endif
            }
            yield return null;
        }
        void Dispatch(Keyboard keyboard,KeyboardState state)
        {
            InputSystem.QueueStateEvent(keyboard,state);InputSystem.Update();world.manualSimulation=false;
            try{typeof(MvpWorld).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(world,null);}finally{world.manualSimulation=true;}
        }
    }
}
