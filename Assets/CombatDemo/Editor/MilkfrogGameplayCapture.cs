using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Milkfrog.CombatDemo.Editor
{
    [InitializeOnLoad]
    public static class MilkfrogGameplayCapture
    {
        const string Key="Milkfrog.Animation.GameplayCapture";
        static readonly string[] Names={"idle","jog","light-trail","charge","thrust-trail","parry","dodge","jump","fall","land","death","respawn"};
        static readonly int[] Frames={8,16,18,33,59,3,8,14,22,38,46,8};
        static MvpWorld world;
        static EditorWindow view;
        static int stage,frame;
        static bool prepared,requested,loadingRespawn;
        static double started,requestedAt;
        static string OutDir=>SessionState.GetString(Key+"Output","");
        static string Shot=>Path.Combine(OutDir,stage.ToString("00")+"-"+Names[stage]+".png");
        static MilkfrogGameplayCapture(){if(Application.isBatchMode && SessionState.GetBool(Key,false))Hook();}
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Validation copy only.");
            var output=Environment.GetEnvironmentVariable("MILKFROG_ANIMATION_EVIDENCE")??Path.GetFullPath("Evidence/AnimationRepair/Gameplay");Directory.CreateDirectory(output);
            SessionState.SetString(Key+"Output",output);
            GameFlowController.SaveDirectoryOverride=Path.Combine(Path.GetTempPath(),"MilkfrogAnimationCapture-"+Guid.NewGuid());
            SessionState.SetString(Key+"Save",GameFlowController.SaveDirectoryOverride);
            EditorSceneManager.OpenScene("Assets/CombatDemo/Scenes/MVP_TestLevel.unity");
            ShaderUtil.allowAsyncCompilation=false;
            view=ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")) as EditorWindow;
            view.ShowUtility();view.position=new Rect(0,0,1280,741);
            SessionState.SetBool(Key,true);Hook();EditorApplication.isPlaying=true;
        }
        static void Hook(){started=EditorApplication.timeSinceStartup;EditorApplication.update-=Update;EditorApplication.update+=Update;}
        static void SetCamera()
        {
            world.manualSimulation=true;world.feedback.hitStop=false;
            var hud=UnityEngine.Object.FindAnyObjectByType<MvpHud>();if(hud!=null)hud.showDebug=false;
            var camera=world.gameplayCamera.GetComponent<DemoCamera>();camera.freeOrbit=false;camera.player=world.player.transform;camera.enemy=world.player.transform;
            camera.followBehindPlayer=false;camera.offset=new Vector3(2.5f,1.25f,3.5f);camera.avoidObstacles=false;camera.ResetImpulse();camera.Step(1);
            foreach(var enemy in world.enemies)enemy.gameObject.SetActive(false);
        }
        static void Prepare()
        {
            if(stage==11 && !loadingRespawn)
            {
                loadingRespawn=true;world.Flow.LoadLevel(world.DeathRespawnSnapshot());world=null;return;
            }
            if(stage<11)
            {
                world.player.ResetActor();world.ClearInput();
                world.player.GetComponent<CombatAnimationPresenter>().ResetVisuals();
                world.SubmitInput(new CombatInputFrame());
            }
            SetCamera();frame=0;prepared=true;requested=false;
            if(File.Exists(Shot))File.Delete(Shot);
            switch(stage)
            {
                case 1:world.SubmitInput(new CombatInputFrame{move=Vector2.down});break;
                case 2:world.SubmitInput(new CombatInputFrame{attackPressed=true,attackHeld=true});world.SubmitInput(new CombatInputFrame{attackReleased=true});break;
                case 3:case 4:world.SubmitInput(new CombatInputFrame{attackPressed=true,attackHeld=true});break;
                case 5:
                    var enemy=world.enemies[0].Actor;enemy.Core.Reset();enemy.Core.RequestAttack();enemy.Core.Tick(enemy.Core.ActiveAttack.Startup);
                    world.player.Core.SetGuard(true,true);enemy.Core.TryHit(world.player.Core,true);break;
                case 6:world.player.RequestDodge(Vector3.right);break;
                case 7:case 8:case 9:world.player.TryJump();break;
                case 10:world.player.Core.RestoreVitals(0,0);world.Flow.PlayerDied();break;
            }
        }
        static void Update()
        {
            if(EditorApplication.timeSinceStartup-started>180){Finish(1);return;}
            if(!EditorApplication.isPlaying)return;
            GameFlowController.SaveDirectoryOverride=SessionState.GetString(Key+"Save",null);
            if(world==null)
            {
                world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();
                if(world==null || world.player.Core==null || world.Flow.Store==null)return;
            }
            if(world.Flow.State==GameFlowState.Loading)return;
            if(world.Flow.Paused)world.Flow.TogglePause();
            if(!prepared){Prepare();return;}
            if(frame<Frames[stage])
            {
                if(stage==4 && frame==50)world.SubmitInput(new CombatInputFrame{attackReleased=true});
                if(stage==10)world.player.GetComponent<CombatAnimationPresenter>().AdvanceVisual(1f/60);
                else world.Simulate(1f/60);
                world.gameplayCamera.GetComponent<DemoCamera>().Step(1f/60);frame++;return;
            }
            if(!requested && !ShaderUtil.anythingCompiling)
            {
                var hud=UnityEngine.Object.FindAnyObjectByType<MvpHud>();if(hud!=null)hud.Refresh();
                Debug.Log("[MilkfrogGameplayCapture] "+Names[stage]+" state="+world.player.Core.State);
                ScreenCapture.CaptureScreenshot(Shot);requested=true;requestedAt=EditorApplication.timeSinceStartup;return;
            }
            if(requested && File.Exists(Shot) && new FileInfo(Shot).Length>1000 && EditorApplication.timeSinceStartup-requestedAt>.1)
            {
                if(++stage==Names.Length){Finish(0);return;}prepared=false;
            }
        }
        static void Finish(int code)
        {SessionState.SetBool(Key,false);EditorApplication.update-=Update;GameFlowController.SaveDirectoryOverride=null;EditorApplication.Exit(code);}
    }
}
