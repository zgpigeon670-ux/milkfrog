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
    public class PhaseThreeTests
    {
        CombatDemoSession session;
        [UnitySetUp] public IEnumerator Load()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/CombatDemo_Animated.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("CombatDemo_Animated");
#endif
            yield return null;session=Object.FindFirstObjectByType<CombatDemoSession>();session.manualSimulation=true;session.SetMode(EnemyMode.Dummy);
        }
        void Advance(float t,int fps=60){while(t>.000001f){float dt=Mathf.Min(t,1f/fps);session.Simulate(dt);t-=dt;}}
        void Press()=>session.SubmitInput(new CombatInputFrame{attackPressed=true,attackHeld=true});
        void Release()=>session.SubmitInput(new CombatInputFrame{attackReleased=true});
        [UnityTest] public IEnumerator ThrustFollowsItsOwnBladeAndHitsOnceAtAllFrameRates()
        {
            foreach(int fps in new[]{30,60,120})
            {
                session.ResetRound();Press();Advance(.8f,fps);Assert.That(session.player.Core.ChargeRatio,Is.EqualTo(1).Within(.001));Release();
                Assert.That(session.player.Core.ActiveAttack.Kind,Is.EqualTo(AttackKind.Thrust));
                Assert.That(session.player.Core.ActiveAttack.Damage,Is.EqualTo(22).Within(.001));
                Advance(.8f,fps);Assert.That(session.enemy.Core.Health,Is.EqualTo(78).Within(.001),"Charged slash failed at "+fps+" fps");
                Assert.That(session.enemy.Core.Posture,Is.EqualTo(28).Within(.001));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator DodgeDistanceDirectionAndWallCollision()
        {
            foreach(int fps in new[]{30,60,120})
            {
                session.ResetRound();Vector3 start=session.player.transform.position;
                session.SubmitInput(new CombatInputFrame{dodgePressed=true});
                Assert.That(Vector3.Dot(session.player.DodgeDirection,session.player.transform.forward),Is.LessThan(-.99f));
                Advance(.8f,fps);Assert.That(Vector3.Distance(start,session.player.transform.position),Is.EqualTo(2.6f).Within(.06));
            }
            session.ResetRound();Vector3 origin=session.player.transform.position;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=origin+Vector3.back*.95f+Vector3.up;wall.transform.localScale=new Vector3(4,3,.2f);Physics.SyncTransforms();
            session.SubmitInput(new CombatInputFrame{dodgePressed=true});Advance(.4f);
            Assert.That(origin.z-session.player.transform.position.z,Is.LessThan(.55f));
            Object.Destroy(wall);yield return null;Advance(.3f);
            Assert.That(origin.z-session.player.transform.position.z,Is.LessThan(.55f),"Blocked distance must not be replayed later");
        }
        [UnityTest] public IEnumerator SnapshotPriorityFreezeReleaseFocusAndResetDoNotQueueAttacks()
        {
            Press();Advance(.4f);session.SubmitInput(new CombatInputFrame{dodgePressed=true,guardHeld=true,guardPressed=true,attackHeld=true});
            Assert.That(session.player.Core.State,Is.EqualTo(CombatState.Dodge));Advance(.5f);Release();
            Assert.That(session.player.Core.AttackId,Is.Zero);
            session.ResetRound();Press();Advance(.4f);session.feedback.Clock.Request(.04f);Release();Advance(.1f);
            Assert.That(session.player.Core.IsPreparing,Is.False);Assert.That(session.player.Core.AttackId,Is.Zero);
            session.ResetRound();Press();Advance(.4f);session.ClearInput();Release();Advance(.5f);Assert.That(session.player.Core.AttackId,Is.Zero);
            session.SubmitInput(new CombatInputFrame{resetPressed=true,dodgePressed=true,attackPressed=true,attackHeld=true});
            Assert.That(session.player.Core.State,Is.EqualTo(CombatState.Neutral));Assert.That(session.player.Core.AttackId,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator DodgeCannotPassThroughEnemyAndHitInterruptsMovement()
        {
            Vector3 origin=session.player.transform.position;
            session.player.RequestDodge(Vector3.forward);Advance(.4f);
            Assert.That(session.player.transform.position.z-origin.z,Is.LessThan(.55f));
            session.ResetRound();session.player.RequestDodge(Vector3.left);Advance(.03f);
            var probe=new CombatCore(new CombatTuning());probe.RequestAttack();probe.Tick(.25f);probe.TryHit(session.player.Core,false);
            Vector3 hitPosition=session.player.transform.position;Advance(.5f);
            Assert.That(Vector3.Distance(hitPosition,session.player.transform.position),Is.LessThan(.01f));
            yield return null;
        }
        [UnityTest] public IEnumerator ChargedThrustCannotDamageThroughAWall()
        {
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position=session.player.transform.position+Vector3.forward*.65f+Vector3.up;
            wall.transform.localScale=new Vector3(3,2,.1f);Physics.SyncTransforms();
            Press();Advance(.8f);Release();Advance(.7f);
            Assert.That(session.enemy.Core.Health,Is.EqualTo(100));Object.Destroy(wall);yield return null;
        }
        [UnityTest] public IEnumerator ThrustVisualMatchesBakedTraceAndResetClearsTrail()
        {
            session.enemy.Motor.enabled=false;session.enemy.transform.position+=Vector3.forward*5;session.enemy.Motor.enabled=true;
            Press();Advance(.8f);Release();Advance(.25f);
            Assert.That(session.player.Core.State,Is.EqualTo(CombatState.AttackActive));
            var player=session.player;var blade=session.playerAnimation.animator.GetBoneTransform(HumanBodyBones.RightHand).Find("Training Sword/Blade");
            for(int i=0;i<10;i++)
            {
                var pose=player.thrustAttack.trace.Sample(player.Core.StateProgress);
                var expected=player.transform.position+Quaternion.LookRotation(player.AttackForward)*pose.tip;
                Assert.That(Vector3.Distance(expected,blade.TransformPoint(Vector3.up*.5f)),Is.LessThan(.04f),"Sample "+i+" phase="+player.Core.StateProgress);Advance(.01f);
            }
            session.ResetRound();Assert.That(session.playerAnimation.swordTrail.emitting,Is.False);Assert.That(player.Core.ChargeRatio,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator TapSlashesHoldThrustsAndShiftDodgesWhileSpaceJumps()
        {
            session.enemy.Motor.enabled=false;session.enemy.transform.position+=Vector3.forward*6;
            Press();Advance(.05f);
            Assert.That(session.player.Core.State,Is.EqualTo(CombatState.AttackStartup));
            Assert.That(session.player.Core.ActiveAttack.Kind,Is.EqualTo(AttackKind.Light));
            Assert.That(session.playerAnimation.CurrentPoseClip,Is.EqualTo(session.player.lightAttack.clip));
            Advance(.3f);
            Assert.That(session.player.Core.IsAttacking,Is.True);
            Assert.That(session.player.Core.State,Is.Not.EqualTo(CombatState.Charging));
            session.ResetRound();Press();Advance(.8f);
            Assert.That(session.player.Core.ActiveAttack.Kind,Is.EqualTo(AttackKind.Light));
            Assert.That(session.player.Core.State,Is.Not.EqualTo(CombatState.Charging));
            session.ResetRound();session.SubmitInput(new CombatInputFrame{dodgePressed=true,move=Vector2.left});
            Assert.That(session.player.Core.State,Is.EqualTo(CombatState.Dodge));
            Assert.That(session.playerAnimation.CurrentPoseClip.name,Is.EqualTo("Armature|Roll"));
            session.SubmitInput(new CombatInputFrame{jumpPressed=true});
            Assert.That(session.player.Core.State,Is.EqualTo(CombatState.Dodge));
            Assert.That(session.player.Grounded,Is.True);
            session.ResetRound();float ground=session.player.transform.position.y;
            session.SubmitInput(new CombatInputFrame{jumpPressed=true});Advance(.2f);
            Assert.That(session.player.transform.position.y,Is.GreaterThan(ground+.2f));
            Assert.That(session.playerAnimation.CurrentPoseClip.name,Does.StartWith("Armature|Jump"));
            Assert.That(session.player.Core.State,Is.EqualTo(CombatState.Neutral));
            float airborne=session.player.transform.position.y;
            session.SubmitInput(new CombatInputFrame{jumpPressed=true});Advance(.02f);
            Assert.That(session.player.transform.position.y,Is.LessThan(airborne+.02f));
            yield return null;
        }
    }
}
