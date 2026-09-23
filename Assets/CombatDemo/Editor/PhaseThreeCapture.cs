using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Milkfrog.CombatDemo.Editor
{
    [InitializeOnLoad]
    public static class PhaseThreeCapture
    {
        const string Key="Milkfrog.Phase3.Capture";
        static CombatDemoSession session;
        static int stage;
        static bool requested;
        static double started, posed;
        static string PathName => Path.GetFullPath("Evidence/Phase3/Locomotion-"+stage+".png");
        static PhaseThreeCapture(){if(Application.isBatchMode && SessionState.GetBool(Key,false))Hook();}
        public static void Locomotion()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Validation copy only");
            Directory.CreateDirectory("Evidence/Phase3");
            EditorSceneManager.OpenScene(AnimatedDemoBuilder.ScenePath);
            ShaderUtil.allowAsyncCompilation=false;
            var view=ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")) as EditorWindow;
            view.ShowUtility(); view.position=new Rect(0,0,960,621);
            SessionState.SetBool(Key,true); EditorApplication.isPlaying=true; Hook();
        }
        static void Hook(){started=EditorApplication.timeSinceStartup;EditorApplication.update-=Update;EditorApplication.update+=Update;}
        static void Pose()
        {
            session.ResetRound();
            var rig=session.gameplayCamera.GetComponent<DemoCamera>(); rig.enemy=session.player.transform;rig.followBehindPlayer=false;
            rig.offset=new Vector3(2,1.1f,3.6f);rig.ResetImpulse();
            var direction=stage/4==0?Vector3.back:stage/4==1?Vector3.left:Vector3.right;
            int frames=5+(stage%4)*5;
            for(int i=0;i<frames;i++){session.player.Move(direction,4,1f/60);session.playerAnimation.AdvanceVisual(1f/60);}
            rig.Step(1);
            posed=EditorApplication.timeSinceStartup; requested=false;
            if(File.Exists(PathName))File.Delete(PathName);
        }
        static void Update()
        {
            if(EditorApplication.timeSinceStartup-started>90){Finish(1);return;}
            if(!EditorApplication.isPlaying)return;
            if(session==null)
            {
                session=UnityEngine.Object.FindFirstObjectByType<CombatDemoSession>();
                if(session==null || session.player.Core==null)return;
                session.manualSimulation=true;session.SetMode(EnemyMode.Dummy);session.enemy.gameObject.SetActive(false);Pose();
            }
            if(!requested && Time.frameCount>40 && EditorApplication.timeSinceStartup-posed>.3 && !ShaderUtil.anythingCompiling)
            {ScreenCapture.CaptureScreenshot(PathName);requested=true;}
            if(requested && File.Exists(PathName) && new FileInfo(PathName).Length>1000)
            {if(++stage>=12){Finish(0);return;}Pose();}
        }
        static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Update;EditorApplication.Exit(code);}
    }
}
