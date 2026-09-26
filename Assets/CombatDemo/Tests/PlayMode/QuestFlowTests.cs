using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Milkfrog.CombatDemo.Tests
{
    public class QuestFlowTests
    {
        MvpWorld world; string directory;
        WorldInteractable Key => UnityEngine.Object.FindObjectsByType<WorldInteractable>().First(x => x.kind == WorldInteractionKind.KeyPickup);
        WorldInteractable Gate => UnityEngine.Object.FindObjectsByType<WorldInteractable>().First(x => x.kind == WorldInteractionKind.SealedDoor);
        [UnitySetUp] public IEnumerator Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "QuestFlow-" + Guid.NewGuid()); GameFlowController.SaveDirectoryOverride = directory;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/MVP_TestLevel.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("MVP_TestLevel");
#endif
            yield return null; world = UnityEngine.Object.FindAnyObjectByType<MvpWorld>(); world.manualSimulation = true;
            if (world.Flow.Paused) world.Flow.TogglePause(); world.feedback.hitStop = false;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("QuestCleanup-" + Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(old); GameFlowController.SaveDirectoryOverride = null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        void Place(Vector3 position)
        { world.player.Motor.enabled = false; world.player.transform.position = position; world.player.Motor.enabled = true; Physics.SyncTransforms(); }
        void Focus(IInteractable target)
        {
            Place(target.Root.position + Vector3.back * 1.8f);
            world.gameplayCamera.transform.SetPositionAndRotation(target.InteractionPoint + Vector3.back * 5, Quaternion.identity);
            world.Interactions.Refresh(); Assert.That(world.Interactions.Focus, Is.EqualTo(target));
        }
        void Unlock()
        {
            var camp = world.FindBonfire(BonfireCheckpoint.BossApproachId); Focus(camp); Assert.That(world.Interactions.TryInteract(), Is.True); world.Flow.TogglePause();
            Focus(Key); Assert.That(world.Interactions.TryInteract(), Is.True);
            Focus(Gate); Assert.That(world.Interactions.TryInteract(), Is.True);
        }
        [UnityTest] public IEnumerator CompleteQuestUsesInteractionsAndPersistsAcrossRestTravelDeathAndReload()
        {
            Unlock(); Assert.That(world.Quests.Objective(world.Snapshot()), Does.Contain("4/4"));
            var saved = world.Snapshot(); world.Restore(saved); Assert.That(Key.visual.activeSelf, Is.False); Assert.That(Gate.visual.activeSelf, Is.False);
            var camp = world.FindBonfire(BonfireCheckpoint.BossApproachId); Focus(camp); Assert.That(world.Interactions.TryInteract(), Is.True);
            Assert.That(world.TryFastTravel(world.FindBonfire(BonfireCheckpoint.StartId)), Is.True); world.Flow.TogglePause();
            var respawn = world.DeathRespawnSnapshot(); Assert.That(respawn.worldState.openedDoors, Does.Contain(WorldStateService.GateId)); world.Restore(respawn);
            var boss = world.enemies.First(x => x.IsBoss); Place(boss.Home + Vector3.back * 6); world.Director.Tick(.01f);
            Assert.That(world.Director.State, Is.EqualTo(EncounterState.BossCombat));
            boss.Actor.Core.RestoreVitals(0, 0); world.Simulate(.01f);
            Assert.That(world.Flow.State, Is.EqualTo(GameFlowState.Victory)); Assert.That(world.Flow.Store.TryLoad(out saved), Is.True);
            Assert.That(saved.questProgress.completed, Is.True); Assert.That(saved.experience, Is.EqualTo(200));
            world.Flow.ContinueExploring(); world.Restore(saved); Assert.That(boss.gameObject.activeSelf, Is.False); yield return null;
        }
        [UnityTest] public IEnumerator GateCannotBeBypassedAndEarlyKeyDoesNotSkipCamp()
        {
            Focus(Key); Assert.That(world.Interactions.TryInteract(), Is.True); Assert.That(world.Quests.Objective(world.Snapshot()), Does.Contain("1/4"));
            Focus(Gate); Assert.That(world.Interactions.TryInteract(), Is.False);
            var boss = world.enemies.First(x => x.IsBoss);
            foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            { Place(boss.Home + direction * 3); world.Director.Tick(.01f); Assert.That(world.Director.State, Is.EqualTo(EncounterState.Exploration)); Assert.That(boss.Actor.AcceptsDamage, Is.False); }
            yield return null;
        }
        [UnityTest] public IEnumerator SaveFailureLeavesKeyAndDoorAndQuestUntouched()
        {
            Focus(Key); var before = world.Snapshot(); string path = Path.Combine(directory, "save.json"); File.Delete(path); Directory.CreateDirectory(path);
            Assert.That(world.Interactions.TryInteract(), Is.False); Assert.That(Key.visual.activeSelf, Is.True); Assert.That(world.Snapshot().questProgress.completedSteps, Is.EqualTo(before.questProgress.completedSteps));
            Directory.Delete(path); Assert.That(world.Interactions.TryInteract(), Is.True);
            var camp = world.FindBonfire(BonfireCheckpoint.BossApproachId); Focus(camp); Assert.That(world.Interactions.TryInteract(), Is.True); world.Flow.TogglePause();
            Focus(Gate); File.Delete(path); Directory.CreateDirectory(path); Assert.That(world.Interactions.TryInteract(), Is.False);
            Assert.That(world.BossUnlocked, Is.False); Assert.That(Gate.visual.activeSelf, Is.True); Directory.Delete(path); yield return null;
        }
        [UnityTest] public IEnumerator RevalidatesRangeHeightOcclusionAndCurrentState()
        {
            Focus(Key); Place(Key.transform.position + Vector3.back * 5); Assert.That(world.Interactions.TryInteract(), Is.False);
            Focus(Key); Place(world.player.transform.position + Vector3.up * 4); Assert.That(world.Interactions.TryInteract(), Is.False);
            Focus(Key); var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = Key.InteractionPoint + Vector3.back * .8f;
            wall.transform.localScale = new Vector3(3, 3, .2f); Physics.SyncTransforms(); Assert.That(world.Interactions.TryInteract(), Is.False); UnityEngine.Object.DestroyImmediate(wall);
            Focus(Key); world.player.Core.RequestAttack(); Assert.That(world.Interactions.TryInteract(), Is.False); world.player.Core.Reset();
            Focus(Key); world.feedback.Clock.Request(.1f); Assert.That(world.Interactions.TryInteract(), Is.False); world.feedback.Clock.Reset();
            Focus(Key); world.Flow.TogglePause(); Assert.That(world.Interactions.TryInteract(), Is.False); world.Flow.TogglePause();
            Focus(Key); var mob = world.enemies[0]; world.player.Motor.enabled = false; world.player.transform.position = mob.Home; world.player.Motor.enabled = true;
            world.Director.Engage(mob); Place(Key.transform.position + Vector3.back); world.Interactions.Refresh(); Assert.That(world.Interactions.TryInteract(), Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator RewardWritesKeepKeyAndDoorProgress()
        {
            Unlock(); var mob = world.enemies[0]; mob.Actor.Core.RestoreVitals(0, 0); world.Simulate(.01f);
            Assert.That(world.Flow.Store.TryLoad(out var saved), Is.True); Assert.That(saved.worldState.openedDoors, Does.Contain(WorldStateService.GateId));
            Assert.That(saved.worldState.keyItems, Does.Contain(WorldStateService.KeyItemId)); Assert.That(saved.experience, Is.EqualTo(20)); yield return null;
        }
        [UnityTest] public IEnumerator FocusSortsByScreenThenDistanceThenIdAndNeverExecutesANewFocusSilently()
        {
            var a = Key; var b = Gate;
            a.visual.SetActive(false); b.visual.SetActive(false);
            a.transform.position = new Vector3(.9f, .02f, -26); b.transform.position = new Vector3(0, .02f, -26);
            Place(new Vector3(0, .02f, -28));
            world.gameplayCamera.transform.SetPositionAndRotation(new Vector3(0, 1.02f, -32), Quaternion.identity);
            Physics.SyncTransforms(); world.Interactions.Refresh(); Assert.That(world.Interactions.Focus, Is.EqualTo(b));
            a.transform.position = new Vector3(0, .02f, -26.5f); world.Interactions.Refresh(); Assert.That(world.Interactions.Focus, Is.EqualTo(a));
            a.transform.position = b.transform.position; world.Interactions.Refresh(); Assert.That(world.Interactions.Focus, Is.EqualTo(b), "gate ID sorts before pickup ID");
            b.transform.position += Vector3.right;
            Assert.That(world.Interactions.TryInteract(), Is.False, "must not silently pick up a newly focused item");
            world.gameplayCamera.transform.rotation = Quaternion.Euler(0, 180, 0); world.Interactions.Refresh(); Assert.That(world.Interactions.Focus, Is.Null);
            yield return null;
        }
        [UnityTest] public IEnumerator BossSaveFailureCanBeRetriedWithoutAwardingExperienceAgain()
        {
            Unlock(); var boss = world.enemies.First(x => x.IsBoss); Place(boss.Home + Vector3.back * 6); world.Director.Tick(.01f);
            string path = Path.Combine(directory, "save.json"); File.Delete(path); Directory.CreateDirectory(path);
            boss.Actor.Core.RestoreVitals(0, 0); world.Simulate(.01f);
            Assert.That(world.Flow.State, Is.EqualTo(GameFlowState.Victory)); Assert.That(world.BossDefeated, Is.True);
            Assert.That(world.Progression.Experience, Is.EqualTo(200)); Assert.That(world.Quests.Evaluate(world.Snapshot()).completed, Is.True);
            Assert.That(world.Flow.Store.TryLoad(out var old) && old.bossDefeated, Is.False);
            Directory.Delete(path); world.Flow.Won(); Assert.That(world.Flow.Store.TryLoad(out var saved), Is.True);
            Assert.That(saved.bossDefeated && saved.questProgress.completed, Is.True); Assert.That(saved.experience, Is.EqualTo(200));
            world.Flow.Won(); Assert.That(world.Progression.Experience, Is.EqualTo(200)); yield return null;
        }
        [UnityTest] public IEnumerator ReloadThroughHomePreservesWorldAndCreatesSingleInteractionSession()
        {
            Unlock(); world.Flow.ReturnHome();
            for (int i = 0; i < 120 && SceneManager.GetActiveScene().name != "MainMenu"; i++) yield return null;
            yield return null;
            var flow = UnityEngine.Object.FindAnyObjectByType<GameFlowController>(); Assert.That(flow.Store.TryLoad(out var saved), Is.True);
            flow.LoadLevel(saved);
            for (int i = 0; i < 120 && SceneManager.GetActiveScene().name != "MVP_TestLevel"; i++) yield return null;
            yield return null; world = UnityEngine.Object.FindAnyObjectByType<MvpWorld>(); world.manualSimulation = true;
            Assert.That(UnityEngine.Object.FindObjectsByType<MvpWorld>().Length, Is.EqualTo(1));
            Assert.That(world.BossUnlocked, Is.True); Assert.That(Key.visual.activeSelf, Is.False); Assert.That(Gate.visual.activeSelf, Is.False);
            Assert.That(world.Quests.Objective(world.Snapshot()), Does.Contain("4/4"));
        }
    }
}
