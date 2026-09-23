using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Milkfrog.CombatDemo.Tests
{
    public class CombatFeelSceneTests
    {
        CombatDemoSession session;
        [UnitySetUp] public IEnumerator Load()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/CombatDemo_Animated.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("CombatDemo_Animated");
#endif
            yield return null;
            session = Object.FindFirstObjectByType<CombatDemoSession>();
            session.manualSimulation = true;
            session.SetMode(EnemyMode.Dummy);
        }
        void Advance(float seconds) => session.Simulate(seconds);

        [UnityTest]
        public IEnumerator RhythmCyclesSlashSlowAndPerilous()
        {
            session.SetMode(EnemyMode.Rhythm);
            var expected = new[] { AttackKind.Light, AttackKind.Slow, AttackKind.Perilous };
            for (int i = 0; i < expected.Length; i++)
            {
                Advance(1.35f);
                Assert.That(session.enemy.Core.State, Is.EqualTo(CombatState.AttackStartup));
                Assert.That(session.enemy.Core.ActiveAttack.Kind, Is.EqualTo(expected[i]));
                Advance(session.enemy.Core.ActiveAttack.Startup + session.enemy.Core.ActiveAttack.Active + session.enemy.Core.ActiveAttack.Recovery + .05f);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PerilousDeflectFailsAndDodgeInvulnerabilityAvoidsIt()
        {
            session.SetMode(EnemyMode.Dummy);
            session.enemy.Core.RequestDefinedAttack(session.enemy.perilousAttack.rules);
            Advance(.62f);
            session.SubmitInput(new CombatInputFrame { guardHeld = true, guardPressed = true });
            Advance(.16f);
            Assert.That(session.player.Core.Posture, Is.EqualTo(36).Within(.01f));
            Assert.That(session.enemy.Core.State, Is.Not.EqualTo(CombatState.DeflectedStun));

            session.ResetRound();
            session.enemy.Motor.enabled = false;
            session.enemy.transform.position += Vector3.forward * 1.2f;
            session.SubmitInput(new CombatInputFrame { move = Vector2.left });
            Advance(.15f);
            session.enemy.Core.RequestDefinedAttack(session.enemy.perilousAttack.rules);
            Advance(.64f);
            session.SubmitInput(new CombatInputFrame { dodgePressed = true, move = Vector2.left });
            Advance(.16f);
            Assert.That(session.player.Core.Health, Is.EqualTo(100));
            Assert.That(session.player.Core.State, Is.EqualTo(CombatState.Dodge));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecoveryGuardCancelAndFollowupAreAvailable()
        {
            session.SetMode(EnemyMode.Dummy);
            session.enemy.Motor.enabled = false;
            session.enemy.transform.position += Vector3.forward * 6;
            session.SubmitInput(new CombatInputFrame { attackPressed = true, attackHeld = true });
            Advance(.02f);
            session.SubmitInput(new CombatInputFrame { attackReleased = true });
            Advance(.42f);
            Assert.That(session.player.Core.State, Is.EqualTo(CombatState.AttackRecovery));
            session.SubmitInput(new CombatInputFrame { guardHeld = true, guardPressed = true });
            Assert.That(session.player.Core.State, Is.EqualTo(CombatState.Guard));

            session.ResetRound();
            session.SubmitInput(new CombatInputFrame { attackPressed = true, attackHeld = true });
            Advance(.02f);
            session.SubmitInput(new CombatInputFrame { attackReleased = true });
            Advance(.72f);
            Assert.That(session.player.Core.ComboOpen, Is.True);
            session.SubmitInput(new CombatInputFrame { attackPressed = true, attackHeld = true });
            Assert.That(session.player.Core.ActiveAttack.Kind, Is.EqualTo(AttackKind.Followup));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TabTogglesLockAndCameraUsesEnemyHeading()
        {
            Assert.That(session.LockedOn, Is.True);
            session.SubmitInput(new CombatInputFrame { lockPressed = true });
            Advance(.05f);
            Assert.That(session.LockedOn, Is.False);
            var camera = session.gameplayCamera.GetComponent<DemoCamera>();
            Assert.That(camera.lockOn, Is.False);
            session.SubmitInput(new CombatInputFrame { lockPressed = true });
            Advance(.05f);
            Assert.That(camera.lockOn, Is.True);
            Advance(.35f);
            Vector3 toEnemy = Vector3.ProjectOnPlane(session.enemy.transform.position - session.player.transform.position, Vector3.up).normalized;
            Vector3 behind = Vector3.ProjectOnPlane(session.gameplayCamera.transform.position - session.player.transform.position, Vector3.up).normalized;
            Assert.That(Vector3.Dot(behind, toEnemy), Is.LessThan(-.7f));
            yield return null;
        }
    }
}
