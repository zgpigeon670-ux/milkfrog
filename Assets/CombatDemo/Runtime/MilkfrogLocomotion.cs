using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Milkfrog.CombatDemo
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class MilkfrogLocomotion : MonoBehaviour
    {
        public Animator animator;
        public AnimationClip idle;
        public AnimationClip walk;
        public float walkSpeed = 1.6f;
        public float turnSpeed = 720f;
        public float gravity = 20f;

        CharacterController motor;
        DemoInput input;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable idleTrack, walkTrack;
        float verticalVelocity;
        float gaitPhase;
        bool ready;

        void Awake()
        {
            motor = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            input = new DemoInput();
            BuildGraph();
        }

        void BuildGraph()
        {
            if (animator == null || idle == null || walk == null) return;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create(name + " Locomotion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            idleTrack = AnimationClipPlayable.Create(graph, idle);
            walkTrack = AnimationClipPlayable.Create(graph, walk);
            idleTrack.SetSpeed(0);
            walkTrack.SetSpeed(0);
            graph.Connect(idleTrack, 0, mixer, 0);
            graph.Connect(walkTrack, 0, mixer, 1);
            var output = AnimationPlayableOutput.Create(graph, "Locomotion", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
            ready = true;
        }

        void OnDestroy()
        {
            input?.Dispose();
            if (graph.IsValid()) graph.Destroy();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 move = input.Move;
            Vector3 direction = new Vector3(move.x, 0, move.y);
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            if (direction.sqrMagnitude > .01f)
            {
                var facing = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * dt);
            }

            if (motor.isGrounded && verticalVelocity < 0) verticalVelocity = -2f;
            else verticalVelocity -= gravity * dt;
            motor.Move((direction * walkSpeed + Vector3.up * verticalVelocity) * dt);

            if (!ready) return;
            float moving = Mathf.Clamp01(direction.magnitude);
            gaitPhase = Mathf.Repeat(gaitPhase + dt * moving, walk.length);
            mixer.SetInputWeight(0, 1f - moving);
            mixer.SetInputWeight(1, moving);
            idleTrack.SetTime(Mathf.Repeat(Time.time, idle.length));
            walkTrack.SetTime(gaitPhase);
            graph.Evaluate(dt);
        }
    }
}
