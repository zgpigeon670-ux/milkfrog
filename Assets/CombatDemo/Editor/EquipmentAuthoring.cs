using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Milkfrog.CombatDemo.Editor
{
    public static class EquipmentAuthoring
    {
        const string Root="Assets/CombatDemo";
        public static readonly string[] Ids={"practice-helmet","practice-armor","practice-leggings","light-shoes",EquipmentService.DefaultWeaponId};
        static readonly string[] Names={"练习头盔","练习胸甲","练习护腿","轻便鞋","默认练习剑"};
        static readonly float[] Weights={1,3,2,0,2},Defense={5,12,8,0,0};
        public static ItemDefinition[] Ensure()
        {
            foreach(string folder in new[]{Root+"/Settings/Items",Root+"/Prefabs/Equipment",Root+"/UI/Inventory"})Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();EnsureLayer();
            var player=PrefabUtility.LoadPrefabContents(Root+"/Prefabs/MVP/Player.prefab");
            try
            {
                var presenter=player.GetComponent<CombatAnimationPresenter>();
                var visual=player.GetComponent<EquipmentVisuals>();
                if(visual==null){visual=player.AddComponent<EquipmentVisuals>();visual.modelRoot=presenter.animator.transform;PrefabUtility.SaveAsPrefabAsset(player,Root+"/Prefabs/MVP/Player.prefab");}
                presenter.profile.idle.SampleAnimation(presenter.animator.gameObject,0);
                var bounds=BodyBounds(player.transform,presenter.animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).First());
                var result=new ItemDefinition[5];
                for(int i=0;i<5;i++)
                {
                    string path=Root+"/Settings/Items/"+Ids[i]+".asset";var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                    if(item==null)
                    {
                        item=ScriptableObject.CreateInstance<ItemDefinition>();item.stableId=Ids[i];item.displayName=Names[i];item.category=ItemCategory.Equipment;item.equipmentSlot=(EquipmentSlot)i;
                        item.description=i==4?"保留原有练习剑及攻击动作。武器有重量，格挡防御降低格挡时积累的架势。":i==3?"轻便的布鞋，提高水平移动速度。不会改变跳跃或闪避。":"基础防护装备。穿戴后增加防御与重量，减轻生命伤害并降低移动速度。";
                        item.weight=Weights[i];item.armorDefense=Defense[i];item.guardDefense=i==4?20:0;item.weaponDamage=10;item.moveSpeedBonus=i==3?.1f:0;
                        item.icon=Icon(Ids[i],i);if(i<4)item.visualPrefab=Geometry(i,player.transform,presenter.animator.transform,bounds);
                        AssetDatabase.CreateAsset(item,path);
                    }
                    result[i]=item;
                }
                AssetDatabase.SaveAssets();return result;
            }
            finally{PrefabUtility.UnloadPrefabContents(player);}
        }
        static Bounds BodyBounds(Transform actor,SkinnedMeshRenderer body)
        {
            var mesh=body.sharedMesh;var matrices=body.bones.Select((bone,i)=>bone.localToWorldMatrix*mesh.bindposes[i]).ToArray();var vertices=mesh.vertices;var weights=mesh.boneWeights;
            var bounds=new Bounds();bool first=true;
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];var v=vertices[i];var world=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                var point=actor.InverseTransformPoint(world);if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
            }
            return bounds;
        }
        public static void RefinePlaceholders()
        {
            var player=PrefabUtility.LoadPrefabContents(Root+"/Prefabs/MVP/Player.prefab");
            try
            {
                var presenter=player.GetComponent<CombatAnimationPresenter>();presenter.profile.idle.SampleAnimation(presenter.animator.gameObject,0);
                var bounds=BodyBounds(player.transform,presenter.animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).First());
                for(int i=0;i<4;i++)Geometry(i,player.transform,presenter.animator.transform,bounds,true);
                AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(player);}
        }
        static GameObject Geometry(int kind,Transform actor,Transform model,Bounds bounds,bool replace=false)
        {
            string path=Root+"/Prefabs/Equipment/"+Ids[kind]+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab!=null && !replace)return prefab;
            string materialPath=Root+"/Prefabs/Equipment/PracticeGear.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="PracticeGear"};material.SetColor("_BaseColor",new Color(.3f,.39f,.45f));AssetDatabase.CreateAsset(material,materialPath);}
            var root=new GameObject(Names[kind]);float h=bounds.size.y,w=bounds.size.x;
            if(kind==0)Part(root,model,"spine.004",PrimitiveType.Cylinder,actor.TransformPoint(new Vector3(bounds.center.x,bounds.max.y+h*.025f,bounds.center.z)),new Vector3(h*.28f,h*.025f,h*.23f),material);
            if(kind==1)Part(root,model,"spine.002",PrimitiveType.Cube,actor.TransformPoint(new Vector3(bounds.center.x,bounds.min.y+h*.48f,bounds.max.z-.01f)),new Vector3(w*.50f,h*.26f,h*.06f),material);
            if(kind>=2)foreach(string side in new[]{"L","R"})
            {
                string boneName=(kind==2?"shin.":"foot.")+side;var bone=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name==boneName);
                Vector3 position=bone.position+(kind==2?new Vector3(0,-h*.09f,h*.02f):new Vector3(0,h*.025f,h*.04f));
                Part(root,model,boneName,PrimitiveType.Cube,position,kind==2?new Vector3(w*.13f,h*.18f,h*.1f):new Vector3(w*.16f,h*.05f,h*.15f),material);
            }
            prefab=PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);return prefab;
        }
        static void Part(GameObject root,Transform model,string boneName,PrimitiveType type,Vector3 position,Vector3 size,Material material)
        {
            var bone=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name==boneName);var part=GameObject.CreatePrimitive(type);part.name=boneName;part.transform.SetParent(root.transform,false);
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=material;
            part.AddComponent<EquipmentVisualPart>().boneName=boneName;part.transform.localPosition=bone.InverseTransformPoint(position);part.transform.localRotation=Quaternion.Inverse(bone.rotation);
            var scale=bone.lossyScale;part.transform.localScale=new Vector3(size.x/scale.x,size.y/scale.y,size.z/scale.z);
        }
        static Sprite Icon(string id,int kind)
        {
            string path=Root+"/UI/Inventory/"+id+".png";
            if(!File.Exists(path))
            {
                var texture=new Texture2D(96,96,TextureFormat.RGBA32,false);var pixels=new Color32[96*96];
                for(int y=0;y<96;y++)for(int x=0;x<96;x++)
                {
                    bool visible=kind==0?(x-48)*(x-48)+(y-42)*(y-42)<31*31 && y>20 || y>=19&&y<=27&&x>=12&&x<=84:
                        kind==1?y>=18&&y<=76 && (x>=28&&x<=68 || y>=56&&x>=12&&x<=84):
                        kind==2?y>=14&&y<=77 && ((x>=19&&x<=42)||(x>=54&&x<=77)):
                        kind==3?x>=15&&x<=78&&y>=22&&y<=41 || x>=20&&x<=45&&y>=40&&y<=73:
                        Math.Abs(x-y)<6&&x>=15&&x<=82 || x+y>=57&&x+y<=66&&x>=16&&x<=47&&y>=16&&y<=47;
                    pixels[y*96+x]=visible?new Color32(213,181,119,255):new Color32(0,0,0,0);
                }
                texture.SetPixels32(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static void EnsureLayer()
        {
            if(LayerMask.NameToLayer("InventoryPreview")>=0)return;var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=settings.FindProperty("layers");
            for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)){layers.GetArrayElementAtIndex(i).stringValue="InventoryPreview";settings.ApplyModifiedPropertiesWithoutUndo();return;}
            throw new InvalidOperationException("No free layer for InventoryPreview.");
        }
    }
}
