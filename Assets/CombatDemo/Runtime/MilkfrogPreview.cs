using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Milkfrog.CombatDemo
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class MilkfrogPreview : MonoBehaviour
    {
        public Animator animator;
        public AnimationClip idle, walk, jog, walkBack, jogBack, walkLeft, jogLeft, walkRight, jogRight;
        public AnimationClip light, thrust, slow, perilous, followup, charge;
        public AnimationClip hit, death, guard, parry, deflected, broken;
        public AnimationClip dodgeForward, dodgeBack, dodgeLeft, dodgeRight;
        public AnimationClip jump, fall, land;
        public float walkSpeed = 1.6f;
        public float jogSpeed = 4f;
        public float turnSpeed = 720f;
        public float gravity = 20f;
        public float jumpSpeed = 6f;

        CharacterController motor;
        DemoInput input;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] tracks;
        AnimationClip[] clips;
        float verticalVelocity;
        float gaitPhase;
        float actionTime = -1f;
        int actionTrack = -1;
        bool airborne;

        void Awake()
        {
            motor = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            input = new DemoInput();
            clips = new[] { idle, walk, jog, walkBack, jogBack, walkLeft, jogLeft, walkRight, jogRight,
                light, thrust, slow, perilous, followup, charge, hit, death, guard, parry, deflected, broken,
                dodgeForward, dodgeBack, dodgeLeft, dodgeRight, jump, fall, land };
            if (animator == null || idle == null) return;
            animator.applyRootMotion = false;
            graph = PlayableGraph.Create(name + " Preview");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            tracks = new AnimationClipPlayable[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) continue;
                tracks[i] = AnimationClipPlayable.Create(graph, clips[i]);
                tracks[i].SetSpeed(0);
                graph.Connect(tracks[i], 0, mixer, i);
            }
            AnimationPlayableOutput.Create(graph, "Preview", animator).SetSourcePlayable(mixer);
            graph.Play();
        }

        void OnDestroy()
        {
            input?.Dispose();
            if (graph.IsValid()) graph.Destroy();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            ReadActions();
            Move(dt);
            Pose(dt);
        }

        void ReadActions()
        {
            if (actionTrack >= 0) return;
            if (input.Snapshot.attackPressed) { Begin(9); return; }
            if (input.Snapshot.guardPressed) { Begin(18); return; }
            if (input.Snapshot.dodgePressed) { Begin(DodgeTrack()); return; }
            if (input.Snapshot.jumpPressed && motor.isGrounded) { verticalVelocity = jumpSpeed; Begin(25); }
        }

        int DodgeTrack()
        {
            Vector3 local = transform.InverseTransformDirection(new Vector3(input.Move.x, 0, input.Move.y));
            if (local.sqrMagnitude < .01f) return 21;
            if (Mathf.Abs(local.z) >= Mathf.Abs(local.x)) return local.z >= 0 ? 21 : 22;
            return local.x >= 0 ? 24 : 23;
        }

        void Begin(int track)
        {
            if (track < 0 || clips[track] == null) return;
            actionTrack = track;
            actionTime = 0;
        }

        void Move(float dt)
        {
            Vector3 direction = Vector3.zero;
            if (actionTrack < 0)
            {
                Vector2 move = input.Move;
                direction = new Vector3(move.x, 0, move.y);
                if (direction.sqrMagnitude > 1f) direction.Normalize();
                if (direction.sqrMagnitude > .01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * dt);
            }
            if (motor.isGrounded && verticalVelocity < 0) verticalVelocity = -2f;
            else verticalVelocity -= gravity * dt;
            float speed = input.Snapshot.attackHeld ? jogSpeed : walkSpeed;
            motor.Move((direction * speed + Vector3.up * verticalVelocity) * dt);
            airborne = !motor.isGrounded;
        }

        void Pose(float dt)
        {
            if (!graph.IsValid()) return;
            int selected = 0;
            float time = Mathf.Repeat(Time.time, Mathf.Max(.01f, idle.length));
            if (actionTrack >= 0)
            {
                actionTime += dt;
                if (actionTime >= clips[actionTrack].length) actionTrack = -1;
                else { selected = actionTrack; time = actionTime; }
            }
            if (actionTrack < 0)
            {
                Vector2 move = input.Move;
                float moving = Mathf.Clamp01(move.magnitude);
                bool running = input.Snapshot.attackHeld;
                if (airborne) { selected = verticalVelocity > 0 ? 25 : 26; time = Mathf.Repeat(Time.time, clips[selected].length); }
                else if (moving > .01f)
                {
                    Vector3 local = transform.InverseTransformDirection(new Vector3(move.x, 0, move.y).normalized);
                    int direction = Mathf.Abs(local.z) >= Mathf.Abs(local.x) ? (local.z >= 0 ? 0 : 1) : (local.x >= 0 ? 3 : 2);
                    selected = 1 + direction * 2 + (running ? 1 : 0);
                    gaitPhase = Mathf.Repeat(gaitPhase + dt * moving * (running ? 2f : 1f), clips[selected].length);
                    time = gaitPhase;
                }
            }
            for (int i = 0; i < clips.Length; i++) mixer.SetInputWeight(i, i == selected ? 1 : 0);
            if (tracks[selected].IsValid()) tracks[selected].SetTime(time);
            graph.Evaluate(dt);
        }
    }
}
