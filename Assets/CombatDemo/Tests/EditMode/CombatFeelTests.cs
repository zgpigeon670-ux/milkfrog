using NUnit.Framework;

namespace Milkfrog.CombatDemo.Tests
{
    public class CombatFeelTests
    {
        static AttackParameters Slash(bool combo = false) => new AttackParameters
        {
            kind = AttackKind.Light, startup = .25f, active = .10f, recovery = .35f, cancelWindow = .16f, CanComboFrom = combo, comboWindow = combo ? .18f : 0
        };
        static CombatCore Armed(AttackParameters followup = null, bool combo = false) => new CombatCore(new CombatTuning(), Slash(combo), AttackParameters.Thrust(), followup);

        [Test]
        public void RecoveryCanBeCancelledIntoGuardOrDodgeButActiveCannot()
        {
            var core = Armed();
            core.RequestAttack(); core.Tick(.25f);
            Assert.That(core.State, Is.EqualTo(CombatState.AttackActive));
            core.SetGuard(true, true);
            Assert.That(core.State, Is.EqualTo(CombatState.AttackActive));
            core.Tick(.10f);
            Assert.That(core.State, Is.EqualTo(CombatState.AttackRecovery));
            Assert.That(core.TryCancelRecovery(), Is.True);
            Assert.That(core.State, Is.EqualTo(CombatState.Guard));
            Assert.That(core.DeflectOpen, Is.False);

            core.Reset(); core.RequestAttack(); core.Tick(.36f);
            Assert.That(core.RequestDodge(), Is.True);
            Assert.That(core.State, Is.EqualTo(CombatState.Dodge));
            Assert.That(core.DodgeElapsed, Is.Zero);
        }

        [Test]
        public void LateRecoveryCannotCancel()
        {
            var core = Armed();
            core.RequestAttack(); core.Tick(.52f);
            Assert.That(core.State, Is.EqualTo(CombatState.AttackRecovery));
            Assert.That(core.CanCancelRecovery, Is.False);
            Assert.That(core.RequestDodge(), Is.False);
            Assert.That(core.State, Is.EqualTo(CombatState.AttackRecovery));
        }

        [Test]
        public void FollowupOnlyOpensAfterALightRecoveryAndExpires()
        {
            var followup = new AttackParameters { kind = AttackKind.Followup, startup = .16f, active = .10f, recovery = .38f, damage = 12, posture = 14, block = 22 };
            var core = Armed(followup, true);
            Assert.That(core.RequestFollowup(), Is.False);
            core.RequestAttack(); core.Tick(.70f);
            Assert.That(core.ComboOpen, Is.True);
            core.Tick(.18f);
            Assert.That(core.ComboOpen, Is.False);
            core.RequestAttack(); core.Tick(.70f);
            Assert.That(core.RequestFollowup(), Is.True);
            Assert.That(core.ActiveAttack.Kind, Is.EqualTo(AttackKind.Followup));
            Assert.That(core.ActiveAttack.Damage, Is.EqualTo(12));
        }

        [Test]
        public void CancellingRecoveryClosesTheComboWindow()
        {
            var core = Armed(new AttackParameters { kind = AttackKind.Followup }, true);
            core.RequestAttack(); core.Tick(.36f); core.RequestDodge(); core.Tick(.38f);
            Assert.That(core.RequestFollowup(), Is.False);
        }

        [Test]
        public void PerilousAttackCannotBeDeflectedOrBlocked()
        {
            var perilous = new AttackParameters { kind = AttackKind.Perilous, response = AttackResponse.DodgeOnly, startup = .20f, active = .10f, recovery = .20f, damage = 24, posture = 36, block = 48 };
            var attacker = new CombatCore(new CombatTuning(), perilous);
            var defender = new CombatCore(new CombatTuning());
            defender.SetGuard(true, true);
            attacker.RequestDefinedAttack(perilous); attacker.Tick(.20f);
            Assert.That(attacker.TryHit(defender, true), Is.EqualTo(HitResult.Hit));
            Assert.That(defender.Health, Is.EqualTo(76));
            Assert.That(defender.Posture, Is.EqualTo(36));
            Assert.That(attacker.State, Is.EqualTo(CombatState.AttackActive));
        }

        [Test]
        public void SlowDeflectUsesItsOwnPostureReward()
        {
            var slow = new AttackParameters { kind = AttackKind.Slow, startup = .20f, active = .10f, recovery = .20f, deflectPosture = 42 };
            var attacker = new CombatCore(new CombatTuning(), slow);
            var defender = new CombatCore(new CombatTuning());
            defender.SetGuard(true, true);
            attacker.RequestDefinedAttack(slow); attacker.Tick(.20f);
            Assert.That(attacker.TryHit(defender, true), Is.EqualTo(HitResult.Deflect));
            Assert.That(attacker.Posture, Is.EqualTo(42));
        }
    }
}
