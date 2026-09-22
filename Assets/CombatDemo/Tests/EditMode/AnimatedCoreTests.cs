using NUnit.Framework;
using UnityEngine;
namespace Milkfrog.CombatDemo.Tests
{
    public class AnimatedCoreTests
    {
        [Test]
        public void HitStopIsBoundedAndConsumesOnlyRealTime()
        {
            var clock = new HitStopClock(); clock.Request(.04f); clock.Request(.04f);
            Assert.That(clock.Remaining, Is.EqualTo(.04f).Within(.00001));
            Assert.That(clock.Consume(.025f), Is.Zero);
            Assert.That(clock.Consume(.025f), Is.EqualTo(.01f).Within(.00001));
            Assert.That(clock.Remaining, Is.Zero);
            clock.Request(5); Assert.That(clock.Remaining, Is.EqualTo(.15f));
            clock.Reset(); Assert.That(clock.Remaining, Is.Zero);
        }
        [Test]
        public void AnimationPhaseMappingUsesCombatProgressAndResets()
        {
            var profile = ScriptableObject.CreateInstance<CombatAnimationProfile>();
            var clip = new AnimationClip(); clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0,0,2,0)); profile.attack = clip;
            var core = new CombatCore(new CombatTuning()); core.RequestAttack(); core.Tick(.125f);
            Assert.That(core.StateProgress, Is.EqualTo(.5f).Within(.001));
            Assert.That(profile.AttackTime(core), Is.EqualTo(.35f).Within(.001));
            core.Tick(.125f); Assert.That(profile.AttackTime(core), Is.EqualTo(.7f).Within(.001));
            core.Reset(); Assert.That(core.StateDuration, Is.Zero); Assert.That(core.StateProgress, Is.Zero);
            Object.DestroyImmediate(profile); Object.DestroyImmediate(clip);
        }
    }
}
