using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Milkfrog.CombatDemo
{
    public sealed class PlayClip : MonoBehaviour
    {
        public AnimationClip clip;
        PlayableGraph graph;

        void Start()
        {
            var animator = GetComponent<Animator>();
            if (animator == null || clip == null) return;
            animator.applyRootMotion = false;
            graph = PlayableGraph.Create(name);
            var playable = AnimationClipPlayable.Create(graph, clip);
            AnimationPlayableOutput.Create(graph, name, animator).SetSourcePlayable(playable);
            graph.Play();
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
