using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Milkfrog.CombatDemo.Editor
{
    [InitializeOnLoad]
    public static class QuickConsumableCapture
    {
        const string Key="Milkfrog.QuickCapture";
        static EditorWindow view;
        static MvpWorld world;
        static int stage;
        static double started,posedAt;
        static bool posed,requested;
        static string DirectoryPath=>Path.GetFullPath(Environment.GetEnvironmentVariable("MILKFROG_QUICK_EVIDENCE")??"Evidence/QuickConsumables");
        static string Output=>Path.Combine(DirectoryPath,stage.ToString("00")+".png");
        static QuickConsumableCapture(){if(Application.isBatchMode&&SessionState.GetBool(Key,false))Hook();}
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Validation copy only.");
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Stage",0);
            SessionState.SetString(Key+"Save",Path.Combine(Path.GetTempPath(),"QuickCapture-"+Guid.NewGuid()));
            Directory.CreateDirectory(DirectoryPath);EditorSceneManager.OpenScene(MvpSceneBuilder.Level);
            ShaderUtil.allowAsyncCompilation=false;
            view=ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"))as EditorWindow;
            view.ShowUtility();view.position=new Rect(0,0,1280,741);Hook();EditorApplication.isPlaying=true;
        }
        static void Hook()
        {
            stage=SessionState.GetInt(Key+"Stage",0);started=EditorApplication.timeSinceStartup;
            GameFlowController.SaveDirectoryOverride=SessionState.GetString(Key+"Save",null);
            EditorApplication.update-=Update;EditorApplication.update+=Update;
        }
        static void Update()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-started>240){Finish(1);return;}
                if(!EditorApplication.isPlaying)return;
                if(world==null)world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();
                if(world==null||world.Flow.Store==null)return;
                world.manualSimulation=true;
                if(!posed)
                {
                    if(view==null)view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                    world.Flow.CloseInventory();if(world.Flow.Paused)world.Flow.TogglePause();
                    var snapshot=new PlayerSnapshot{position=world.spawn,health=64,posture=30,inventory=new InventoryData{
                        equippedItemId="attack-tonic",entries=new[]{new InventoryEntry{itemId="attack-tonic",quantity=3},
                            new InventoryEntry{itemId="healing-medicine",quantity=3},new InventoryEntry{itemId="calming-pill",quantity=3}}}};
                    if(stage==6)snapshot.inventory.equippedItemId="";
                    if(stage==7)snapshot.inventory.entries=new[]{new InventoryEntry{itemId="healing-medicine",quantity=3}};
                    if(stage==8){snapshot.inventory.equippedItemId="future-item";snapshot.inventory.entries=Array.Empty<InventoryEntry>();}
                    world.Restore(snapshot);world.Flow.Store.TrySave(snapshot);world.Flow.Notify("");world.cameraRig.ResetImpulse();world.cameraRig.Step(1);
                    world.player.Core.TryApplyAttackBuff(1.25f,20);world.player.Core.Tick(6);
                    if(stage<3||stage==9)
                    {
                        if(stage==9)world.player.Core.RequestAttack();
                        world.Flow.OpenInventory();world.Flow.inventoryView.SetCategory(0);world.Flow.inventoryView.SelectItem("attack-tonic");
                    }
                    world.GetComponent<MvpHud>().Refresh();
                    int size=stage<6?stage%3:1;SetSize(size==0?640:size==1?1280:1920,size==0?360:size==1?720:1080);
                    Canvas.ForceUpdateCanvases();posed=true;requested=false;posedAt=EditorApplication.timeSinceStartup;
                    if(File.Exists(Output))File.Delete(Output);Debug.Log("[Quick Capture] stage="+stage);
                }
                if(!requested&&EditorApplication.timeSinceStartup-posedAt>3&&!ShaderUtil.anythingCompiling)
                {ScreenCapture.CaptureScreenshot(Output);requested=true;}
                if(requested&&File.Exists(Output)&&new FileInfo(Output).Length>1000)
                {stage++;SessionState.SetInt(Key+"Stage",stage);posed=false;if(stage>=10)Finish(0);}
            }
            catch(Exception error){Debug.LogException(error);Finish(1);}
        }
        static void SetSize(int width,int height)
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});
            var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),width,height,"Quick "+width+"x"+height});
            int index=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,index);
        }
        static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Update;EditorApplication.Exit(code);}
    }
}
