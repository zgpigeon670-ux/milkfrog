using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Milkfrog.CombatDemo.Editor
{
    [InitializeOnLoad]
    public static class InventoryCapture
    {
        const string Key="Milkfrog.InventoryCapture";
        static EditorWindow view;
        static MvpWorld world;
        static int stage;
        static double started, posedAt;
        static bool posed, requested;
        static string DirectoryPath => Path.GetFullPath(Environment.GetEnvironmentVariable("MILKFROG_INVENTORY_EVIDENCE") ?? "Evidence/Inventory");
        static string Output => Path.Combine(DirectoryPath,stage.ToString("00")+".png");
        static InventoryCapture() {if(Application.isBatchMode && SessionState.GetBool(Key,false))Hook();}
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Capture must use a validation copy in batch mode.");
            SessionState.SetBool(Key,true); SessionState.SetInt(Key+"Stage",0);
            SessionState.SetString(Key+"Save",Path.Combine(Path.GetTempPath(),"MilkfrogInventoryCapture-"+Guid.NewGuid()));
            Directory.CreateDirectory(DirectoryPath); EditorSceneManager.OpenScene(MvpSceneBuilder.Level);
            ShaderUtil.allowAsyncCompilation=false;
            view=ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")) as EditorWindow;
            view.ShowUtility(); view.position=new Rect(0,0,1280,741); Hook(); EditorApplication.isPlaying=true;
        }
        static void Hook()
        {
            stage=SessionState.GetInt(Key+"Stage",0); started=EditorApplication.timeSinceStartup;
            GameFlowController.SaveDirectoryOverride=SessionState.GetString(Key+"Save",null);
            EditorApplication.update-=Update; EditorApplication.update+=Update;
        }
        static void Update()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-started>240){Finish(1);return;}
                if(!EditorApplication.isPlaying)return;
                if(world==null)world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();
                if(world==null || world.Flow.Store==null)return;
                world.manualSimulation=true;
                if(!posed)
                {
                    if(view==null)view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                    world.Flow.CloseInventory();
                    if(world.Flow.Paused)world.Flow.TogglePause();
                    world.Restore(new PlayerSnapshot{position=world.spawn,health=64,posture=30});
                    if(stage>0)
                    {
                        world.Inventory.TryAdd("healing-medicine",3); world.Inventory.TryAdd("calming-pill",3); world.Inventory.TryAdd(WorldStateService.KeyItemId,1);
                    }
                    if(stage==5)world.player.Core.RequestAttack();
                    world.Flow.OpenInventory();
                    var ui=world.Flow.inventoryView;
                    ui.SetCategory(stage==4?2:0);
                    if(stage==6)
                    {
                        for(int i=0;i<36;i++)world.Inventory.TryAdd("验证物品"+i.ToString("00"),i+1);
                        ui.Refresh();
                    }
                    else ui.SelectItem(stage==4?WorldStateService.KeyItemId:"healing-medicine");
                    if(stage>=7) {world.Flow.CloseInventory(); world.Flow.TogglePause();}
                    SetSize(stage==1 || stage==8 ?640:stage==3?1920:1280,stage==1 || stage==8 ?360:stage==3?1080:720);
                    Canvas.ForceUpdateCanvases(); posed=true; requested=false; posedAt=EditorApplication.timeSinceStartup;
                    if(File.Exists(Output))File.Delete(Output);
                    Debug.Log("[Inventory Capture] stage="+stage);
                }
                if(!requested && EditorApplication.timeSinceStartup-posedAt>1 && !ShaderUtil.anythingCompiling)
                {
                    if(stage==6)world.Flow.inventoryView.scroll.verticalNormalizedPosition=.5f;
                    ScreenCapture.CaptureScreenshot(Output); requested=true;
                }
                if(requested && File.Exists(Output) && new FileInfo(Output).Length>1000)
                {
                    stage++;SessionState.SetInt(Key+"Stage",stage);posed=false;
                    if(stage>=9)Finish(0);
                }
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
            var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),width,height,"Inventory "+width+"x"+height});
            int index=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,index);
        }
        static void Finish(int code)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Update;
            EditorApplication.Exit(code);
        }
    }
}
