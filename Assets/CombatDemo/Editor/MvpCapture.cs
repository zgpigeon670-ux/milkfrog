using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Milkfrog.CombatDemo.Editor
{
    [InitializeOnLoad]
    public static class MvpCapture
    {
        const string Key = "Milkfrog.MvpCapture";
        static EditorWindow view;
        static int stage;
        static bool posed, requested;
        static double started, poseTime;
        static MvpWorld world;
        static string PathName => Path.GetFullPath("Evidence/MVP/"+stage.ToString("00")+".png");
        static MvpCapture() { if (Application.isBatchMode && SessionState.GetBool(Key,false)) Hook(); }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a validation copy in batch mode.");
            Directory.CreateDirectory("Evidence/MVP");
            SessionState.SetString(Key+"Save",Path.Combine(Path.GetTempPath(),"MilkfrogMvpCapture-"+Guid.NewGuid().ToString("N")));
            EditorSceneManager.OpenScene(MvpSceneBuilder.Home);
            ShaderUtil.allowAsyncCompilation=false;
            view=ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")) as EditorWindow;
            view.ShowUtility(); view.position=new Rect(0,0,1280,741);
            SessionState.SetBool(Key,true); Hook(); EditorApplication.isPlaying=true;
        }
        public static void BuildAndCapture() { MvpSceneBuilder.BuildPlayer(); Run(); }
        static void Hook()
        {
            GameFlowController.SaveDirectoryOverride=SessionState.GetString(Key+"Save",null);
            started=EditorApplication.timeSinceStartup; EditorApplication.update-=Update;EditorApplication.update+=Update;
        }
        static void Click(string label)
        {
            foreach(var button in UnityEngine.Object.FindObjectsByType<Button>())
                if(button.GetComponentInChildren<Text>().text==label){button.onClick.Invoke();return;}
            throw new InvalidOperationException("No button "+label);
        }
        static void Update()
        {
            if(EditorApplication.timeSinceStartup-started>160){Finish(1);return;}
            if(!EditorApplication.isPlaying)return;
            var flow=UnityEngine.Object.FindAnyObjectByType<GameFlowController>();
            if(flow==null || flow.Store==null || flow.State==GameFlowState.Loading)return;
            if(stage>=3 && world==null)
            {
                world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>(); if(world==null)return;
                world.manualSimulation=true; if(flow.Paused)flow.TogglePause();
            }
            if(!posed)
            {
                if(view==null)view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                if(stage==0 || stage==3)SetSize(1280,720);
                if(stage==1)SetSize(640,360);
                if(stage==2)SetSize(1280,800);
                if(world!=null)
                {
                    if(stage==4)
                    {
                        MovePlayer(world.enemies[0].Home+Vector3.back*4);
                        world.Director.Tick(.01f);world.Lock.Force(world.enemies[0]);world.cameraRig.Step(1);
                    }
                    if(stage==5)
                    {
                        world.Restore(new PlayerSnapshot{position=new Vector3(0,.02f,-8)});
                        Place(world.enemies[0].Actor,new Vector3(-1,.02f,-5));Place(world.enemies[1].Actor,new Vector3(1,.02f,-4));
                        world.Director.Engage(world.enemies[0]);world.Director.Engage(world.enemies[1]);world.Lock.Force(world.enemies[0]);world.cameraRig.Step(1);
                    }
                    if(stage==6)
                    {
                        world.Restore(new PlayerSnapshot{position=new Vector3(0,.02f,20)});
                        MovePlayer(world.enemies[3].Home+Vector3.back*6); world.Director.Tick(.01f);world.cameraRig.Step(1);
                    }
                    if(stage==7)
                    { world.enemies[3].Actor.Core.RestoreVitals(0,0);world.Simulate(.01f); }
                    if(stage==8){world.Flow.ContinueExploring(); world.Flow.TogglePause();}
                    if(stage==9){world.Flow.TogglePause();world.player.Core.RestoreVitals(0,0);world.Simulate(.01f);}
                    if(stage==10 || stage==11)
                    {
                        if(stage==10)world.Flow.ContinueExploring();
                        world.Restore(new PlayerSnapshot{position=world.spawn});
                        if(stage==10)SetSize(640,360);else SetSize(1280,800);
                    }
                    world.GetComponent<MvpHud>().Refresh();
                }
                posed=true;poseTime=EditorApplication.timeSinceStartup;requested=false;
                if(File.Exists(PathName))File.Delete(PathName);
                Debug.Log("[MVP Capture] stage="+stage+" flow="+flow.State+" encounter="+(world==null?"-":world.Director.State.ToString()));
            }
            if(!requested && EditorApplication.timeSinceStartup-poseTime>.7 && !ShaderUtil.anythingCompiling)
            {ScreenCapture.CaptureScreenshot(PathName);requested=true;}
            if(requested && File.Exists(PathName) && new FileInfo(PathName).Length>1000)
            {
                if(stage==2)Click("Start Game / Continue");
                stage++;posed=false;
                if(stage>=12)Finish(0);
            }
        }
        static void Place(CombatActor actor,Vector3 position)
        {actor.Motor.enabled=false;actor.transform.position=position;actor.Motor.enabled=true;Physics.SyncTransforms();}
        static void MovePlayer(Vector3 position){Place(world.player,position);world.player.transform.rotation=Quaternion.identity;world.cameraRig.yaw=0;}
        static void SetSize(int width,int height)
        {
            // Editor-only capture: fix the render size independently of desktop window limits.
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
            var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");
            var kind=assembly.GetType("UnityEditor.GameViewSizeType");
            var size=Activator.CreateInstance(sizeType,new object[]{Enum.Parse(kind,"FixedResolution"),width,height,"MVP "+width+"x"+height});
            int index=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,index);
        }
        static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Update;EditorApplication.Exit(code);}
    }
}
