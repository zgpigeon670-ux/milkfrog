using NUnit.Framework;
namespace Milkfrog.CombatDemo.Tests
{
    public class DodgeCoreTests
    {
        [Test] public void InvulnerabilityIsHalfOpenAndDoesNotConsumeHitDeduplication()
        {
            var attacker=new CombatCore(new CombatTuning{active=.5f}); var target=new CombatCore(new CombatTuning());
            attacker.RequestAttack();attacker.Tick(.25f);target.RequestDodge();
            target.Tick(.079f);Assert.That(target.IsInvulnerable,Is.False);
            target.Tick(.001f);Assert.That(target.IsInvulnerable,Is.True);
            Assert.That(attacker.TryHit(target,false),Is.EqualTo(HitResult.Ignore));
            Assert.That(target.Health,Is.EqualTo(100));Assert.That(target.Posture,Is.Zero);
            target.Tick(.08f);Assert.That(target.IsInvulnerable,Is.False);
            Assert.That(attacker.TryHit(target,false),Is.EqualTo(HitResult.Hit));
            Assert.That(target.State,Is.EqualTo(CombatState.HitStun));
        }
        [Test] public void DodgeRejectsActionsAndLargeStepCompletesItsWholeInterval()
        {
            var core=new CombatCore(new CombatTuning());float travelled=0;
            core.DodgeInterval+=(a,b)=>travelled+=b-a;
            Assert.That(core.RequestDodge(),Is.True);Assert.That(core.RequestDodge(),Is.False);
            Assert.That(core.RequestAttack(),Is.False);core.SetGuard(true,true);Assert.That(core.State,Is.EqualTo(CombatState.Dodge));
            core.Tick(1);Assert.That(travelled,Is.EqualTo(.38f).Within(.00001f));
            Assert.That(core.State,Is.EqualTo(CombatState.Guard));Assert.That(core.DeflectOpen,Is.False);
            core.Reset();Assert.That(core.DodgeProgress,Is.Zero);Assert.That(core.IsInvulnerable,Is.False);
        }
    }
}
