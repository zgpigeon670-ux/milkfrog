using System;
using System.Collections;
using Milkfrog.CombatDemo.BellCrossing;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Milkfrog.CombatDemo.Tests
{
    public sealed class BellCrossingTests
    {
        BellCrossingLevel level;
        [UnitySetUp]public IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/BellCrossing.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            level=Object.FindAnyObjectByType<BellCrossingLevel>();level.manualSimulation=true;level.SetPaused(false);level.feedback.hitStop=level.feedback.sound=level.feedback.cameraImpulse=false;yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("BellCleanup-"+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);
        }
        void Place(Vector3 point){BellCrossingLevel.Place(level.Player,point);level.ClearInput();}
        void Defeat(CrossingRoom room){for(int i=0;i<level.Enemies.Length;i++)if(BellCrossingLayout.EnemyRooms[i]==room)level.Enemies[i].Core.RestoreVitals(0,0);}
        void TakeSeals()
        {
            Defeat(CrossingRoom.WestSeal);Place(BellCrossingLayout.WestPoint);Assert.IsTrue(level.Interact());
            Defeat(CrossingRoom.EastSeal);Place(BellCrossingLayout.EastPoint);Assert.IsTrue(level.Interact());
        }
        void WalkTo(Vector3 point)
        {
            for(int f=0;f<500&&Vector3.ProjectOnPlane(point-level.Player.transform.position,Vector3.up).magnitude>.09f;f++)
            {
                var delta=point-level.Player.transform.position;delta.y=0;level.Player.Move(delta.normalized,Mathf.Min(4,delta.magnitude*60),1f/60);level.Player.Core.Tick(1f/60);Physics.SyncTransforms();
                Assert.Greater(level.Player.transform.position.y,-.1f,"Floor seam fall");
            }
            Assert.Less(Vector3.ProjectOnPlane(point-level.Player.transform.position,Vector3.up).magnitude,.13f,"Blocked authored connection: target="+point+" actual="+level.Player.transform.position);
        }
        [UnityTest]public IEnumerator EveryConnectionCanBeTraversedWithRealControllerAfterUnlocking()
        {
            foreach(var actor in level.Enemies)actor.Motor.enabled=false;
            TakeSeals();Place(BellCrossingLayout.LeverPoint);Assert.IsTrue(level.Interact());
            Defeat(CrossingRoom.Guardian);Place(BellCrossingLayout.BellPoint);level.Interact();
            for(int i=0;i<BellCrossingLayout.Edges.GetLength(0);i++)
            {
                var a=(CrossingRoom)BellCrossingLayout.Edges[i,0];var b=(CrossingRoom)BellCrossingLayout.Edges[i,1];
                Place(BellCrossingLayout.Center(a));WalkTo(BellCrossingLayout.Center(b));WalkTo(BellCrossingLayout.Center(a));
            }
            yield return null;
        }
        [UnityTest]public IEnumerator SealGateCannotBeBypassedByRunningJumpOrDodge()
        {
            Place(BellCrossingLayout.Center(CrossingRoom.Junction));
            for(int f=0;f<220;f++){level.Player.Move(Vector3.forward,4,1f/60);level.Player.Core.Tick(1f/60);}
            Assert.IsTrue(level.Player.TryJump());level.Player.RequestDodge(Vector3.forward);
            for(int f=0;f<100;f++){level.Player.Move(Vector3.forward,4,1f/60);level.Player.Core.Tick(1f/60);}
            Assert.Less(level.Player.transform.position.z,7);yield return null;
        }
        [UnityTest]public IEnumerator BothSealsAndLocalGuardClearanceAreRequired()
        {
            Place(BellCrossingLayout.WestPoint);Assert.IsFalse(level.Interact());
            Defeat(CrossingRoom.WestSeal);Assert.IsTrue(level.Interact());Assert.IsTrue(level.WestSeal);Assert.IsFalse(level.EastSeal);
            Assert.IsTrue(GameObject.Find("Two seal gate").activeSelf);
            Place(BellCrossingLayout.EastPoint);level.Enemies[4].Core.RestoreVitals(0,0);Assert.IsFalse(level.Interact());
            level.Enemies[5].Core.RestoreVitals(0,0);Assert.IsTrue(level.Interact());Assert.IsNull(GameObject.Find("Two seal gate"));yield return null;
        }
        [UnityTest]public IEnumerator ShortcutOnlyOpensFromItsWestSide()
        {
            Place(new Vector3(-4.5f,.02f,-2));Assert.IsFalse(level.Interact());Assert.IsFalse(level.ShortcutOpen);
            Place(BellCrossingLayout.LeverPoint);Assert.IsTrue(level.Interact());Assert.IsTrue(level.ShortcutOpen);yield return null;
        }
        [UnityTest]public IEnumerator CheckpointPersistsSealsKillsAndShortcutButResetsLiveEnemies()
        {
            Place(BellCrossingLayout.RestPoint);Assert.IsTrue(level.Interact());TakeSeals();Place(BellCrossingLayout.LeverPoint);level.Interact();
            level.Enemies[1].Core.RestoreVitals(32,44);level.Player.Core.RestoreVitals(0,0);level.Respawn();
            Assert.IsTrue(level.CheckpointLit&&level.WestSeal&&level.EastSeal&&level.ShortcutOpen);
            Assert.Less(Vector3.Distance(level.Player.transform.position,BellCrossingLayout.Center(CrossingRoom.Hearth)),.1f);
            Assert.AreEqual(level.Enemies[1].Core.Tuning.maxHealth,level.Enemies[1].Core.Health);Assert.AreEqual(CombatState.Dead,level.Enemies[2].Core.State);
            Assert.AreEqual(level.Player.Core.Tuning.maxHealth,level.Player.Core.Health);yield return null;
        }
        [UnityTest]public IEnumerator ArenaSealsOnEntryAndReopensAfterDefeat()
        {
            Assert.IsFalse(level.Enemies[7].AcceptsDamage);TakeSeals();Place(BellCrossingLayout.Center(CrossingRoom.Guardian));level.Simulate(.02f);
            Assert.IsTrue(level.BossActive);Assert.IsTrue(level.Enemies[7].AcceptsDamage);Assert.IsNotNull(GameObject.Find("Arena entrance"));
            level.Enemies[7].Core.RestoreVitals(12,50);level.Respawn();Assert.IsFalse(level.BossActive);Assert.IsNull(GameObject.Find("Arena entrance"));Assert.AreEqual(level.Enemies[7].Core.Tuning.maxHealth,level.Enemies[7].Core.Health);
            Place(BellCrossingLayout.Center(CrossingRoom.Guardian));level.Simulate(.02f);Defeat(CrossingRoom.Guardian);level.Simulate(.02f);
            Assert.IsFalse(level.BossActive);Assert.IsNull(GameObject.Find("Arena exit"));yield return null;
        }
        [UnityTest]public IEnumerator FinaleRequiresBossAndExplicitBellInteractionAndRestartClearsAll()
        {
            Place(BellCrossingLayout.BellPoint);Assert.IsFalse(level.Interact());Assert.IsFalse(level.Completed);
            TakeSeals();Defeat(CrossingRoom.Guardian);Place(BellCrossingLayout.BellPoint);level.Simulate(.02f);Assert.IsFalse(level.Completed);Assert.IsTrue(level.Interact());Assert.IsTrue(level.Completed);
            level.Restart();Assert.IsFalse(level.Completed||level.WestSeal||level.EastSeal||level.CheckpointLit);Assert.AreEqual(3,level.Medicines);Assert.AreEqual(level.bossDefinition.tuning.maxHealth,level.Enemies[7].Core.Health);yield return null;
        }
        [UnityTest]public IEnumerator PauseMapAndCompletionDoNotConsumeHealingOrLatentInput()
        {
            level.Player.Core.RestoreVitals(50,0);int before=level.Medicines;
            level.SetPaused(true);Assert.IsFalse(level.UseMedicine());Assert.IsFalse(level.Interact());level.Simulate(1);Assert.AreEqual(50,level.Player.Core.Health);
            level.SetPaused(false);level.SetMap(true);Assert.IsFalse(level.UseMedicine());level.SubmitInput(new CombatInputFrame{attackHeld=true,attackPressed=true});
            level.SetMap(false);level.SubmitInput(new CombatInputFrame{attackHeld=true,attackPressed=true});Assert.IsFalse(level.Player.Core.IsPreparing);
            Assert.AreEqual(before,level.Medicines);Assert.IsTrue(level.UseMedicine());Assert.AreEqual(90,level.Player.Core.Health);yield return null;
        }
        [UnityTest]public IEnumerator OfferingIsOptionalAndCanBeCollectedOnlyOnce()
        {
            Place(BellCrossingLayout.ShrinePoint);Assert.IsFalse(level.Interact());Defeat(CrossingRoom.Shrine);Assert.IsTrue(level.Interact());Assert.AreEqual(5,level.Medicines);Assert.IsFalse(level.Interact());Assert.IsTrue(level.OfferingTaken);yield return null;
        }
        [UnityTest]public IEnumerator RoomEnemiesEngageLocallyAndReturnWithoutCrossRoomPursuit()
        {
            var home=BellCrossingLayout.Home(1);Place(home+Vector3.back*4);level.Simulate(.4f);Assert.Greater(Vector3.Distance(home,level.Enemies[1].transform.position),.1f);
            Place(BellCrossingLayout.Center(CrossingRoom.Hearth));level.Simulate(3);Assert.Less(Vector3.Distance(home,level.Enemies[1].transform.position),.2f);yield return null;
        }
        [UnityTest]public IEnumerator RealAttackDamagesPlayerAndPlayerAttackDamagesEnemy()
        {
            var enemy=level.Enemies[0];Place(enemy.transform.position+Vector3.back*1.15f);enemy.FaceTarget();level.Player.transform.rotation=Quaternion.identity;
            enemy.Core.SetGuard(false,false);Assert.IsTrue(enemy.Core.RequestAttack());for(int i=0;i<150;i++){enemy.Core.Tick(1f/120);level.Player.Core.Tick(1f/120);}
            Assert.Less(level.Player.Core.Health,level.Player.Core.Tuning.maxHealth);
            level.Player.Core.Reset();enemy.Core.Reset();enemy.Core.SetGuard(false,false);level.Player.Target=enemy;level.Player.FaceTarget();Assert.IsTrue(level.Player.RequestPlayerAttack());
            for(int i=0;i<150;i++){level.Player.Core.Tick(1f/120);enemy.Core.Tick(1f/120);}
            Assert.Less(enemy.Core.Health,enemy.Core.Tuning.maxHealth);yield return null;
        }
        [UnityTest]public IEnumerator NoMainlineWorldOrDiskStoreExistsInStandaloneScene()
        {
            Assert.IsNull(Object.FindAnyObjectByType<MvpWorld>());Assert.IsNull(Object.FindAnyObjectByType<GameFlowController>());
            Assert.AreEqual(8,level.Enemies.Length);Assert.AreEqual(16,BellCrossingLayout.Cells.Length);yield return null;
        }
        [UnityTest]public IEnumerator ActualUpdateAutomaticallyRecoversFromDeathAndFalling()
        {
            Place(BellCrossingLayout.RestPoint);level.Interact();TakeSeals();
            for(int cause=0;cause<2;cause++)
            {
                level.SetPaused(false);
                if(cause==0)level.Player.Core.RestoreVitals(0,0);else Place(Vector3.down*6);
                level.manualSimulation=false;float deadline=Time.realtimeSinceStartup+7;
                while(level.Deaths<=cause&&Time.realtimeSinceStartup<deadline)yield return null;
                level.manualSimulation=true;Assert.AreEqual(cause+1,level.Deaths);
                Assert.IsTrue(level.WestSeal&&level.EastSeal);Assert.AreEqual(level.Player.Core.Tuning.maxHealth,level.Player.Core.Health);
                Assert.Less(Vector3.Distance(level.Player.transform.position,BellCrossingLayout.Center(CrossingRoom.Hearth)),.2f);
            }
        }
    }
}
