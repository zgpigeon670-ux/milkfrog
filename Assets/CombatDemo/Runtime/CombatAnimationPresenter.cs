using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Milkfrog.CombatDemo
{
    public sealed class CombatAnimationPresenter : MonoBehaviour
    {
        public CombatActor actor;
        public Animator animator;
        public CombatAnimationProfile profile;
        public TrailRenderer swordTrail;
        public float SampledAttackTime { get; private set; }
        public float LastVisualDelta { get; private set; }
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] tracks;
        AnimationClip[] clips;
        readonly float[] weights = new float[10];
        float loopTime, reactionTime, deathTime, finisherTime = -1;
        Vector3 lastPosition, modelPosition;
        Quaternion modelRotation;
        bool initialized;

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            modelPosition = animator.transform.localPosition;
            modelRotation = animator.transform.localRotation;
            clips = new[] { profile.idle, profile.walk, profile.jog, profile.attack, profile.hit,
                profile.death, profile.guard, profile.parry, profile.deflected, profile.broken };
            graph = PlayableGraph.Create(actor.name + " Combat Visuals");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            tracks = new AnimationClipPlayable[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                tracks[i] = AnimationClipPlayable.Create(graph, clips[i]);
                tracks[i].SetApplyFootIK(false);
                tracks[i].SetSpeed(0);
                graph.Connect(tracks[i], 0, mixer, i);
            }
            var output = AnimationPlayableOutput.Create(graph, "Humanoid", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
            actor.Core.HitResolved += OnHit;
            actor.Core.StateChanged += OnState;
            ResetVisuals();
        }

        void OnState(CombatState from, CombatState to)
        {
            reactionTime = 0;
            if (to == CombatState.Dead) deathTime = 0;
            if (to == CombatState.HitStun || to == CombatState.DeflectedStun || to == CombatState.PostureBroken || to == CombatState.Dead)
            {
                finisherTime = -1;
                for (int i = 0; i < weights.Length; i++) weights[i] = 0;
                if (swordTrail != null) { swordTrail.emitting = false; swordTrail.Clear(); }
            }
        }

        void OnHit(HitEvent hit)
        {
            if (hit.Result == HitResult.Deflect && hit.Defender == actor.Core) reactionTime = -.16f;
            if (hit.Result == HitResult.Deathblow && hit.Attacker == actor.Core) finisherTime = 0;
        }

        public void ResetVisuals()
        {
            if (!initialized) return;
            loopTime = reactionTime = deathTime = 0;
            finisherTime = -1;
            lastPosition = actor.transform.position;
            for (int i = 0; i < weights.Length; i++) { weights[i] = 0; mixer.SetInputWeight(i, 0); }
            weights[0] = 1;
            if (swordTrail != null) { swordTrail.Clear(); swordTrail.emitting = false; }
            AdvanceVisual(0, true);
        }

        public void AdvanceVisual(float dt, bool force = false)
        {
            if (!initialized) return;
            LastVisualDelta = dt;
            if (dt <= 0 && !force) return;
            loopTime += dt;
            if (reactionTime < 0) reactionTime = Mathf.Min(0, reactionTime + dt);
            deathTime += dt;
            if (finisherTime >= 0) finisherTime += dt;
            float speed = dt > 0 ? Vector3.ProjectOnPlane(actor.transform.position - lastPosition, Vector3.up).magnitude / dt : 0;
            lastPosition = actor.transform.position;
            int selected = 0;
            var core = actor.Core;
            if (core.IsAttacking) selected = 3;
            else switch (core.State)
            {
                case CombatState.Guard: selected = reactionTime < 0 ? 7 : 6; break;
                case CombatState.HitStun: selected = 4; break;
                case CombatState.DeflectedStun: selected = 8; break;
                case CombatState.PostureBroken: selected = 9; break;
                case CombatState.Dead: selected = 5; break;
            }
            if (finisherTime >= 0 && finisherTime < profile.attack.length) selected = 3;
            float blend = profile.blendTime <= 0 ? 1 : Mathf.Clamp01(dt / profile.blendTime);
            if (dt == 0) blend = 1;
            float sum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                float target = i == selected ? 1 : 0;
                if (selected == 0)
                {
                    float moving = Mathf.Clamp01(speed / profile.walkSpeed);
                    float jogging = Mathf.InverseLerp(profile.walkSpeed, profile.jogSpeed, speed);
                    target = i == 0 ? 1 - moving : i == 1 ? moving * (1 - jogging) : i == 2 ? moving * jogging : 0;
                }
                weights[i] = Mathf.Lerp(weights[i], target, blend);
                sum += weights[i];
            }
            if (sum < .001f) { weights[selected] = 1; sum = 1; }
            for (int i = 0; i < weights.Length; i++)
            {
                mixer.SetInputWeight(i, weights[i] / sum);
                double time = loopTime % Mathf.Max(.01f, clips[i].length);
                if (i == 3) time = finisherTime >= 0 ? Mathf.Min(finisherTime, clips[3].length) : profile.AttackTime(core);
                if (i == 4 || i == 8) time = core.StateProgress * clips[i].length;
                if (i == 5) time = Mathf.Min(deathTime, clips[i].length - .001f);
                if (i == 6 || i == 9) time = clips[i].length * .5f;
                if (i == 7) time = Mathf.Clamp01(1 + reactionTime / .16f) * clips[i].length;
                tracks[i].SetTime(time);
                if (i == 3) SampledAttackTime = (float)time;
            }
            graph.Evaluate(0);
            // Root motion must never displace the controller, even when importing different clips.
            animator.transform.localPosition = modelPosition;
            animator.transform.localRotation = modelRotation;
            if (swordTrail != null) swordTrail.emitting = core.State == CombatState.AttackActive && dt > 0;
        }

        void OnDestroy()
        {
            if (actor != null && actor.Core != null)
            { actor.Core.HitResolved -= OnHit; actor.Core.StateChanged -= OnState; }
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
