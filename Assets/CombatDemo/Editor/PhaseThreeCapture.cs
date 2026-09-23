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
        static bool ActionsMode => SessionState.GetBool(Key+"Actions",false);
        static EditorWindow view;
        static string PathName => Path.GetFullPath("Evidence/Phase3/"+(ActionsMode?"Actions-":"Locomotion-")+stage+".png");
        static PhaseThreeCapture(){if(Application.isBatchMode && SessionState.GetBool(Key,false))Hook();}
        public static void Locomotion()
        {SessionState.SetBool(Key+"Actions",false);Run();}
        public static void Actions()
        {SessionState.SetBool(Key+"Actions",true);Run();}
        public static void BuildAndCapture(){CombatBenchmarkBatch.BuildPlayer();Actions();}
        static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Validation copy only");
            Directory.CreateDirectory("Evidence/Phase3");
            EditorSceneManager.OpenScene(AnimatedDemoBuilder.ScenePath);
            ShaderUtil.allowAsyncCompilation=false;
            view=ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")) as EditorWindow;
            view.ShowUtility(); view.position=new Rect(0,0,960,621);
            SessionState.SetBool(Key,true); EditorApplication.isPlaying=true; Hook();
        }
        static void Hook(){started=EditorApplication.timeSinceStartup;EditorApplication.update-=Update;EditorApplication.update+=Update;}
        static void Pose()
        {
            session.ResetRound();
            var rig=session.gameplayCamera.GetComponent<DemoCamera>(); rig.enemy=session.player.transform;rig.followBehindPlayer=false;
            rig.offset=new Vector3(2,1.1f,3.6f);rig.ResetImpulse();
            if(!ActionsMode)
            {
                var direction=stage/4==0?Vector3.back:stage/4==1?Vector3.left:Vector3.right;
                int frames=5+(stage%4)*5;
                for(int i=0;i<frames;i++){session.player.Move(direction,4,1f/60);session.playerAnimation.AdvanceVisual(1f/60);}
            }
            else
            {
                session.enemy.gameObject.SetActive(stage<5 || stage>8);
                if(stage<=4)
                {
                    session.SubmitInput(new CombatInputFrame{attackHeld=true,attackPressed=true});Advance(stage==0?.4f:.8f);
                    if(stage>=2){session.SubmitInput(new CombatInputFrame{attackReleased=true});Advance(stage==2?.08f:stage==3?.20f:.4f);}
                }
                else if(stage<=8)
                {session.player.RequestDodge(stage==5?Vector3.forward:stage==6?Vector3.back:stage==7?Vector3.left:Vector3.right);Advance(.12f);}
                else
                {
                    if(view==null)view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                    if(stage==12){view.Show();view.maximized=true;}
                    else view.position=stage==9?new Rect(0,0,640,381):stage==10?new Rect(0,0,1280,741):new Rect(0,0,1280,821);
                    rig.followBehindPlayer=true;rig.offset=new Vector3(1.45f,1.25f,-4.2f);rig.enemy=session.enemy.transform;
                }
                Debug.Log("[Phase3] Capture "+stage+" state="+session.player.Core.State+" charge="+session.player.Core.ChargeRatio);
            }
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
                session.manualSimulation=true;session.SetMode(EnemyMode.Dummy);if(!ActionsMode)session.enemy.gameObject.SetActive(false);Pose();
            }
            if(!requested && Time.frameCount>40 && EditorApplication.timeSinceStartup-posed>.3 && !ShaderUtil.anythingCompiling)
            {ScreenCapture.CaptureScreenshot(PathName);requested=true;}
            if(requested && File.Exists(PathName) && new FileInfo(PathName).Length>1000)
            {if(++stage>=(ActionsMode?13:12)){Finish(0);return;}Pose();}
        }
        static void Advance(float time){while(time>.000001f){float dt=Mathf.Min(time,1f/120);session.Simulate(dt);time-=dt;}}
        static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Update;EditorApplication.Exit(code);}
    }
}
