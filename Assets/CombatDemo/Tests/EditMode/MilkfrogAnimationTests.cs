using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Milkfrog.CombatDemo.Tests
{
    public class MilkfrogAnimationTests
    {
        const string Root = "Assets/CombatDemo/";
        [Test] public void AllMilkfrogActionsAreImportedAndMappedToTheirOwnClips()
        {
            var modelPath = Root + "Animations/milkfrog/milkfrog.fbx";
            Assert.That(((ModelImporter)AssetImporter.GetAtPath(modelPath)).animationType, Is.EqualTo(ModelImporterAnimationType.Generic));
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            Assert.That(clips.Length, Is.EqualTo(28));
            var profile = AssetDatabase.LoadAssetAtPath<CombatAnimationProfile>(Root + "Animations/MilkfrogCombat.asset");
            foreach (var field in typeof(CombatAnimationProfile).GetFields().Where(f => f.FieldType == typeof(AnimationClip)))
            {
                var clip = (AnimationClip)field.GetValue(profile);
                Assert.That(clip, Is.Not.Null, field.Name);
                Assert.That(clip.name, Is.EqualTo(field.Name == "attack" ? "light" : field.Name), field.Name);
                Assert.That(clip.frameRate, Is.EqualTo(24).Within(.01f));
            }
            foreach (string name in new[] { "walk", "jog", "walkBack", "jogBack", "walkLeft", "jogLeft", "walkRight", "jogRight", "fall" })
                Assert.That(clips.Single(c => c.name == name).isLooping, Is.True, name);
            foreach (string name in new[] { "light", "followup", "thrust", "death", "parry", "dodgeForward", "jump", "land" })
                Assert.That(clips.Single(c => c.name == name).isLooping, Is.False, name);
        }

        [Test] public void PlayerHasCenteredModelAndIndependentActualSwordTraces()
        {
            var instance = PrefabUtility.LoadPrefabContents(Root + "Prefabs/MVP/Player.prefab");
            try
            {
                var actor = instance.GetComponent<CombatActor>();
                var presenter = instance.GetComponent<CombatAnimationPresenter>();
                Assert.That(presenter.bladeRoot, Is.Not.Null); Assert.That(presenter.bladeTip, Is.Not.Null);
                Assert.That(presenter.swordTrail, Is.Not.Null);
                Assert.That(presenter.swordTrail.transform, Is.EqualTo(presenter.bladeTip));
                Assert.That(presenter.bladeRoot.parent.name, Is.EqualTo("sword"));
                Assert.That(presenter.bladeRoot.parent, Is.EqualTo(presenter.bladeTip.parent));
                Assert.That(presenter.animator.applyRootMotion, Is.False);
                Assert.That(presenter.animator.isHuman, Is.False);
                Assert.That(actor.lightAttack.trace, Is.Not.SameAs(actor.followupAttack.trace));
                Assert.That(actor.thrustAttack.trace, Is.Not.SameAs(actor.lightAttack.trace));
                foreach (var definition in new[] { actor.lightAttack, actor.thrustAttack, actor.followupAttack })
                {
                    Assert.That(definition.trace.IsValid, Is.True, definition.name);
                    Assert.That(definition.trace.definition, Is.EqualTo(definition));
                    Assert.That(definition.trace.poses.Length, Is.EqualTo(65));
                    foreach (var pose in definition.trace.poses)
                        Assert.That(Vector3.Distance(pose.root, pose.tip), Is.InRange(.5f, 1f));
                }
                presenter.profile.idle.SampleAnimation(presenter.animator.gameObject, 0);
                foreach (var foot in presenter.animator.GetComponentsInChildren<Transform>().Where(t => t.name == "foot.L" || t.name == "foot.R"))
                    Assert.That(instance.transform.InverseTransformPoint(foot.position).y, Is.InRange(0,.3f), foot.name);
                var head=presenter.animator.GetComponentsInChildren<Transform>().Single(t=>t.name=="spine.006");
                Assert.That(instance.transform.InverseTransformPoint(head.position).y,Is.InRange(1.4f,2f));
                Assert.That(Mathf.Abs(presenter.animator.transform.localPosition.x),Is.LessThan(.05f));
            }
            finally { PrefabUtility.UnloadPrefabContents(instance); }
        }
    }
}
