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
        AnimationLayerMixerPlayable layers;
        AnimationMixerPlayable upperGuard;
        AnimationClipPlayable guardPose, parryPose;
        AvatarMask guardMask;
        public float UpperGuardWeight { get; private set; }
        public float LocomotionWeight => weights[1] + weights[2] + weights[10] + weights[11] + weights[12] + weights[13] + weights[14] + weights[15];
        public Vector4 DirectionWeights { get; private set; } = new Vector4(1,0,0,0);
        public double AttackPoseTime => poseTimes[3];
        public AnimationClip CurrentPoseClip => initialized && previousTrack >= 0 ? clips[previousTrack] : null;
        AnimationClipPlayable[] tracks;
        AnimationClip[] clips;
        readonly float[] weights = new float[28];
        readonly double[] poseTimes = new double[28];
        readonly float[] blendFrom = new float[28];
        int previousTrack = -1;
        float blendElapsed;
        float visualSpeed;
        float gaitPhase;
        Vector2 localVelocity;
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
            clips = new[] { profile.idle, profile.walk, profile.jog, actor.lightAttack!=null?actor.lightAttack.clip:profile.attack, profile.hit,
                profile.death, profile.guard, profile.parry, profile.deflected, profile.broken,
                profile.walkBack != null ? profile.walkBack : profile.walk, profile.jogBack != null ? profile.jogBack : profile.jog,
                profile.walkLeft != null ? profile.walkLeft : profile.walk, profile.jogLeft != null ? profile.jogLeft : profile.jog,
                profile.walkRight != null ? profile.walkRight : profile.walk, profile.jogRight != null ? profile.jogRight : profile.jog,
                profile.dodgeForward != null ? profile.dodgeForward : profile.idle, profile.dodgeBack != null ? profile.dodgeBack : profile.idle,
                profile.dodgeLeft != null ? profile.dodgeLeft : profile.idle, profile.dodgeRight != null ? profile.dodgeRight : profile.idle,
                profile.charge != null ? profile.charge : profile.idle, actor.thrustAttack!=null?actor.thrustAttack.clip:profile.thrust != null ? profile.thrust : profile.attack,
                actor.slowAttack != null && actor.slowAttack.clip != null ? actor.slowAttack.clip : profile.attack,
                actor.perilousAttack != null && actor.perilousAttack.clip != null ? actor.perilousAttack.clip : profile.attack,
                actor.followupAttack != null && actor.followupAttack.clip != null ? actor.followupAttack.clip : profile.attack,
                profile.jump != null ? profile.jump : profile.idle, profile.fall != null ? profile.fall : profile.idle,
                profile.land != null ? profile.land : profile.idle };
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
            layers = AnimationLayerMixerPlayable.Create(graph, 2);
            graph.Connect(mixer, 0, layers, 0); layers.SetInputWeight(0, 1);
            upperGuard = AnimationMixerPlayable.Create(graph, 2);
            guardPose = AnimationClipPlayable.Create(graph, profile.guard); guardPose.SetSpeed(0);
            parryPose = AnimationClipPlayable.Create(graph, profile.parry); parryPose.SetSpeed(0);
            graph.Connect(guardPose, 0, upperGuard, 0); graph.Connect(parryPose, 0, upperGuard, 1);
            graph.Connect(upperGuard, 0, layers, 1);
            guardMask = new AvatarMask();
            for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++)
                guardMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)part, part != (int)AvatarMaskBodyPart.Root &&
                    part != (int)AvatarMaskBodyPart.LeftLeg && part != (int)AvatarMaskBodyPart.RightLeg &&
                    part != (int)AvatarMaskBodyPart.LeftFootIK && part != (int)AvatarMaskBodyPart.RightFootIK);
            layers.SetLayerMaskFromAvatarMask(1, guardMask);
            output.SetSourcePlayable(layers);
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
                previousTrack = -1;
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
            previousTrack = -1; blendElapsed = 0;
            visualSpeed = UpperGuardWeight = 0;
            gaitPhase = 0; localVelocity = Vector2.zero; DirectionWeights = new Vector4(1,0,0,0);
            lastPosition = actor.transform.position;
            for (int i = 0; i < weights.Length; i++) { weights[i] = 0; poseTimes[i] = 0; mixer.SetInputWeight(i, 0); }
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
            Vector3 local = dt > 0 ? actor.transform.InverseTransformDirection(actor.transform.position - lastPosition) / dt : Vector3.zero;
            localVelocity = force ? new Vector2(local.x,local.z) : Vector2.Lerp(localVelocity,new Vector2(local.x,local.z),1-Mathf.Exp(-dt/.07f));
            float totalDirection = Mathf.Abs(localVelocity.x) + Mathf.Abs(localVelocity.y);
            if (totalDirection > .01f) DirectionWeights = new Vector4(Mathf.Max(0,localVelocity.y),Mathf.Max(0,-localVelocity.y),Mathf.Max(0,-localVelocity.x),Mathf.Max(0,localVelocity.x)) / totalDirection;
            visualSpeed = force ? speed : Mathf.Lerp(visualSpeed, speed, 1 - Mathf.Exp(-dt / Mathf.Max(.01f, profile.blendTime)));
            float stride=Mathf.Lerp(Vector4.Dot(DirectionWeights,profile.walkStrideLengths),Vector4.Dot(DirectionWeights,profile.jogStrideLengths),Mathf.InverseLerp(profile.walkSpeed,profile.jogSpeed,visualSpeed));
            gaitPhase += dt * visualSpeed / Mathf.Max(.1f,stride);
            lastPosition = actor.transform.position;
            int selected = 0;
            var core = actor.Core;
            if (core.IsAttacking)
                selected = core.ActiveAttack.Kind == AttackKind.Followup ? 24 :
                    core.ActiveAttack.Kind == AttackKind.Slow ? 22 :
                    core.ActiveAttack.Kind == AttackKind.Perilous ? 23 : 3;
            else if(core.State==CombatState.Dodge)selected=16;
            else if(core.CanAct && actor.Jumping)selected=25;
            else if(core.CanAct && actor.Falling)selected=26;
            else if(core.CanAct && actor.LandRemaining>0)selected=27;
            else switch (core.State)
            {
                case CombatState.Guard: selected = visualSpeed > .05f ? 0 : reactionTime < 0 ? 7 : 6; break;
                case CombatState.HitStun: selected = 4; break;
                case CombatState.DeflectedStun: selected = 8; break;
                case CombatState.PostureBroken: selected = 9; break;
                case CombatState.Dead: selected = 5; break;
            }
            if (finisherTime >= 0 && finisherTime < profile.attack.length) selected = 3;
            else if (finisherTime >= profile.attack.length) finisherTime = -1;
            if (selected != previousTrack)
            {
                System.Array.Copy(weights, blendFrom, weights.Length);
                blendElapsed = 0; previousTrack = selected;
            }
            blendElapsed += dt;
            float blend = force || profile.blendTime <= 0 ? 1 : Mathf.Clamp01(blendElapsed / profile.blendTime);
            float sum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                float target = i == selected ? 1 : 0;
                if (selected == 0)
                {
                    float moving = Mathf.Clamp01(visualSpeed / profile.walkSpeed);
                    float jogging = Mathf.InverseLerp(profile.walkSpeed, profile.jogSpeed, visualSpeed);
                    int directionIndex = i <= 2 ? 0 : (i-10)/2+1;
                    bool gait = i == 1 || i == 2 || i >= 10 && i <=15;
                    bool run = i == 2 || i >= 10 && i % 2 == 1;
                    target = i == 0 ? 1-moving : gait ? moving * (run ? jogging : 1-jogging) * DirectionWeights[directionIndex] : 0;
                }
                if(selected==16)
                {
                    Vector3 dodge=actor.transform.InverseTransformDirection(actor.DodgeDirection);
                    float sumDirection=Mathf.Max(.001f,Mathf.Abs(dodge.x)+Mathf.Abs(dodge.z));
                    target=i==16?Mathf.Max(0,dodge.z)/sumDirection:i==17?Mathf.Max(0,-dodge.z)/sumDirection:i==18?Mathf.Max(0,-dodge.x)/sumDirection:i==19?Mathf.Max(0,dodge.x)/sumDirection:0;
                }
                weights[i] = Mathf.Lerp(blendFrom[i], target, blend);
                sum += weights[i];
            }
            if (sum < .001f) { weights[selected] = 1; sum = 1; }
            for (int i = 0; i < weights.Length; i++)
            {
                mixer.SetInputWeight(i, weights[i] / sum);
                double time = loopTime % Mathf.Max(.01f, clips[i].length);
                if (i == 1 || i == 2 || i >= 10 && i <=15) time = Mathf.Repeat(gaitPhase,1) * clips[i].length;
                if (i == 3) time = finisherTime >= 0 ? Mathf.Min(finisherTime, clips[3].length) :
                    (core.State==CombatState.AttackPrepare || core.State==CombatState.Charging) ? HeldSlashTime(core, clips[3]) : AttackPose(core, i);
                if(i>=16 && i<=19)time=Mathf.Clamp01(core.DodgeElapsed/Mathf.Max(.001f,clips[i].length))*clips[i].length;
                if(i==20)time=core.ChargeRatio*clips[i].length;
                if ((i == 22 || i == 23 || i == 24) && selected == i) time = AttackPose(core, i);
                if (i == 25 && selected == 25) time = Mathf.Clamp01(actor.AirTime / Mathf.Max(.001f, clips[i].length)) * clips[i].length;
                if (i == 26 && selected == 26) time = Mathf.Repeat(actor.AirTime, clips[i].length);
                if (i == 27 && selected == 27) time = (1f - Mathf.Clamp01(actor.LandRemaining / CombatActor.LandDuration)) * clips[i].length;
                if (i == 4 || i == 8) time = core.StateProgress * clips[i].length;
                if (i == 5) time = Mathf.Min(deathTime, clips[i].length - .001f);
                if (i == 6 || i == 9) time = clips[i].length * .5f;
                if (i == 7) time = Mathf.Clamp01(1 + reactionTime / .16f) * clips[i].length;
                // An outgoing action keeps its final sampled pose instead of jumping back to frame zero.
                if (i <= 2 || i >= 10 && i <=15 || i == selected || selected==16 && i>=16 && i<=19 || force) poseTimes[i] = time;
                tracks[i].SetTime(poseTimes[i]);
                if (i == selected && (i == 3 || i == 21 || i == 22 || i == 23 || i == 24)) SampledAttackTime = (float)poseTimes[i];
            }
            // Legs keep stepping while guarding; attack and all interrupts own the full body immediately.
            UpperGuardWeight = core.State == CombatState.Guard ? 1 : 0;
            layers.SetInputWeight(1, UpperGuardWeight);
            guardPose.SetTime(profile.guard.length * .5f);
            parryPose.SetTime(Mathf.Clamp01(1 + reactionTime / .16f) * profile.parry.length);
            upperGuard.SetInputWeight(0, reactionTime < 0 ? 0 : 1);
            upperGuard.SetInputWeight(1, reactionTime < 0 ? 1 : 0);
            graph.Evaluate(0);
            // Root motion must never displace the controller, even when importing different clips.
            animator.transform.localPosition = modelPosition;
            animator.transform.localRotation = modelRotation;
            if (swordTrail != null) swordTrail.emitting = core.State == CombatState.AttackActive && dt > 0;
        }

        float HeldSlashTime(CombatCore core, AnimationClip clip)
        {
            var definition = actor.lightAttack;
            float windup = definition != null && definition.clip != null ? definition.activeStart : profile.attackActiveStart;
            float held = core.State == CombatState.Charging ? 1f : Mathf.Clamp01(core.ChargeElapsed / Mathf.Max(.001f, core.Tuning.prepareThreshold));
            return windup * held * clip.length;
        }

        float AttackPose(CombatCore core, int track)
        {
            var definition = actor.CurrentAttackDefinition;
            if (definition == null || definition.clip == null) return profile.AttackTime(core);
            if (core.ActiveAttack.Kind == AttackKind.Followup)
            {
                // The follow-up has a shorter wind-up, but its sampled clip time must never
                // run backwards when Startup enters Active (especially on a blocked contact).
                float start = definition.activeStart * .35f;
                float a = start, b = definition.activeStart;
                if (core.State == CombatState.AttackActive) { a = definition.activeStart; b = definition.activeEnd; }
                if (core.State == CombatState.AttackRecovery) { a = definition.activeEnd; b = 1; }
                return Mathf.Lerp(a, b, core.StateProgress) * definition.clip.length;
            }
            return definition.SampleTime(core);
        }

        void OnDestroy()
        {
            if (actor != null && actor.Core != null)
            { actor.Core.HitResolved -= OnHit; actor.Core.StateChanged -= OnState; }
            if (graph.IsValid()) graph.Destroy();
            if (guardMask != null) Destroy(guardMask);
        }
    }
}
