using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Milkfrog.CombatDemo.Tests
{
    public class CombatSceneTests
    {
        CombatDemoSession session;
        CombatDemoSettings original;
        readonly List<HitResult> results = new List<HitResult>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/CombatDemo.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("CombatDemo");
#endif
            yield return null;
            session = Object.FindFirstObjectByType<CombatDemoSession>();
            Assert.That(session, Is.Not.Null);
            session.manualSimulation = true;
            original = session.settings;
            session.settings = Object.Instantiate(original);
            session.settings.logEvents = false;
            session.ResetRound();
            session.CombatEvent += hit => results.Add(hit.Result);
            results.Clear();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (session != null) { var copy = session.settings; session.settings = original; Object.Destroy(copy); }
            yield return null;
        }

        void Advance(float seconds) => session.Simulate(seconds);
        void Input(bool attack = false, bool guard = false, bool press = false)
            => session.SubmitInput(Vector2.zero, guard, press, attack);
        void AttackAndRecover()
        {
            Assert.That(session.player.Core.CanAct, Is.True);
            Input(attack: true); Advance(.72f);
        }

        [UnityTest]
        public IEnumerator FullExchangeBreakDeathblowAndRepeatedReset()
        {
            session.SetMode(EnemyMode.Duel); Advance(.01f);
            AttackAndRecover(); AttackAndRecover();
            Assert.That(results, Is.EqualTo(new[] { HitResult.Block, HitResult.Block }));
            Input(attack: true); Advance(.27f);
            Assert.That(results[2], Is.EqualTo(HitResult.Deflect));
            Assert.That(session.player.Core.State, Is.EqualTo(CombatState.DeflectedStun));
            Assert.That(session.Brain.Blocks, Is.Zero);
            // Counter: 0.10 delay + 0.45 startup; tap during the last 0.10 seconds.
            Advance(.44f);
            Assert.That(session.enemy.Core.State, Is.EqualTo(CombatState.AttackStartup));
            Assert.That(session.player.Core.CanAct, Is.True);
            Input(guard: true, press: true); Advance(.12f);
            Assert.That(results[3], Is.EqualTo(HitResult.Deflect));
            Assert.That(session.enemy.Core.State, Is.EqualTo(CombatState.DeflectedStun));
            Assert.That(session.enemy.Core.Posture, Is.EqualTo(70));
            Input(); Advance(.22f);
            AttackAndRecover(); AttackAndRecover();
            Assert.That(session.enemy.Core.State, Is.EqualTo(CombatState.PostureBroken));
            Input(attack: true);
            Assert.That(session.enemy.Core.State, Is.EqualTo(CombatState.Dead));
            Assert.That(results[results.Count - 1], Is.EqualTo(HitResult.Deathblow));
            int count = results.Count; Advance(5); Assert.That(results.Count, Is.EqualTo(count));
            for (int i = 0; i < 5; i++)
            {
                session.ResetRound();
                Assert.That(session.player.Core.Health, Is.EqualTo(100));
                Assert.That(session.enemy.Core.Posture, Is.Zero);
                Assert.That(session.Brain.Blocks, Is.Zero);
                Assert.That(session.player.Core.AttackId, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PhysicsDetectionDeduplicatesMultipleCollidersAndCancelsInterruptedAttack()
        {
            session.SetMode(EnemyMode.Dummy);
            var extra = new GameObject("Extra Hurt Collider", typeof(BoxCollider));
            extra.transform.SetParent(session.enemy.transform, false);
            extra.transform.localPosition = Vector3.up;
            Physics.SyncTransforms();
            Input(attack: true); Advance(.35f);
            Assert.That(session.enemy.Core.Health, Is.EqualTo(90));
            Assert.That(results.Count, Is.EqualTo(1));
            session.ResetRound(); results.Clear();
            session.enemy.Core.RequestAttack();
            Input(attack: true); Advance(.8f);
            Assert.That(session.player.Core.Health, Is.EqualTo(100));
            Assert.That(session.enemy.Core.Health, Is.EqualTo(90));
            Object.Destroy(extra);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MovementRespectsActorCollisionAndArenaFloor()
        {
            session.SetMode(EnemyMode.Dummy);
            var before = session.player.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(session.gameplayCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 towardEnemy = (session.enemy.transform.position - before).normalized;
            session.SubmitInput(new Vector2(Vector3.Dot(towardEnemy, right), Vector3.Dot(towardEnemy, forward)), false, false, false);
            Advance(1);
            Assert.That(session.player.transform.position.z, Is.GreaterThan(before.z));
            Assert.That(session.player.DistanceToTarget, Is.GreaterThan(.7f));
            Assert.That(session.player.transform.position.z, Is.LessThan(session.enemy.transform.position.z));
            Assert.That(session.player.transform.position.y, Is.InRange(-.1f, .1f));
            session.ResetRound();
            Assert.That(Vector3.Distance(before, session.player.transform.position), Is.LessThan(.001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EarlyGuardBlocksLateGuardDeflectsAndPlayerCanDie()
        {
            session.SetMode(EnemyMode.Rhythm);
            Input(guard: true, press: true); Advance(2.1f);
            Assert.That(results[0], Is.EqualTo(HitResult.Block));
            Assert.That(session.player.Core.Health, Is.EqualTo(100));
            session.ResetRound(); results.Clear();
            Advance(1.85f); Input(guard: true, press: true); Advance(.13f);
            Assert.That(results[0], Is.EqualTo(HitResult.Deflect));
            session.ResetRound(); results.Clear();
            for (int i = 0; i < 50 && !session.Finished; i++) Advance(1);
            Assert.That(session.player.Core.State, Is.EqualTo(CombatState.Dead));
            Assert.That(session.Finished, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ModeSwitchClearsPendingCounterAndResetsPositions()
        {
            session.SetMode(EnemyMode.Duel); Advance(.01f);
            AttackAndRecover(); AttackAndRecover(); Input(attack: true); Advance(.26f);
            Assert.That(results[2], Is.EqualTo(HitResult.Deflect));
            session.SetMode(EnemyMode.Dummy); Advance(2);
            Assert.That(session.enemy.Core.State, Is.EqualTo(CombatState.Neutral));
            Assert.That(session.enemy.Core.AttackId, Is.Zero);
            Assert.That(session.Brain.Blocks, Is.Zero);
            Assert.That(session.player.Core.Health, Is.EqualTo(100));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SustainedGuardCanBreakPlayerAndRecoverWithoutEnemyDeathblow()
        {
            session.SetMode(EnemyMode.Rhythm);
            // No initial tap window: this scenario exercises sustained blocking only.
            Input(guard: true);
            for (int i = 0; i < 400 && session.player.Core.State != CombatState.PostureBroken; i++) Advance(.1f);
            Assert.That(session.player.Core.State, Is.EqualTo(CombatState.PostureBroken));
            Assert.That(session.player.Core.RequestAttack(), Is.False);
            Advance(2.05f);
            Assert.That(session.player.Core.CanAct, Is.True);
            Assert.That(session.player.Core.Posture, Is.LessThan(25));
            Assert.That(results.Contains(HitResult.Deathblow), Is.False);
            session.ResetRound();
            Assert.That(session.player.Core.State, Is.EqualTo(CombatState.Neutral));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewInputSystemBindingsReadKeyboardAndMouse()
        {
            // Batch test runners do not have a focused Game View. Isolate that policy to this test.
            var testSettings = InputSystem.settings;
            var previousBackground = testSettings.backgroundBehavior;
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            var previousEditorPolicy = testSettings.editorInputBehaviorInPlayMode;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            var reader = new DemoInput();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.R, Key.F1, Key.LeftShift));
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
                InputSystem.Update();
                Assert.That(reader.Move.y, Is.EqualTo(1));
                Assert.That(reader.AttackPressed, Is.True);
                Assert.That(reader.Snapshot.dodgePressed,Is.True);
                Assert.That(reader.Snapshot.jumpPressed,Is.False);
                Assert.That(reader.Snapshot.attackHeld,Is.True);
                Assert.That(reader.GuardPressed, Is.True);
                Assert.That(reader.GuardHeld, Is.True);
                Assert.That(reader.ResetPressed, Is.True);
                Assert.That(reader.ModePressed, Is.True);
                yield return null;
                Assert.That(reader.GuardHeld, Is.True);
                Assert.That(reader.GuardPressed, Is.False);
                Assert.That(reader.AttackPressed, Is.False);
                Assert.That(reader.Snapshot.dodgePressed,Is.False);
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(reader.Snapshot.attackReleased,Is.True);
                Assert.That(reader.Snapshot.attackHeld,Is.False);
                Assert.That(reader.GuardHeld, Is.False);
                Assert.That(reader.Move, Is.EqualTo(Vector2.zero));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                InputSystem.Update();
                Assert.That(reader.Snapshot.jumpPressed,Is.True);
                Assert.That(reader.Snapshot.dodgePressed,Is.False);
            }
            finally
            {
                reader.Dispose();
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
                testSettings.backgroundBehavior = previousBackground;
#if UNITY_EDITOR
                testSettings.editorInputBehaviorInPlayMode = previousEditorPolicy;
#endif
            }
        }
    }
}
