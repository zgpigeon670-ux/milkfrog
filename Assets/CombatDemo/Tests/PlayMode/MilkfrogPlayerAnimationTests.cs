using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Milkfrog.CombatDemo.Tests
{
    public class MilkfrogPlayerAnimationTests
    {
        GameObject player, target, floor;
        CombatActor actor, enemy;
        CombatAnimationPresenter presenter;
        [UnitySetUp] public IEnumerator Setup()
        {
            var fixture=SceneManager.CreateScene("MilkfrogPlayerFixture-"+System.Guid.NewGuid());SceneManager.SetActiveScene(fixture);
#if UNITY_EDITOR
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CombatDemo/Prefabs/MVP/Player.prefab"));
#endif
            actor = player.GetComponent<CombatActor>();
            // This isolated fixture exercises the real prefab without world AI or camera input.
            Object.Destroy(player.GetComponent<CombatActorView>());
            target = new GameObject("Milkfrog blade target", typeof(CharacterController), typeof(CombatActor));
            enemy = target.GetComponent<CombatActor>();
            var controller = target.GetComponent<CharacterController>(); controller.height = 2; controller.radius = .4f; controller.center = Vector3.up;
            enemy.Initialize(new CombatTuning()); actor.Initialize(new CombatTuning());
            presenter = player.GetComponent<CombatAnimationPresenter>(); presenter.Initialize();
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Milkfrog test floor"; floor.transform.localScale = new Vector3(20,.2f,20); floor.transform.position = Vector3.down*.1f;
            actor.transform.position = Vector3.up*.02f; PlaceTarget(new Vector3(0,.02f,1.05f));
            Physics.SyncTransforms(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { Object.Destroy(player); Object.Destroy(target); Object.Destroy(floor); yield return null; }
        void PlaceTarget(Vector3 position)
        { enemy.Motor.enabled = false; target.transform.position = position; enemy.Motor.enabled = true; Physics.SyncTransforms(); }
        void Step(float dt)
        { actor.Core.Tick(dt); presenter.AdvanceVisual(dt); }
        void StartAttack(AttackKind kind)
        {
            actor.Core.Reset(); presenter.ResetVisuals(); enemy.Core.Reset();
            if (kind == AttackKind.Followup)
            {
                actor.Core.RequestAttack(); actor.Core.Tick(.7f); enemy.Core.Reset();
                Assert.That(actor.Core.RequestFollowup(), Is.True);
            }
            else if (kind == AttackKind.Thrust)
            {
                actor.Core.SetAttackHeld(true); Assert.That(actor.Core.BeginPreparation(), Is.True);
                actor.Core.Tick(.8f); actor.Core.ReleaseAttack();
            }
            else Assert.That(actor.Core.RequestAttack(), Is.True);
        }

        [UnityTest] public IEnumerator ActualSwordMatchesEachBakedTraceAtThirtySixtyAndOneTwentyFps()
        {
            PlaceTarget(Vector3.forward*5);
            foreach (int fps in new[] {30,60,120}) foreach (var kind in new[] {AttackKind.Light,AttackKind.Thrust,AttackKind.Followup})
            {
                StartAttack(kind);
                int samples = 0;
                for (int i=0;i<fps && actor.Core.IsAttacking;i++)
                {
                    Step(1f/fps);
                    if(actor.Core.State != CombatState.AttackActive) continue;
                    var pose=actor.CurrentAttackDefinition.trace.Sample(actor.Core.StateProgress);
                    var facing=Quaternion.LookRotation(actor.AttackForward);
                    Assert.That(Vector3.Distance(actor.transform.position+facing*pose.tip,presenter.bladeTip.position),Is.LessThan(.025f),kind+" tip @"+fps);
                    Assert.That(Vector3.Distance(actor.transform.position+facing*pose.root,presenter.bladeRoot.position),Is.LessThan(.025f),kind+" root @"+fps);
                    samples++;
                }
                Assert.That(samples,Is.GreaterThan(0));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator EachAttackHitsOnceAndWallOcclusionPreventsDamage()
        {
            foreach (int fps in new[] {30,60,120}) foreach (var kind in new[] {AttackKind.Light,AttackKind.Thrust,AttackKind.Followup})
            {
                StartAttack(kind); int hits=0;
                System.Action<HitEvent> count=hit=>{if(hit.Attacker==actor.Core && hit.Result==HitResult.Hit)hits++;};
                actor.Core.HitResolved+=count;
                for(int i=0;i<fps && actor.Core.IsAttacking;i++)Step(1f/fps);
                actor.Core.HitResolved-=count;
                Assert.That(hits,Is.EqualTo(1),kind+" @"+fps);
            }
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position=new Vector3(0,1,.5f);wall.transform.localScale=new Vector3(3,3,.1f);Physics.SyncTransforms();
            foreach(var kind in new[]{AttackKind.Light,AttackKind.Thrust,AttackKind.Followup})
            {
                StartAttack(kind); for(int i=0;i<120 && actor.Core.IsAttacking;i++)Step(1f/120);
                Assert.That(enemy.Core.Health,Is.EqualTo(enemy.Core.Tuning.maxHealth),kind+" through wall");
            }
            Object.Destroy(wall);yield return null;
        }

        [UnityTest] public IEnumerator GenericGuardMasksOnlyUpperBodyAndLegsKeepMoving()
        {
            PlaceTarget(Vector3.forward*5);
            var mask=(AvatarMask)typeof(CombatAnimationPresenter).GetField("guardMask",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(presenter);
            bool upper=false,lower=false;
            for(int i=0;i<mask.transformCount;i++)
            {
                string path=mask.GetTransformPath(i);
                if(path.EndsWith("upper_arm.R")){Assert.That(mask.GetTransformActive(i),Is.True);upper=true;}
                if(path.EndsWith("thigh.L")){Assert.That(mask.GetTransformActive(i),Is.False);lower=true;}
            }
            Assert.That(upper && lower,Is.True);
            actor.Core.SetGuard(true,false);
            var thigh=presenter.animator.GetComponentsInChildren<Transform>().Single(t=>t.name=="thigh.L");
            var arm=presenter.animator.GetComponentsInChildren<Transform>().Single(t=>t.name=="upper_arm.R");
            Quaternion before=thigh.localRotation,armGuard=Quaternion.identity;
            for(int i=0;i<24;i++)
            {
                actor.Move(Vector3.forward,1.5f,1f/60);presenter.AdvanceVisual(1f/60);
                if(i==12)armGuard=arm.localRotation;
            }
            Assert.That(presenter.LocomotionWeight,Is.GreaterThan(.8f));
            Assert.That(Quaternion.Angle(before,thigh.localRotation),Is.GreaterThan(1));
            Assert.That(Quaternion.Angle(armGuard,arm.localRotation),Is.LessThan(.05f));yield return null;
        }

        [UnityTest] public IEnumerator ChargeDodgeAndResetUseCorrectPoseAndClearTrail()
        {
            actor.Core.SetAttackHeld(true);actor.Core.BeginPreparation();Step(.4f);
            Assert.That(presenter.CurrentPoseClip,Is.EqualTo(presenter.profile.charge));
            actor.Core.CancelPreparation();actor.RequestDodge(Vector3.left);Step(actor.Core.Tuning.dodgeDuration*.9f);
            Assert.That(presenter.swordTrail.emitting,Is.False);
            presenter.swordTrail.emitting=true;presenter.swordTrail.AddPosition(Vector3.one);actor.ResetActor();presenter.ResetVisuals();
            Assert.That(presenter.swordTrail.emitting,Is.False);Assert.That(presenter.swordTrail.positionCount,Is.Zero);yield return null;
        }

        [UnityTest] public IEnumerator DodgeInterruptsTheDeathblowVisualImmediately()
        {
            enemy.Core.RestoreVitals(100,95);
            actor.Core.RequestAttack();actor.Core.Tick(actor.Core.ActiveAttack.Startup);
            actor.Core.TryHit(enemy.Core,false);actor.Core.Reset();
            Assert.That(enemy.Core.State,Is.EqualTo(CombatState.PostureBroken));
            Assert.That(actor.Core.TryDeathblow(enemy.Core,true,1.05f,true),Is.True);
            presenter.AdvanceVisual(.01f);
            Assert.That(presenter.CurrentPoseClip,Is.EqualTo(presenter.profile.attack));
            Assert.That(actor.RequestDodge(Vector3.left),Is.True);Step(.08f);
            Assert.That(presenter.CurrentPoseClip,Is.EqualTo(presenter.profile.dodgeForward));
            Assert.That(presenter.swordTrail.emitting,Is.False);yield return null;
        }
    }
}
