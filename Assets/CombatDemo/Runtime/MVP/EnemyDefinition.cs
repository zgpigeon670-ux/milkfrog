using UnityEngine;

namespace Milkfrog.CombatDemo
{
    [CreateAssetMenu(menuName = "Combat Demo/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public string displayName = "Swordsman";
        public CombatTuning tuning = new CombatTuning();
        public CombatAttackDefinition slash;
        public float speed = 2.5f, wait = 1.35f, counterDelay = .1f;
        public int blocksBeforeDeflect = 2;
        public float alertRadius = 6, patrolRadius = 3, leashMargin = 12;
        public float patrolPause = 1, bossTriggerRadius = 8;
    }
}
