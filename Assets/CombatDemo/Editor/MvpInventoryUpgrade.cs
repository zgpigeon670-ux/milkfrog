using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Milkfrog.CombatDemo.Editor
{
    public sealed class MvpInventoryUpgrade : IProcessSceneWithReport
    {
        const string Root = "Assets/CombatDemo";
        public int callbackOrder => 1;
        public void OnProcessScene(Scene scene, BuildReport report) => Validate(scene);

        [MenuItem("Tools/Combat Demo/Upgrade MVP Inventory")]
        public static void Upgrade()
        {
            EditorSceneManager.OpenScene(MvpSceneBuilder.Level);
            var world = UnityEngine.Object.FindAnyObjectByType<MvpWorld>();
            if (world == null) throw new InvalidOperationException("Missing MVP world.");
            foreach (string folder in new[] {Root+"/Settings/Items",Root+"/UI/Inventory"}) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var medicine = EnsureItem("healing-medicine", "疗伤药", "止血疗伤的药品。可在战斗中暂停背包使用；当前动作必须已结束。", ItemCategory.Consumable,40,0,0);
            var calming = EnsureItem("calming-pill", "定心丸", "平复呼吸、稳定架势。可在战斗中使用，架势崩溃期间无法使用。", ItemCategory.Consumable,0,40,1);
            var key = EnsureItem(WorldStateService.KeyItemId,"守门钥匙","用于开启守门人前方的封印门。取得后永久保留。",ItemCategory.Quest,0,0,2);
            bool changed = false;
            var definitions = world.items == null ? new List<ItemDefinition>() : world.items.ToList();
            foreach (var item in new[] {medicine,calming,key}) if (!definitions.Contains(item)) { definitions.Add(item); changed = true; }
            if (changed) { world.items = definitions.ToArray(); EditorUtility.SetDirty(world); }
            changed |= EnsurePickup("pickup-healing-medicine",medicine,new Vector3(-3,.02f,-33));
            changed |= EnsurePickup("pickup-calming-pill",calming,new Vector3(-6,.02f,-33));
            var flow = world.GetComponent<GameFlowController>();
            if (flow.inventoryView == null)
            {
                string path = Root+"/UI/Inventory/InventoryView.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    var root = new GameObject("InventoryView",typeof(RectTransform));
                    var font = Resources.Load<Font>("NotoSansSC-VF") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    InventoryView.BuildLayout(root,font); root.SetActive(false);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root,path); UnityEngine.Object.DestroyImmediate(root);
                }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                flow.inventoryView = instance.GetComponent<InventoryView>(); EditorUtility.SetDirty(flow); changed = true;
            }
            Validate(EditorSceneManager.GetActiveScene());
            if (changed) EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log(changed ? "[Inventory] Incremental upgrade saved." : "[Inventory] Already upgraded; scene preserved.");
        }
        static ItemDefinition EnsureItem(string id,string name,string description,ItemCategory category,float health,float posture,int iconKind)
        {
            string path = Root+"/Settings/Items/"+id+".asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item != null) return item;
            item = ScriptableObject.CreateInstance<ItemDefinition>(); item.stableId=id; item.displayName=name;
            item.description=description; item.category=category; item.restoreHealth=health; item.reducePosture=posture;
            item.icon=EnsureIcon(id,iconKind); AssetDatabase.CreateAsset(item,path); return item;
        }
        static Sprite EnsureIcon(string id,int kind)
        {
            string path=Root+"/UI/Inventory/"+id+".png";
            if (!File.Exists(path))
            {
                var texture=new Texture2D(96,96,TextureFormat.RGBA32,false);
                var pixels=new Color32[96*96];
                for(int y=0;y<96;y++) for(int x=0;x<96;x++)
                {
                    bool shape = kind==0 ? (x>=24 && x<=72 && y>=14 && y<=65) || (x>=34 && x<=62 && y>=65 && y<=81) :
                        kind==1 ? (x-48)*(x-48)+(y-48)*(y-48)<=30*30 :
                        ((x-48)*(x-48)+(y-67)*(y-67)<=18*18 && (x-48)*(x-48)+(y-67)*(y-67)>=9*9) ||
                        (x>=43 && x<=53 && y>=12 && y<=54) || (x>=50 && x<=68 && (y>=13 && y<=23 || y>=31 && y<=41));
                    Color32 color = kind==0 ? new Color32(204,111,99,255) : kind==1 ? new Color32(116,188,151,255) : new Color32(235,190,111,255);
                    if (kind==0 && ((x>=43 && x<=53 && y>=26 && y<=54) || (x>=34 && x<=62 && y>=35 && y<=45))) color=new Color32(255,238,211,255);
                    if (kind==1 && Math.Abs(x-y)<5 && x>=29 && x<=67) color=new Color32(217,235,205,255);
                    pixels[y*96+x]=shape?color:new Color32(0,0,0,0);
                }
                texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite;
            importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static bool EnsurePickup(string id,ItemDefinition item,Vector3 position)
        {
            if(UnityEngine.Object.FindObjectsByType<InventoryPickup>(FindObjectsInactive.Include).Any(x=>x.stableId==id))return false;
            string path=Root+"/Prefabs/MVP/"+id+".prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null)
            {
                var root=new GameObject(item.displayName); var pickup=root.AddComponent<InventoryPickup>(); pickup.stableId=id; pickup.item=item;
                var visual=GameObject.CreatePrimitive(PrimitiveType.Cube); visual.name="Visual"; visual.transform.SetParent(root.transform,false);
                visual.transform.localPosition=Vector3.up*.65f; visual.transform.localScale=new Vector3(.4f,.7f,.4f);
                visual.GetComponent<Collider>().isTrigger=true;
                visual.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/MvpArena.mat");
                pickup.visual=visual; prefab=PrefabUtility.SaveAsPrefabAsset(root,path); UnityEngine.Object.DestroyImmediate(root);
            }
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab); instance.transform.position=position; return true;
        }
        public static void Validate(Scene scene)
        {
            var components=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var world=components.OfType<MvpWorld>().FirstOrDefault(); if(world==null)return;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            if(world.items==null || world.items.Length==0)throw new BuildFailedException("Missing inventory catalog.");
            foreach(var item in world.items)
                if(item==null || string.IsNullOrWhiteSpace(item.stableId) || !ids.Add(item.stableId) || string.IsNullOrWhiteSpace(item.displayName) || string.IsNullOrWhiteSpace(item.description) || item.icon==null ||
                    !Enum.IsDefined(typeof(ItemCategory),item.category) || float.IsNaN(item.restoreHealth) || float.IsInfinity(item.restoreHealth) || item.restoreHealth<0 ||
                    float.IsNaN(item.reducePosture) || float.IsInfinity(item.reducePosture) || item.reducePosture<0 ||
                    (item.category==ItemCategory.Consumable && !item.Usable))throw new BuildFailedException("Invalid or duplicate item definition.");
            var flow = world.GetComponent<GameFlowController>();
            if(flow==null || flow.inventoryView==null || !flow.inventoryView.Configured)throw new BuildFailedException("Missing inventory view configuration.");
            foreach(var pickup in components.OfType<InventoryPickup>())
                if(pickup.item==null || !world.items.Contains(pickup.item) || pickup.quantity<=0 || pickup.visual==null)
                    throw new BuildFailedException("Invalid inventory pickup: "+pickup.name);
            foreach(var key in components.OfType<WorldInteractable>().Where(x=>x.kind==WorldInteractionKind.KeyPickup))
                if(!world.items.Any(x=>x.stableId==key.itemId && x.category==ItemCategory.Quest))throw new BuildFailedException("Missing key item definition.");
            MvpQuestUpgrade.Validate(scene);
        }
        public static void BuildPlayer() { Upgrade(); MvpSceneBuilder.BuildPlayer(); }
    }
}
