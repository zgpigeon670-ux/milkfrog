using NUnit.Framework;
using UnityEngine;

namespace Milkfrog.CombatDemo.Tests
{
    public class CombatCoreTests
    {
        static CombatCore Actor(CombatTuning tuning = null) => new CombatCore(tuning ?? new CombatTuning());
        static void Active(CombatCore core) { Assert.That(core.RequestAttack(), Is.True); core.Tick(core.Tuning.startup + .00001f); }
        static HitResult Strike(CombatCore attacker, CombatCore defender, bool front = true)
        { Active(attacker); return attacker.TryHit(defender, front); }

        [Test]
        public void AttackRejectsRepeatedAttackAndGuard()
        {
            var a = Actor();
            a.RequestAttack();
            Assert.That(a.RequestAttack(), Is.False);
            a.SetGuard(true, true);
            Assert.That(a.State, Is.EqualTo(CombatState.AttackStartup));
            a.Tick(.251f);
            Assert.That(a.State, Is.EqualTo(CombatState.AttackActive));
            Assert.That(a.RequestAttack(), Is.False);
            a.Tick(.11f);
            Assert.That(a.State, Is.EqualTo(CombatState.AttackRecovery));
            a.Tick(.5f);
            Assert.That(a.State, Is.EqualTo(CombatState.Guard));
            Assert.That(a.DeflectOpen, Is.False, "Blocked input must not create a delayed parry.");
        }

        [Test]
        public void SlowFrameStillSamplesActiveAndFinishesAttack()
        {
            var a = Actor(); var b = Actor(); int samples = 0;
            a.ActiveSample += () => { samples++; a.TryHit(b, true); };
            a.RequestAttack(); a.Tick(1);
            Assert.That(samples, Is.GreaterThanOrEqualTo(1));
            Assert.That(b.Health, Is.EqualTo(90));
            Assert.That(a.State, Is.EqualTo(CombatState.Neutral));
        }

        [Test]
        public void OneAttackHitsEachTargetOnlyOnce()
        {
            var a = Actor(); var b = Actor(); Active(a);
            Assert.That(a.TryHit(b, true), Is.EqualTo(HitResult.Hit));
            for (int i = 0; i < 5; i++) Assert.That(a.TryHit(b, true), Is.EqualTo(HitResult.Ignore));
            Assert.That(b.Health, Is.EqualTo(90));
        }

