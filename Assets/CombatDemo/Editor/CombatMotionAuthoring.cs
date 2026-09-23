using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Milkfrog.CombatDemo.Editor
{
    // Editable project-authored muscle curves, on the existing CC0 skeleton.
    public static class CombatMotionAuthoring
    {
        const string Root = "Assets/CombatDemo/Animations/Directional";
        public static void BuildLocomotion()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run in a validation copy.");
            EditorSceneManager.OpenScene(AnimatedDemoBuilder.ScenePath);
            var session = UnityEngine.Object.FindFirstObjectByType<CombatDemoSession>();
            var p = session.playerAnimation.profile;
            Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            p.walkBack = Gait(session.playerAnimation, "Walk_Back", Vector3.back, false);
            p.jogBack = Gait(session.playerAnimation, "Jog_Back", Vector3.back, true);
            p.walkLeft = Gait(session.playerAnimation, "Walk_Left", Vector3.left, false);
            p.jogLeft = Gait(session.playerAnimation, "Jog_Left", Vector3.left, true);
            p.walkRight = Gait(session.playerAnimation, "Walk_Right", Vector3.right, false);
            p.jogRight = Gait(session.playerAnimation, "Jog_Right", Vector3.right, true);
            EditorUtility.SetDirty(p); AssetDatabase.SaveAssets();
            Debug.Log("[Phase3] Authored backward, left and right walk/jog clips.");
        }

        static AnimationClip Gait(CombatAnimationPresenter presenter, string name, Vector3 direction, bool run)
        {
            float duration = run ? .45f : .8f, speed = run ? presenter.profile.jogSpeed : presenter.profile.walkSpeed;
            return Author(presenter, name, duration, true, (animator, t) =>
            {
                Vector3 worldDirection = presenter.actor.transform.TransformDirection(direction);
                for (int side=0; side<2; side++)
                {
                    bool left=side==0;
                    Transform hip=animator.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                    Transform knee=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                    Transform foot=animator.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                    float phase=Mathf.Repeat(t/duration+side*.5f,1);
                    const float stance=.6f;
                    float travel=duration*speed*stance;
                    float stride=phase<stance ? .5f-phase/stance : Mathf.Lerp(-.5f,.5f,Mathf.SmoothStep(0,1,(phase-stance)/(1-stance)));
                    float lift=phase<stance ? 0 : Mathf.Sin((phase-stance)/(1-stance)*Mathf.PI)*(run?.14f:.085f);
                    Vector3 target=foot.position+worldDirection*(travel*stride)+Vector3.up*lift;
                    Quaternion rotation=foot.rotation;
                    SolveLimb(hip,knee,foot,target,presenter.actor.transform.forward);
                    foot.rotation=rotation;
                }
                var spine=animator.GetBoneTransform(HumanBodyBones.Spine);
                spine.rotation=Quaternion.AngleAxis(direction.z*(run?5:3),presenter.actor.transform.right)*
                    Quaternion.AngleAxis(-direction.x*(run?5:3),presenter.actor.transform.forward)*spine.rotation;
            });
        }

        public static AnimationClip Author(CombatAnimationPresenter presenter, string name, float duration, bool loop, Action<Animator,float> poseAt)
        {
            var animator=presenter.animator; animator.Rebind(); animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var source=presenter.profile.idle;
            var clip=UnityEngine.Object.Instantiate(source); clip.name=name; clip.frameRate=60;
            foreach(var binding in AnimationUtility.GetCurveBindings(source))
            {
                float value=AnimationUtility.GetEditorCurve(source,binding).Evaluate(source.length*.5f);
                AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0,duration,value));
            }
            var curves=new AnimationCurve[HumanTrait.MuscleCount];
            for(int i=0;i<curves.Length;i++)curves[i]=new AnimationCurve();
            var graph=PlayableGraph.Create("Author "+name); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var input=AnimationClipPlayable.Create(graph,source); input.SetSpeed(0); input.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(graph,"Author",animator).SetSourcePlayable(input); graph.Play();
            using(var handler=new HumanPoseHandler(animator.avatar,animator.transform))
            {
                try
                {
                    var pose=new HumanPose(); int frames=Mathf.CeilToInt(duration*60);
                    for(int frame=0;frame<=frames;frame++)
                    {
                        float t=(float)frame/frames*duration;
                        input.SetTime(source.length*.5f); graph.Evaluate(0);
                        animator.transform.localPosition=Vector3.zero; animator.transform.localRotation=Quaternion.identity;
                        poseAt(animator,t); handler.GetHumanPose(ref pose);
                        for(int muscle=0;muscle<curves.Length;muscle++) curves[muscle].AddKey(t,pose.muscles[muscle]);
                    }
                    for(int muscle=0;muscle<curves.Length;muscle++)
                    {
                        for(int key=0;key<curves[muscle].length;key++)
                        {
                            AnimationUtility.SetKeyLeftTangentMode(curves[muscle],key,AnimationUtility.TangentMode.ClampedAuto);
                            AnimationUtility.SetKeyRightTangentMode(curves[muscle],key,AnimationUtility.TangentMode.ClampedAuto);
                        }
                        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),HumanTrait.MuscleName[muscle]),curves[muscle]);
                    }
                }
                finally {graph.Destroy(); animator.Rebind();}
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.startTime=0; settings.stopTime=duration; settings.loopTime=loop; settings.loopBlend=loop;
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            string path=Root+"/"+name+".anim";
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(existing==null)AssetDatabase.CreateAsset(clip,path);
            else {EditorUtility.CopySerialized(clip,existing); UnityEngine.Object.DestroyImmediate(clip); clip=existing;}
            EditorUtility.SetDirty(clip); return clip;
        }

        public static void SolveLimb(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole)
        {
            float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,end.position);
            Vector3 axis=(target-upper.position).normalized;
            float distance=Mathf.Clamp(Vector3.Distance(upper.position,target),Mathf.Abs(a-b)+.001f,a+b-.002f);
            Vector3 bend=Vector3.ProjectOnPlane(pole,axis).normalized;
            if(bend.sqrMagnitude<.01f)bend=Vector3.ProjectOnPlane(Vector3.right,axis).normalized;
            float along=(a*a+distance*distance-b*b)/(2*distance);
            Vector3 knee=upper.position+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,knee-upper.position)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(end.position-lower.position,upper.position+axis*distance-lower.position)*lower.rotation;
        }
    }
}
