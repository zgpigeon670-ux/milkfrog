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
        public static void BuildActions()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Run in a validation copy.");
            var scene=EditorSceneManager.OpenScene(AnimatedDemoBuilder.ScenePath);
            var session=UnityEngine.Object.FindFirstObjectByType<CombatDemoSession>();var presenter=session.playerAnimation;var p=presenter.profile;
            p.dodgeForward=Dodge(presenter,"Dodge_Forward",Vector3.forward);
            p.dodgeBack=Dodge(presenter,"Dodge_Back",Vector3.back);
            p.dodgeLeft=Dodge(presenter,"Dodge_Left",Vector3.left);
            p.dodgeRight=Dodge(presenter,"Dodge_Right",Vector3.right);
            p.charge=Author(presenter,"Charge_Thrust",.62f,false,(anim,t)=>ThrustPose(presenter,t/.62f,0));
            p.jump=Author(presenter,"Jump",.30f,false,(anim,t)=>AirPose(presenter,Mathf.Lerp(.15f,.85f,t/.30f),false));
            p.fall=Author(presenter,"Fall",.24f,true,(anim,t)=>AirPose(presenter,.9f,false));
            p.land=Author(presenter,"Land",.12f,false,(anim,t)=>AirPose(presenter,Mathf.Lerp(.35f,0,t/.12f),true));
            p.thrust=Author(presenter,"Sword_Thrust",.66f,false,(anim,t)=>
            {
                float extension=t<.12f?Mathf.Lerp(0,.15f,t/.12f):t<.24f?Mathf.Lerp(.15f,1,(t-.12f)/.12f):1-Mathf.SmoothStep(0,1,(t-.24f)/.42f);
                ThrustPose(presenter,1,extension);
            });
            var light=Definition("PlayerSlash");light.rules=new AttackParameters();light.clip=p.attack;light.activeStart=p.attackActiveStart;light.activeEnd=p.attackActiveEnd;light.trace=session.player.bladeTrace;
            var enemyLight=Definition("EnemySlash");enemyLight.rules=new AttackParameters{startup=.45f,recovery=.45f};enemyLight.clip=p.attack;enemyLight.activeStart=p.attackActiveStart;enemyLight.activeEnd=p.attackActiveEnd;enemyLight.trace=session.enemy.bladeTrace;
            var thrust=Definition("PlayerThrust");thrust.rules=AttackParameters.Thrust();thrust.clip=p.thrust;thrust.activeStart=.12f/.66f;thrust.activeEnd=.24f/.66f;
            thrust.trace=CombatBladeTraceBaker.Bake(presenter,thrust,"Assets/CombatDemo/Animations/ThrustBladeTrace.asset");
            session.player.lightAttack=light;session.player.thrustAttack=thrust;session.enemy.lightAttack=enemyLight;
            EditorUtility.SetDirty(p);EditorUtility.SetDirty(light);EditorUtility.SetDirty(enemyLight);EditorUtility.SetDirty(thrust);
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("[Phase3] Authored four dodges, charge and thrust; baked independent thrust trace.");
        }
        static CombatAttackDefinition Definition(string name)
        {
            string path="Assets/CombatDemo/Settings/"+name+".asset";
            var definition=AssetDatabase.LoadAssetAtPath<CombatAttackDefinition>(path);
            if(definition==null){definition=ScriptableObject.CreateInstance<CombatAttackDefinition>();AssetDatabase.CreateAsset(definition,path);}return definition;
        }
        static AnimationClip Dodge(CombatAnimationPresenter p,string name,Vector3 direction)
        {
            return Author(p,name,.38f,false,(anim,t)=>
            {
                float phase=t/.38f,pulse=Mathf.Sin(phase*Mathf.PI);
                for(int side=0;side<2;side++)
                {
                    bool left=side==0;
                    var hip=anim.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                    var knee=anim.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                    var foot=anim.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                    float reach=Mathf.Sin(phase*Mathf.PI*2+(left?0:Mathf.PI))*.28f;
                    Vector3 target=foot.position+p.actor.transform.TransformDirection(direction)*reach+Vector3.up*(Mathf.Max(0,reach)*.30f);
                    Quaternion rotation=foot.rotation;SolveLimb(hip,knee,foot,target,p.actor.transform.forward);foot.rotation=rotation;
                }
                var spine=anim.GetBoneTransform(HumanBodyBones.Spine);
                spine.rotation=Quaternion.AngleAxis(direction.z*18*pulse,p.actor.transform.right)*Quaternion.AngleAxis(-direction.x*18*pulse,p.actor.transform.forward)*spine.rotation;
            });
        }
        static void AirPose(CombatAnimationPresenter p,float tuck,bool landing)
        {
            var actor=p.actor.transform;
            float bend=landing?Mathf.Lerp(28f,8f,tuck):Mathf.Lerp(8f,42f,tuck);
            for(int side=0;side<2;side++)
            {
                bool left=side==0;
                var hip=p.animator.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                var knee=p.animator.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                var foot=p.animator.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                float spread=left?-1f:1f;
                Vector3 target=hip.position+actor.TransformDirection(new Vector3(spread*.12f,-.72f+bend*.004f,.08f+tuck*.12f));
                Quaternion rotation=foot.rotation;
                SolveLimb(hip,knee,foot,target,actor.forward);
                foot.rotation=rotation;
            }
            var spine=p.animator.GetBoneTransform(HumanBodyBones.Spine);
            spine.rotation=Quaternion.AngleAxis(landing?-6f:8f*tuck,actor.right)*spine.rotation;
        }
        static void ThrustPose(CombatAnimationPresenter p,float charge,float extension)
        {
            var a=p.animator;var actor=p.actor.transform;
            var upper=a.GetBoneTransform(HumanBodyBones.RightUpperArm);var lower=a.GetBoneTransform(HumanBodyBones.RightLowerArm);var hand=a.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 target=actor.TransformPoint(new Vector3(.27f,1.24f,Mathf.Lerp(.25f,.03f,charge)+extension*.65f));
            SolveLimb(upper,lower,hand,target,actor.right);
            hand.rotation=Quaternion.LookRotation(actor.right,Vector3.up);
            var leftUpper=a.GetBoneTransform(HumanBodyBones.LeftUpperArm);var leftLower=a.GetBoneTransform(HumanBodyBones.LeftLowerArm);var left=a.GetBoneTransform(HumanBodyBones.LeftHand);
            SolveLimb(leftUpper,leftLower,left,actor.TransformPoint(new Vector3(-.15f,1.2f,.25f)), -actor.right);
            var spine=a.GetBoneTransform(HumanBodyBones.Spine);
            spine.rotation=Quaternion.AngleAxis(Mathf.Lerp(-6,12,extension),actor.right)*spine.rotation;
        }
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
            var directions=new[]{Vector3.back,Vector3.left,Vector3.right};
            var walks=new[]{p.walkBack,p.walkLeft,p.walkRight};var jogs=new[]{p.jogBack,p.jogLeft,p.jogRight};
            for(int i=0;i<3;i++)
            {
                p.walkStrideLengths[i+1]=MeasureStride(session.playerAnimation,walks[i],directions[i]);
                p.jogStrideLengths[i+1]=MeasureStride(session.playerAnimation,jogs[i],directions[i]);
            }
            Directory.CreateDirectory("Evidence/Phase3");
            File.WriteAllText("Evidence/Phase3/stride-calibration.txt","World meters per cycle, F/B/L/R\nWalk="+p.walkStrideLengths.ToString("F4")+"\nJog="+p.jogStrideLengths.ToString("F4"));
            EditorUtility.SetDirty(p); AssetDatabase.SaveAssets();
            Debug.Log("[Phase3] Authored backward, left and right walk/jog clips.");
        }

        static float MeasureStride(CombatAnimationPresenter p,AnimationClip clip,Vector3 direction)
        {
            var graph=PlayableGraph.Create("Measure planted stride");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var input=AnimationClipPlayable.Create(graph,clip);input.SetSpeed(0);input.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(graph,"Measure",p.animator).SetSourcePlayable(input);graph.Play();
            try
            {
                input.SetTime(clip.length*.1);graph.Evaluate(0);p.animator.transform.localPosition=Vector3.zero;
                Vector3 a=p.actor.transform.InverseTransformPoint(p.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
                input.SetTime(clip.length*.5);graph.Evaluate(0);p.animator.transform.localPosition=Vector3.zero;
                Vector3 b=p.actor.transform.InverseTransformPoint(p.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
                return Mathf.Max(.4f,-Vector3.Dot(b-a,direction)/.4f);
            }
            finally {graph.Destroy();p.animator.Rebind();}
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
