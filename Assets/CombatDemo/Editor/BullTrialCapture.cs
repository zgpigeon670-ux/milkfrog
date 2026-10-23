using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Milkfrog.CombatDemo.Editor
{
    [InitializeOnLoad]
    public static class BullTrialCapture
    {
        const string Key="Milkfrog.BullCapture";
        static EditorWindow view;
        static MvpWorld world;
        static int stage;
        static bool posed,requested,slamBalance;
        static double started,posedAt;
        static string DirectoryPath=>Path.GetFullPath(Environment.GetEnvironmentVariable("MILKFROG_BULL_EVIDENCE")??"Builds/BullValidation/Screenshots");
        static string Output=>Path.Combine(DirectoryPath,stage.ToString("00")+".png");
        static BullTrialCapture(){if(Application.isBatchMode&&SessionState.GetBool(Key,false))Hook();}
        public static void SlamBalance() { SessionState.SetBool(Key+"Balance",true); Run(); }
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Validation copy only.");
            Directory.CreateDirectory(DirectoryPath);SessionState.SetBool(Key,true);BullTrialSession.Begin(null,false);
            EditorSceneManager.OpenScene(BullTrialAuthoring.ScenePath);ShaderUtil.allowAsyncCompilation=false;
            view=ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"))as EditorWindow;
            view.ShowUtility();view.position=new Rect(0,0,1280,741);Hook();EditorApplication.isPlaying=true;
        }
        static void Hook(){slamBalance=SessionState.GetBool(Key+"Balance",false);started=EditorApplication.timeSinceStartup;EditorApplication.update-=Update;EditorApplication.update+=Update;}
        static void Update()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-started>240)throw new TimeoutException("Bull capture stage="+stage+" requested="+requested);
                if(!EditorApplication.isPlaying)return;
                if(world==null)world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();if(world==null||world.Flow.Store==null)return;
                world.manualSimulation=true;
                if(!posed)
                {
                    if(view==null)view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                    if(world.Flow.InventoryOpen)world.Flow.CloseInventory();if(world.Flow.Paused)world.Flow.TogglePause();
                    var snapshot=BullTrialSession.Fresh();snapshot.position=new Vector3(-3,.02f,-3);world.Restore(snapshot);PlacePlayer(snapshot.position);
                    world.feedback.hitStop=false;world.feedback.cameraImpulse=false;world.Director.Tick(.001f);
                    var boss=(BullBossEnemy)world.enemies[0];
                    if(slamBalance)
                    {
                        boss.TryStartMove(BullMoveKind.Slam);
                        var move=boss.bull.Get(BullMoveKind.Slam);
                        world.Simulate(stage<3 ? .2f : move.attack.startup-move.lockBeforeImpact+.001f);
                        if(stage>=6) { world.player.RequestDodge(Vector3.right); world.Simulate(.55f); }
                    }
                    else if(stage>=3 && stage<=5)boss.TryStartMove(BullMoveKind.Charge);
                    else if(stage==6)boss.TryStartMove(BullMoveKind.Slam);
                    else if(stage==7)boss.TryStartMove(BullMoveKind.Wave);
                    else if(stage==8)
                    {
                        boss.Actor.Core.RestoreVitals(290,0);boss.Tick(.001f);boss.Tick(1.3f);boss.TryStartMove(BullMoveKind.Wave);
                        boss.Actor.Core.Tick(1.8f);
                    }
                    else if(stage==9)
                    {
                        PlacePlayer(boss.transform.position-boss.transform.forward*2);boss.TryStartMove(BullMoveKind.Kick);
                    }
                    else if(stage==10)boss.TryStartMove(BullMoveKind.Horn);
                    else if(stage==12){boss.Actor.Core.RestoreVitals(290,0);boss.Tick(.001f);boss.Tick(.4f);}
                    if(!slamBalance && boss.CurrentMove!=null && stage!=8) { boss.Actor.Core.Tick(boss.CurrentMove.attack.startup*.65f); boss.Tick(0); }
                    boss.AdvancePresentation(.1f);world.GetComponent<MvpHud>().Refresh();
                    int resolution=slamBalance?stage%3:stage<6?stage%3:1;SetSize(resolution==0?640:resolution==1?1280:1920,resolution==0?360:resolution==1?720:1080);
                    world.cameraRig.Suspended=true;world.gameplayCamera.transform.position=new Vector3(7,5,-8);world.gameplayCamera.transform.LookAt(new Vector3(0,1.4f,1));
                    if(stage==11)world.Flow.OpenInventory();
                    Canvas.ForceUpdateCanvases();posed=true;requested=false;posedAt=EditorApplication.timeSinceStartup;if(File.Exists(Output))File.Delete(Output);
                    Debug.Log("[BullCapture] stage="+stage);
                }
                view.Repaint();
                if(!requested&&EditorApplication.timeSinceStartup-posedAt>3&&(!ShaderUtil.anythingCompiling||EditorApplication.timeSinceStartup-posedAt>15)) {ScreenCapture.CaptureScreenshot(Output);requested=true;}
                if(requested&&File.Exists(Output)&&new FileInfo(Output).Length>1000){stage++;posed=false;if(stage==(slamBalance?9:13))Finish(0);}
            }
            catch(Exception error){Debug.LogException(error);Finish(1);}
        }
        static void PlacePlayer(Vector3 value){world.player.Motor.enabled=false;world.player.transform.position=value;world.player.Motor.enabled=true;Physics.SyncTransforms();}
        static void SetSize(int width,int height)
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});
            var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),width,height,"Bull "+width+"x"+height});
            int index=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,index);
        }
        static void Finish(int code){SessionState.SetBool(Key+"Balance",false);SessionState.SetBool(Key,false);EditorApplication.update-=Update;EditorApplication.Exit(code);}
    }
}
