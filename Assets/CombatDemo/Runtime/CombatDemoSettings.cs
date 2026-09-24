using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public enum AttackPattern { Slash, Slow, Perilous }

    [CreateAssetMenu(menuName = "Combat Demo/Settings")]
    public sealed class CombatDemoSettings : ScriptableObject
    {
        public CombatTuning player = new CombatTuning();
        public CombatTuning enemy = new CombatTuning { startup = .45f, recovery = .45f };
        public float playerSpeed = 4, enemySpeed = 2.5f;
        public float enemyWait = 1.35f, counterDelay = .10f;
        public int blocksBeforeDeflect = 2;
        public bool logEvents = true;
        public AttackPattern[] enemyPattern = { AttackPattern.Slash, AttackPattern.Slow, AttackPattern.Perilous };
        public bool usePatternInDuel;
        public bool randomAttacks;
        public float slashWeight = 5, slowWeight = 3, perilousWeight = 2;
    }
}
