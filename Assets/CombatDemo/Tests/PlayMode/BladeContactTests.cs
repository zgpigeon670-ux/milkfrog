using System;
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
    public class BladeContactTests
    {
        CombatDemoSession session;
        [UnitySetUp] public IEnumerator Load()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/CombatDemo_Animated.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("CombatDemo_Animated");
#endif
            yield return null;
            session=UnityEngine.Object.FindFirstObjectByType<CombatDemoSession>(); session.manualSimulation=true; session.SetMode(EnemyMode.Dummy);
        }
        void PlaceEnemy(Vector3 position)
        { session.enemy.Motor.enabled=false; session.enemy.transform.position=position; session.enemy.Motor.enabled=true; Physics.SyncTransforms(); }
        [UnityTest] public IEnumerator BlockedLightAndFollowupKeepTheirAttackPoseThroughRecovery()
        {
            session.SetMode(EnemyMode.Duel);
            session.feedback.hitStop = true;
            PlaceEnemy(session.player.transform.position + Vector3.forward * 1.2f);
            int blocks = 0;
            session.CombatEvent += hit => { if (hit.Attacker == session.player.Core && hit.Result == HitResult.Block) blocks++; };
            session.player.Core.RequestAttack();
            for (int i = 0; i < 120 && blocks == 0; i++) session.Simulate(1f / 120);
            Assert.That(blocks, Is.EqualTo(1));
            Assert.That(session.player.Core.IsAttacking, Is.True, "An ordinary block must not interrupt the attacker.");
            float held = session.playerAnimation.SampledAttackTime;
            session.Simulate(.006f);
            Assert.That(session.playerAnimation.SampledAttackTime, Is.EqualTo(held), "Hit stop must hold the sampled pose.");
            for (int i = 0; i < 120 && !session.player.Core.CanAct; i++) session.Simulate(1f / 120);
            Assert.That(session.player.Core.State, Is.EqualTo(CombatState.Neutral));
            Assert.That(session.player.Core.RequestFollowup(), Is.True);
            Assert.That(session.player.Core.ActiveAttack.Kind, Is.EqualTo(AttackKind.Followup));
            float previous = -1;
            for (int i = 0; i < 100 && blocks < 2; i++)
            {
                session.Simulate(1f / 120);
                float sampled = session.playerAnimation.SampledAttackTime;
                Assert.That(sampled + .0001f, Is.GreaterThanOrEqualTo(previous), "Follow-up clip sampling must not jump backward at a phase boundary.");
                previous = sampled;
            }
            Assert.That(blocks, Is.EqualTo(2));
            Assert.That(session.player.Core.IsAttacking, Is.True);
            for (int i = 0; i < 120 && !session.player.Core.CanAct; i++) session.Simulate(1f / 120);
            Assert.That(session.player.Core.State, Is.EqualTo(CombatState.Neutral));
            Assert.That(session.enemy.Core.Health, Is.EqualTo(session.enemy.Core.Tuning.maxHealth));
            yield return null;
        }

        [UnityTest] public IEnumerator EnemyDeflectInterruptsAttackAndShowsDeflectedPoseAfterHitStop()
        {
            session.SetMode(EnemyMode.Duel);
            session.feedback.hitStop = true;
            PlaceEnemy(session.player.transform.position + Vector3.forward * 1.2f);
            HitResult last = HitResult.Ignore;
            session.CombatEvent += hit => { if (hit.Attacker == session.player.Core) last = hit.Result; };
            for (int attack = 0; attack < 3; attack++)
            {
                Assert.That(session.player.Core.RequestAttack(), Is.True);
                for (int i = 0; i < 130 && session.player.Core.IsAttacking; i++) session.Simulate(1f / 120);
                if (attack < 2) Assert.That(last, Is.EqualTo(HitResult.Block));
            }
            Assert.That(last, Is.EqualTo(HitResult.Deflect));
            Assert.That(session.player.Core.State, Is.EqualTo(CombatState.DeflectedStun));
            session.Simulate(session.feedback.deflectFreeze + .02f);
            Assert.That(session.playerAnimation.CurrentPoseClip, Is.EqualTo(session.playerAnimation.profile.deflected));
            Assert.That(session.enemy.Core.Health, Is.EqualTo(session.enemy.Core.Tuning.maxHealth));
            yield return null;
        }
        [UnityTest] public IEnumerator CenteredBladeHitsOnlyWhenItReachesTheTarget()
        {
            var player=session.player; var enemy=session.enemy;
            PlaceEnemy(player.transform.position+Vector3.forward*1.2f);
            int overlaps=0; bool visible=false; float nearest=999; Vector3 closestTip=Vector3.zero;
            foreach(var pose in player.bladeTrace.poses)
            {
                Vector3 a=player.transform.position+pose.root,b=player.transform.position+pose.tip;
                float gap=Vector3.Distance(enemy.Motor.ClosestPoint(b),b); if(gap<nearest){nearest=gap;closestTip=b;}
                foreach(var contact in Physics.OverlapCapsule(a,b,player.bladeTrace.radius))
                    if(contact.GetComponentInParent<CombatActor>()==enemy) {overlaps++;visible|=player.HasLineOfSight(enemy,contact.ClosestPoint(b));}
            }
            Assert.That(overlaps,Is.GreaterThan(0),"Baked blade must physically reach a centered opponent; player="+player.transform.position+" enemy="+enemy.transform.position+" bounds="+enemy.Motor.bounds+" nearest="+nearest+" tip="+closestTip+" radius="+player.bladeTrace.radius);
            Assert.That(visible,Is.True,"Line of sight must not reject an unobstructed opponent.");
            player.RequestPlayerAttack(); player.Core.Tick(.25f);
            Assert.That(enemy.Core.Health,Is.EqualTo(100),"The opening part of the swing has not reached the opponent.");
            player.Core.Tick(.1f);
            Assert.That(enemy.Core.Health,Is.EqualTo(90));
            Assert.That(player.HasContactPoint,Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator SlowFrameSweepsAndMultipleCollidersDoNotDoubleHit()
        {
            PlaceEnemy(session.player.transform.position+Vector3.forward*1.2f);
            var extra=new GameObject("Additional hurt volume",typeof(CapsuleCollider));
            extra.transform.SetParent(session.enemy.transform,false); extra.transform.localPosition=Vector3.up;
            extra.GetComponent<CapsuleCollider>().height=2; extra.GetComponent<CapsuleCollider>().radius=.35f;
            Physics.SyncTransforms(); session.player.RequestPlayerAttack(); session.player.Core.Tick(1);
            Assert.That(session.enemy.Core.Health,Is.EqualTo(90));
            Assert.That(session.player.Core.State,Is.EqualTo(CombatState.Neutral));
            UnityEngine.Object.Destroy(extra); yield return null;
        }
        [UnityTest] public IEnumerator OldBroadBoxCannotHitOutsideTheBladeReach()
        {
            PlaceEnemy(session.player.transform.position+Vector3.forward*1.95f);
            session.player.RequestPlayerAttack(); session.Simulate(.8f);
            Assert.That(session.enemy.Core.Health,Is.EqualTo(100));
            yield return null;
        }
        [UnityTest] public IEnumerator WallBlocksBothDamageAndDeathblow()
        {
            PlaceEnemy(session.player.transform.position+Vector3.forward*1.2f);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position=session.player.transform.position+Vector3.forward*.7f+Vector3.up;
            wall.transform.localScale=new Vector3(3,2,.15f); Physics.SyncTransforms();
            session.player.RequestPlayerAttack(); session.player.Core.Tick(.8f);
            Assert.That(session.enemy.Core.Health,Is.EqualTo(100));
            BreakEnemy(); session.player.RequestPlayerAttack();
            Assert.That(session.enemy.Core.State,Is.EqualTo(CombatState.PostureBroken));
            UnityEngine.Object.Destroy(wall); yield return null; Physics.SyncTransforms();
            session.player.Core.Reset(); session.player.RequestPlayerAttack();
            Assert.That(session.enemy.Core.State,Is.EqualTo(CombatState.Dead));
        }
        void BreakEnemy()
        {
            var probe=new CombatCore(new CombatTuning {hitDamage=0,hitPosture=100});
            probe.RequestAttack(); probe.Tick(.25f); probe.TryHit(session.enemy.Core,false);
        }
        [UnityTest] public IEnumerator HeightAndRearFacingPreventInvalidExecution()
        {
            BreakEnemy(); PlaceEnemy(session.player.transform.position+Vector3.forward+Vector3.up*2);
            session.player.RequestPlayerAttack(); Assert.That(session.enemy.Core.State,Is.EqualTo(CombatState.PostureBroken));
            session.player.Core.Reset(); PlaceEnemy(session.player.transform.position-Vector3.forward);
            session.player.RequestPlayerAttack(); Assert.That(session.enemy.Core.State,Is.EqualTo(CombatState.PostureBroken));
            yield return null;
        }
        [UnityTest] public IEnumerator BladeContactUsesDefenderFacingForParry()
        {
            foreach(bool facing in new[]{true,false})
            {
                session.ResetRound(); PlaceEnemy(session.player.transform.position+Vector3.forward*1.2f);
                session.enemy.transform.rotation=Quaternion.Euler(0,facing?180:0,0); Physics.SyncTransforms();
                session.player.RequestPlayerAttack(); session.player.Core.Tick(.25f);
                session.enemy.Core.SetGuard(true,true); session.player.Core.Tick(.1f);
                Assert.That(session.enemy.Core.Health,Is.EqualTo(facing?100:90));
                Assert.That(session.player.Core.State,Is.EqualTo(facing?CombatState.DeflectedStun:CombatState.AttackRecovery));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator BakedBladeAgreesWithRenderedWeaponAndOutgoingPoseIsRetained()
        {
            var presentation=session.playerAnimation; var player=session.player;
            PlaceEnemy(player.transform.position+Vector3.forward*5);
            var blade=presentation.animator.GetBoneTransform(HumanBodyBones.RightHand).Find("Training Sword/Blade");
            player.RequestPlayerAttack(); session.Simulate(.25f);
            for(int i=0;i<10;i++)
            {
                var pose=player.bladeTrace.Sample(player.Core.StateProgress);
                Vector3 expected=player.transform.position+Quaternion.LookRotation(player.AttackForward)*pose.tip;
                Assert.That(Vector3.Distance(expected,blade.TransformPoint(Vector3.up*.5f)),Is.LessThan(.035f),"Rendered sword must follow its baked hit trajectory.");
                session.Simulate(.009f);
            }
            session.Simulate(.34f); double outgoing=presentation.AttackPoseTime;
            session.Simulate(.035f);
            Assert.That(player.Core.State,Is.EqualTo(CombatState.Neutral));
            Assert.That(presentation.AttackPoseTime,Is.EqualTo(outgoing),"Fading attack must not restart at frame zero.");
            yield return null;
        }
        [UnityTest] public IEnumerator MovingGuardKeepsLegsAnimatedAndInterruptRemovesUpperMask()
        {
            PlaceEnemy(session.player.transform.position+Vector3.forward*4);
            session.SubmitInput(Vector2.left,true,false,false);
            for(int i=0;i<20;i++)session.Simulate(1f/60);
            Assert.That(session.playerAnimation.LocomotionWeight,Is.GreaterThan(.7f));
            Assert.That(session.playerAnimation.UpperGuardWeight,Is.EqualTo(1));
            session.SubmitInput(Vector2.zero,false,false,true); session.Simulate(.1f);
            Assert.That(session.playerAnimation.UpperGuardWeight,Is.Zero);
            yield return null;
        }
    }
}


