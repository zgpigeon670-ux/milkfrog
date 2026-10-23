using System;
using System.IO;
using System.Reflection;
using Milkfrog.CombatDemo.BellCrossing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Milkfrog.CombatDemo.Editor
{
    // These are deliberately staged presentation shots, separate from traversal/combat tests.
    [InitializeOnLoad]
    public static class BellCrossingCapture
    {
        const string Key="Milkfrog.BellCrossingCapture";
        static EditorWindow view;
        static BellCrossingLevel level;
        static int stage;
        static bool posed,requested;
        static double started,poseAt;
        static string Folder=>Path.GetFullPath(BellCrossingAuthoring.Evidence+"/Screenshots");
        static string Output=>Path.Combine(Folder,(stage+1).ToString("00")+".png");
        static BellCrossingCapture(){if(Application.isBatchMode&&SessionState.GetBool(Key,false))Hook();}
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use batch capture for evidence.");
            Directory.CreateDirectory(Folder);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(BellCrossingAuthoring.ScenePath);ShaderUtil.allowAsyncCompilation=false;
            view=ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"))as EditorWindow;
            view.ShowUtility();view.position=new Rect(0,0,1600,921);SetSize();Hook();EditorApplication.isPlaying=true;
        }
        static void Hook(){started=EditorApplication.timeSinceStartup;EditorApplication.update-=Update;EditorApplication.update+=Update;}
        static void Update()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-started>240)throw new TimeoutException("BellCrossing capture stage="+stage);
                if(!EditorApplication.isPlaying)return;
                if(level==null)level=UnityEngine.Object.FindAnyObjectByType<BellCrossingLevel>();if(level==null)return;
                level.manualSimulation=true;
                if(!posed)
                {
                    if(view==null)view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));SetSize();
                    level.Restart();level.feedback.sound=false;
                    if(stage==1)
                    {
                        level.cameraRig.GetComponent<Camera>().enabled=false;level.overviewCamera.gameObject.SetActive(true);
                        level.overviewCamera.orthographicSize=59;level.overviewCamera.transform.SetPositionAndRotation(new Vector3(0,100,0),Quaternion.Euler(90,0,0));
                    }
                    if(stage==2){Place(BellCrossingLayout.RestPoint+Vector3.back*1.5f);level.Interact();}
                    if(stage==3){Defeat(CrossingRoom.WestSeal);Place(BellCrossingLayout.WestPoint+Vector3.back*1.7f);}
                    if(stage==4||stage==5)
                    {
                        Defeat(CrossingRoom.WestSeal);Place(BellCrossingLayout.WestPoint);level.Interact();Defeat(CrossingRoom.EastSeal);Place(BellCrossingLayout.EastPoint);level.Interact();
                        Place(BellCrossingLayout.Center(CrossingRoom.Guardian));level.Simulate(.02f);
                        if(stage==5){Defeat(CrossingRoom.Guardian);level.Simulate(.02f);Place(BellCrossingLayout.BellPoint+Vector3.back*2);level.Interact();}
                    }
                    if(stage==6)level.SetMap(true);
                    if(stage!=1&&stage!=6){level.cameraRig.Suspended=false;level.cameraRig.ResetImpulse();level.cameraRig.Step(1);level.cameraRig.Suspended=true;}
                    posed=true;requested=false;poseAt=EditorApplication.timeSinceStartup;
                    if(File.Exists(Output))File.Delete(Output);Debug.Log("[BellCrossing-Capture] stage="+stage);
                }
                view.Repaint();
                if(!requested&&EditorApplication.timeSinceStartup-poseAt>2&&(!ShaderUtil.anythingCompiling||EditorApplication.timeSinceStartup-poseAt>20)){ScreenCapture.CaptureScreenshot(Output);requested=true;}
                if(requested&&File.Exists(Output)&&new FileInfo(Output).Length>2000){stage++;posed=false;if(stage>=7)Finish(0);}
            }
            catch(Exception error){Debug.LogException(error);Finish(1);}
        }
        static void Place(Vector3 point){BellCrossingLevel.Place(level.Player,point);level.ClearInput();}
        static void Defeat(CrossingRoom room){for(int i=0;i<level.Enemies.Length;i++)if(BellCrossingLayout.EnemyRooms[i]==room)level.Enemies[i].Core.RestoreVitals(0,0);}
        static void SetSize()
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});
            var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),1600,900,"BellCrossing evidence"});
            int index=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,index);
        }
        static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Update;EditorApplication.Exit(code);}
    }
}
