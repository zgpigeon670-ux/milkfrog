using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Milkfrog.CombatDemo.Tests
{
    public class InventoryFlowTests
    {
        MvpWorld world; string directory;
        InventoryPickup Medicine => UnityEngine.Object.FindObjectsByType<InventoryPickup>().First(x=>x.item.stableId=="healing-medicine");
        InventoryPickup Calming => UnityEngine.Object.FindObjectsByType<InventoryPickup>().First(x=>x.item.stableId=="calming-pill");
        [UnitySetUp] public IEnumerator Setup()
        {
            directory=Path.Combine(Path.GetTempPath(),"InventoryFlow-"+Guid.NewGuid()); GameFlowController.SaveDirectoryOverride=directory;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/MVP_TestLevel.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("MVP_TestLevel");
#endif
            yield return null; world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>(); world.manualSimulation=true;
            if(world.Flow.Paused)world.Flow.TogglePause(); world.feedback.hitStop=false;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var old=SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("InventoryCleanup-"+Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(old); GameFlowController.SaveDirectoryOverride=null;
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
        void Place(Vector3 position) {world.player.Motor.enabled=false; world.player.transform.position=position; world.player.Motor.enabled=true; Physics.SyncTransforms();}
        void Focus(IInteractable target)
        {
            Place(target.Root.position+Vector3.back*1.8f); world.gameplayCamera.transform.SetPositionAndRotation(target.InteractionPoint+Vector3.back*5,Quaternion.identity);
            world.Interactions.Refresh(); Assert.That(world.Interactions.Focus,Is.EqualTo(target));
        }
        void Pick(InventoryPickup item) {Focus(item); Assert.That(world.Interactions.TryInteract(),Is.True,world.Flow.Message);}
        void PrepareMedicine() {Pick(Medicine); world.player.Core.RestoreVitals(20,60); Assert.That(world.Flow.Save(),Is.True);}
        string MenuText(string name) => GameObject.Find("MvpMenuCanvas").GetComponentsInChildren<UnityEngine.UI.Text>()
            .First(t => t.gameObject.name == name).text;
        void ClickMenu(string label) => GameObject.Find("MvpMenuCanvas").GetComponentsInChildren<UnityEngine.UI.Button>()
            .First(b => b.GetComponentInChildren<UnityEngine.UI.Text>().text == label).onClick.Invoke();

        [UnityTest] public IEnumerator InventoryReturnsToQuestAndEscapeNavigatesOneLayerAtATime()
        {
            world.Flow.TogglePause(); ClickMenu("任务 / 属性"); ClickMenu("任务进度");
            string questTitle = MenuText("Title");
            world.Flow.OpenInventory(); world.Flow.Escape();
            Assert.That(MenuText("Title"),Is.EqualTo(questTitle)); Assert.That(world.Flow.Paused,Is.True);
            world.Flow.Escape(); Assert.That(MenuText("Title"),Is.EqualTo("角色记录"));
            world.Flow.Escape(); Assert.That(MenuText("Title"),Is.EqualTo("PAUSED"));
            world.Flow.Escape(); Assert.That(world.Running,Is.True); yield return null;
        }
        [UnityTest] public IEnumerator AttributeReturnRefreshesVitalsAndPreservesItsOriginalParent()
        {
            PrepareMedicine(); world.Flow.OpenAttributes(); world.Flow.OpenInventory();
            Assert.That(world.TryUseItem("healing-medicine"),Is.True);
            world.Flow.CloseInventory(); Assert.That(MenuText("Title"),Is.EqualTo("角色属性"));
            Assert.That(MenuText("Subtitle"),Does.Contain("生命 60/100"));
            world.Flow.Escape(); Assert.That(world.Running,Is.True);
            world.Flow.TogglePause(); ClickMenu("任务 / 属性"); ClickMenu("Attributes");
            world.Flow.OpenInventory(); world.Flow.CloseInventory(); world.Flow.Escape();
            Assert.That(MenuText("Title"),Is.EqualTo("角色记录")); yield return null;
        }
        [UnityTest] public IEnumerator InventoryReturnsToFastTravelAndThenBonfire()
        {
            Focus(world.FindBonfire(BonfireCheckpoint.StartId)); Assert.That(world.Interactions.TryInteract(),Is.True);
            ClickMenu("快速传送"); world.Flow.OpenInventory(); world.Flow.CloseInventory();
            Assert.That(MenuText("Title"),Is.EqualTo("快速传送"));
            world.Flow.Escape(); Assert.That(MenuText("Title"),Is.EqualTo(world.Flow.ActiveBonfire.displayName));
            world.Flow.Escape(); Assert.That(world.Running,Is.True); yield return null;
        }
        [UnityTest] public IEnumerator PauseAndItsInventoryKeepTheCameraFrozenUntilResume()
        {
            world.cameraRig.Step(.01f); var position=world.gameplayCamera.transform.position;
            var rotation=world.gameplayCamera.transform.rotation;
            world.Flow.TogglePause(); world.cameraRig.Look(new Vector2(200,100)); world.cameraRig.Step(1);
            Assert.That(world.gameplayCamera.transform.position,Is.EqualTo(position));
            Assert.That(world.gameplayCamera.transform.rotation,Is.EqualTo(rotation));
            world.Flow.OpenInventory(); world.Flow.CloseInventory(); Assert.That(world.cameraRig.Suspended,Is.True);
            world.cameraRig.Step(1); Assert.That(world.gameplayCamera.transform.position,Is.EqualTo(position));
            world.Flow.Escape(); Assert.That(world.cameraRig.Suspended,Is.False);
            world.SubmitInput(new CombatInputFrame{attackPressed=true,attackHeld=true});
            Assert.That(world.player.Core.IsPreparing,Is.False);
            world.SubmitInput(new CombatInputFrame()); world.SubmitInput(new CombatInputFrame{attackPressed=true,attackHeld=true});
            Assert.That(world.player.Core.IsPreparing,Is.True); yield return null;
        }
        [UnityTest] public IEnumerator QuantityRefreshReusesTilesAndKeepsUsableButtonFocus()
        {
            PrepareMedicine(); world.Flow.OpenInventory(); yield return null; yield return null;
            var view=world.Flow.inventoryView;
            var tile=view.content.GetComponentsInChildren<InventoryTile>().Single();
            EventSystem.current.SetSelectedGameObject(view.useButton.gameObject);
            view.useButton.onClick.Invoke(); yield return null;
            Assert.That(view.content.GetComponentsInChildren<InventoryTile>().Single(),Is.SameAs(tile));
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(view.useButton.gameObject));
            Assert.That(tile.count.text,Is.EqualTo("×2"));
            view.useButton.onClick.Invoke(); yield return null;
            Assert.That(world.player.Core.Health,Is.EqualTo(100)); Assert.That(view.useButton.interactable,Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(tile.gameObject));
            world.player.Core.RestoreVitals(20,0); Assert.That(world.TryUseItem("healing-medicine"),Is.True);
            yield return null; yield return null;
            Assert.That(view.content.GetComponentsInChildren<InventoryTile>().Length,Is.Zero);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(view.categoryButtons[0].gameObject));
        }
        [UnityTest] public IEnumerator MissingInventoryReferencesLeaveFlowResumable()
        {
            var view=world.Flow.inventoryView; var button=view.useButton;
            try
            {
                view.useButton=null; world.Flow.OpenInventory();
                Assert.That(world.Flow.InventoryOpen,Is.False); Assert.That(world.Running,Is.True);
                Assert.That(world.Flow.Message,Does.Contain("配置缺失")); Assert.That(world.cameraRig.Suspended,Is.False);
            }
            finally {view.useButton=button;}
            yield return null;
        }
        [UnityTest] public IEnumerator PressingDuringAnActionCannotBufferAnAttackUntilRecovery()
        {
            world.player.Core.RequestAttack(); world.player.Core.Tick(.02f);
            world.SubmitInput(new CombatInputFrame{attackPressed=true,attackHeld=true});
            int before=world.player.Core.AttackId; world.player.Core.Tick(1);
            world.SubmitInput(new CombatInputFrame{attackHeld=true});
            Assert.That(world.player.Core.AttackId,Is.EqualTo(before)); Assert.That(world.player.Core.IsPreparing,Is.False);
            world.SubmitInput(new CombatInputFrame()); world.SubmitInput(new CombatInputFrame{attackPressed=true,attackHeld=true});
            Assert.That(world.player.Core.IsPreparing,Is.True); yield return null;
        }
        [UnityTest] public IEnumerator PressingDuringHitStopCannotBufferAnAttack()
        {
            world.feedback.Clock.Request(.1f); world.SubmitInput(new CombatInputFrame{attackPressed=true,attackHeld=true});
            world.feedback.Clock.Reset(); world.SubmitInput(new CombatInputFrame{attackHeld=true});
            Assert.That(world.player.Core.IsPreparing,Is.False); Assert.That(world.player.Core.AttackId,Is.Zero);
            world.SubmitInput(new CombatInputFrame()); world.SubmitInput(new CombatInputFrame{attackPressed=true,attackHeld=true});
            Assert.That(world.player.Core.IsPreparing,Is.True); yield return null;
        }
        [UnityTest] public IEnumerator ReleaseAndFreshPressInTheSameFrameDoNotRequireAnExtraIdleFrame()
        {
            world.player.Core.RequestAttack();
            world.SubmitInput(new CombatInputFrame{attackPressed=true,attackHeld=true});
            world.player.Core.Tick(1);
            world.SubmitInput(new CombatInputFrame{attackReleased=true,attackPressed=true,attackHeld=true});
            Assert.That(world.player.Core.IsPreparing,Is.True);
            world.feedback.Clock.Request(.1f);
            world.SubmitInput(new CombatInputFrame{attackReleased=true,attackPressed=true,attackHeld=true});
            Assert.That(world.player.Core.IsPreparing,Is.False,"Releasing during hit stop cancels the previous preparation even if pressed again");
            world.feedback.Clock.Reset(); world.SubmitInput(new CombatInputFrame{attackHeld=true});
            Assert.That(world.player.Core.IsPreparing,Is.False); yield return null;
        }
        [UnityTest] public IEnumerator PickupsStackPersistAndCannotBeCollectedTwice()
        {
            Pick(Medicine); Pick(Calming); Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(3));
            Assert.That(Medicine.visual.activeSelf,Is.False); Assert.That(world.TryPickup(Medicine),Is.False);
            Assert.That(world.Flow.Store.TryLoad(out var snapshot),Is.True); world.Restore(snapshot);
            Assert.That(world.Inventory.Quantity("calming-pill"),Is.EqualTo(3)); Assert.That(Medicine.visual.activeSelf,Is.False); yield return null;
        }
        [UnityTest] public IEnumerator PickupFailureLeavesCountAndWorldObjectUntouched()
        {
            Focus(Medicine); string path=Path.Combine(directory,"save.json"); File.Delete(path); Directory.CreateDirectory(path);
            Assert.That(world.Interactions.TryInteract(),Is.False); Assert.That(world.Inventory.Quantity("healing-medicine"),Is.Zero);
            Assert.That(world.WorldState.IsCollected(Medicine.stableId),Is.False); Assert.That(Medicine.visual.activeSelf,Is.True);
            Directory.Delete(path); Assert.That(world.Interactions.TryInteract(),Is.True); yield return null;
        }
        [UnityTest] public IEnumerator UseClampsVitalsAndEmptyCountsDisappearFromGrid()
        {
            PrepareMedicine(); world.Flow.OpenInventory(); var view=world.Flow.inventoryView;
            Assert.That(world.TryUseItem("healing-medicine"),Is.True); Assert.That(world.player.Core.Health,Is.EqualTo(60));
            Assert.That(world.TryUseItem("healing-medicine"),Is.True); Assert.That(world.player.Core.Health,Is.EqualTo(100));
            Assert.That(world.TryUseItem("healing-medicine"),Is.False); Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(1));
            world.player.Core.RestoreVitals(50,0); Assert.That(world.TryUseItem("healing-medicine"),Is.True);
            yield return null; Assert.That(world.Inventory.HasItem("healing-medicine"),Is.False);
            Assert.That(view.content.GetComponentsInChildren<InventoryTile>().Length,Is.Zero); Assert.That(view.empty.gameObject.activeSelf,Is.True);
        }
        [UnityTest] public IEnumerator BothConsumablesApplyAndZeroPostureDoesNotConsume()
        {
            Pick(Calming); world.player.Core.RestoreVitals(100,60); world.Flow.OpenInventory();
            Assert.That(world.TryUseItem("calming-pill"),Is.True); Assert.That(world.player.Core.Posture,Is.EqualTo(20));
            Assert.That(world.TryUseItem("calming-pill"),Is.True); Assert.That(world.player.Core.Posture,Is.Zero);
            Assert.That(world.TryUseItem("calming-pill"),Is.False); Assert.That(world.Inventory.Quantity("calming-pill"),Is.EqualTo(1)); yield return null;
        }
        [UnityTest] public IEnumerator CombatUseOnlyUpdatesInventoryInTheSafeSave()
        {
            PrepareMedicine(); Assert.That(world.Flow.Store.TryLoad(out var before),Is.True);
            var mob=world.enemies.First(x=>!x.IsBoss); Place(mob.Home); world.Director.Engage(mob);
            world.player.Core.RestoreVitals(30,30); world.Flow.OpenInventory(); Assert.That(world.TryUseItem("healing-medicine"),Is.True);
            Assert.That(world.player.Core.Health,Is.EqualTo(70)); Assert.That(world.Flow.Store.TryLoad(out var after),Is.True);
            Assert.That(after.position,Is.EqualTo(before.position)); Assert.That(after.health,Is.EqualTo(before.health)); Assert.That(after.posture,Is.EqualTo(before.posture));
            Assert.That(after.inventory.entries.First(x=>x.itemId=="healing-medicine").quantity,Is.EqualTo(2)); yield return null;
        }
        [UnityTest] public IEnumerator UseFailureDoesNotConsumeOrHeal()
        {
            PrepareMedicine(); world.Flow.OpenInventory(); string path=Path.Combine(directory,"save.json"); File.Delete(path); Directory.CreateDirectory(path);
            Assert.That(world.TryUseItem("healing-medicine"),Is.False); Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(3)); Assert.That(world.player.Core.Health,Is.EqualTo(20));
            Directory.Delete(path); Assert.That(world.TryUseItem("healing-medicine"),Is.True); yield return null;
        }
        [UnityTest] public IEnumerator OpeningDuringAttackOrChargeCannotUseOrAdvanceTheAction()
        {
            PrepareMedicine(); world.player.Core.RequestAttack(); var state=world.player.Core.State; float remaining=world.player.Core.Remaining;
            world.Flow.OpenInventory(); Assert.That(world.Flow.InventoryUseAllowed,Is.False); Assert.That(world.TryUseItem("healing-medicine"),Is.False);
            world.Simulate(2); Assert.That(world.player.Core.State,Is.EqualTo(state)); Assert.That(world.player.Core.Remaining,Is.EqualTo(remaining));
            world.Flow.CloseInventory(); world.player.Core.Reset(); world.player.Core.BeginPreparation(); world.player.Core.SetAttackHeld(true);
            Assert.That(world.player.Core.IsPreparing,Is.True); world.Flow.OpenInventory(); Assert.That(world.Flow.InventoryUseAllowed,Is.False);
            Assert.That(world.player.Core.IsPreparing,Is.True); Assert.That(world.TryUseItem("healing-medicine"),Is.False); yield return null;
        }
        [UnityTest] public IEnumerator EnemyPlayerAnimationAndCameraStayFrozenWhileInventoryIsOpen()
        {
            PrepareMedicine(); var mob=world.enemies.First(x=>!x.IsBoss); world.Director.Engage(mob); mob.Actor.Core.RequestAttack();
            float timer=mob.Actor.Core.Remaining; var playerPosition=world.player.transform.position; var enemyPosition=mob.transform.position;
            var rotation=world.gameplayCamera.transform.rotation; var cameraPosition=world.gameplayCamera.transform.position;
            var animation=mob.GetComponent<CombatAnimationPresenter>(); double poseTime=animation.AttackPoseTime;
            world.Flow.OpenInventory(); world.Simulate(5); yield return null; yield return null;
            Assert.That(mob.Actor.Core.Remaining,Is.EqualTo(timer)); Assert.That(mob.transform.position,Is.EqualTo(enemyPosition));
            Assert.That(world.player.transform.position,Is.EqualTo(playerPosition)); Assert.That(world.gameplayCamera.transform.rotation,Is.EqualTo(rotation));
            Assert.That(world.gameplayCamera.transform.position,Is.EqualTo(cameraPosition));
            Assert.That(world.Running,Is.False); Assert.That(animation.AttackPoseTime,Is.EqualTo(poseTime));
        }
        [UnityTest] public IEnumerator KeyIsShownAndOpeningGateDoesNotConsumeIt()
        {
            var key=UnityEngine.Object.FindObjectsByType<WorldInteractable>().First(x=>x.kind==WorldInteractionKind.KeyPickup);
            Focus(key); Assert.That(world.Interactions.TryInteract(),Is.True); world.Flow.OpenInventory(); world.Flow.inventoryView.SetCategory(2); yield return null;
            Assert.That(world.Inventory.Quantity(WorldStateService.KeyItemId),Is.EqualTo(1)); Assert.That(world.Flow.inventoryView.itemName.text,Is.EqualTo("守门钥匙"));
            Assert.That(world.TryUseItem(WorldStateService.KeyItemId),Is.False); world.Flow.CloseInventory();
            var camp=world.FindBonfire(BonfireCheckpoint.BossApproachId); Focus(camp); Assert.That(world.Interactions.TryInteract(),Is.True); world.Flow.TogglePause();
            var gate=UnityEngine.Object.FindObjectsByType<WorldInteractable>().First(x=>x.kind==WorldInteractionKind.SealedDoor); Focus(gate); Assert.That(world.Interactions.TryInteract(),Is.True);
            Assert.That(world.Inventory.Quantity(WorldStateService.KeyItemId),Is.EqualTo(1)); Assert.That(world.BossUnlocked,Is.True);
        }
        [UnityTest] public IEnumerator GateChecksInventoryInsteadOfHistoricalAcquisitionFacts()
        {
            var snapshot=world.Snapshot(); snapshot.worldState.keyItems=new[]{WorldStateService.KeyItemId};
            snapshot.unlockedCheckpointIds=new[]{BonfireCheckpoint.StartId,BonfireCheckpoint.BossApproachId}; world.Restore(snapshot);
            var gate=UnityEngine.Object.FindObjectsByType<WorldInteractable>().First(x=>x.kind==WorldInteractionKind.SealedDoor); Focus(gate);
            Assert.That(gate.BlockReason(world),Is.EqualTo("需要守门钥匙")); Assert.That(world.Interactions.TryInteract(),Is.False);
            Assert.That(world.BossUnlocked,Is.False); yield return null;
        }
        [UnityTest] public IEnumerator RestTravelDeathAndRewardWritesPreserveConsumedCounts()
        {
            PrepareMedicine(); world.Flow.OpenInventory(); Assert.That(world.TryUseItem("healing-medicine"),Is.True); world.Flow.CloseInventory();
            var start=world.FindBonfire(BonfireCheckpoint.StartId); Focus(start); Assert.That(world.Interactions.TryInteract(),Is.True); world.Flow.TogglePause();
            var end=world.FindBonfire(BonfireCheckpoint.BossApproachId); Focus(end); Assert.That(world.Interactions.TryInteract(),Is.True);
            Assert.That(world.TryFastTravel(start),Is.True); world.Flow.TogglePause();
            var mob=world.enemies.First(x=>!x.IsBoss); mob.Actor.Core.RestoreVitals(0,0); world.Simulate(.01f);
            Assert.That(world.Flow.Store.TryLoad(out var saved),Is.True); Assert.That(saved.inventory.entries.First(x=>x.itemId=="healing-medicine").quantity,Is.EqualTo(2));
            var respawn=world.DeathRespawnSnapshot(); world.Restore(respawn);
            Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(2)); Assert.That(Medicine.visual.activeSelf,Is.False); yield return null;
        }
        [UnityTest] public IEnumerator PauseMenuAndBonfireReturnKeepTheCorrectParentMenu()
        {
            world.Flow.TogglePause(); world.Flow.OpenInventory(); world.Flow.TogglePause(); Assert.That(world.Flow.Paused,Is.True); Assert.That(world.Flow.InventoryOpen,Is.False);
            world.Flow.TogglePause(); Assert.That(world.Running,Is.True);
            Focus(world.FindBonfire(BonfireCheckpoint.StartId)); Assert.That(world.Interactions.TryInteract(),Is.True);
            world.Flow.OpenInventory(); world.Flow.CloseInventory(); Assert.That(world.Flow.ActiveBonfire,Is.Not.Null); Assert.That(world.Flow.Paused,Is.True); yield return null;
        }
        [UnityTest] public IEnumerator MenuMouseClickCannotAttackAfterClosingAndBAndEscapeAreBound()
        {
            var previousBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            var previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            var mouse=InputSystem.AddDevice<Mouse>(); var keyboard=InputSystem.AddDevice<Keyboard>();
            var input=(DemoInput)typeof(MvpWorld).GetField("input",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(world);
            var map=(InputActionMap)typeof(DemoInput).GetField("map",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(input);
            map.devices=new InputDevice[]{mouse,keyboard}; InputSystem.EnableDevice(mouse); InputSystem.EnableDevice(keyboard);
            try
            {
                DispatchKeyboard(keyboard,new KeyboardState(Key.B));
                Assert.That(world.Flow.InventoryOpen,Is.True,$"B pressed={input.InventoryPressed}, raw={keyboard.bKey.isPressed}, device={keyboard.enabled}, value={map["Inventory"].ReadValue<float>()}, phase={map["Inventory"].phase}, pause={input.PausePressed}, enabled={map.enabled}, state={world.Flow.State}");
                DispatchKeyboard(keyboard,new KeyboardState());
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left,true)); InputSystem.Update();
                int before=world.player.Core.AttackId; world.Flow.CloseInventory();
                world.SubmitInput(new CombatInputFrame{attackHeld=true,attackPressed=true});
                Assert.That(world.player.Core.AttackId,Is.EqualTo(before));
                InputSystem.QueueStateEvent(mouse,new MouseState()); DispatchKeyboard(keyboard,new KeyboardState(Key.B));
                Assert.That(world.Flow.InventoryOpen,Is.True);
                DispatchKeyboard(keyboard,new KeyboardState(Key.Escape));
                Assert.That(world.Flow.InventoryOpen,Is.False); Assert.That(world.Running,Is.True);
                world.SubmitInput(new CombatInputFrame()); world.SubmitInput(new CombatInputFrame{attackHeld=true,attackPressed=true});
                Assert.That(world.player.Core.IsPreparing,Is.True);
                world.SubmitInput(new CombatInputFrame{attackReleased=true});
                Assert.That(world.player.Core.AttackId,Is.GreaterThan(before)); yield return null;
            }
            finally
            {
                world.manualSimulation=true; map.devices=null; InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior=previousBackground;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
#endif
            }
        }
        void DispatchKeyboard(Keyboard device,KeyboardState state)
        {
            InputSystem.QueueStateEvent(device,state); InputSystem.Update(); world.manualSimulation=false;
            try {typeof(MvpWorld).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(world,null);}
            finally {world.manualSimulation=true;}
        }
        [UnityTest] public IEnumerator DirectionNavigationAndSubmitOperateTheEditableGridAndUseButton()
        {
            PrepareMedicine(); world.Inventory.TryAdd("calming-pill",2); world.Flow.OpenInventory(); yield return null; yield return null;
            var view=world.Flow.inventoryView;
            var first=EventSystem.current.currentSelectedGameObject; Assert.That(first.GetComponent<InventoryTile>(),Is.Not.Null);
            var move=new AxisEventData(EventSystem.current){moveDir=MoveDirection.Right,moveVector=Vector2.right};
            ExecuteEvents.Execute(first,move,ExecuteEvents.moveHandler);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.Not.EqualTo(first));
            view.SelectItem("healing-medicine"); EventSystem.current.SetSelectedGameObject(view.useButton.gameObject);
            ExecuteEvents.Execute(view.useButton.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
            Assert.That(world.player.Core.Health,Is.EqualTo(60)); Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator UnlimitedItemsScrollAndEmptyCategoryHasUsableNavigation()
        {
            for(int i=0;i<40;i++)world.Inventory.TryAdd("future-item-"+i.ToString("00"),1);
            world.Flow.OpenInventory(); yield return null; yield return null;
            var view=world.Flow.inventoryView; Canvas.ForceUpdateCanvases();
            Assert.That(view.content.GetComponentsInChildren<InventoryTile>().Length,Is.EqualTo(40));
            Assert.That(view.content.rect.height,Is.GreaterThan(view.scroll.viewport.rect.height));
            var last=view.content.GetComponentsInChildren<InventoryTile>().Last(); EventSystem.current.SetSelectedGameObject(last.gameObject);
            Assert.That(view.scroll.verticalNormalizedPosition,Is.LessThan(.1f));
            view.SetCategory(2); yield return null; yield return null;
            Assert.That(view.empty.gameObject.activeSelf,Is.True); Assert.That(view.useButton.interactable,Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(view.categoryButtons[2].gameObject));
        }
        [UnityTest] public IEnumerator ReturnThroughHomeRestoresItemsAndOneInventoryView()
        {
            Pick(Medicine); world.Flow.ReturnHome();
            for(int i=0;i<120 && SceneManager.GetActiveScene().name!="MainMenu";i++)yield return null;
            yield return null; var flow=UnityEngine.Object.FindAnyObjectByType<GameFlowController>(); Assert.That(flow.Store.TryLoad(out var saved),Is.True); flow.LoadLevel(saved);
            for(int i=0;i<120 && SceneManager.GetActiveScene().name!="MVP_TestLevel";i++)yield return null;
            yield return null; world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>(); world.manualSimulation=true;
            Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(3)); Assert.That(Medicine.visual.activeSelf,Is.False);
            Assert.That(UnityEngine.Object.FindObjectsByType<InventoryView>(FindObjectsInactive.Include).Length,Is.EqualTo(1));
        }
    }
}
