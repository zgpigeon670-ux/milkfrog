using System;
using UnityEngine;

namespace Milkfrog.CombatDemo
{
    [Serializable]
    public struct BladePose
    {
        public Vector3 root, tip;
        public static BladePose Lerp(BladePose a, BladePose b, float t) =>
            new BladePose { root = Vector3.Lerp(a.root, b.root, t), tip = Vector3.Lerp(a.tip, b.tip, t) };
    }

    [CreateAssetMenu(menuName = "Combat Demo/Baked Blade Trace")]
    public sealed class CombatBladeTrace : ScriptableObject
    {
        public CombatAnimationProfile sourceProfile;
        public CombatAttackDefinition definition;
        public AnimationClip bakedClip;
        public float bakedStart, bakedEnd;
        [Min(.01f)] public float radius = .10f;
        [Min(.01f)] public float spatialStep = .04f;
        public float engageDistance = 1.25f;
        public BladePose[] poses = Array.Empty<BladePose>();
        public bool IsValid => poses != null && poses.Length >= 2 && (definition != null ?
            bakedClip==definition.clip && Mathf.Approximately(bakedStart,definition.activeStart) && Mathf.Approximately(bakedEnd,definition.activeEnd) : sourceProfile != null &&
            bakedClip == sourceProfile.attack && Mathf.Approximately(bakedStart, sourceProfile.attackActiveStart) &&
            Mathf.Approximately(bakedEnd, sourceProfile.attackActiveEnd));

        public BladePose Sample(float progress)
        {
            float frame = Mathf.Clamp01(progress) * (poses.Length - 1);
            int index = Mathf.Min(Mathf.FloorToInt(frame), poses.Length - 2);
            return BladePose.Lerp(poses[index], poses[index + 1], frame - index);
        }
    }
}

