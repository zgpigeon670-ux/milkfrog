using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Milkfrog.CombatDemo.Tests
{
    public class GameplayPolishTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void FullChargeIncludesPreparationAndDoesNotCommitUntilRelease(int fps)
        {
            var core = new CombatCore(new CombatTuning());
            core.SetAttackHeld(true); core.BeginPreparation();
            for (int i = 0; i < fps; i++) core.Tick(.8f / fps);
            Assert.That(core.State, Is.EqualTo(CombatState.Charging));
            Assert.That(core.ChargeRatio, Is.EqualTo(1).Within(.00001));
            Assert.That(core.AttackId, Is.Zero);
            core.Tick(5); Assert.That(core.AttackId, Is.Zero);
            Assert.That(core.ReleaseAttack(), Is.True);
            Assert.That(core.ActiveAttack.Damage, Is.EqualTo(22).Within(.00001));
        }

        [TestCase(.05f)] [TestCase(.17f)]
        public void AShortHeldTapCreditsItsPreparationAgainstSlashStartup(float held)
        {
            var core = new CombatCore(new CombatTuning());
            core.SetAttackHeld(true); core.BeginPreparation(); core.Tick(held);
            Assert.That(core.AttackId, Is.Zero);
            Assert.That(core.ReleaseAttack(), Is.True);
            Assert.That(core.ActiveAttack.Kind, Is.EqualTo(AttackKind.Light));
            Assert.That(core.Remaining, Is.EqualTo(.25f - held).Within(.00001));
            core.Tick(.25f - held);
            Assert.That(core.State, Is.EqualTo(CombatState.AttackActive));
        }

        [TestCase(false)] [TestCase(true)]
        public void ResetClearsComboAndHeldAttackFromBothRecoveryAndReady(bool completed)
        {
            var slash = new AttackParameters { CanComboFrom = true };
            var followup = new AttackParameters { kind = AttackKind.Followup };
            var core = new CombatCore(new CombatTuning(), slash, null, followup);
            core.SetAttackHeld(true); core.RequestAttack(); core.Tick(completed ? .70f : .36f);
            core.Reset(); Assert.That(core.ComboOpen, Is.False); Assert.That(core.RequestFollowup(), Is.False);
            core.BeginPreparation(); core.Tick(.01f);
            Assert.That(core.State, Is.EqualTo(CombatState.AttackStartup), "Reset must clear the previous hold");
        }

        [Test]
        public void RecoveryGuardCancelOpensNeitherComboNorDeflection()
        {
            var core = new CombatCore(new CombatTuning(), new AttackParameters { CanComboFrom = true });
            core.RequestAttack(); core.Tick(.36f); core.SetGuard(true, true);
            Assert.That(core.State, Is.EqualTo(CombatState.Guard));
            Assert.That(core.ComboOpen, Is.False); Assert.That(core.DeflectOpen, Is.False);
            core.SetGuard(false, false); core.SetGuard(true, true);
            Assert.That(core.DeflectOpen, Is.True, "A later fresh guard press still opens the normal window");
        }

        [Test]
        public void SlowFramesConsumeTheNewComboWindowAfterRecoveryEnds()
        {
            var core = new CombatCore(new CombatTuning(), new AttackParameters { CanComboFrom = true });
            core.RequestAttack(); core.Tick(1);
            Assert.That(core.State, Is.EqualTo(CombatState.Neutral)); Assert.That(core.ComboOpen, Is.False);
        }

#if UNITY_EDITOR
        [Test]
        public void AuthoredPlayerThrustUsesItsIndependentMotionAndTrace()
        {
            var thrust = AssetDatabase.LoadAssetAtPath<CombatAttackDefinition>("Assets/CombatDemo/Settings/PlayerThrust.asset");
            var settings = AssetDatabase.LoadAssetAtPath<CombatDemoSettings>("Assets/CombatDemo/Settings/AnimatedCombat.asset");
            Assert.That(settings.player.prepareThreshold, Is.EqualTo(.18f));
            Assert.That(thrust.clip.name, Is.EqualTo("Sword_Thrust"));
            Assert.That(thrust.rules.startup, Is.EqualTo(.12f)); Assert.That(thrust.rules.active, Is.EqualTo(.12f));
            Assert.That(thrust.rules.recovery, Is.EqualTo(.42f));
            Assert.That(thrust.trace.bakedClip, Is.EqualTo(thrust.clip)); Assert.That(thrust.trace.IsValid, Is.True);
            Assert.That(thrust.trace.bakedStart, Is.EqualTo(thrust.activeStart)); Assert.That(thrust.trace.bakedEnd, Is.EqualTo(thrust.activeEnd));
        }
#endif
    }
}
