using NUnit.Framework;
using UnityEngine;
namespace Milkfrog.CombatDemo.Tests
{
    public class BladeIntervalTests
    {
        [Test] public void SlowStepVisitsEntireActiveIntervalBeforeRecovery()
        {
            var core=new CombatCore(new CombatTuning()); float from=-1,to=-1;
            core.ActiveInterval+=(a,b)=>{from=a;to=b;Assert.That(core.State,Is.EqualTo(CombatState.AttackActive));};
            core.RequestAttack(); core.Tick(1);
            Assert.That(from,Is.Zero); Assert.That(to,Is.EqualTo(1)); Assert.That(core.State,Is.EqualTo(CombatState.Neutral));
        }
        [Test] public void IntervalDeflectionCannotBeOverwrittenByOldRecovery()
        {
            var attacker=new CombatCore(new CombatTuning()); var defender=new CombatCore(new CombatTuning());
            defender.SetGuard(true,true); int calls=0;
            attacker.ActiveInterval+=(a,b)=>{calls++;attacker.TryHit(defender,true);};
            attacker.RequestAttack(); attacker.Tick(1);
            Assert.That(attacker.State,Is.EqualTo(CombatState.DeflectedStun));
            Assert.That(attacker.Remaining,Is.EqualTo(.2f).Within(.0001));
            attacker.Tick(.1f); Assert.That(calls,Is.EqualTo(1)); Assert.That(defender.Health,Is.EqualTo(100));
        }
    }
}
