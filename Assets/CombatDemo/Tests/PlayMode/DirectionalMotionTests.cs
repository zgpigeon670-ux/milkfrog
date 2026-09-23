using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
namespace Milkfrog.CombatDemo.Tests
{
    public class DirectionalMotionTests
    {
        [UnityTest] public IEnumerator AuthoredDirectionsDriveLegsWithoutRotatingTheModel()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/CombatDemo_Animated.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("CombatDemo_Animated");
#endif
            yield return null;
            var session=Object.FindFirstObjectByType<CombatDemoSession>(); session.manualSimulation=true; session.SetMode(EnemyMode.Dummy);
            var p=session.playerAnimation; var profile=p.profile;
            foreach(var clip in new[]{profile.walkBack,profile.jogBack,profile.walkLeft,profile.jogLeft,profile.walkRight,profile.jogRight})
                Assert.That(clip!=null && clip.humanMotion && clip.isLooping,Is.True,"Missing authored directional Humanoid loop");
            var foot=p.animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            foreach(var direction in new[]{Vector3.back,Vector3.left,Vector3.right})
            {
                session.ResetRound(); Vector3 root=p.animator.transform.localPosition;
                Vector3 before=p.animator.transform.InverseTransformPoint(foot.position);
                for(int i=0;i<12;i++) {session.player.Move(direction,4,1f/60); p.AdvanceVisual(1f/60);}
                Vector3 after=p.animator.transform.InverseTransformPoint(foot.position);
                int index=direction==Vector3.back?1:direction==Vector3.left?2:3;
                Assert.That(p.DirectionWeights[index],Is.GreaterThan(.95f));
                Assert.That(Vector3.Distance(before,after),Is.GreaterThan(.05f),"Feet must articulate instead of holding a sliding pose");
                Assert.That(p.animator.transform.localPosition,Is.EqualTo(root));
                Assert.That(Quaternion.Angle(p.animator.transform.localRotation,Quaternion.identity),Is.LessThan(.01f));
            }
        }
    }
}
