using System.IO;
using System.Text;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace Milkfrog.CombatDemo.Editor
{
    public static class AnimationCalibration
    {
        public static void Calibrate()
        {
            var profile = AssetDatabase.LoadAssetAtPath<CombatAnimationProfile>("Assets/CombatDemo/Animations/HumanoidCombat.asset");
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusAnimationImport.ModelPath));
            var animator = model.GetComponent<Animator>(); animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Rebind();
            var graph = PlayableGraph.Create(); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, profile.attack); playable.SetSpeed(0);
            AnimationPlayableOutput.Create(graph,"probe",animator).SetSourcePlayable(playable); graph.Play();
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand); var text = new StringBuilder();
            text.AppendLine("Sword_Attack normalized time; right hand in model space; hand local axes in model space");
            for (int i=0;i<=10;i++)
            {
                playable.SetTime(i*.1f*profile.attack.length); graph.Evaluate(0); model.transform.position=Vector3.zero; model.transform.rotation=Quaternion.identity;
                text.AppendLine(i*.1f + " hand=" + hand.position.ToString("F3") + " X=" + hand.right.ToString("F3") + " Y=" + hand.up.ToString("F3") + " Z=" + hand.forward.ToString("F3"));
            }
            graph.Destroy();
            graph = PlayableGraph.Create(); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimationClipPlayable.Create(graph, profile.guard); playable.SetSpeed(0);
            AnimationPlayableOutput.Create(graph,"guard probe",animator).SetSourcePlayable(playable); graph.Play();
            playable.SetTime(profile.guard.length*.5); graph.Evaluate(0);
            using (var handler = new HumanPoseHandler(animator.avatar, model.transform))
            {
                var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                foreach (bool right in new[] { true, false })
                {
                    string side = right ? "Right" : "Left";
                    string[] names = { side + " Arm Front-Back", side + " Arm Down-Up", side + " Forearm Stretch", side + " Arm Twist In-Out" };
                    var indices = Array.ConvertAll(names, n => Array.IndexOf(HumanTrait.MuscleName,n));
                    var best = new float[4]; float bestCost = float.PositiveInfinity;
                    var targetHand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                    Vector3 target = right ? new Vector3(.23f,1.32f,.35f) : new Vector3(.08f,1.27f,.33f);
                    for (int a=0;a<5;a++) for(int b=0;b<5;b++) for(int c=0;c<5;c++) for(int d=0;d<5;d++)
                    {
                        float av=-.8f+a*.4f, bv=-.6f+b*.3f, cv=-.9f+c*.4f, dv=-.8f+d*.4f;
                        pose.muscles[indices[0]]=av; pose.muscles[indices[1]]=bv; pose.muscles[indices[2]]=cv; pose.muscles[indices[3]]=dv;
                        handler.SetHumanPose(ref pose);
                        Vector3 position=model.transform.InverseTransformPoint(targetHand.position);
                        Vector3 blade=model.transform.InverseTransformDirection(-targetHand.right);
                        float cost=(position-target).sqrMagnitude + (right ? .12f*(blade-new Vector3(0,.9f,.4f).normalized).sqrMagnitude : 0);
                        if(cost<bestCost) { bestCost=cost; best[0]=av; best[1]=bv; best[2]=cv; best[3]=dv; }
                    }
                    for(int i=0;i<4;i++)
                    {
                        pose.muscles[indices[i]]=best[i]; text.AppendLine("Guard " + names[i] + "=" + best[i]);
                        foreach(var clip in new[] { profile.guard,profile.parry })
                        {
                            var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),names[i]);
                            var curve=AnimationCurve.Constant(0,clip.length,best[i]);
                            if(clip==profile.parry && i==0 && right) curve=new AnimationCurve(new Keyframe(0,best[i]),new Keyframe(clip.length*.3f,Mathf.Clamp(best[i]+.2f,-1,1)),new Keyframe(clip.length,best[i]));
                            AnimationUtility.SetEditorCurve(clip,binding,curve);
                        }
                    }
                    handler.SetHumanPose(ref pose);
                    text.AppendLine(side + " guarded hand=" + model.transform.InverseTransformPoint(targetHand.position));
                }
            }
            profile.attackActiveStart=.25f; profile.attackActiveEnd=.38f; EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Evidence"); File.WriteAllText("Evidence/animation-calibration.txt",text.ToString());
            graph.Destroy(); UnityEngine.Object.DestroyImmediate(model);
        }
    }
}

