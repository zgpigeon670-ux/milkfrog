using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.Build.Reporting;
using Object = UnityEngine.Object;

namespace Milkfrog.CombatDemo.Editor
{
    public static class BullTrialAuthoring
    {
        const string Root="Assets/CombatDemo";
        public const string ScenePath=Root+"/Scenes/BullTrial.unity";
        const string Folder=Root+"/Art/YellowBull";
        const string PrefabPath=Root+"/Prefabs/MVP/YellowBull.prefab";
        [MenuItem("Tools/Combat Demo/Upgrade Yellow Bull Trial")]
        public static void Upgrade()
        {
            AssetDatabase.Refresh();
            string fbx=Folder+"/YellowBull.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(fbx);
            if(importer==null)throw new InvalidOperationException("Run Tools/Author-YellowBull.py first.");
            if(importer.animationType!=ModelImporterAnimationType.Generic || importer.globalScale!=1 || !importer.importAnimation || importer.avatarSetup!=ModelImporterAvatarSetup.CreateFromThisModel)
            {importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.globalScale=1;importer.importAnimation=true;importer.SaveAndReimport();}
            var clips=AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            AnimationClip Clip(string suffix)=>clips.FirstOrDefault(c=>c.name.EndsWith(suffix,StringComparison.Ordinal))??throw new InvalidOperationException("Missing "+suffix+": "+string.Join(",",clips.Select(c=>c.name)));
            var d=AssetDatabase.LoadAssetAtPath<BullBossDefinition>(Folder+"/BullBoss.asset");
            if(d==null)
            {
                d=ScriptableObject.CreateInstance<BullBossDefinition>();d.idle=Clip("Bull_Idle");d.walk=Clip("Bull_Walk");d.hit=Clip("Bull_Hit");d.death=Clip("Bull_Death");d.broken=Clip("Bull_Broken");d.roar=Clip("Bull_Roar");
                d.moves=new[]{Move(BullMoveKind.Horn,.7f,.18f,.8f,18,22,26,3,1,Clip("Bull_Horn")),Move(BullMoveKind.Kick,.65f,.16f,.9f,20,26,32,2.8f,1,Clip("Bull_Kick")),Move(BullMoveKind.Charge,1,1,1.5f,30,32,0,10,.85f,Clip("Bull_Charge")),Move(BullMoveKind.Slam,1.4f,.18f,1.8f,22,24,0,1.8f,1,Clip("Bull_Slam")),Move(BullMoveKind.Wave,1,8f/6,1.2f,22,25,0,8,.14f,Clip("Bull_Wave"))};
                d.Get(BullMoveKind.Charge).moveSpeed=10;d.Get(BullMoveKind.Kick).cooldown=6;d.Get(BullMoveKind.Kick).halfAngle=55;
                d.Get(BullMoveKind.Slam).lockBeforeImpact=.8f;
                AssetDatabase.CreateAsset(d,Folder+"/BullBoss.asset");
            }
            if(!d.IsValid)throw new InvalidOperationException("Invalid BullBoss configuration.");
            var def=AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Folder+"/BullEnemy.asset");
            if(def==null)
            {
                def=ScriptableObject.CreateInstance<EnemyDefinition>();def.displayName="黄牛·踏地";def.tuning.maxHealth=600;def.tuning.maxPosture=300;def.tuning.ignoreOrdinaryHitStun=true;def.tuning.brokenDuration=3;def.speed=2.2f;def.bossTriggerRadius=11;
                AssetDatabase.CreateAsset(def,Folder+"/BullEnemy.asset");
            }
            var warning=Material("AttackWarning",new Color(1,.2f,.04f),true);
            var lineShader=Shader.Find("Milkfrog/BullWarning");
            if(lineShader==null)throw new InvalidOperationException("Missing bull warning shader.");
            if(warning.shader!=lineShader){warning.shader=lineShader;EditorUtility.SetDirty(warning);}
            if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)==null)
            {
                var root=new GameObject("Yellow Bull");var motor=root.AddComponent<CharacterController>();motor.height=3.6f;motor.radius=.78f;motor.center=new Vector3(0,1.8f,0);motor.stepOffset=.25f;
                root.AddComponent<CombatActor>();var enemy=root.AddComponent<BullBossEnemy>();enemy.stableId="boss-yellow-bull";enemy.definition=def;enemy.bull=d;
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));model.transform.SetParent(root.transform,false);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>Material(m.name.Replace(" (Instance)",""),m.color)).ToArray();
                var view=root.AddComponent<BullPresentation>();view.animator=model.GetComponentInChildren<Animator>();view.warningMaterial=warning;enemy.presentation=view;
                var anchor=new GameObject("Chest Lock Target").transform;anchor.SetParent(root.transform,false);anchor.localPosition=new Vector3(0,2.25f,0);enemy.lockAnchor=anchor;
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);Object.DestroyImmediate(root);
            }
            var contents=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view=contents.GetComponent<BullPresentation>();
                if(view.animator==null)
                {
                    view.animator=contents.GetComponentInChildren<Animator>();
                    if(view.animator==null)view.animator=contents.transform.GetChild(0).gameObject.AddComponent<Animator>();
                    PrefabUtility.SaveAsPrefabAsset(contents,PrefabPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            if(!File.Exists(ScenePath))CreateScene();
            EditorSceneManager.OpenScene(ScenePath);
            var trialGround=GameObject.Find("Circular Trial Ground");
            if(trialGround.GetComponent<CapsuleCollider>()!=null)
            {
                Object.DestroyImmediate(trialGround.GetComponent<CapsuleCollider>());
                trialGround.AddComponent<MeshCollider>().sharedMesh=trialGround.GetComponent<MeshFilter>().sharedMesh;
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }
            if(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath))EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
            AssetDatabase.SaveAssets();Debug.Log("[Bull] Independent model, 11 clips, five moves and trial ready.");
        }
        static BullMove Move(BullMoveKind kind,float startup,float active,float recovery,float damage,float posture,float block,float range,float width,AnimationClip clip)
        {
            bool perilous=kind>=BullMoveKind.Charge;
            return new BullMove{kind=kind,clip=clip,range=range,halfWidth=width,attack=new AttackParameters{kind=perilous?AttackKind.Perilous:AttackKind.Light,response=perilous?AttackResponse.DodgeOnly:AttackResponse.Deflectable,startup=startup,active=active,recovery=recovery,damage=damage,maxDamage=damage,posture=posture,maxPosture=posture,block=block,maxBlock=block,deflectPosture=30,cancelWindow=0}};
        }
        static readonly System.Collections.Generic.Dictionary<string,Color> Palette = new System.Collections.Generic.Dictionary<string,Color> {
            {"GoldenCoat",new Color(1,.66f,.025f)}, {"CreamMuzzle",new Color(1,.8f,.28f)}, {"HornIvory",new Color(.94f,.88f,.63f)},
            {"HoofBrown",new Color(.18f,.075f,.028f)}, {"EyeWhite",new Color(.99f,.97f,.86f)}, {"EyeBlack",new Color(.023f,.017f,.01f)}, {"NoseBrown",new Color(.26f,.115f,.035f)} };
        public static void RefinePalette()
        {
            foreach(var pair in Palette) { var m=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+pair.Key+".mat"); if(m!=null){m.color=pair.Value;EditorUtility.SetDirty(m);} }
            AssetDatabase.SaveAssets();
        }
        static Material Material(string name,Color color,bool unlit=false)
        {
            string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;
            if(Palette.TryGetValue(name,out var paletteColor))color=paletteColor;
            m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));m.color=color;
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(m,path);return m;
        }
        static void CreateScene()
        {
            EditorSceneManager.OpenScene(Root+"/Scenes/MVP_TestLevel.unity");var original=Object.FindAnyObjectByType<MvpWorld>();string settingsPath=AssetDatabase.GetAssetPath(original.settings);string flashPath=AssetDatabase.GetAssetPath(original.feedback.flashMaterial);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cylinder);ground.name="Circular Trial Ground";ground.transform.position=new Vector3(0,-.2f,0);ground.transform.localScale=new Vector3(36,.2f,36);ground.GetComponent<Renderer>().sharedMaterial=Material("TrialStone",new Color(.22f,.27f,.24f));
            Object.DestroyImmediate(ground.GetComponent<Collider>());ground.AddComponent<MeshCollider>().sharedMesh=ground.GetComponent<MeshFilter>().sharedMesh;
            var boundary=Material("TrialBoundary",new Color(.36f,.3f,.16f));
            for(int i=0;i<48;i++)
            {
                float angle=i*360f/48;var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Boundary "+i;wall.transform.position=Quaternion.Euler(0,angle,0)*Vector3.forward*18.4f+Vector3.up*.9f;wall.transform.rotation=Quaternion.Euler(0,angle,0);wall.transform.localScale=new Vector3(2.5f,1.8f,.5f);wall.GetComponent<Renderer>().sharedMaterial=boundary;
            }
            var sun=new GameObject("Trial Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-35,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.59f,.5f);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener),typeof(DemoCamera)).GetComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=60;camera.backgroundColor=new Color(.14f,.19f,.23f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.cullingMask &= ~(1<<LayerMask.NameToLayer("InventoryPreview"));var rig=camera.GetComponent<DemoCamera>();rig.freeOrbit=true;rig.avoidObstacles=true;
            var root=new GameObject("Bull Trial Session");var flow=root.AddComponent<GameFlowController>();root.AddComponent<MvpMenuView>();root.AddComponent<MvpHud>();var world=root.AddComponent<MvpWorld>();world.bullTrial=true;world.settings=AssetDatabase.LoadAssetAtPath<CombatDemoSettings>(settingsPath);world.spawn=new Vector3(0,.02f,-10);world.cameraRig=rig;world.gameplayCamera=camera;
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/MVP/Player.prefab"));player.transform.position=world.spawn;world.player=player.GetComponent<CombatActor>();rig.player=player.transform;
            foreach(var view in player.GetComponentsInChildren<CombatActorView>())view.viewCamera=camera;
            var enemy=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));enemy.transform.position=new Vector3(0,.02f,4);enemy.transform.rotation=Quaternion.Euler(0,180,0);world.enemies=new EnemyController[]{enemy.GetComponent<BullBossEnemy>()};
            var feedback=root.AddComponent<CombatFeedback>();feedback.cameraRig=rig;feedback.flashMaterial=AssetDatabase.LoadAssetAtPath<Material>(flashPath);world.feedback=feedback;
            world.items=AssetDatabase.FindAssets("t:ItemDefinition",new[]{Root+"/Settings/Items"}).Select(g=>AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();world.bonfires=Array.Empty<BonfireCheckpoint>();
            var inventory=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/UI/Inventory/InventoryView.prefab"));flow.inventoryView=inventory.GetComponent<InventoryView>();inventory.SetActive(false);
            camera.transform.position=world.spawn+new Vector3(0,3,-5);camera.transform.LookAt(enemy.transform.position+Vector3.up*2);
            EditorSceneManager.SaveScene(scene,ScenePath);
        }
        public static void BuildPlayer()
        {
            Upgrade();string path=Path.GetFullPath(Environment.GetEnvironmentVariable("MILKFROG_BUILD_OUTPUT")??"Builds/MilkfrogMVP-YellowBull/MilkfrogMVP.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Root+"/Scenes/MainMenu.unity",Root+"/Scenes/MVP_TestLevel.unity",ScenePath,AnimatedDemoBuilder.ScenePath,CombatDemoBuilder.ScenePath},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Bull build failed: "+result.summary.result);
            File.Copy(Root+"/ThirdParty/NotoSansSC/OFL.txt",Path.Combine(Path.GetDirectoryName(path),"NotoSansSC-OFL.txt"),true);Debug.Log("[Bull] Windows build succeeded.");
        }
    }
}

