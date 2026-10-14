using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;

namespace Milkfrog.CombatDemo.Editor
{
    [InitializeOnLoad]
    public static class EquipmentCapture
    {
        const string Key="Milkfrog.EquipmentCapture";
        static EditorWindow view;
        static MvpWorld world;
        static int stage;
        static double started,posedAt;
        static bool posed,requested;
        static string DirectoryPath=>Path.GetFullPath(Environment.GetEnvironmentVariable("MILKFROG_EQUIPMENT_EVIDENCE")??"Evidence/Equipment");
        static string Output=>Path.Combine(DirectoryPath,stage.ToString("00")+".png");
        static EquipmentCapture(){if(Application.isBatchMode&&SessionState.GetBool(Key,false))Hook();}
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Validation copy only.");
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Stage",0);
            SessionState.SetString(Key+"Save",Path.Combine(Path.GetTempPath(),"EquipmentCapture-"+Guid.NewGuid()));
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
                if(EditorApplication.timeSinceStartup-started>240){Debug.LogError("Equipment capture timed out at stage "+stage+", requested="+requested+", shaders="+ShaderUtil.anythingCompiling);Finish(1);return;}
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
                    var bag=new InventoryService();bag.Restore(snapshot.inventory);
                    for(int i=0;i<3;i++)bag.TryAssignQuickSlot(i,new[]{"healing-medicine","calming-pill","attack-tonic"}[i]);
                    for(int i=0;i<4;i++){var item=world.FindItem(EquipmentAuthoring.Ids[i]);bag.TryAdd(item.stableId,1);if(stage!=0)bag.Equipment.TryEquip(item,true);}
                    if(stage==6)bag.ClearQuickSlot(0);if(stage==7)bag.TryConsume("attack-tonic",3);
                    if(stage==8){var data=bag.Capture();data.quickSlots[2]="future-item";bag.Restore(data);}
                    snapshot.inventory=bag.Capture();world.Restore(snapshot);world.Flow.Store.TrySave(snapshot);world.Flow.Notify("");world.cameraRig.ResetImpulse();world.cameraRig.Step(1);
                    if(stage==10)
                    {
                        for(int i=0;i<40;i++)
                        {
                            var definition=ScriptableObject.CreateInstance<ItemDefinition>();definition.stableId="validation-item-"+i.ToString("00");definition.displayName="测试物品 "+i;definition.restoreHealth=10;definition.icon=world.FindItem("healing-medicine").icon;
                            definition.description=string.Concat(Enumerable.Repeat("长说明验证：此文本应在详情区滚动，不覆盖操作按钮。",8));
                            world.items=world.items.Concat(new[]{definition}).ToArray();world.Inventory.TryAdd(definition.stableId,1);
                        }
                    }
                    world.player.Core.TryApplyAttackBuff(1.25f,20);world.player.Core.Tick(6);
                    if(stage<3||stage>=9)
                    {
                        if(stage==9)world.player.Core.RequestAttack();
                        world.Flow.OpenInventory();world.Flow.inventoryView.SetCategory(0);world.Flow.inventoryView.SelectItem(stage==9?"attack-tonic":"practice-helmet");
                        if(stage==10)world.Flow.inventoryView.SelectItem("validation-item-39");
                        if(stage==11)world.Flow.inventoryView.preview.OnDrag(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,delta=new Vector2(150,0)});
                        if(stage==12)world.Flow.inventoryView.FilterEquipment(EquipmentSlot.Weapon);
                    }
                    world.GetComponent<MvpHud>().Refresh();
                    int size=stage<6?stage%3:1;SetSize(size==0?640:size==1?1280:1920,size==0?360:size==1?720:1080);
                    Canvas.ForceUpdateCanvases();posed=true;requested=false;posedAt=EditorApplication.timeSinceStartup;
                    if(File.Exists(Output))File.Delete(Output);Debug.Log("[Equipment Capture] stage="+stage);
                }
                if(view!=null)view.Repaint();
                if(!requested&&EditorApplication.timeSinceStartup-posedAt>3&&(!ShaderUtil.anythingCompiling||EditorApplication.timeSinceStartup-posedAt>15))
                {ScreenCapture.CaptureScreenshot(Output);requested=true;}
                if(requested&&File.Exists(Output)&&new FileInfo(Output).Length>1000)
                {stage++;SessionState.SetInt(Key+"Stage",stage);posed=false;if(stage>=13)Finish(0);}
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

