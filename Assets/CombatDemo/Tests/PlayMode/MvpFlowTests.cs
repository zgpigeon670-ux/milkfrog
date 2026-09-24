using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Milkfrog.CombatDemo.Tests
{
    public class MvpFlowTests
    {
        MvpWorld world;
        string directory;
        [UnitySetUp] public IEnumerator Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "MilkfrogMvpTest-" + Guid.NewGuid().ToString("N"));
            GameFlowController.SaveDirectoryOverride = directory;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/MVP_TestLevel.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("MVP_TestLevel");
#endif
            yield return null;
            world = UnityEngine.Object.FindAnyObjectByType<MvpWorld>(); world.manualSimulation = true;
            if (world.Flow.Paused) world.Flow.TogglePause();
            world.feedback.hitStop = false;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var scene = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("MvpTestCleanup-" + Guid.NewGuid()); SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(scene);
            GameFlowController.SaveDirectoryOverride = null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        void Advance(float seconds, int fps = 60)
        { while (seconds > .000001f) { float dt = Mathf.Min(seconds, 1f / fps); world.Simulate(dt); seconds -= dt; } }
        static void Place(CombatActor actor, Vector3 position)
        { actor.Motor.enabled = false; actor.transform.position = position; actor.Motor.enabled = true; Physics.SyncTransforms(); }

        static AttackPattern PatternFor(AttackKind kind)
        {
            switch (kind)
            {
                case AttackKind.Slow: return AttackPattern.Slow;
                case AttackKind.Perilous: return AttackPattern.Perilous;
                default: return AttackPattern.Slash;
            }
        }

        static AttackPattern[] CollectBossPatterns(EnemyController boss, int seed, int count)
        {
            var result = new AttackPattern[count];
            boss.Brain.Reset(EnemyMode.Duel);
            boss.Actor.Core.Reset();
            boss.Brain.SetRandomSeed(seed);
            for (int i = 0; i < count; i++)
            {
                boss.Brain.Tick(boss.definition.wait + .01f);
                result[i] = PatternFor(boss.Actor.Core.ActiveAttack.Kind);
                Assert.That(boss.Brain.NextPattern, Is.EqualTo(result[i]), "The chosen pattern should describe the committed attack.");
                boss.Actor.Core.Reset();
            }
            return result;
        }

        static int FindFirstBossPatternSeed(EnemyController boss, AttackPattern pattern)
        {
            for (int seed = 1; seed <= 128; seed++)
            {
                boss.Brain.Reset(EnemyMode.Duel);
                boss.Actor.Core.Reset();
                boss.Brain.SetRandomSeed(seed);
                boss.Brain.Tick(boss.definition.wait + .01f);
                if (PatternFor(boss.Actor.Core.ActiveAttack.Kind) == pattern) return seed;
            }
            Assert.Fail("No fixed seed selected " + pattern + " within the expected sample range.");
            return -1;
        }

        [UnityTest] public IEnumerator FreshStartHasThreeMobsBossAndSafeSave()
        {
            Assert.That(world.enemies.Length, Is.EqualTo(4));
            Assert.That(world.Director.State, Is.EqualTo(EncounterState.Exploration)); Assert.That(world.Lock.Target, Is.Null);
            Assert.That(world.player.Core.State, Is.EqualTo(CombatState.Neutral));
            Assert.That(world.Flow.Store.TryLoad(out var saved), Is.True); Assert.That(saved.health, Is.EqualTo(100));
            Assert.That(world.enemies[3].Actor.Core.Health, Is.EqualTo(750));
            Assert.That(world.enemies[3].Actor.Core.Tuning.maxPosture, Is.EqualTo(350));
            Assert.That(world.enemies[0].Actor.Core.ActiveAttack.Damage, Is.EqualTo(10));
            yield return null;
        }
        [UnityTest] public IEnumerator RecoveryIsFrameRateIndependentAndPauseFreezesIt()
        {
            foreach (int fps in new[] { 30, 60, 120 })
            {
                world.player.Core.RestoreVitals(50, 80); Advance(1, fps);
                Assert.That(world.player.Core.Health, Is.EqualTo(55).Within(.01));
                Assert.That(world.player.Core.Posture, Is.EqualTo(60).Within(.01));
            }
            world.Flow.TogglePause(); float health = world.player.Core.Health; Advance(3);
            Assert.That(world.player.Core.Health, Is.EqualTo(health)); world.Flow.TogglePause();
            world.player.Core.RestoreVitals(99, 1); Advance(1);
            Assert.That(world.player.Core.Health, Is.EqualTo(100)); Assert.That(world.player.Core.Posture, Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator MultipleParticipantsAndSingleDeathDoNotStopWorld()
        {
            Place(world.player, new Vector3(0,.02f,-8));
            world.Director.Engage(world.enemies[0]); world.Director.Engage(world.enemies[1]);
            Assert.That(world.Director.Count, Is.EqualTo(2));
            world.enemies[0].Actor.Core.RestoreVitals(0,0); Advance(.1f);
            Assert.That(world.Director.Count, Is.EqualTo(1)); Assert.That(world.Flow.State, Is.EqualTo(GameFlowState.Playing));
            Assert.That(world.Defeated.Contains("mob-1"), Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator TimeoutRearmsOnlyAfterLeavingAndReturning()
        {
            var enemy = world.enemies[0]; Place(world.player, enemy.Home + Vector3.back * 4);
            world.Director.Tick(.001f); Assert.That(world.Director.Count, Is.EqualTo(1));
            world.Director.Tick(9.998f); Assert.That(world.Director.State, Is.EqualTo(EncounterState.NormalCombat));
            world.Director.Tick(.001f); Assert.That(world.Director.State, Is.EqualTo(EncounterState.Exploration));
            Assert.That(enemy.Activity, Is.EqualTo(EnemyActivity.Returning));
            world.Director.Tick(.1f); Assert.That(world.Director.Count, Is.Zero);
            enemy.Tick(.1f); // Already at home -> patrol, still suppressed.
            world.Director.Tick(.1f); Assert.That(world.Director.Count, Is.Zero);
            Place(world.player, enemy.Home + Vector3.back * 8); world.Director.Tick(.1f);
            Place(world.player, enemy.Home + Vector3.back * 4); world.Director.Tick(.1f);
            Assert.That(world.Director.Count, Is.EqualTo(1));
            yield return null;
        }
        [UnityTest] public IEnumerator InteractionResetsTimerAndLeashCancelsAttack()
        {
            var enemy = world.enemies[0]; Place(world.player, enemy.Home + Vector3.back * 4); world.Director.Engage(enemy);
            world.Director.Tick(9);
            world.Director.OnInteraction(new HitEvent(world.player.Core,enemy.Actor.Core,HitResult.Block));
            Assert.That(world.Director.Remaining, Is.EqualTo(10)); world.Director.Tick(2);
            Assert.That(world.Director.State, Is.EqualTo(EncounterState.NormalCombat));
            enemy.Actor.Core.RequestAttack(); Place(world.player, enemy.Home+Vector3.back*20); world.Director.Tick(.01f);
            Assert.That(world.Director.State, Is.EqualTo(EncounterState.Exploration)); Assert.That(enemy.Actor.AcceptsDamage, Is.False);
            Assert.That(enemy.Actor.Core.IsAttacking, Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator FactionsRejectEnemyDamageAndHitFeedbackIsDeliveredOnce()
        {
            var a = world.enemies[0].Actor; var b = world.enemies[1].Actor;
            Place(a, new Vector3(-10,.02f,-15)); Place(b,a.transform.position+Vector3.forward*.9f);
            a.transform.rotation = Quaternion.identity; a.Core.RequestAttack(); a.Core.Tick(.7f);
            Assert.That(b.Core.Health, Is.EqualTo(100));
            a.Core.Reset(); Place(b,new Vector3(10,.02f,-2)); Place(world.player,a.transform.position+Vector3.forward*.9f);
            int before = world.ContactCount; a.Core.RequestAttack(); a.Core.Tick(.7f);
            Assert.That(world.player.Core.Health, Is.LessThan(100)); Assert.That(world.ContactCount-before, Is.EqualTo(1));
            yield return null;
        }
        [UnityTest] public IEnumerator LockSelectsScreenCenterAndUnlocksWithoutEndingCombat()
        {
            Place(world.player,new Vector3(0,.02f,-35));
            Place(world.enemies[0].Actor,new Vector3(0,.02f,-25)); Place(world.enemies[1].Actor,new Vector3(4,.02f,-25));
            world.gameplayCamera.transform.SetPositionAndRotation(new Vector3(0,1.2f,-36),Quaternion.identity);
            world.Lock.Toggle(); Assert.That(world.Lock.Target, Is.EqualTo(world.enemies[0]));
            Assert.That(world.Director.State, Is.EqualTo(EncounterState.Exploration));
            world.Lock.Toggle(); Assert.That(world.Lock.Target, Is.Null);
            world.Lock.Force(world.enemies[1]); world.enemies[1].Actor.Core.RestoreVitals(0,0); world.Lock.Tick(.01f);
            Assert.That(world.Lock.Target, Is.Null);
            world.cameraRig.Step(.1f); Assert.That(float.IsNaN(world.gameplayCamera.transform.position.x), Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator SaveRestoresResourcesAndDefeatedEnemiesButClearsTransientActions()
        {
            var snapshot = new PlayerSnapshot { position = new Vector3(2,.02f,-32), yaw = 75, health = 63, posture = 32, defeatedEnemyIds = new[] { "mob-2" } };
            world.player.Core.BeginPreparation(); world.player.Core.Tick(.5f); world.Restore(snapshot);
            Assert.That(world.player.Core.Health, Is.EqualTo(63)); Assert.That(world.player.Core.Posture, Is.EqualTo(32));
            Assert.That(world.player.Core.IsPreparing, Is.False); Assert.That(world.Lock.Target, Is.Null);
            Assert.That(world.enemies[1].gameObject.activeSelf, Is.False);
            Assert.That(world.Flow.Save(), Is.True); Assert.That(world.Flow.Store.TryLoad(out var loaded), Is.True);
            Assert.That(loaded.position.x, Is.EqualTo(2)); Assert.That(loaded.defeatedEnemyIds, Does.Contain("mob-2"));
            world.Restore(new PlayerSnapshot { position = new Vector3(1000,0,0) });
            Assert.That(world.player.transform.position, Is.EqualTo(world.spawn));
            yield return null;
        }
        [UnityTest] public IEnumerator BossForcesLockCannotDisengageAndVictoryPersists()
        {
            var boss = world.enemies[3]; Place(world.player,boss.Home+Vector3.back*7);
            world.Director.Tick(.01f); Assert.That(world.Director.State, Is.EqualTo(EncounterState.BossCombat));
            Assert.That(world.Lock.Target, Is.EqualTo(boss)); Assert.That(world.arena.activeSelf, Is.True);
            world.Lock.Toggle(); world.Director.Tick(20); Assert.That(world.Lock.Target, Is.EqualTo(boss));
            Assert.That(world.Flow.Save(), Is.False);
            boss.Actor.Core.RestoreVitals(0,0); Advance(.01f);
            Assert.That(world.Flow.State, Is.EqualTo(GameFlowState.Victory)); Assert.That(world.arena.activeSelf, Is.False);
            Assert.That(world.Lock.Target, Is.Null); Assert.That(world.Flow.Store.TryLoad(out var saved), Is.True);
            Assert.That(saved.bossDefeated, Is.True); Assert.That(saved.defeatedEnemyIds, Does.Contain("boss-guardian"));
            world.Flow.ContinueExploring(); Assert.That(world.Flow.State, Is.EqualTo(GameFlowState.Playing));
            yield return null;
        }
        [UnityTest] public IEnumerator PlayerDeathKeepsSafeSaveAndRetryLoadsIt()
        {
            world.player.Core.RestoreVitals(70,20); Assert.That(world.Flow.Save(), Is.True);
            world.player.Core.RestoreVitals(0,0); Advance(.01f);
            Assert.That(world.Flow.State, Is.EqualTo(GameFlowState.PlayerDead));
            Assert.That(world.Flow.Store.TryLoad(out var saved), Is.True); Assert.That(saved.health, Is.EqualTo(70));
            world.Flow.Retry();
            for(int i=0;i<120;i++) { yield return null; var next=UnityEngine.Object.FindAnyObjectByType<MvpWorld>(); if(next!=null && next!=world && next.Flow.State==GameFlowState.Playing){world=next;break;} }
            world.manualSimulation=true;
            Assert.That(world.Flow.State,Is.EqualTo(GameFlowState.Playing)); Assert.That(world.player.Core.Health,Is.EqualTo(70).Within(.2));
            Assert.That(UnityEngine.Object.FindObjectsByType<MvpWorld>().Length,Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator MiddleMouseBindingAndFreezeReleaseDoNotQueueAttack()
        {
            var policy=InputSystem.settings;
            var previousBackground=policy.backgroundBehavior;
            policy.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            var previousEditorPolicy=policy.editorInputBehaviorInPlayMode;
            policy.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                using (var input = new DemoInput("<Mouse>/middleButton"))
                {
                    InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Middle));
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); yield return null;
                    Assert.That(input.Snapshot.lockPressed,Is.True);
                    Assert.That(input.PausePressed,Is.True);
                }
            }
            finally
            {
                InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard); policy.backgroundBehavior=previousBackground;
#if UNITY_EDITOR
                policy.editorInputBehaviorInPlayMode=previousEditorPolicy;
#endif
            }
            world.ClearInput(); world.SubmitInput(new CombatInputFrame());
            world.SubmitInput(new CombatInputFrame {attackPressed=true,attackHeld=true}); Advance(.4f);
            world.feedback.Clock.Request(.07f); world.SubmitInput(new CombatInputFrame {attackReleased=true}); Advance(.2f);
            Assert.That(world.player.Core.IsPreparing,Is.False); Assert.That(world.player.Core.AttackId,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator OcclusionDiscardsCandidatesAndBreaksLockAfterOneSecond()
        {
            var enemy=world.enemies[0]; Place(world.player,new Vector3(0,.02f,-35)); Place(enemy.Actor,new Vector3(0,.02f,-25));
            Place(world.enemies[1].Actor,new Vector3(35,.02f,0)); Place(world.enemies[2].Actor,new Vector3(-35,.02f,0));
            world.gameplayCamera.transform.SetPositionAndRotation(new Vector3(0,1.2f,-36),Quaternion.identity);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position=new Vector3(0,1.5f,-30);wall.transform.localScale=new Vector3(5,3,.3f); Physics.SyncTransforms();
            world.Lock.Toggle(); Assert.That(world.Lock.Target,Is.Null);
            world.Lock.Force(enemy); world.Lock.Tick(.99f); Assert.That(world.Lock.Target,Is.EqualTo(enemy));
            world.Lock.Tick(.02f); Assert.That(world.Lock.Target,Is.Null);
            UnityEngine.Object.Destroy(wall); yield return null;
        }
        [UnityTest] public IEnumerator BossBoundaryBlocksEscapeAndSimultaneousDeathIsFailure()
        {
            var boss=world.enemies[3]; Place(world.player,boss.Home+Vector3.back*7);world.Director.Tick(.01f);
            Place(world.player,boss.Home+Vector3.right*12); Physics.SyncTransforms();
            world.player.Move(Vector3.right,10,1); Assert.That(world.player.transform.position.x-boss.Home.x,Is.LessThan(14));
            boss.Actor.Core.RestoreVitals(0,0); world.player.Core.RestoreVitals(0,0); Advance(.01f);
            Assert.That(world.Flow.State,Is.EqualTo(GameFlowState.PlayerDead));Assert.That(world.BossDefeated,Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator TimeoutAndFlowAreConsistentAcrossThirtySixtyAndOneTwenty()
        {
            foreach(int fps in new[]{30,60,120})
            {
                world.Restore(new PlayerSnapshot{position=world.enemies[0].Home+Vector3.back*4});
                world.Director.Engage(world.enemies[0]);
                for(int i=0;i<10*fps-1;i++)world.Director.Tick(1f/fps);
                Assert.That(world.Director.State,Is.EqualTo(EncounterState.NormalCombat));
                world.Director.Tick(1f/fps+.0001f);
                Assert.That(world.Director.State,Is.EqualTo(EncounterState.Exploration));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator MobLockDoesNotSteerMovementOrCameraButAimsCommittedAttack()
        {
            var mob = world.enemies[0];
            Place(world.player, mob.Home + Vector3.back * 1.4f);
            world.player.transform.rotation = Quaternion.LookRotation(Vector3.right);
            float beforeYaw = world.cameraRig.yaw;
            world.Lock.Force(mob);
            Assert.That(world.cameraRig.lockOn, Is.False);
            Assert.That(Vector3.Angle(world.player.transform.forward, Vector3.right), Is.LessThan(.1f));
            world.cameraRig.Look(new Vector2(20, 0));
            Assert.That(world.cameraRig.yaw, Is.GreaterThan(beforeYaw));
            world.Simulate(.02f);
            Assert.That(Vector3.Angle(world.player.transform.forward, Vector3.right), Is.LessThan(.1f));
            world.SubmitInput(new CombatInputFrame { attackPressed = true, attackHeld = true });
            Assert.That(world.player.Core.IsPreparing, Is.True);
            Assert.That(Vector3.Angle(world.player.transform.forward,
                Vector3.ProjectOnPlane(mob.transform.position - world.player.transform.position, Vector3.up)), Is.LessThan(.1f));
            yield return null;
        }
        [UnityTest] public IEnumerator AttackSelectionUsesLockThenReticleThenNearestThenCameraRay()
        {
            Place(world.player, new Vector3(0, .02f, -30));
            var reticle = world.enemies[0]; var near = world.enemies[1];
            Place(reticle.Actor, world.player.transform.position + Vector3.forward * 1.8f);
            Place(near.Actor, world.player.transform.position + new Vector3(1.1f, 0, .2f));
            Place(world.enemies[2].Actor, new Vector3(-30, .02f, 0));
            world.gameplayCamera.transform.SetPositionAndRotation(world.player.transform.position + new Vector3(0, 1.2f, -4), Quaternion.identity);
            Assert.That(world.SelectAttackTarget(), Is.EqualTo(reticle.Actor));
            world.Lock.Force(near); Assert.That(world.SelectAttackTarget(), Is.EqualTo(near.Actor));
            world.Lock.Clear();
            world.gameplayCamera.transform.rotation = Quaternion.LookRotation(Vector3.back);
            Assert.That(world.SelectAttackTarget(), Is.EqualTo(near.Actor));
            Place(reticle.Actor, new Vector3(-25, .02f, 0)); Place(near.Actor, new Vector3(25, .02f, 0));
            world.gameplayCamera.transform.rotation = Quaternion.LookRotation(Vector3.right);
            Assert.That(world.SelectAttackTarget(), Is.Null);
            world.SubmitInput(new CombatInputFrame { attackPressed = true, attackHeld = true });
            Assert.That(Vector3.Angle(world.player.transform.forward, Vector3.right), Is.LessThan(.1f));
            yield return null;
        }
        [UnityTest] public IEnumerator SideLockedSlashUsesTurnedBladeAndActuallyConnects()
        {
            var mob = world.enemies[0];
            Place(mob.Actor, mob.Home);
            Place(world.player, mob.Home + Vector3.right * .9f);
            world.player.transform.rotation = Quaternion.LookRotation(Vector3.back);
            world.Lock.Force(mob);
            int contacts = world.ContactCount;
            world.SubmitInput(new CombatInputFrame { attackPressed = true, attackHeld = true });
            world.SubmitInput(new CombatInputFrame { attackReleased = true });
            Assert.That(world.player.Core.State, Is.EqualTo(CombatState.AttackStartup));
            Assert.That(Vector3.Angle(world.player.AttackForward, Vector3.left), Is.LessThan(.1f));
            world.player.Core.Tick(.35f);
            Assert.That(world.ContactCount, Is.EqualTo(contacts + 1));
            Assert.That(mob.Actor.Core.Health, Is.LessThan(mob.Actor.Core.Tuning.maxHealth));
            yield return null;
        }
        [UnityTest] public IEnumerator ChargeReleaseReaimsAndKeepsBladeForwardAligned()
        {
            var mob = world.enemies[0];
            Place(world.player, mob.Home + Vector3.back * 1.5f);
            world.Lock.Force(mob);
            world.SubmitInput(new CombatInputFrame { attackPressed = true, attackHeld = true });
            Advance(.3f);
            Assert.That(world.player.Core.IsPreparing, Is.True);
            Place(mob.Actor, world.player.transform.position + Vector3.right * 1.5f);
            world.SubmitInput(new CombatInputFrame { attackReleased = true });
            Assert.That(world.player.Core.State, Is.EqualTo(CombatState.AttackStartup));
            Assert.That(world.player.Core.ActiveAttack.Kind, Is.EqualTo(AttackKind.Thrust));
            Assert.That(Vector3.Angle(world.player.AttackForward, Vector3.right), Is.LessThan(.1f));
            Assert.That(Vector3.Angle(world.player.transform.forward, world.player.AttackForward), Is.LessThan(.1f));
            yield return null;
        }
        [UnityTest] public IEnumerator FollowupAndExecutionUseSelectedDirection()
        {
            var mob = world.enemies[0];
            Place(world.player, new Vector3(0, .02f, -35));
            world.player.Core.RequestAttack(); world.player.Core.Tick(.7f);
            Assert.That(world.player.Core.ComboOpen, Is.True);
            Place(mob.Actor, world.player.transform.position + Vector3.back * 1.2f);
            world.player.transform.rotation = Quaternion.LookRotation(Vector3.right);
            world.Lock.Force(mob);
            world.SubmitInput(new CombatInputFrame { attackPressed = true, attackHeld = true });
            Assert.That(world.player.Core.ActiveAttack.Kind, Is.EqualTo(AttackKind.Followup));
            Assert.That(Vector3.Angle(world.player.AttackForward, Vector3.back), Is.LessThan(.1f));
            world.player.Core.Reset(); mob.Actor.Core.RestoreVitals(100, 95);
            world.player.Core.RequestAttack(); world.player.Core.Tick(.25f);
            world.player.Core.TryHit(mob.Actor.Core, true);
            Assert.That(mob.Actor.Core.State, Is.EqualTo(CombatState.PostureBroken));
            world.player.Core.Reset(); world.player.transform.rotation = Quaternion.LookRotation(Vector3.right);
            world.SubmitInput(new CombatInputFrame { attackReleased = true });
            world.SubmitInput(new CombatInputFrame { attackPressed = true, attackHeld = true });
            Assert.That(mob.Actor.Core.State, Is.EqualTo(CombatState.Dead));
            Assert.That(Vector3.Angle(world.player.transform.forward, Vector3.back), Is.LessThan(.1f));
            yield return null;
        }
        [UnityTest] public IEnumerator GuardTurnsTowardEachMobButBreakAndPerilousStillHit()
        {
            var a = world.enemies[0].Actor; var b = world.enemies[1].Actor;
            Place(world.player, new Vector3(0, .02f, -28));
            Place(a, world.player.transform.position + Vector3.back * .9f);
            Place(b, world.player.transform.position + Vector3.forward * .9f);
            a.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            b.transform.rotation = Quaternion.LookRotation(Vector3.back);
            world.player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            world.player.Core.SetGuard(true, false);
            a.Core.RequestAttack(); a.Core.Tick(.7f);
            Assert.That(world.player.Core.Health, Is.EqualTo(100));
            Assert.That(world.player.Core.Posture, Is.EqualTo(20));
            Assert.That(Vector3.Angle(world.player.transform.forward, Vector3.back), Is.LessThan(.1f));
            b.Core.RequestAttack(); b.Core.Tick(.7f);
            Assert.That(world.player.Core.Health, Is.EqualTo(100));
            Assert.That(world.player.Core.Posture, Is.EqualTo(40));
            Assert.That(Vector3.Angle(world.player.transform.forward, Vector3.forward), Is.LessThan(.1f));
            world.player.Core.RestoreVitals(100, 95); world.player.Core.SetGuard(true, false);
            a.Core.Reset(); a.Core.RequestAttack(); a.Core.Tick(.7f);
            Assert.That(world.player.Core.State, Is.EqualTo(CombatState.PostureBroken));
            b.Core.Reset(); b.Core.RequestAttack(); b.Core.Tick(.7f);
            Assert.That(world.player.Core.Health, Is.LessThan(100));
            yield return null;
        }
        [UnityTest] public IEnumerator BossUsesReproducibleWeightedAttacksAndWarningTracksPerilousStartup()
        {
            var boss = world.enemies[3];
            Place(world.player, boss.Home + Vector3.back * 1f);
            world.Director.Tick(.01f);
            Assert.That(world.Lock.Target, Is.EqualTo(boss));
            Assert.That(world.cameraRig.lockOn, Is.True);
            Assert.That(boss.definition.randomAttacks, Is.True);
            Assert.That(boss.definition.slashWeight, Is.EqualTo(5));
            Assert.That(boss.definition.slowWeight, Is.EqualTo(3));
            Assert.That(boss.definition.perilousWeight, Is.EqualTo(2));
            Assert.That(world.enemies[0].definition.randomAttacks, Is.False, "Regular MVP enemies should retain their deterministic attack selection.");

            AttackPattern[] first = CollectBossPatterns(boss, 7319, 500);
            AttackPattern[] second = CollectBossPatterns(boss, 7319, 500);
            Assert.That(second, Is.EqualTo(first), "A fixed random seed should reproduce the same attack sequence.");
            int slash = 0, slow = 0, perilous = 0;
            for (int i = 0; i < first.Length; i++)
            {
                switch (first[i])
                {
                    case AttackPattern.Slash: slash++; break;
                    case AttackPattern.Slow: slow++; break;
                    case AttackPattern.Perilous: perilous++; break;
                }
                if (i > 0 && (first[i - 1] == AttackPattern.Slow || first[i - 1] == AttackPattern.Perilous))
                    Assert.That(first[i], Is.EqualTo(AttackPattern.Slash), "A strong attack must be followed by a fast attack.");
            }
            Assert.That(slash, Is.InRange(290, 390));
            Assert.That(slow, Is.InRange(65, 145));
            Assert.That(perilous, Is.InRange(40, 105));

            var presenter = boss.GetComponent<CombatAnimationPresenter>();
            int perilousSeed = FindFirstBossPatternSeed(boss, AttackPattern.Perilous);
            boss.Brain.Reset(EnemyMode.Duel);
            boss.Actor.Core.Reset();
            boss.Brain.SetRandomSeed(perilousSeed);
            boss.Brain.Tick(boss.definition.wait + .01f);
            Assert.That(boss.Actor.Core.State, Is.EqualTo(CombatState.AttackStartup));
            Assert.That(boss.Actor.Core.ActiveAttack.Kind, Is.EqualTo(AttackKind.Perilous));
            presenter.AdvanceVisual(.01f);
            Assert.That(presenter.CurrentPoseClip, Is.EqualTo(boss.Actor.perilousAttack.clip));
            var hud = world.GetComponent<MvpHud>(); hud.Refresh();
            Assert.That(hud.PerilousWarningVisible, Is.True);
            world.Flow.TogglePause(); hud.Refresh(); Assert.That(hud.PerilousWarningVisible, Is.False);
            world.Flow.TogglePause();
            boss.Actor.Core.Reset();
            hud.Refresh();
            var finalHud = hud;
            Assert.That(finalHud.PerilousWarningVisible, Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator BossDoesNotRerollWhileWaitingForItsAttackCommit()
        {
            var boss = world.enemies[3];
            Place(world.player, boss.Home + Vector3.back * 1f);
            world.Director.Tick(.01f);
            boss.Brain.Reset(EnemyMode.Duel);
            boss.Actor.Core.Reset();
            boss.Brain.SetRandomSeed(13);
            AttackPattern waitingPattern = boss.Brain.NextPattern;
            int attackId = boss.Actor.Core.AttackId;

            float beforeCommit = boss.definition.wait * .9f;
            boss.Brain.Tick(beforeCommit);
            Assert.That(boss.Actor.Core.AttackId, Is.EqualTo(attackId));
            Assert.That(boss.Actor.Core.State, Is.EqualTo(CombatState.Guard));
            Assert.That(boss.Brain.NextPattern, Is.EqualTo(waitingPattern), "An uncommitted wait must not consume or publish a random pick.");

            boss.Brain.Tick(boss.definition.wait - beforeCommit + .01f);
            Assert.That(boss.Actor.Core.AttackId, Is.EqualTo(attackId + 1));
            Assert.That(boss.Actor.Core.State, Is.EqualTo(CombatState.AttackStartup));
            Assert.That(boss.Brain.NextPattern, Is.EqualTo(AttackPattern.Slow), "The first seeded weighted pick should be committed once the wait completes.");
            yield return null;
        }
        [UnityTest] public IEnumerator OverShoulderCameraAvoidsWallAndBossLockFramesTarget()
        {
            Place(world.player, new Vector3(0, .02f, -35));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 1.8f, -36.8f);
            wall.transform.localScale = new Vector3(4, 3, .2f);
            Physics.SyncTransforms();
            world.cameraRig.yaw = 0; world.cameraRig.pitch = 12; world.cameraRig.ResetImpulse();
            world.cameraRig.Step(.1f);
            Assert.That(world.gameplayCamera.transform.position.z, Is.GreaterThan(-36.6f));
            UnityEngine.Object.Destroy(wall);
            yield return null;
            var boss = world.enemies[3];
            Place(world.player, boss.Home + Vector3.back * 7);
            world.Director.Tick(.01f);
            world.cameraRig.ResetImpulse(); world.cameraRig.Step(.1f);
            Vector3 toBoss = boss.transform.position + Vector3.up * 1.35f - world.gameplayCamera.transform.position;
            Assert.That(Vector3.Angle(world.gameplayCamera.transform.forward, toBoss), Is.LessThan(25f));
            float yaw = world.cameraRig.yaw;
            world.cameraRig.Look(new Vector2(40, 0));
            Assert.That(world.cameraRig.yaw, Is.EqualTo(yaw), "Boss lock should own the camera heading.");
        }
        [UnityTest] public IEnumerator BossPerilousBladeHitsThroughGuardButRespectsDodgeInvulnerability()
        {
            var boss = world.enemies[3].Actor;
            Place(world.player, boss.transform.position + Vector3.back * .9f);
            boss.transform.rotation = Quaternion.LookRotation(Vector3.back);
            world.player.transform.rotation = Quaternion.LookRotation(Vector3.back);
            world.player.Core.SetGuard(true, false);
            Assert.That(boss.Core.RequestDefinedAttack(boss.perilousAttack.rules), Is.True);
            boss.Core.Tick(.76f);
            Assert.That(world.player.Core.Health, Is.LessThan(100), "The boss's dangerous blade must bypass Guard.");
            boss.Core.Reset(); world.player.Core.RestoreVitals(100, 0);
            Assert.That(world.player.RequestDodge(Vector3.forward), Is.True);
            world.player.Core.Tick(.10f);
            Assert.That(world.player.Core.IsInvulnerable, Is.True);
            Assert.That(boss.Core.RequestDefinedAttack(boss.perilousAttack.rules), Is.True);
            boss.Core.Tick(.62f);
            Assert.That(world.player.Core.Health, Is.EqualTo(100), "Dodge invulnerability must ignore the dangerous contact.");
            yield return null;
        }
        static void Click(string label)
        {
            foreach(var button in UnityEngine.Object.FindObjectsByType<Button>())
                if(button.GetComponentInChildren<Text>().text==label){Assert.That(button.interactable,Is.True);button.onClick.Invoke();return;}
            Assert.Fail("Missing menu button: "+label);
        }
        IEnumerator WaitForScene(string scene)
        {
            for(int i=0;i<180;i++){yield return null;if(SceneManager.GetActiveScene().name==scene && UnityEngine.Object.FindAnyObjectByType<GameFlowController>()?.State!=GameFlowState.Loading)yield break;}
            Assert.Fail("Scene did not finish loading: "+scene);
        }
        [UnityTest] public IEnumerator HomeContinueNewGameConfirmationAndRepeatedSceneChanges()
        {
            world.player.Core.RestoreVitals(61,12); world.Flow.Save(); world.Flow.ReturnHome(); yield return WaitForScene("MainMenu");
            Assert.That(UnityEngine.Object.FindObjectsByType<MvpWorld>().Length,Is.Zero);
            Click("New Game"); Assert.That(UnityEngine.Object.FindAnyObjectByType<GameFlowController>().Store.TryLoad(out var prior),Is.True);
            Assert.That(prior.health,Is.EqualTo(61)); Click("Back");
            Click("Start Game / Continue"); yield return WaitForScene("MVP_TestLevel");
            world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();world.manualSimulation=true;
            Assert.That(world.player.Core.Health,Is.EqualTo(61).Within(.5));
            world.Flow.TogglePause(); yield return null;
            Assert.That(UnityEngine.Object.FindObjectsByType<EventSystem>().Length,Is.EqualTo(1));
            Click("Resume"); Assert.That(world.Flow.Paused,Is.False);
            world.Flow.ReturnHome();yield return WaitForScene("MainMenu");Click("New Game");Click("Confirm New Game");yield return WaitForScene("MVP_TestLevel");
            world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();world.manualSimulation=true;
            Assert.That(world.player.Core.Health,Is.EqualTo(100));Assert.That(UnityEngine.Object.FindObjectsByType<MvpWorld>().Length,Is.EqualTo(1));
        }
    }
}
