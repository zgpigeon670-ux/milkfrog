using System;
using System.Collections;
using Milkfrog.CombatDemo.PCG;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Milkfrog.CombatDemo.Tests
{
    public sealed class PcgPlayTests
    {
        PcgDemo demo;
        [UnitySetUp] public IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/PCG_Whitebox.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            demo = Object.FindAnyObjectByType<PcgDemo>(); demo.manualSimulation = true; demo.FinishGeneration();
            demo.feedback.hitStop = demo.feedback.cameraImpulse = false;
            Assert.IsNotNull(demo.Map, demo.Message);
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("PcgCleanup-" + Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(old);
        }
        void Place(Vector3 position)
        {
            demo.Player.ResetActor(); demo.Player.Motor.enabled = false; demo.Player.transform.position = position; demo.Player.Motor.enabled = true; Physics.SyncTransforms();
        }
        void WalkTo(Vector3 target)
        {
            for (int frame = 0; frame < 260 && Vector3.ProjectOnPlane(target - demo.Player.transform.position, Vector3.up).magnitude > .09f; frame++)
            {
                Vector3 delta = target - demo.Player.transform.position; delta.y = 0;
                demo.Player.Move(delta.normalized, Mathf.Min(4, delta.magnitude * 60), 1f / 60); demo.Player.Core.Tick(1f / 60); Physics.SyncTransforms();
                Assert.Greater(demo.Player.transform.position.y, -.1f, "Fell through a floor seam.");
            }
            Assert.Less(Vector3.ProjectOnPlane(target - demo.Player.transform.position, Vector3.up).magnitude, .12f, "Blocked on a logically open connection.");
        }
        [UnityTest] public IEnumerator RealControllerTraversesWholeShortestRouteOnThreeSeeds()
        {
            foreach (int seed in new[] { 0, 18, 99 })
            {
                demo.BeginGeneration(seed); demo.FinishGeneration(); yield return null;
                Place(PcgGeometry.Center(demo.config, demo.Map.start) + Vector3.up * .02f);
                foreach (int cell in demo.Map.route) WalkTo(PcgGeometry.Center(demo.config, cell) + Vector3.up * .02f);
                Assert.AreEqual(demo.Map.goal, PcgGeometry.Cell(demo.config, demo.Player.transform.position));
            }
        }
        [UnityTest] public IEnumerator EveryOpenConnectionHasContinuousPhysicalFloorAndDoorClearance()
        {
            // Test the authored environment here; live enemy collision/engagement is covered separately.
            foreach (var enemy in demo.Enemies) enemy.Motor.enabled = false;
            for (int cell = 0; cell < demo.Map.tiles.Length; cell++)
            {
                int ports = demo.config.modules[demo.Map.tiles[cell]].ports;
                for (int d = 0; d < 2; d++) if ((ports & (1 << d)) != 0)
                {
                    Place(PcgGeometry.Center(demo.config, cell) + Vector3.up * .02f);
                    int next = cell + WfcSolver.Dz[d] * demo.config.width + WfcSolver.Dx[d];
                    WalkTo(PcgGeometry.Center(demo.config, next) + Vector3.up * .02f);
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ClosedWallsStopRunningJumpingAndDodgingAndCameraIsConstrained()
        {
            int checkedWalls = 0;
            for (int cell = 0; cell < demo.Map.tiles.Length && checkedWalls < 12; cell++)
            {
                var module = demo.config.modules[demo.Map.tiles[cell]]; if (module.ports == 0) continue;
                for (int d = 0; d < 4 && checkedWalls < 12; d++) if ((module.ports & (1 << d)) == 0)
                {
                    Vector3 center = PcgGeometry.Center(demo.config, cell), outward = new Vector3(WfcSolver.Dx[d], 0, WfcSolver.Dz[d]);
                    Place(center + Vector3.up * .02f);
                    for (int f = 0; f < 120; f++) { demo.Player.Move(outward, 4, 1f / 60); demo.Player.Core.Tick(1f / 60); }
                    Assert.IsTrue(demo.Player.TryJump()); demo.Player.RequestDodge(outward);
                    for (int f = 0; f < 60; f++) { demo.Player.Move(outward, 4, 1f / 60); demo.Player.Core.Tick(1f / 60); }
                    float limit = module.room ? demo.config.cellSize * .5f - .25f : demo.config.corridorWidth * .5f;
                    Assert.Less(Vector3.Dot(demo.Player.transform.position - center, outward), limit + .05f);
                    Vector3 pivot = center + Vector3.up * 1.25f, desired = pivot + outward * 7;
                    Assert.Less(Vector3.Distance(pivot, demo.cameraRig.ConstrainPosition(pivot, desired)), limit + .1f);
                    checkedWalls++;
                }
            }
            Assert.AreEqual(12, checkedWalls); yield return null;
        }
        [UnityTest] public IEnumerator EnemyEngagesOnlyInRoomAndReturnsWithoutCrossRoomPursuit()
        {
            var enemy = demo.Enemies[0]; var home = demo.EnemyHomes[0];
            Place(home + Vector3.back * 2.7f); demo.Simulate(.3f); Assert.IsTrue(demo.Engaged[0]);
            Assert.Greater(Vector3.Distance(home, enemy.transform.position), .05f);
            Place(PcgGeometry.Center(demo.config, demo.Map.start) + Vector3.up * .02f); demo.ClearInput(); demo.Simulate(3);
            Assert.IsFalse(demo.Engaged[0]); Assert.Less(Vector3.Distance(home, enemy.transform.position), .15f);
            Assert.AreEqual(demo.Map.encounters[0], PcgGeometry.Cell(demo.config, enemy.transform.position)); yield return null;
        }
        [UnityTest] public IEnumerator ExistingEnemyCanDamageAndBeDamagedWithFactionSafeCombat()
        {
            var enemy = demo.Enemies[0]; Vector3 point = enemy.transform.position;
            Place(point + Vector3.back * 1.1f); demo.Player.transform.rotation = Quaternion.identity; enemy.transform.rotation = Quaternion.Euler(0, 180, 0);
            demo.Player.Target = enemy; Assert.IsTrue(demo.Player.Core.RequestAttack());
            demo.Player.Core.Tick(demo.Player.Core.ActiveAttack.Startup + .1f);
            Assert.Less(enemy.Core.Health, enemy.Core.Tuning.maxHealth);
            enemy.Core.Reset(); Assert.IsTrue(enemy.Core.RequestAttack()); enemy.Core.Tick(enemy.Core.ActiveAttack.Startup + .1f);
            Assert.Less(demo.Player.Core.Health, demo.Player.Core.Tuning.maxHealth); yield return null;
        }
        [UnityTest] public IEnumerator PauseObservationAndRegenerationSuppressHeldMenuAttack()
        {
            demo.SetPaused(true); demo.SetPaused(false);
            demo.SubmitInput(new CombatInputFrame { attackPressed = true, attackHeld = true }); Assert.IsFalse(demo.Player.Core.IsPreparing);
            demo.SubmitInput(new CombatInputFrame()); demo.SubmitInput(new CombatInputFrame { attackPressed = true, attackHeld = true }); Assert.IsTrue(demo.Player.Core.IsPreparing);
            demo.SetObserving(true); Vector3 position = demo.Player.transform.position; demo.Simulate(1); Assert.AreEqual(position, demo.Player.transform.position); Assert.IsFalse(demo.Player.Core.IsPreparing);
            demo.SetObserving(false); demo.BeginGeneration(19); demo.FinishGeneration(); yield return null;
            Assert.AreEqual(19, demo.Map.stats.seed); Assert.AreEqual(2, demo.Enemies.Length); Assert.AreEqual(demo.Player.Core.Tuning.maxHealth, demo.Player.Core.Health);
            Assert.IsNull(Object.FindAnyObjectByType<MvpWorld>()); Assert.IsNull(Object.FindAnyObjectByType<GameFlowController>());
        }
        [UnityTest] public IEnumerator FailedGenerationPreservesPreviousMapAndPlayableActor()
        {
            var original = demo.config; demo.config = Object.Instantiate(original); var player = demo.Player; var map = demo.Map;
            demo.config.modules[0].weight = 0; demo.BeginGeneration(42); demo.FinishGeneration();
            Assert.AreSame(map, demo.Map); Assert.AreSame(player, demo.Player); Assert.IsTrue(player.gameObject.activeInHierarchy); Assert.IsNotEmpty(demo.Message);
            Object.Destroy(demo.config); demo.config = original; yield return null;
        }
        [UnityTest] public IEnumerator HeldAttackStillChargesAcrossRepeatedInputFrames()
        {
            demo.SubmitInput(new CombatInputFrame());
            demo.SubmitInput(new CombatInputFrame { attackPressed = true, attackHeld = true });
            for (int i = 0; i < 30; i++) { demo.SubmitInput(new CombatInputFrame { attackHeld = true }); demo.Simulate(1f / 60); }
            Assert.AreEqual(CombatState.Charging, demo.Player.Core.State);
            demo.SubmitInput(new CombatInputFrame { attackReleased = true });
            Assert.AreEqual(CombatState.AttackStartup, demo.Player.Core.State); yield return null;
        }
        [UnityTest] public IEnumerator ExitCompletionAndSameSeedResetRestoreEncountersAndFullHealth()
        {
            Place(PcgGeometry.Center(demo.config, demo.Map.goal) + Vector3.up * .02f); demo.Simulate(.01f); Assert.IsTrue(demo.Completed);
            demo.Enemies[0].Core.RestoreVitals(0, 0); demo.Player.Core.RestoreVitals(12, 0);
            var tiles = (int[])demo.Map.tiles.Clone(); demo.BeginGeneration(demo.SelectedSeed); demo.FinishGeneration(); yield return null;
            CollectionAssert.AreEqual(tiles, demo.Map.tiles); Assert.AreEqual(2, demo.RemainingEnemies); Assert.IsFalse(demo.Completed); Assert.AreEqual(100, demo.Player.Core.Health);
            for (int cause = 0; cause < 2; cause++)
            {
                var previous = demo.Player; demo.SetPaused(false);
                if (cause == 0) previous.Core.RestoreVitals(0, 0);
                else { previous.Motor.enabled = false; previous.transform.position = Vector3.down * 6; previous.Motor.enabled = true; }
                demo.manualSimulation = false; float deadline = Time.realtimeSinceStartup + 8;
                while (demo.Player == previous && Time.realtimeSinceStartup < deadline) yield return null;
                demo.manualSimulation = true; Assert.AreNotSame(previous, demo.Player, "Automatic death/fall restart timed out.");
                Assert.AreEqual(100, demo.Player.Core.Health); Assert.AreEqual(2, demo.RemainingEnemies); CollectionAssert.AreEqual(tiles, demo.Map.tiles);
            }
        }
    }
}
