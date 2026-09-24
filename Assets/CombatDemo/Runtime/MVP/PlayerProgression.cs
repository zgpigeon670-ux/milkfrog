using System;

namespace Milkfrog.CombatDemo
{
    public enum GrowthStat { Vitality, Resolve, Power }

    // The snapshot is the durable source of truth; callers commit a proposed copy before applying it.
    public sealed class PlayerProgression
    {
        public const int MaximumRank = 10;
        public int Experience { get; private set; }
        public int Vitality { get; private set; }
        public int Resolve { get; private set; }
        public int Power { get; private set; }
        public int Level => 1 + Vitality + Resolve + Power;
        public int NextCost => 40 + 20 * (Level - 1);
        public float AttackMultiplier => 1f + .05f * Power;

        public void Restore(PlayerSnapshot snapshot)
        {
            Experience = snapshot?.experience ?? 0;
            Vitality = snapshot?.vitality ?? 0;
            Resolve = snapshot?.resolve ?? 0;
            Power = snapshot?.power ?? 0;
        }

        public void Grant(int amount)
        {
            if (amount < 0 || Experience > int.MaxValue - amount) throw new ArgumentOutOfRangeException(nameof(amount));
            Experience += amount;
        }

        public bool CanUpgrade(GrowthStat stat)
        {
            int rank = stat == GrowthStat.Vitality ? Vitality : stat == GrowthStat.Resolve ? Resolve : Power;
            return rank < MaximumRank && Experience >= NextCost;
        }

        public bool TryUpgrade(GrowthStat stat)
        {
            if (!CanUpgrade(stat)) return false;
            Experience -= NextCost;
            switch (stat)
            {
                case GrowthStat.Vitality: Vitality++; break;
                case GrowthStat.Resolve: Resolve++; break;
                case GrowthStat.Power: Power++; break;
                default: throw new ArgumentOutOfRangeException(nameof(stat));
            }
            return true;
        }

        public void WriteTo(PlayerSnapshot snapshot)
        {
            snapshot.experience = Experience;
            snapshot.vitality = Vitality;
            snapshot.resolve = Resolve;
            snapshot.power = Power;
        }
    }
}
