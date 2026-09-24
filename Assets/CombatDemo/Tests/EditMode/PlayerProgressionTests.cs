using System;
using Milkfrog.CombatDemo;
using NUnit.Framework;

namespace Milkfrog.CombatDemo.Tests
{
    public sealed class PlayerProgressionTests
    {
        [Test]
        public void NewSnapshotStartsAtLevelOneWithFirstPointCostForty()
        {
            var progression = new PlayerProgression();
            progression.Restore(new PlayerSnapshot());

            Assert.That(progression.Experience, Is.Zero);
            Assert.That(progression.Level, Is.EqualTo(1));
            Assert.That(progression.NextCost, Is.EqualTo(40));
            Assert.That(progression.AttackMultiplier, Is.EqualTo(1f));
            Assert.That(progression.TryUpgrade(GrowthStat.Vitality), Is.False);
        }

        [Test]
        public void UpgradeCostGrowsWithLevelAndConsumesExperienceOnlyOnSuccess()
        {
            var progression = new PlayerProgression();
            progression.Restore(new PlayerSnapshot());
            progression.Grant(99);

            Assert.That(progression.NextCost, Is.EqualTo(40));
            Assert.That(progression.TryUpgrade(GrowthStat.Vitality), Is.True);
            Assert.That(progression.Experience, Is.EqualTo(59));
            Assert.That(progression.Level, Is.EqualTo(2));
            Assert.That(progression.NextCost, Is.EqualTo(60));

            Assert.That(progression.TryUpgrade(GrowthStat.Resolve), Is.False);
            Assert.That(progression.Experience, Is.EqualTo(59));
            progression.Grant(1);
            Assert.That(progression.TryUpgrade(GrowthStat.Resolve), Is.True);
            Assert.That(progression.Experience, Is.Zero);
            Assert.That(progression.Level, Is.EqualTo(3));
            Assert.That(progression.NextCost, Is.EqualTo(80));
        }

        [Test]
        public void EachStatCapsAtTenAndPowerRaisesMultiplierToOnePointFive()
        {
            var progression = new PlayerProgression();
            progression.Restore(new PlayerSnapshot { experience = 1300 });

            for (int i = 0; i < PlayerProgression.MaximumRank; i++)
                Assert.That(progression.TryUpgrade(GrowthStat.Power), Is.True, "power rank " + (i + 1));

            Assert.That(progression.Power, Is.EqualTo(10));
            Assert.That(progression.AttackMultiplier, Is.EqualTo(1.5f).Within(.0001f));
            Assert.That(progression.Level, Is.EqualTo(11));
            Assert.That(progression.NextCost, Is.EqualTo(240));
            Assert.That(progression.TryUpgrade(GrowthStat.Power), Is.False);

            progression = new PlayerProgression();
            progression.Restore(new PlayerSnapshot { experience = 1300 });
            for (int i = 0; i < PlayerProgression.MaximumRank; i++)
                Assert.That(progression.TryUpgrade(GrowthStat.Vitality), Is.True);
            Assert.That(progression.Vitality, Is.EqualTo(10));
            Assert.That(progression.TryUpgrade(GrowthStat.Vitality), Is.False);

            progression = new PlayerProgression();
            progression.Restore(new PlayerSnapshot { experience = 1300 });
            for (int i = 0; i < PlayerProgression.MaximumRank; i++)
                Assert.That(progression.TryUpgrade(GrowthStat.Resolve), Is.True);
            Assert.That(progression.Resolve, Is.EqualTo(10));
            Assert.That(progression.TryUpgrade(GrowthStat.Resolve), Is.False);
        }

        [Test]
        public void WriteToCopiesGrowthWithoutChangingOtherSnapshotFields()
        {
            var snapshot = new PlayerSnapshot { experience = 100, vitality = 1, position = new UnityEngine.Vector3(3, 4, 5) };
            var progression = new PlayerProgression();
            progression.Restore(snapshot);
            progression.Grant(50);
            Assert.That(progression.TryUpgrade(GrowthStat.Power), Is.True);

            var output = new PlayerSnapshot { position = new UnityEngine.Vector3(9, 8, 7), health = 42 };
            progression.WriteTo(output);

            Assert.That(output.experience, Is.EqualTo(90));
            Assert.That(output.vitality, Is.EqualTo(1));
            Assert.That(output.resolve, Is.Zero);
            Assert.That(output.power, Is.EqualTo(1));
            Assert.That(output.position, Is.EqualTo(new UnityEngine.Vector3(9, 8, 7)));
            Assert.That(output.health, Is.EqualTo(42));
        }

        [Test]
        public void GrantRejectsNegativeExperience()
        {
            var progression = new PlayerProgression();
            Assert.Throws<ArgumentOutOfRangeException>(() => progression.Grant(-1));
        }
    }
}
