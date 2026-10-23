using System;
using System.IO;
using Milkfrog.CombatDemo.BellCrossing;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Milkfrog.CombatDemo.Editor
{
    public static class BellCrossingAuthoring
    {
        const string Root="Assets/CombatDemo";
        public const string ScenePath=Root+"/Scenes/BellCrossing.unity";
        public const string Evidence=Root+"/Validation/BellCrossing";
        const string Palette=Root+"/Settings/BellCrossing";
        [MenuItem("Tools/Combat Demo/Bell Crossing/Create Independent Level")]
        public static void Create()
        {
            if(File.Exists(ScenePath)) { Debug.Log("[BellCrossing] Existing authored scene retained.");return; }
            Directory.CreateDirectory(Palette);Directory.CreateDirectory(Evidence);AssetDatabase.Refresh();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.39f,.48f,.51f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.007f;RenderSettings.fogColor=new Color(.14f,.24f,.28f);
            var light=new GameObject("Late afternoon through the marsh mist").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.color=new Color(1,.87f,.66f);light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(48,-36,0);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener),typeof(DemoCamera)).GetComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=65;camera.nearClipPlane=.08f;camera.farClipPlane=220;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=RenderSettings.fogColor;
            var rig=camera.GetComponent<DemoCamera>();rig.freeOrbit=true;rig.lockOn=false;rig.avoidObstacles=true;rig.orbitDistance=3.8f;
            var overview=new GameObject("Architect's overhead camera").AddComponent<Camera>();overview.orthographic=true;overview.orthographicSize=62;overview.transform.SetPositionAndRotation(new Vector3(0,100,0),Quaternion.Euler(90,0,0));overview.farClipPlane=200;overview.clearFlags=CameraClearFlags.SolidColor;overview.backgroundColor=RenderSettings.fogColor;overview.gameObject.SetActive(false);
            var root=new GameObject("沉钟渡 · Independent Session");var level=root.AddComponent<BellCrossingLevel>();
            level.combat=AssetDatabase.LoadAssetAtPath<CombatDemoSettings>(Root+"/Settings/AnimatedCombat.asset");
            level.mobDefinition=AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Root+"/Settings/MobEnemy.asset");level.bossDefinition=AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Root+"/Settings/BossEnemy.asset");
            level.playerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/MVP/Player.prefab");level.enemyPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/MVP/MobEnemy.prefab");level.bossPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/MVP/BossEnemy.prefab");
            level.cameraRig=rig;level.overviewCamera=overview;
            var feedback=root.AddComponent<CombatFeedback>();feedback.cameraRig=rig;feedback.flashMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Impact.mat");level.feedback=feedback;
            level.architecture=BellCrossingGeometry.Build(null,Mat("RainStone",new Color(.27f,.34f,.35f)),Mat("OldLimestone",new Color(.44f,.49f,.46f)),Mat("CopperLantern",new Color(.92f,.39f,.12f),true),Mat("JadeLantern",new Color(.19f,.65f,.58f),true),Mat("BellGold",new Color(.86f,.65f,.25f),true));
            camera.transform.position=BellCrossingLayout.Center(CrossingRoom.Dock)+new Vector3(0,3,-4);
            camera.transform.LookAt(BellCrossingLayout.Center(CrossingRoom.Dock)+Vector3.up);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Debug.Log("[BellCrossing] Authored 16 spaces, 17 connections, two seal circuits, checkpoint, shortcut, shrine and finale. Mainline build settings unchanged.");
        }
        static Material Mat(string name,Color color,bool emission=false)
        {
            string path=Palette+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;material.SetFloat("_Smoothness",.18f);
            if(emission){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*.55f);}
            AssetDatabase.CreateAsset(material,path);return material;
        }
        [MenuItem("Tools/Combat Demo/Bell Crossing/Rebuild Prototype Architecture")]
        public static void RebuildArchitecture()
        {
            Create();EditorSceneManager.OpenScene(ScenePath);var level=UnityEngine.Object.FindAnyObjectByType<BellCrossingLevel>();
            UnityEngine.Object.DestroyImmediate(level.architecture);
            level.architecture=BellCrossingGeometry.Build(null,Mat("RainStone",new Color(.27f,.34f,.35f)),Mat("OldLimestone",new Color(.44f,.49f,.46f)),Mat("CopperLantern",new Color(.92f,.39f,.12f),true),Mat("JadeLantern",new Color(.19f,.65f,.58f),true),Mat("BellGold",new Color(.86f,.65f,.25f),true));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Combat Demo/Bell Crossing/Build Windows Playable")]
        public static void Build()
        {
            Create();string target=Path.GetFullPath("Builds/Milkfrog-BellCrossing/Milkfrog-BellCrossing.exe");Directory.CreateDirectory(Path.GetDirectoryName(target));
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.StandaloneWindows64,locationPathName=target,options=BuildOptions.Development});
            Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"/build.json",JsonUtility.ToJson(new BuildInfo{result=result.summary.result.ToString(),bytes=(long)result.summary.totalSize,seconds=result.summary.totalTime.TotalSeconds},true));
            if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("BellCrossing Windows build failed.");
            File.Copy(Root+"/ThirdParty/NotoSansSC/OFL.txt",Path.Combine(Path.GetDirectoryName(target),"NotoSansSC-OFL.txt"),true);
            if(File.Exists("Docs/BellCrossing/README.md"))File.Copy("Docs/BellCrossing/README.md",Path.Combine(Path.GetDirectoryName(target),"游玩说明.md"),true);
            Debug.Log("[BellCrossing] Windows build succeeded: "+target);
        }
        [Serializable]sealed class BuildInfo{public string result;public long bytes;public double seconds;}
    }
}