        [Test]
        public void GuardBlocksWithoutHealthDamage()
        {
            var a = Actor(); var b = Actor(); b.SetGuard(true, false);
            Assert.That(Strike(a, b), Is.EqualTo(HitResult.Block));
            Assert.That(b.Health, Is.EqualTo(100)); Assert.That(b.Posture, Is.EqualTo(20));
            Assert.That(b.State, Is.EqualTo(CombatState.Guard));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RearAttackBypassesBothGuardAndDeflect(bool window)
        {
            var a = Actor(); var b = Actor(); b.SetGuard(true, window);
            Assert.That(Strike(a, b, false), Is.EqualTo(HitResult.Hit));
            Assert.That(b.Health, Is.EqualTo(90));
        }

        [Test]
        public void SuccessfulDeflectInterruptsAttackerAndPreservesDefender()
        {
            var a = Actor(); var b = Actor(); b.SetGuard(true, true);
            Assert.That(Strike(a, b), Is.EqualTo(HitResult.Deflect));
            Assert.That(a.State, Is.EqualTo(CombatState.DeflectedStun));
            Assert.That(a.Posture, Is.EqualTo(30));
            Assert.That(b.Health, Is.EqualTo(100)); Assert.That(b.Posture, Is.Zero);
            Assert.That(a.TryHit(b, true), Is.EqualTo(HitResult.Ignore));
            Assert.That(b.RequestAttack(), Is.True);
        }

        [TestCase(.149f, HitResult.Deflect)]
        [TestCase(.150f, HitResult.Block)]
        [TestCase(.151f, HitResult.Block)]
        public void DeflectWindowHasExclusiveEnd(float elapsed, HitResult expected)
        {
            var a = Actor(); var b = Actor(); b.SetGuard(true, true); b.Tick(elapsed);
            Assert.That(Strike(a, b), Is.EqualTo(expected));
        }

        [Test]
        public void HeldGuardCannotRefreshWindowButReleaseAndPressCan()
        {
            var b = Actor(); b.SetGuard(true, true); b.Tick(.2f);
            b.SetGuard(true, true); Assert.That(b.DeflectOpen, Is.False);
            b.SetGuard(false, false); b.SetGuard(true, true); Assert.That(b.DeflectOpen, Is.True);
        }

        [Test]
        public void InterruptedAttackCannotDeliverLateDamage()
        {
            var a = Actor(); var b = Actor(); a.RequestAttack();
            Strike(b, a); Assert.That(a.State, Is.EqualTo(CombatState.HitStun));
            int samples = 0; a.ActiveSample += () => samples++;
            a.Tick(2);
            Assert.That(samples, Is.Zero); Assert.That(a.State, Is.EqualTo(CombatState.Neutral));
        }

        [Test]
        public void DeflectCanBreakAttackerInsteadOfOrdinaryStun()
        {
            var a = Actor(new CombatTuning { maxPosture = 30 }); var b = Actor(); b.SetGuard(true, true);
            Strike(a, b); Assert.That(a.State, Is.EqualTo(CombatState.PostureBroken));
        }

        [Test]
        public void DeathWinsOverPostureBreakAndOldAttack()
        {
            var a = Actor(new CombatTuning { maxHealth = 10, maxPosture = 10 });
            a.RequestAttack(); Strike(Actor(), a);
            Assert.That(a.State, Is.EqualTo(CombatState.Dead));
            a.Tick(10); a.SetGuard(true, true);
            Assert.That(a.State, Is.EqualTo(CombatState.Dead)); Assert.That(a.RequestAttack(), Is.False);
        }

        [Test]
        public void BrokenHitsDoNotRenewTimerAndBreakExpiresToZeroPosture()
        {
            var b = Actor(new CombatTuning { maxPosture = 10 }); Strike(Actor(), b);
            Assert.That(b.State, Is.EqualTo(CombatState.PostureBroken));
            b.Tick(.5f); Strike(Actor(), b);
            Assert.That(b.Remaining, Is.EqualTo(1.5f).Within(.001f));
            b.Tick(1.51f);
            Assert.That(b.State, Is.EqualTo(CombatState.Neutral)); Assert.That(b.Posture, Is.Zero);
        }

        [Test]
        public void RecoveryWaitsForDelayAndOnlyRunsWhileReady()
        {
            var a = Actor(); var b = Actor(); b.SetGuard(true, false); Strike(a, b);
            b.Tick(1.9f); Assert.That(b.Posture, Is.EqualTo(20));
            b.Tick(.2f); Assert.That(b.Posture, Is.EqualTo(18.5f).Within(.001f));
            b.RequestAttack(); b.Tick(.4f); Assert.That(b.Posture, Is.EqualTo(18.5f).Within(.001f));
        }

        [Test]
        public void DeathblowRequiresPlayerReadyFacingAndRange()
        {
            var a = Actor(); var b = Actor(new CombatTuning { maxPosture = 10 }); Strike(Actor(), b);
            Assert.That(a.TryDeathblow(b, false, 1, true), Is.False);
            Assert.That(a.TryDeathblow(b, true, 2.01f, true), Is.False);
            Assert.That(a.TryDeathblow(b, true, 1, false), Is.False);
            a.RequestAttack(); Assert.That(a.TryDeathblow(b, true, 1, true), Is.False); a.Tick(1);
            Assert.That(a.TryDeathblow(b, true, 2, true), Is.True);
            Assert.That(b.State, Is.EqualTo(CombatState.Dead));
            Assert.That(a.TryDeathblow(b, true, 1, true), Is.False);
        }

        [Test]
        public void ResetClearsDeathGuardWindowAttackAndResources()
        {
            var a = Actor(new CombatTuning { maxHealth = 10 }); a.RequestAttack(); Strike(Actor(), a);
            a.SetGuard(true, true); a.Reset();
            Assert.That(a.State, Is.EqualTo(CombatState.Neutral)); Assert.That(a.Health, Is.EqualTo(10));
            Assert.That(a.Posture, Is.Zero); Assert.That(a.AttackId, Is.Zero); Assert.That(a.DeflectRemaining, Is.Zero);
            a.Tick(1); Assert.That(a.State, Is.EqualTo(CombatState.Neutral));
        }

        [Test]
        public void FacingUsesHorizontalFullAngle()
        {
            Assert.That(CombatActor.IsInFront(Vector3.forward, new Vector3(0, 10, 1), 120), Is.True);
            Assert.That(CombatActor.IsInFront(Vector3.forward, Vector3.back, 120), Is.False);
            Assert.That(CombatActor.IsInFront(Vector3.forward, Quaternion.Euler(0, 59, 0) * Vector3.forward, 120), Is.True);
            Assert.That(CombatActor.IsInFront(Vector3.forward, Quaternion.Euler(0, 61, 0) * Vector3.forward, 120), Is.False);
        }

        [Test]
        public void PoliciesCannotParryOutsideGuardOrFromBehind()
        {
            var b = Actor(); b.DeflectPolicy = () => true;
            Assert.That(Strike(Actor(), b), Is.EqualTo(HitResult.Hit));
            b.Reset(); b.SetGuard(true, false);
            Assert.That(Strike(Actor(), b, false), Is.EqualTo(HitResult.Hit));
        }
    }
}
