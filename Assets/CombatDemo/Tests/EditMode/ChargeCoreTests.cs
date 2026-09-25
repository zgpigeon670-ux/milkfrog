using NUnit.Framework;
namespace Milkfrog.CombatDemo.Tests
{
    public class ChargeCoreTests
    {
        [Test] public void ShortTapStartsTheSlashImmediately()
        {
            var core=new CombatCore(new CombatTuning());core.BeginPreparation();core.Tick(.1f);core.ReleaseAttack();
            Assert.That(core.ActiveAttack.Kind,Is.EqualTo(AttackKind.Light));Assert.That(core.State,Is.EqualTo(CombatState.AttackStartup));
            core.Tick(.15f);Assert.That(core.State,Is.EqualTo(CombatState.AttackActive));Assert.That(core.AttackId,Is.EqualTo(1));
        }
        [TestCase(.25f,AttackKind.Light,false)]
        [TestCase(.46f,AttackKind.Thrust,true)]
        [TestCase(.70f,AttackKind.Thrust,true)]
        public void HoldingPastTheWindupChargesWithoutReleasingEarly(float held,AttackKind expected,bool preparing)
        {
            var core=new CombatCore(new CombatTuning());core.BeginPreparation();core.SetAttackHeld(true);core.Tick(held);
            Assert.That(core.IsPreparing,Is.EqualTo(preparing));
            if(preparing){core.ReleaseAttack();Assert.That(core.ActiveAttack.Kind,Is.EqualTo(expected));}
            else Assert.That(core.ActiveAttack.Kind,Is.EqualTo(expected));
        }
        [Test] public void FullChargeHoldsAndSnapshotsDamageInsteadOfReadingMutableDefinition()
        {
            var definition=AttackParameters.Thrust();var core=new CombatCore(new CombatTuning(),null,definition);
            core.BeginPreparation();core.SetAttackHeld(true);core.Tick(10);Assert.That(core.State,Is.EqualTo(CombatState.Charging));Assert.That(core.ChargeRatio,Is.EqualTo(1));
            core.ReleaseAttack();definition.maxDamage=99;core.Tick(.25f);
            var target=new CombatCore(new CombatTuning());core.TryHit(target,false);
            Assert.That(target.Health,Is.EqualTo(78));Assert.That(target.Posture,Is.EqualTo(28));
            core.Tick(1);core.RequestAttack();Assert.That(core.ActiveAttack.Damage,Is.EqualTo(10));
        }
        [TestCase(false)] [TestCase(true)]
        public void GuardAndDodgeCancelChargeButNotAReleasedSlash(bool charged)
        {
            var core=new CombatCore(new CombatTuning());core.BeginPreparation();core.SetAttackHeld(charged);core.Tick(charged?.5f:.05f);
            if(!charged)Assert.That(core.State,Is.EqualTo(CombatState.AttackStartup));
            else
            {
                core.SetGuard(true,true);Assert.That(core.State,Is.EqualTo(CombatState.Guard));Assert.That(core.DeflectOpen,Is.True);
                Assert.That(core.ReleaseAttack(),Is.False);Assert.That(core.ChargeRatio,Is.Zero);
                core.Reset();core.BeginPreparation();core.SetAttackHeld(true);core.Tick(.5f);Assert.That(core.RequestDodge(),Is.True);
                Assert.That(core.ReleaseAttack(),Is.False);Assert.That(core.ChargeRatio,Is.Zero);
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void ThrustCanBeBlockedOrDeflected(bool parry)
        {
            var core=new CombatCore(new CombatTuning());var target=new CombatCore(new CombatTuning());
            core.BeginPreparation();core.SetAttackHeld(true);core.Tick(.8f);core.ReleaseAttack();core.Tick(.25f);
            target.SetGuard(true,parry);Assert.That(core.TryHit(target,true),Is.EqualTo(parry?HitResult.Deflect:HitResult.Block));
            Assert.That(target.Health,Is.EqualTo(100));Assert.That(target.Posture,Is.EqualTo(parry?0:35));
            if(parry)Assert.That(core.State,Is.EqualTo(CombatState.DeflectedStun));
        }
        [Test] public void HitClearsChargeAndCannotReleaseAfterRecovery()
        {
            var target=new CombatCore(new CombatTuning());var attacker=new CombatCore(new CombatTuning());
            target.BeginPreparation();target.SetAttackHeld(true);target.Tick(.8f);attacker.RequestAttack();attacker.Tick(.25f);attacker.TryHit(target,false);
            Assert.That(target.ChargeRatio,Is.Zero);target.Tick(1);Assert.That(target.ReleaseAttack(),Is.False);
            Assert.That(target.AttackId,Is.Zero);target.Reset();Assert.That(target.ChargeElapsed,Is.Zero);
        }
        [Test] public void ReleasedThrustCannotBeCancelled()
        {
            var core=new CombatCore(new CombatTuning());core.BeginPreparation();core.SetAttackHeld(true);core.Tick(.8f);core.ReleaseAttack();
            core.SetGuard(true,true);core.CancelPreparation();Assert.That(core.RequestDodge(),Is.False);
            Assert.That(core.State,Is.EqualTo(CombatState.AttackStartup));
            Assert.That(core.ActiveAttack.Damage,Is.EqualTo(22).Within(.001));
        }
    }
}
