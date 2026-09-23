using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Milkfrog.CombatDemo.Editor
{
    public static class CombatBladeTraceBaker
    {
        const string AssetPath = "Assets/CombatDemo/Animations/SwordBladeTrace.asset";
        // Work on the animation scene explicitly; never rewrite the capsule regression scene.
        public static void UpgradeScene()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use this batch entry point in a validation copy.");
            var scene = EditorSceneManager.OpenScene(AnimatedDemoBuilder.ScenePath);
            var session = UnityEngine.Object.FindFirstObjectByType<CombatDemoSession>();
            foreach (var presentation in new[] { session.playerAnimation, session.enemyAnimation })
            {
                var weapon = presentation.animator.GetBoneTransform(HumanBodyBones.RightHand).Find("Training Sword");
                weapon.Find("Blade").localScale = new Vector3(.06f, 1, .018f);
                weapon.Find("Blade").localPosition = Vector3.up * .64f;
                presentation.swordTrail.transform.localPosition = Vector3.up * 1.14f;
            }
            var trace = Bake(session.playerAnimation);
            session.player.bladeTrace = session.enemy.bladeTrace = trace;
            session.player.transform.position = new Vector3(0,.02f,-.625f);
            session.enemy.transform.position = new Vector3(0,.02f,.625f);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static CombatBladeTrace Bake(CombatAnimationPresenter presentation)
        {
            var trace = AssetDatabase.LoadAssetAtPath<CombatBladeTrace>(AssetPath);
            if (trace == null) { trace = ScriptableObject.CreateInstance<CombatBladeTrace>(); AssetDatabase.CreateAsset(trace,AssetPath); }
            var model = presentation.animator;
            var profile = presentation.profile;
            var hand = model.GetBoneTransform(HumanBodyBones.RightHand);
            var sword = hand.Find("Training Sword");
            if (sword == null) throw new InvalidOperationException("Missing right-hand Training Sword.");
            var blade = sword.Find("Blade");
            Vector3 position = model.transform.localPosition; Quaternion rotation = model.transform.localRotation;
            model.cullingMode = AnimatorCullingMode.AlwaysAnimate; model.applyRootMotion = false; model.Rebind();
            var graph = PlayableGraph.Create("Bake blade trajectory"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            try
            {
                var clip = AnimationClipPlayable.Create(graph,profile.attack); clip.SetSpeed(0); clip.SetApplyFootIK(false);
                AnimationPlayableOutput.Create(graph,"Blade sampling",model).SetSourcePlayable(clip); graph.Play();
                trace.engageDistance = 1.25f;
                trace.sourceProfile = profile; trace.bakedClip = profile.attack;
                trace.bakedStart = profile.attackActiveStart; trace.bakedEnd = profile.attackActiveEnd;
                trace.poses = new BladePose[65];
                var report = new StringBuilder("Active progress,blade root (actor space),blade tip (actor space)\n");
                for (int i=0;i<trace.poses.Length;i++)
                {
                    float progress = (float)i/(trace.poses.Length-1);
                    clip.SetTime(Mathf.Lerp(trace.bakedStart,trace.bakedEnd,progress)*profile.attack.length); graph.Evaluate(0);
                    model.transform.localPosition=position; model.transform.localRotation=rotation;
                    trace.poses[i] = new BladePose {
                        root=presentation.actor.transform.InverseTransformPoint(blade.TransformPoint(Vector3.down*.5f)),
                        tip=presentation.actor.transform.InverseTransformPoint(blade.TransformPoint(Vector3.up*.5f)) };
                    report.AppendLine(progress.ToString("F4") + "," + trace.poses[i].root.ToString("F4") + "," + trace.poses[i].tip.ToString("F4"));
                }
                Directory.CreateDirectory("Evidence"); File.WriteAllText("Evidence/blade-trajectory.txt",report.ToString());
                EditorUtility.SetDirty(trace);
            }
            finally
            {
                graph.Destroy(); model.Rebind(); model.transform.localPosition=position; model.transform.localRotation=rotation;
            }
            return trace;
        }
    }
}


