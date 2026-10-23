using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;

namespace Milkfrog.CombatDemo.Tests
{
    public class BullSlamBalanceTests
    {
        MvpWorld world;
        BullBossEnemy boss;
        BullBossDefinition localDefinition;
        BullMove Slam => boss.bull.Get(BullMoveKind.Slam);
        [UnitySetUp] public IEnumerator Load()
        {
            BullTrialSession.Begin(null,false);
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/BullTrial.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();world.manualSimulation=true;
            world.feedback.hitStop=false;world.feedback.cameraImpulse=false;boss=(BullBossEnemy)world.enemies[0];
            localDefinition=UnityEngine.Object.Instantiate(boss.bull);boss.bull=localDefinition;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("SlamCleanup-"+Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(old);BullTrialSession.Clear();UnityEngine.Object.Destroy(localDefinition);
        }
        void Place(Vector3 point)
        { world.player.Motor.enabled=false;world.player.transform.position=point;world.player.Motor.enabled=true;Physics.SyncTransforms(); }
        void Prepare(float distance=5)
        {
            world.Restore(BullTrialSession.Fresh());Place(boss.transform.position+Vector3.back*distance);world.Director.Tick(.001f);
            Assert.That(boss.TryStartMove(BullMoveKind.Slam),Is.True);
        }
        void Lock()
        {
            world.Simulate(Slam.attack.startup-Slam.lockBeforeImpact+.001f);
            Assert.That(boss.TargetLocked,Is.True);
            Assert.That(boss.Actor.Core.Remaining,Is.EqualTo(Slam.lockBeforeImpact-.001f).Within(.01f));
        }
        IEnumerator SideDodge(float distance)
        {
            foreach(float delay in new[]{0f,.2f})
            foreach(float sign in new[]{-1f,1f})
            {
                Prepare(distance);Lock();var center=boss.LockedPoint;world.Simulate(delay);
                Assert.That(world.player.RequestDodge(Vector3.right*sign),Is.True);
                world.Simulate(world.player.Core.Tuning.invulnerableEnd+.03f);
                Assert.That(world.player.Core.IsInvulnerable,Is.False);
                Assert.That(boss.Actor.Core.State,Is.EqualTo(CombatState.AttackStartup));
                world.Simulate(Slam.lockBeforeImpact+Slam.attack.active);
                Assert.That(world.player.Core.State,Is.EqualTo(CombatState.Neutral));
                Assert.That(world.player.Core.Health,Is.EqualTo(100),$"distance={distance}, side={sign}, delay={delay}");
                Assert.That(world.player.Core.Posture,Is.Zero);Assert.That(boss.LockedPoint,Is.EqualTo(center));
                Assert.That(EnemyController.FlatDistance(world.player.transform.position,center),Is.GreaterThan(Slam.range+world.player.Motor.radius));
                yield return null;
            }
        }
        [UnityTest] public IEnumerator RealSideDodgeClearsCircularSlamAtThreeMeters() => SideDodge(3);
        [UnityTest] public IEnumerator RealSideDodgeClearsCircularSlamAtFiveMeters() => SideDodge(5);
        [UnityTest] public IEnumerator RealSideDodgeClearsCircularSlamAtEightMeters() => SideDodge(8);
        [UnityTest] public IEnumerator OriginalSlamRadiusPunishesOneFullSideDodge()
        {
            Slam.attack.startup=1.1f;Slam.lockBeforeImpact=.4f;Slam.range=3;Slam.attack.damage=Slam.attack.maxDamage=28;
            Prepare();Lock();var center=boss.LockedPoint;
            Assert.That(world.player.RequestDodge(Vector3.right),Is.True);
            world.Simulate(Slam.lockBeforeImpact+Slam.attack.active+.2f);
            Assert.That(world.player.Core.IsInvulnerable,Is.False);
            Assert.That(world.player.Core.Health,Is.EqualTo(72));
            Assert.That(EnemyController.FlatDistance(world.player.transform.position,center),Is.LessThan(Slam.range+world.player.Motor.radius));
            yield return null;
        }
        [UnityTest] public IEnumerator LandingTracksEarlyThenFreezesThroughImpact()
        {
            Prepare();world.Simulate(.2f);Place(world.player.transform.position+Vector3.right*.8f);world.Simulate(.1f);
            Assert.That(EnemyController.FlatDistance(boss.LockedPoint,world.player.transform.position),Is.LessThan(.01f));
            world.Simulate(Slam.attack.startup-Slam.lockBeforeImpact-.3f+.001f);
            Assert.That(boss.TargetLocked,Is.True);var center=boss.LockedPoint;
            Place(world.player.transform.position+Vector3.right*5);world.Simulate(Slam.lockBeforeImpact+Slam.attack.active);
            Assert.That(boss.LockedPoint,Is.EqualTo(center));Assert.That(world.player.Core.Health,Is.EqualTo(100));yield return null;
        }
        [UnityTest] public IEnumerator WarningRadiusMatchesCapsuleBoundaryAndOnlyOneHit()
        {
            foreach(float offset in new[]{-.05f,.05f})
            {
                Prepare();Lock();var center=boss.LockedPoint;
                var line=boss.presentation.GetComponentsInChildren<LineRenderer>()[0];
                Assert.That(line.enabled,Is.True);
                for(int i=0;i<line.positionCount;i++)
                    Assert.That(EnemyController.FlatDistance(line.GetPosition(i),center),Is.EqualTo(Slam.range).Within(.01f));
                Place(center+Vector3.right*(Slam.range+world.player.Motor.radius+offset));
                world.Simulate(Slam.lockBeforeImpact+Slam.attack.active+.01f);
                Assert.That(world.player.Core.Health,Is.EqualTo(offset<0?78:100));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator PauseInventoryAndHitstopFreezeLockedSlam()
        {
            Prepare();Lock();float remaining=boss.Actor.Core.Remaining;var center=boss.LockedPoint;
            world.Flow.OpenInventory();world.Simulate(.5f);
            Assert.That(boss.Actor.Core.Remaining,Is.EqualTo(remaining));world.Flow.CloseInventory();
            world.Flow.TogglePause();world.Simulate(.5f);
            Assert.That(boss.Actor.Core.Remaining,Is.EqualTo(remaining));world.Flow.TogglePause();
            world.feedback.Clock.Request(.2f);world.Simulate(.1f);
            Assert.That(boss.Actor.Core.Remaining,Is.EqualTo(remaining));Assert.That(boss.LockedPoint,Is.EqualTo(center));yield return null;
        }
        [UnityTest] public IEnumerator BrokenSlamCancelsWarningAndPendingDamage()
        {
            Prepare();Lock();Assert.That(boss.presentation.TelegraphVisible,Is.True);
            var attacker=new CombatCore(new CombatTuning());
            attacker.RequestDefinedAttack(new AttackParameters{startup=.01f,posture=400,maxPosture=400});attacker.Tick(.02f);attacker.TryHit(boss.Actor.Core,true);
            Assert.That(boss.Actor.Core.State,Is.EqualTo(CombatState.PostureBroken));Assert.That(boss.CurrentMove,Is.Null);
            Assert.That(boss.presentation.TelegraphVisible,Is.False);world.Simulate(1.1f);
            Assert.That(world.player.Core.Health,Is.EqualTo(100));yield return null;
        }
        [UnityTest] public IEnumerator SlamBypassesGuardButRespectsArmorAndDodgeInvulnerability()
        {
            Prepare();Lock();world.player.Core.ApplyEquipment(1,.2f,0);world.player.Core.SetGuard(true,true);
            world.Simulate(Slam.lockBeforeImpact+Slam.attack.active+.01f);
            Assert.That(world.player.Core.Health,Is.EqualTo(82.4f).Within(.001f));
            Prepare();Lock();world.player.Core.RequestDodge();world.player.Core.Tick(.1f);
            Assert.That(world.player.Core.IsInvulnerable,Is.True);
            boss.Actor.Core.Tick(Slam.lockBeforeImpact+Slam.attack.active);
            Assert.That(world.player.Core.Health,Is.EqualTo(100));yield return null;
        }
    }
}
