using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Milkfrog.CombatDemo.Editor
{
    public static class MilkfrogCombatSetup
    {
        const string ModelPath = "Assets/CombatDemo/Animations/milkfrog/milkfrog.fbx";
        const string ScenePath = "Assets/CombatDemo/Scenes/milkfrog.unity";
        const string ProfilePath = "Assets/CombatDemo/Animations/milkfrog/MilkfrogCombat.asset";

        public static void Build()
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__")).ToDictionary(c => c.name);
            AnimationClip Need(string name)
            {
                if (!clips.TryGetValue(name, out var clip)) throw new System.InvalidOperationException("missing clip " + name);
                return clip;
            }

            var profile = AssetDatabase.LoadAssetAtPath<CombatAnimationProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CombatAnimationProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            profile.idle = Need("idle"); profile.walk = Need("walk"); profile.jog = Need("jog");
            profile.attack = Need("light"); profile.hit = Need("hit"); profile.death = Need("death");
            profile.guard = Need("guard"); profile.parry = Need("parry"); profile.deflected = Need("deflected");
            profile.broken = Need("broken"); profile.walkBack = Need("walkBack"); profile.jogBack = Need("jogBack");
            profile.walkLeft = Need("walkLeft"); profile.jogLeft = Need("jogLeft"); profile.walkRight = Need("walkRight");
            profile.jogRight = Need("jogRight"); profile.dodgeForward = Need("dodgeForward"); profile.dodgeBack = Need("dodgeBack");
            profile.dodgeLeft = Need("dodgeLeft"); profile.dodgeRight = Need("dodgeRight"); profile.charge = Need("charge");
            profile.thrust = Need("thrust"); profile.jump = Need("jump"); profile.fall = Need("fall"); profile.land = Need("land");
            profile.attackActiveStart = 8f / 24f; profile.attackActiveEnd = 14f / 24f;
            EditorUtility.SetDirty(profile);

            var attacks = new[]
            {
                Attack("light", AttackKind.Light, Need("light"), 0f, 8f / 24f, 14f / 24f, true),
                Attack("thrust", AttackKind.Thrust, Need("thrust"), 0f, 6f / 24f, 12f / 24f, false),
                Attack("followup", AttackKind.Followup, Need("followup"), 0f, 6f / 20f, 12f / 20f, false),
                Attack("slow", AttackKind.Slow, Need("slow"), 0f, 14f / 36f, 22f / 36f, false),
                Attack("perilous", AttackKind.Perilous, Need("perilous"), 0f, 16f / 40f, 26f / 40f, false)
            };
            attacks[4].rules.response = AttackResponse.DodgeOnly;

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var actorObject = GameObject.Find("milkfrog");
            var animator = actorObject.GetComponentInChildren<Animator>();
            var actor = actorObject.GetComponent<CombatActor>() ?? actorObject.AddComponent<CombatActor>();
            actor.isPlayer = true;
            actor.lightAttack = attacks[0]; actor.thrustAttack = attacks[1]; actor.followupAttack = attacks[2];
            actor.slowAttack = attacks[3]; actor.perilousAttack = attacks[4];
            actor.bladeTrace = attacks[0].trace;
            var presentation = actorObject.GetComponent<CombatAnimationPresenter>() ?? actorObject.AddComponent<CombatAnimationPresenter>();
            presentation.actor = actor; presentation.animator = animator; presentation.profile = profile;
            var preview = actorObject.GetComponent<MilkfrogPreview>();
            if (preview != null) Object.DestroyImmediate(preview);
            var driver = actorObject.GetComponent<MilkfrogCombatDriver>() ?? actorObject.AddComponent<MilkfrogCombatDriver>();
            driver.actor = actor; driver.presentation = presentation;

            foreach (var attack in attacks) Bake(actorObject.transform, animator, attack);
            actor.bladeTrace = attacks[0].trace;
            EditorUtility.SetDirty(actor);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("MILKFROG COMBAT wired traces=" + string.Join(",", attacks.Select(a => a.trace.poses.Length.ToString())));
            EditorApplication.Exit(0);
        }

        static CombatAttackDefinition Attack(string name, AttackKind kind, AnimationClip clip, float start, float activeStart, float activeEnd, bool combo)
        {
            var path = "Assets/CombatDemo/Animations/milkfrog/" + name + ".asset";
            var attack = AssetDatabase.LoadAssetAtPath<CombatAttackDefinition>(path);
            if (attack == null)
            {
                attack = ScriptableObject.CreateInstance<CombatAttackDefinition>();
                AssetDatabase.CreateAsset(attack, path);
            }
            float length = Mathf.Max(.01f, clip.length);
            attack.rules.kind = kind;
            attack.rules.startup = (activeStart - start) * length;
            attack.rules.active = (activeEnd - activeStart) * length;
            attack.rules.recovery = (1f - activeEnd) * length;
            attack.rules.CanComboFrom = combo;
            attack.clip = clip;
            attack.activeStart = activeStart;
            attack.activeEnd = activeEnd;
            EditorUtility.SetDirty(attack);
            return attack;
        }

        static void Bake(Transform actor, Animator animator, CombatAttackDefinition attack)
        {
            var path = "Assets/CombatDemo/Animations/milkfrog/" + attack.name + "-trace.asset";
            var trace = AssetDatabase.LoadAssetAtPath<CombatBladeTrace>(path);
            if (trace == null)
            {
                trace = ScriptableObject.CreateInstance<CombatBladeTrace>();
                AssetDatabase.CreateAsset(trace, path);
            }
            var sword = animator.transform.Find("metarig/shoulder.R/upper_arm.R/forearm.R/hand.R/sword");
            if (sword == null) sword = animator.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "sword");
            if (sword == null) throw new System.InvalidOperationException("sword missing");
            var graph = PlayableGraph.Create("milkfrog blade");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var clip = AnimationClipPlayable.Create(graph, attack.clip);
            clip.SetSpeed(0);
            AnimationPlayableOutput.Create(graph, "blade", animator).SetSourcePlayable(clip);
            graph.Play();
            trace.definition = attack; trace.bakedClip = attack.clip;
            trace.bakedStart = attack.activeStart; trace.bakedEnd = attack.activeEnd;
            trace.poses = new BladePose[33];
            for (int i = 0; i < trace.poses.Length; i++)
            {
                float progress = (float)i / (trace.poses.Length - 1);
                clip.SetTime(Mathf.Lerp(attack.activeStart, attack.activeEnd, progress) * attack.clip.length);
                graph.Evaluate(0);
                trace.poses[i] = new BladePose
                {
                    root = actor.InverseTransformPoint(sword.TransformPoint(new Vector3(0, -.275f, 0))),
                    tip = actor.InverseTransformPoint(sword.TransformPoint(new Vector3(0, .275f, 0)))
                };
            }
            graph.Destroy();
            attack.trace = trace;
            EditorUtility.SetDirty(trace);
            EditorUtility.SetDirty(attack);
        }
    }
}
