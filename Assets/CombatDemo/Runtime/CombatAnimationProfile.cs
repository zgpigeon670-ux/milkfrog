using UnityEngine;

namespace Milkfrog.CombatDemo
{
    [CreateAssetMenu(menuName = "Combat Demo/Animation Profile")]
    public sealed class CombatAnimationProfile : ScriptableObject
    {
        public AnimationClip idle, walk, jog, attack, hit, death, guard, parry, deflected, broken;
        public AnimationClip walkBack, jogBack, walkLeft, jogLeft, walkRight, jogRight;
        public AnimationClip dodgeForward, dodgeBack, dodgeLeft, dodgeRight, charge, thrust;
        public AnimationClip jump, fall, land;
        [Range(0, 1)] public float attackActiveStart = .35f;
        [Range(0, 1)] public float attackActiveEnd = .55f;
        [Min(0)] public float blendTime = .07f;
        public float walkSpeed = 1.5f, jogSpeed = 4f;
        public Vector4 walkStrideLengths = new Vector4(1.2f,1.2f,1.2f,1.2f);
        public Vector4 jogStrideLengths = new Vector4(1.8f,1.8f,1.8f,1.8f);
        public float AttackTime(CombatCore core)
        {
            float a = 0, b = attackActiveStart;
            if (core.State == CombatState.AttackActive) { a = attackActiveStart; b = attackActiveEnd; }
            if (core.State == CombatState.AttackRecovery) { a = attackActiveEnd; b = 1; }
            return Mathf.Lerp(a, b, core.StateProgress) * attack.length;
        }
    }
}
