using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
namespace Milkfrog.CombatDemo.Tests
{
    public class AnimatedSceneTests
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
            session = Object.FindFirstObjectByType<CombatDemoSession>(); session.manualSimulation = true; session.ResetRound();
        }
        void Advance(float duration, int fps = 60)
        { while (duration > .000001f) { float dt = Mathf.Min(duration, 1f/fps); session.Simulate(dt); duration -= dt; } }
        void Attack() { session.SubmitInput(Vector2.zero, false, false, false); session.SubmitInput(Vector2.zero, false, false, true); }
        [UnityTest] public IEnumerator HumanoidClipsAnimateWithoutControllerOrRootDrift()
        {
            var presentation = session.playerAnimation; var animator = presentation.animator;
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            var profile = presentation.profile;
            foreach (var clip in new[] { profile.idle,profile.walk,profile.jog,profile.attack,profile.hit,profile.death,profile.guard,profile.parry,profile.deflected,profile.broken })
                Assert.That(clip != null && clip.humanMotion, Is.True, clip == null ? "Missing clip" : clip.name);
            session.SetMode(EnemyMode.Dummy);
            Vector3 root = session.player.transform.position, local = animator.transform.localPosition;
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand); Vector3 idleHand = hand.position;
            Attack(); Advance(.24f);
            Assert.That(Vector3.Distance(idleHand, hand.position), Is.GreaterThan(.03f), "Attack must visibly move the hand.");
            Advance(3);
            Assert.That(Vector3.Distance(root, session.player.transform.position), Is.LessThan(.05f));
            Assert.That(Vector3.Distance(local, animator.transform.localPosition), Is.LessThan(.001f));
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(presentation.swordTrail.emitting, Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator FreezeIgnoresFreshInputsAndResetClearsAllFeedback()
        {
            float globalTimeScale = Time.timeScale;
            session.SetMode(EnemyMode.Dummy); Attack(); Advance(.1f);
            session.feedback.Clock.Request(.04f);
            float remaining = session.player.Core.Remaining, sample = session.playerAnimation.SampledAttackTime;
            session.SubmitInput(Vector2.zero, true, true, true); session.Simulate(.02f);
            Assert.That(session.player.Core.Remaining, Is.EqualTo(remaining).Within(.00001));
            Assert.That(session.playerAnimation.SampledAttackTime, Is.EqualTo(sample));
            Advance(.8f); Assert.That(session.player.Core.State, Is.EqualTo(CombatState.Guard)); Assert.That(session.player.Core.DeflectOpen, Is.False);
            Assert.That(session.player.Core.AttackId, Is.EqualTo(1));
            session.ResetRound(); Assert.That(session.IsFrozen, Is.False); Assert.That(session.feedback.ActiveFlashes, Is.Zero);
            Assert.That(session.playerAnimation.swordTrail.emitting, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(globalTimeScale));
            yield return null;
        }
        [UnityTest] public IEnumerator InterruptedAttackCannotLeaveDamageOrTrail()
        {
            session.SetMode(EnemyMode.Dummy);
            session.enemy.Core.RequestAttack(); Advance(.3f); Attack(); Advance(.24f);
            Assert.That(session.player.Core.State, Is.EqualTo(CombatState.HitStun));
            Assert.That(session.playerAnimation.swordTrail.emitting, Is.False);
            Advance(.5f); Assert.That(session.enemy.Core.Health, Is.EqualTo(100));
            yield return null;
        }
        [UnityTest] public IEnumerator FreshGuardAfterExactFreezeBoundaryIsNotOverwritten()
        {
            session.SetMode(EnemyMode.Dummy);
            session.feedback.Clock.Request(.02f);
            session.SubmitInput(Vector2.zero,false,false,true);
            session.Simulate(.02f);
            Assert.That(session.IsFrozen, Is.False);
            Assert.That(session.player.Core.AttackId, Is.Zero);
            session.SubmitInput(Vector2.zero,true,true,false); session.Simulate(.001f);
            Assert.That(session.player.Core.DeflectOpen, Is.True);
            float scale = Time.timeScale;
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Exit validation"));
            yield return SceneManager.UnloadSceneAsync(previous);
            Assert.That(Time.timeScale, Is.EqualTo(scale));
        }
        [UnityTest] public IEnumerator CameraSphereCastKeepsCameraInFrontOfWall()
        {
            var rig = session.gameplayCamera.GetComponent<DemoCamera>();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(4,3,-3); wall.transform.localScale = new Vector3(5,6,.4f);
            Physics.SyncTransforms(); Vector3 focus = new Vector3(4,3,0);
            Vector3 limited = rig.ConstrainPosition(focus, new Vector3(4,3,-6));
            Assert.That(limited.z, Is.GreaterThan(-2.6f)); Assert.That(limited.z, Is.LessThan(-2));
            Object.Destroy(wall); yield return null;
        }
        [UnityTest] public IEnumerator FullExchangeAt30Fps() { FullExchange(30); yield return null; }
        [UnityTest] public IEnumerator FullExchangeAt60Fps() { FullExchange(60); yield return null; }
        [UnityTest] public IEnumerator FullExchangeAt120Fps() { FullExchange(120); yield return null; }
        void FullExchange(int fps)
        {
            var events = new List<HitResult>(); session.CombatEvent += hit => events.Add(hit.Result);
            session.SetMode(EnemyMode.Duel); Advance(.01f,fps);
            Attack(); Advance(.72f,fps); Attack(); Advance(.72f,fps);
            Attack();
            session.SubmitInput(Vector2.zero,false,false,false);
            Until(() => session.player.Core.State == CombatState.DeflectedStun, fps);
            Until(() => session.enemy.Core.State == CombatState.AttackStartup && session.enemy.Core.Remaining <= .08f, fps);
            Assert.That(session.enemy.Core.State, Is.EqualTo(CombatState.AttackStartup));
            session.SubmitInput(Vector2.zero,true,true,false);
            Until(() => session.enemy.Core.State == CombatState.DeflectedStun, fps);
            Assert.That(session.enemy.Core.State, Is.EqualTo(CombatState.DeflectedStun));
            session.SubmitInput(Vector2.zero,false,false,false);
            Until(() => session.enemy.Core.CanAct && !session.IsFrozen, fps);
            Attack(); Advance(.72f,fps); Attack(); Advance(.72f,fps);
            Assert.That(session.enemy.Core.State, Is.EqualTo(CombatState.PostureBroken));
            Until(() => session.player.Core.CanAct, fps);
            Attack(); Assert.That(session.enemy.Core.State, Is.EqualTo(CombatState.Dead));
            Advance(3,fps);
            Assert.That(events, Is.EqualTo(new[] { HitResult.Block,HitResult.Block,HitResult.Deflect,HitResult.Deflect,HitResult.Block,HitResult.Block,HitResult.Deathblow }));
            Assert.That(session.enemyAnimation.animator.GetBoneTransform(HumanBodyBones.Head).position.y, Is.LessThan(1), "Death pose should lie down.");
            for (int i=0;i<5;i++) { session.ResetRound(); Assert.That(session.IsFrozen,Is.False); Assert.That(session.Brain.Blocks,Is.Zero); Assert.That(session.player.Core.AttackId,Is.Zero); }
        }
        void Until(System.Func<bool> condition, int fps)
        {
            for (int i = 0; i < fps * 3 && !condition(); i++) session.Simulate(1f / fps);
            Assert.That(condition(), Is.True, "Combat exchange did not reach its expected state within 3 seconds.");
        }
    }
}
