using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;

namespace Milkfrog.CombatDemo.Tests
{
    public class BullTrialTests
    {
        MvpWorld world; BullBossEnemy boss; string directory; byte[] primary,backup;
        const string Scene="Assets/CombatDemo/Scenes/BullTrial.unity";
        [UnitySetUp] public IEnumerator Load()
        {
            directory=Path.Combine(Path.GetTempPath(),"BullPlay-"+Guid.NewGuid());GameFlowController.SaveDirectoryOverride=directory;
            var disk=new SaveService(directory);var value=BullTrialSession.DefaultLoadout();disk.TrySave(value);value.health=77;disk.TrySave(value);
            primary=File.ReadAllBytes(Path.Combine(directory,"save.json"));backup=File.ReadAllBytes(Path.Combine(directory,"save.backup.json"));
            BullTrialSession.Begin(value,false);EditorSceneManager.LoadSceneInPlayMode(Scene,new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();world.manualSimulation=true;world.feedback.hitStop=false;world.feedback.cameraImpulse=false;boss=(BullBossEnemy)world.enemies[0];
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("BullCleanup-"+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);
            BullTrialSession.Clear();GameFlowController.SaveDirectoryOverride=null;if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
        void Place(Vector3 point){world.player.Motor.enabled=false;world.player.transform.position=point;world.player.Motor.enabled=true;Physics.SyncTransforms();}
        void Engage(){Place(boss.transform.position+Vector3.back*5);world.Director.Tick(.001f);Assert.That(world.Director.State,Is.EqualTo(EncounterState.BossCombat));}
        void DiskUnchanged(){Assert.That(File.ReadAllBytes(Path.Combine(directory,"save.json")),Is.EqualTo(primary));Assert.That(File.ReadAllBytes(Path.Combine(directory,"save.backup.json")),Is.EqualTo(backup));}
        [UnityTest] public IEnumerator IndependentBossHasFullHealthModelNoSwordAndChestLock()
        {
            Assert.That(world.Flow.Store.IsMemory,Is.True);Assert.That(boss.Actor.Core.Health,Is.EqualTo(600));Assert.That(boss.Actor.Core.Tuning.maxPosture,Is.EqualTo(300));
            Assert.That(boss.GetComponent<CombatAnimationPresenter>(),Is.Null);Assert.That(boss.presentation.animator,Is.Not.Null);Assert.That(boss.GetComponentsInChildren<SkinnedMeshRenderer>().Length,Is.EqualTo(1));
            Engage();Assert.That(world.Lock.Target,Is.EqualTo(boss));Assert.That(world.cameraRig.enemy,Is.EqualTo(boss.lockAnchor));Assert.That(world.cameraRig.orbitDistance,Is.EqualTo(4.5f));
            DiskUnchanged();yield return null;
        }
        [UnityTest] public IEnumerator ChargeLocksDirectionAndSlamLocksLandingPoint()
        {
            Engage();Assert.That(boss.TryStartMove(BullMoveKind.Charge),Is.True);var original=boss.LockedDirection;
            Place(boss.transform.position+Vector3.right*6);
            boss.Actor.Core.Tick(.76f);boss.Tick(.01f);
            var locked=boss.LockedDirection;Assert.That(Vector3.Angle(original,locked),Is.GreaterThan(80));Assert.That(boss.TargetLocked,Is.True);
            Place(boss.transform.position+Vector3.left*6);boss.Tick(.01f);Assert.That(boss.LockedDirection,Is.EqualTo(locked));
            boss.ResetForLoad();boss.Engage();Assert.That(boss.TryStartMove(BullMoveKind.Slam),Is.True);boss.Actor.Core.Tick(.72f);boss.Tick(.01f);var point=boss.LockedPoint;
            Place(Vector3.zero);boss.Tick(.01f);Assert.That(boss.LockedPoint,Is.EqualTo(point));yield return null;
        }
        [UnityTest] public IEnumerator ChargeSweepsSpaceAndStopsAtItsConfiguredDistance()
        {
            Engage();boss.TryStartMove(BullMoveKind.Charge);var start=boss.transform.position;Place(start+boss.LockedDirection*4+Vector3.right*.1f);
            var charge=boss.bull.Get(BullMoveKind.Charge);
            boss.Actor.Core.Tick(charge.attack.startup+charge.attack.active*.7f);Assert.That(Vector3.Distance(start,boss.transform.position),Is.GreaterThan(5));Assert.That(world.player.Core.Health,Is.EqualTo(70),$"bull={boss.transform.position}, player={world.player.transform.position}, dir={boss.LockedDirection}, state={boss.Actor.Core.State}, LOS={boss.Actor.HasLineOfSight(world.player,world.player.transform.position+Vector3.up)}, contact={boss.Actor.HasContactPoint}");
            boss.Actor.Core.Tick(charge.attack.active*.3f+.001f);Assert.That(Vector3.Distance(start,boss.transform.position),Is.EqualTo(charge.range).Within(.02f));Assert.That(world.player.Core.Health,Is.EqualTo(70));yield return null;
        }
        [UnityTest] public IEnumerator HornDeflectsButChargeIgnoresGuard()
        {
            Engage();Place(boss.transform.position+Vector3.back*2);boss.TryStartMove(BullMoveKind.Horn);boss.Actor.Core.Tick(.69f);world.player.Core.SetGuard(true,true);boss.Actor.Core.Tick(.03f);
            Assert.That(world.player.Core.Health,Is.EqualTo(100));Assert.That(boss.Actor.Core.State,Is.EqualTo(CombatState.DeflectedStun));Assert.That(boss.CurrentMove,Is.Null);
            boss.Actor.Core.Tick(.3f);Assert.That(boss.TryStartMove(BullMoveKind.Charge),Is.True);boss.Actor.Core.Tick(boss.bull.Get(BullMoveKind.Charge).attack.startup+.4f);Assert.That(world.player.Core.Health,Is.EqualTo(70));yield return null;
        }
        [UnityTest] public IEnumerator RearKickRequiresRearPositionAndHonorsCooldown()
        {
            Engage();boss.transform.rotation=Quaternion.identity;Place(boss.transform.position+Vector3.forward*2);Assert.That(boss.TryStartMove(BullMoveKind.Kick),Is.False);
            Place(boss.transform.position+Vector3.back*2);Assert.That(boss.TryStartMove(BullMoveKind.Kick),Is.True);boss.Actor.Core.Tick(2);boss.Tick(.001f);
            Assert.That(boss.TryStartMove(BullMoveKind.Kick),Is.False);Assert.That(boss.KickCooldown,Is.GreaterThan(0));yield return null;
        }
        [UnityTest] public IEnumerator RoarIsOnceAndDoubleChargeHasSeparateStartup()
        {
            Engage();boss.Actor.Core.RestoreVitals(290,0);boss.Tick(.01f);Assert.That(boss.PhaseChanges,Is.EqualTo(1));Assert.That(boss.RoarRemaining,Is.GreaterThan(0));
            boss.Tick(1.3f);boss.TryStartMove(BullMoveKind.Charge);int id=boss.Actor.Core.AttackId;
            var move=boss.bull.Get(BullMoveKind.Charge);
            boss.Actor.Core.Tick(move.attack.startup+move.attack.active+.25f+.01f);boss.Tick(.01f);Assert.That(boss.CurrentMove.kind,Is.EqualTo(BullMoveKind.Charge));Assert.That(boss.Actor.Core.State,Is.EqualTo(CombatState.AttackStartup));Assert.That(boss.Actor.Core.AttackId,Is.EqualTo(id+1));
            boss.Actor.Core.Tick(move.attack.startup+move.attack.active+move.attack.recovery+.01f);boss.Tick(.01f);boss.Tick(2);Assert.That(boss.PhaseChanges,Is.EqualTo(1));Assert.That(boss.TryStartMove(BullMoveKind.Charge),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator DoubleWaveHitsOncePerRingAndJumpClearsTheWave()
        {
            Engage();boss.Actor.Core.RestoreVitals(290,0);boss.Tick(.01f);boss.Tick(1.3f);Place(boss.transform.position+Vector3.back*3);boss.TryStartMove(BullMoveKind.Wave);
            boss.Actor.Core.Tick(1.7f);Assert.That(world.player.Core.Health,Is.EqualTo(78));boss.Actor.Core.Tick(.65f);Assert.That(world.player.Core.Health,Is.EqualTo(56));
            boss.ResetForLoad();boss.Engage();world.player.Core.Reset();Place(boss.transform.position+Vector3.back*3+Vector3.up);boss.TryStartMove(BullMoveKind.Wave);boss.Actor.Core.Tick(1.7f);Assert.That(world.player.Core.Health,Is.EqualTo(100));yield return null;
        }
        [UnityTest] public IEnumerator PausingAndInventoryFreezeBossPoseWarningAndAttack()
        {
            Engage();boss.TryStartMove(BullMoveKind.Charge);boss.AdvancePresentation(.1f);float remaining=boss.Actor.Core.Remaining,pose=boss.presentation.VisualTime;Vector3 point=boss.transform.position;
            world.Flow.OpenInventory();world.Simulate(.5f);yield return null;Assert.That(boss.Actor.Core.Remaining,Is.EqualTo(remaining));Assert.That(boss.presentation.VisualTime,Is.EqualTo(pose));Assert.That(boss.transform.position,Is.EqualTo(point));Assert.That(boss.presentation.TelegraphVisible,Is.True);
            world.Flow.CloseInventory();world.Flow.TogglePause();world.Simulate(.5f);Assert.That(boss.Actor.Core.Remaining,Is.EqualTo(remaining));world.Flow.TogglePause();world.Simulate(.02f);Assert.That(boss.Actor.Core.Remaining,Is.LessThan(remaining));
        }
        [UnityTest] public IEnumerator BrokenOrDeadCancelsTheAttackAndWarningImmediately()
        {
            Engage();boss.TryStartMove(BullMoveKind.Charge);boss.AdvancePresentation(.1f);Assert.That(boss.presentation.TelegraphVisible,Is.True);
            var attacker=new CombatCore(new CombatTuning());attacker.RequestDefinedAttack(new AttackParameters{startup=.01f,posture=400,maxPosture=400});attacker.Tick(.02f);attacker.TryHit(boss.Actor.Core,true);
            Assert.That(boss.Actor.Core.State,Is.EqualTo(CombatState.PostureBroken));Assert.That(boss.CurrentMove,Is.Null);Assert.That(boss.presentation.TelegraphVisible,Is.False);
            boss.Actor.Core.RestoreVitals(0,0);boss.AdvancePresentation(.01f);Assert.That(boss.CurrentMove,Is.Null);Assert.That(boss.presentation.TelegraphVisible,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator ConsumptionAndVictoryDoNotChangeMainlineFilesOrAwardExperience()
        {
            Engage();world.player.Core.RestoreVitals(60,0);Assert.That(world.TryUseEquippedItem(),Is.True);Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(2));
            int xp=world.Progression.Experience;boss.Actor.Core.RestoreVitals(0,0);world.Simulate(.01f);Assert.That(world.Flow.State,Is.EqualTo(GameFlowState.Playing));Assert.That(world.Progression.Experience,Is.EqualTo(xp));
            Assert.That(world.BossDefeated,Is.True);Assert.That(world.Running,Is.True);Assert.That(world.GetComponent<MvpMenuView>().Visible,Is.False);
            Assert.That(world.cameraRig.Suspended,Is.False);Assert.That(boss.gameObject.activeInHierarchy,Is.True);
            float visualTime=boss.presentation.VisualTime;world.Simulate(.5f);Assert.That(boss.presentation.VisualTime,Is.GreaterThan(visualTime));
            world.Flow.Escape();Assert.That(world.Flow.Paused,Is.True);Assert.That(world.GetComponent<MvpMenuView>().Visible,Is.True);
            world.Flow.Escape();Assert.That(world.Running,Is.True);Assert.That(world.GetComponent<MvpMenuView>().Visible,Is.False);
            DiskUnchanged();Assert.That(BullTrialSession.Fresh().inventory.entries.First(x=>x.itemId=="healing-medicine").quantity,Is.EqualTo(3));yield return null;
        }
        [UnityTest] public IEnumerator RetryLoadsFullResourcesAndEntryInventory()
        {
            Engage();world.player.Core.RestoreVitals(60,0);world.TryUseEquippedItem();world.player.Core.RestoreVitals(0,0);world.Simulate(.01f);world.Flow.Retry();
            yield return new WaitUntil(()=>UnityEngine.Object.FindAnyObjectByType<MvpWorld>()!=world && UnityEngine.Object.FindAnyObjectByType<MvpWorld>()!=null);
            world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();world.manualSimulation=true;yield return null;
            Assert.That(world.player.Core.Health,Is.EqualTo(100));Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(3));Assert.That(world.Inventory.ActiveQuickSlot,Is.Zero);Assert.That(world.player.Core.AttackBuffRemaining,Is.Zero);DiskUnchanged();
        }
        [UnityTest] public IEnumerator MainlinePauseEntryAndReturnPreserveLivePositionInventoryAndSelectedSlot()
        {
            BullTrialSession.Clear();EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/MVP_TestLevel.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();world.manualSimulation=true;world.player.Core.RestoreVitals(63,17);world.Inventory.SelectQuickSlot(2);var position=world.player.transform.position;
            world.Flow.TogglePause();Assert.That(world.Flow.CanStartTrial,Is.True);world.Flow.StartBullTrial();yield return new WaitUntil(()=>SceneManager.GetActiveScene().name=="BullTrial");yield return null;
            world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();world.manualSimulation=true;world.Flow.ReturnFromTrial();yield return new WaitUntil(()=>SceneManager.GetActiveScene().name=="MVP_TestLevel");yield return null;
            world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();world.manualSimulation=true;Assert.That(world.player.Core.Health,Is.InRange(63,64));Assert.That(world.player.Core.Posture,Is.InRange(16,17));Assert.That(world.Inventory.ActiveQuickSlot,Is.EqualTo(2));Assert.That(Vector3.Distance(world.player.transform.position,position),Is.LessThan(.05f));DiskUnchanged();
        }
        [UnityTest] public IEnumerator DodgeWindowAvoidsHornAndThirdIdenticalMoveIsRejected()
        {
            Engage();Place(boss.transform.position+Vector3.back*2);boss.TryStartMove(BullMoveKind.Horn);
            boss.Actor.Core.Tick(.69f);world.player.Core.RequestDodge();world.player.Core.Tick(.1f);boss.Actor.Core.Tick(.02f);
            Assert.That(world.player.Core.Health,Is.EqualTo(100));boss.Actor.Core.Tick(2);boss.Tick(.001f);world.player.Core.Reset();
            Assert.That(boss.TryStartMove(BullMoveKind.Horn),Is.True);boss.Actor.Core.Tick(2);boss.Tick(.001f);
            Assert.That(boss.TryStartMove(BullMoveKind.Horn),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator PhaseWaitsUntilAttackCompletesAndRoarRemainsVulnerable()
        {
            Engage();boss.Actor.Core.RestoreVitals(310,0);boss.TryStartMove(BullMoveKind.Slam);
            var attacker=new CombatCore(new CombatTuning());attacker.RequestDefinedAttack(new AttackParameters{startup=.01f,damage=20,maxDamage=20});attacker.Tick(.02f);attacker.TryHit(boss.Actor.Core,true);
            boss.Tick(.01f);Assert.That(boss.PhaseTwo,Is.False);boss.Actor.Core.Tick(4);boss.Tick(.01f);Assert.That(boss.PhaseTwo,Is.True);
            attacker.Tick(1);attacker.RequestAttack();attacker.Tick(.3f);float health=boss.Actor.Core.Health;attacker.TryHit(boss.Actor.Core,true);Assert.That(boss.Actor.Core.Health,Is.LessThan(health));yield return null;
        }
        [UnityTest] public IEnumerator MainMenuWithoutAnySaveUsesFallbackAndCreatesNoDiskFiles()
        {
            File.Delete(Path.Combine(directory,"save.json"));File.Delete(Path.Combine(directory,"save.backup.json"));BullTrialSession.Clear();
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/MainMenu.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            UnityEngine.Object.FindAnyObjectByType<GameFlowController>().StartBullTrial();yield return new WaitUntil(()=>SceneManager.GetActiveScene().name=="BullTrial");yield return null;
            world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();world.manualSimulation=true;Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(3));Assert.That(Directory.GetFiles(directory),Is.Empty);
        }
        [UnityTest] public IEnumerator MainlinePauseSnapshotsEligibilityBeforeClearingGuardOrCharge()
        {
            BullTrialSession.Clear();EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/MVP_TestLevel.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();world.manualSimulation=true;
            world.player.Core.SetGuard(true,true);world.Flow.TogglePause();Assert.That(world.Flow.CanStartTrial,Is.False);world.Flow.TogglePause();
            world.player.Core.BeginPreparation();world.player.Core.SetAttackHeld(true);world.Flow.TogglePause();Assert.That(world.Flow.CanStartTrial,Is.False);world.Flow.TogglePause();
            world.player.Core.Reset();world.feedback.Clock.Request(.1f);world.Flow.TogglePause();Assert.That(world.Flow.CanStartTrial,Is.False);world.Flow.TogglePause();
        }
        [UnityTest] public IEnumerator LockedBullCameraRotatesAndSuspendsWithMenus()
        {
            Engage();float yaw=world.cameraRig.yaw;world.cameraRig.Look(new Vector2(120,0));world.cameraRig.Step(.1f);Assert.That(world.cameraRig.yaw,Is.GreaterThan(yaw));
            world.Flow.OpenInventory();yaw=world.cameraRig.yaw;world.cameraRig.Look(new Vector2(120,0));Assert.That(world.cameraRig.yaw,Is.EqualTo(yaw));world.Flow.CloseInventory();yield return null;
        }
        [UnityTest] public IEnumerator PlayerBakedSwordTraceCanActuallyDamageTheLargeBull()
        {
            Engage();Place(boss.transform.position+Vector3.back*1.4f);world.player.FaceTarget();
            Assert.That(world.player.Core.RequestAttack(),Is.True);world.player.Core.Tick(1);
            Assert.That(boss.Actor.Core.Health,Is.LessThan(600));Assert.That(world.ContactCount,Is.GreaterThan(0));DiskUnchanged();yield return null;
        }
        [UnityTest] public IEnumerator MainMenuWithMissingOrDamagedSaveUsesFallbackWithoutWriting()
        {
            File.WriteAllText(Path.Combine(directory,"save.json"),"broken");File.WriteAllText(Path.Combine(directory,"save.backup.json"),"broken backup");BullTrialSession.Clear();
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/MainMenu.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            var flow=UnityEngine.Object.FindAnyObjectByType<GameFlowController>();flow.StartBullTrial();yield return new WaitUntil(()=>SceneManager.GetActiveScene().name=="BullTrial");yield return null;
            world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();world.manualSimulation=true;Assert.That(world.Inventory.Quantity("healing-medicine"),Is.EqualTo(3));Assert.That(File.ReadAllText(Path.Combine(directory,"save.json")),Is.EqualTo("broken"));Assert.That(File.ReadAllText(Path.Combine(directory,"save.backup.json")),Is.EqualTo("broken backup"));
        }
    }
}
