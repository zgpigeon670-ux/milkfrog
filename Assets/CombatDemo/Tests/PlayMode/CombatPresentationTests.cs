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
    public class CombatPresentationTests
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
            session.manualSimulation = true; session.SetMode(EnemyMode.Dummy);
        }

        [UnityTest] public IEnumerator EssentialBarsSurviveDiagnosticsToggle()
        {
            var debug = Object.FindFirstObjectByType<CombatDebugHud>();
            var hud = Object.FindFirstObjectByType<CombatHud>();
            Assert.That(hud.session, Is.EqualTo(session));
            debug.showDebug = false; yield return null;
            Assert.That(hud.isActiveAndEnabled, Is.True);
            Assert.That(session.player.GetComponent<CombatActorView>().label.gameObject.activeSelf, Is.False);
            debug.showDebug = true; yield return null;
            Assert.That(hud.isActiveAndEnabled, Is.True);
            Assert.That(session.player.GetComponent<CombatActorView>().label.gameObject.activeSelf, Is.True);
        }

        [UnityTest] public IEnumerator CameraFollowsPlayersBackAfterTurningAndReset()
        {
            var rig = session.gameplayCamera.GetComponent<DemoCamera>();
            Assert.That(rig.followBehindPlayer, Is.True);
            rig.ResetImpulse(); rig.Step(1f / 60);
            AssertBehind(rig);
            session.player.transform.rotation = Quaternion.Euler(0, 180, 0);
            for (int i = 0; i < 120; i++) rig.Step(1f / 60);
            AssertBehind(rig);
            session.ResetRound(); rig.Step(1f / 60); AssertBehind(rig);
            yield return null;
        }
        void AssertBehind(DemoCamera rig)
        {
            var relative = Vector3.ProjectOnPlane(rig.transform.position - session.player.transform.position, Vector3.up).normalized;
            Assert.That(Vector3.Dot(relative, session.player.transform.forward), Is.LessThan(-.85f));
        }

        [UnityTest] public IEnumerator PerfectDeflectFlashesAttackerAndResetClearsShader()
        {
            var playerView = session.player.GetComponent<CombatActorView>();
            var enemyView = session.enemy.GetComponent<CombatActorView>();
            session.enemy.Core.RequestAttack(); session.enemy.Core.Tick(.45f);
            session.player.Core.SetGuard(true, true);
            session.enemy.Core.Tick(.1f);
            Assert.That(session.enemy.Core.State, Is.EqualTo(CombatState.DeflectedStun));
            Assert.That(enemyView.FlashAmount, Is.EqualTo(1));
            Assert.That(playerView.FlashAmount, Is.Zero);
            var properties = new MaterialPropertyBlock(); enemyView.body.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_FlashAmount"), Is.EqualTo(1));
            Assert.That(enemyView.body.sharedMaterial.shader.isSupported, Is.True);
            session.feedback.TickReal(.1f);
            Assert.That(enemyView.FlashAmount, Is.InRange(.49f, .51f));
            session.ResetRound(); enemyView.body.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_FlashAmount"), Is.Zero);
            Assert.That(session.IsFrozen, Is.False);
            Assert.That(session.feedback.ActiveFlashes, Is.Zero);
            yield return null;
        }
    }
}
