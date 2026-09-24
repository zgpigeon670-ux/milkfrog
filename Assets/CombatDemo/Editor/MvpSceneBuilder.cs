using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Milkfrog.CombatDemo.Editor
{
    public static class MvpSceneBuilder
    {
        public const string Level = "Assets/CombatDemo/Scenes/MVP_TestLevel.unity";
        public const string Home = "Assets/CombatDemo/Scenes/MainMenu.unity";
        const string Root = "Assets/CombatDemo";
        [MenuItem("Tools/Combat Demo/Create Missing MVP Scenes")]
        public static void Build()
        {
            if (File.Exists(Level) || File.Exists(Home)) { Debug.Log("MVP scenes exist; preserved."); return; }
            Directory.CreateDirectory(Root + "/Prefabs/MVP"); AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(AnimatedDemoBuilder.ScenePath);
            var source = Object.FindAnyObjectByType<CombatDemoSession>();
            var settings = source.settings; var flash = source.feedback.flashMaterial;
            var playerPrefab = SaveActor(source.player, "Player");
            var mobPrefab = SaveActor(source.enemy, "MobEnemy");
            var bossPrefab = SaveActor(source.enemy, "BossEnemy");
            var mobDefinition = ScriptableObject.CreateInstance<EnemyDefinition>();
            mobDefinition.tuning = JsonUtility.FromJson<CombatTuning>(JsonUtility.ToJson(settings.enemy));
            mobDefinition.slash = source.enemy.lightAttack; mobDefinition.speed = settings.enemySpeed;
            mobDefinition.wait = settings.enemyWait; mobDefinition.counterDelay = settings.counterDelay;
            mobDefinition.blocksBeforeDeflect = settings.blocksBeforeDeflect;
            AssetDatabase.CreateAsset(mobDefinition, Root + "/Settings/MobEnemy.asset");
            var bossDefinition = Object.Instantiate(mobDefinition); bossDefinition.displayName = "THE TRAINING GUARDIAN";
            bossDefinition.tuning.maxHealth = 750; bossDefinition.tuning.maxPosture = 350;
            bossDefinition.tuning.ignoreOrdinaryHitStun = true;
            bossDefinition.speed = 3.2f; bossDefinition.wait = .85f;
            bossDefinition.randomAttacks = true;
            bossDefinition.slashWeight = 5; bossDefinition.slowWeight = 3; bossDefinition.perilousWeight = 2;
            var bossAttack = Object.Instantiate(source.enemy.lightAttack);
            bossAttack.rules = JsonUtility.FromJson<AttackParameters>(JsonUtility.ToJson(source.enemy.lightAttack.rules));
            bossAttack.rules.startup = .35f; bossAttack.rules.damage = bossAttack.rules.maxDamage = 28;
            bossAttack.rules.posture = bossAttack.rules.maxPosture = 28;
            bossAttack.rules.block = bossAttack.rules.maxBlock = 44; bossAttack.rules.recovery = .4f;
            AssetDatabase.CreateAsset(bossAttack, Root + "/Settings/BossSlash.asset");
            bossDefinition.slash = bossAttack;
            var bossSlow = Object.Instantiate(source.enemy.slowAttack);
            bossSlow.rules = JsonUtility.FromJson<AttackParameters>(JsonUtility.ToJson(source.enemy.slowAttack.rules));
            bossSlow.rules.damage = bossSlow.rules.maxDamage = 36;
            bossSlow.rules.posture = bossSlow.rules.maxPosture = 38;
            bossSlow.rules.block = bossSlow.rules.maxBlock = 55;
            AssetDatabase.CreateAsset(bossSlow, Root + "/Settings/BossSlow.asset");
            bossDefinition.slow = bossSlow;
            var bossPerilous = Object.Instantiate(source.enemy.perilousAttack);
            bossPerilous.rules = JsonUtility.FromJson<AttackParameters>(JsonUtility.ToJson(source.enemy.perilousAttack.rules));
            bossPerilous.rules.damage = bossPerilous.rules.maxDamage = 42;
            bossPerilous.rules.posture = bossPerilous.rules.maxPosture = 45;
            bossPerilous.rules.block = bossPerilous.rules.maxBlock = 0;
            AssetDatabase.CreateAsset(bossPerilous, Root + "/Settings/BossPerilous.asset");
            bossDefinition.perilous = bossPerilous;
            AssetDatabase.CreateAsset(bossDefinition, Root + "/Settings/BossEnemy.asset");
            ConfigurePrefab<MobEnemy>(mobPrefab, mobDefinition);
            ConfigurePrefab<BossEnemy>(bossPrefab, bossDefinition);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var stone = Material("MvpGround", new Color(.18f,.23f,.26f));
            var wall = Material("MvpBoundary", new Color(.28f,.33f,.36f));
            var gold = Material("MvpArena", new Color(.7f,.40f,.12f));
            var blue = Material("MvpSpawn", new Color(.12f,.5f,.62f));
            Cube("Flat test ground",new Vector3(0,-.25f,0),new Vector3(80,.5f,100),stone);
            Cube("West boundary",new Vector3(-40,1,0),new Vector3(.5f,2,100),wall);
            Cube("East boundary",new Vector3(40,1,0),new Vector3(.5f,2,100),wall);
            Cube("North boundary",new Vector3(0,1,50),new Vector3(80,2,.5f),wall);
            Cube("South boundary",new Vector3(0,1,-50),new Vector3(80,2,.5f),wall);
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(48,-30,0); light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f,.6f,.67f);
            var camera = new GameObject("Main Camera", typeof(Camera),typeof(AudioListener),typeof(DemoCamera)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.fieldOfView = 60; camera.farClipPlane = 180;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f,.075f,.10f);
            var rig = camera.GetComponent<DemoCamera>(); rig.freeOrbit = true; rig.lockOn = false; rig.avoidObstacles = true;
            var root = new GameObject("MVP Session");
            var flow = root.AddComponent<GameFlowController>(); root.AddComponent<MvpMenuView>(); root.AddComponent<MvpHud>();
            var world = root.AddComponent<MvpWorld>();
            world.settings = settings; world.gameplayCamera = camera; world.cameraRig = rig;
            world.player = Spawn(playerPrefab,world.spawn,"Player",camera); rig.player = world.player.transform;
            var feedback = root.AddComponent<CombatFeedback>(); feedback.cameraRig = rig; feedback.flashMaterial = flash;
            world.feedback = feedback;
            var positions = new[] { new Vector3(-10,.02f,-15), new Vector3(10,.02f,-2), new Vector3(-10,.02f,10) };
            var enemies = new EnemyController[4];
            for (int i = 0; i < 3; i++)
            {
                var actor = Spawn(mobPrefab,positions[i],"Patrol "+(i+1),camera);
                var controller = actor.GetComponent<MobEnemy>(); controller.stableId = "mob-"+(i+1); enemies[i] = controller;
                Disc("Patrol home "+(i+1), positions[i],3,new Color(.22f,.30f,.28f));
            }
            Vector3 bossHome = new Vector3(0,.02f,30);
            var boss = Spawn(bossPrefab,bossHome,"Training Guardian",camera).GetComponent<BossEnemy>(); boss.stableId = "boss-guardian"; enemies[3] = boss;
            world.enemies = enemies;
            Disc("Boss trigger zone",bossHome,8,new Color(.34f,.25f,.16f));
            Cube("Safe spawn marker",world.spawn+Vector3.down*.015f,new Vector3(3,.025f,3),blue).GetComponent<Collider>().enabled = false;
            world.arena = new GameObject("Boss arena barrier");
            for (int i = 0; i < 32; i++)
            {
                float angle = i*360f/32; var rotation = Quaternion.Euler(0,angle,0);
                var segment = Cube("Arena segment "+i,bossHome+rotation*Vector3.forward*14+Vector3.up*1.2f,new Vector3(2.9f,2.4f,.35f),gold);
                segment.transform.rotation = rotation; segment.transform.SetParent(world.arena.transform);
            }
            world.arena.SetActive(false);
            camera.transform.position = world.spawn+new Vector3(0,3,-5);
            camera.transform.LookAt(world.spawn+Vector3.up);
            EditorSceneManager.SaveScene(scene,Level);

            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var menuCamera = new GameObject("Menu Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
            menuCamera.clearFlags = CameraClearFlags.SolidColor; menuCamera.backgroundColor = new Color(.025f,.035f,.055f);
            var menu = new GameObject("Game Flow",typeof(MvpMenuView),typeof(GameFlowController)); menu.GetComponent<GameFlowController>().mainMenu = true;
            EditorSceneManager.SaveScene(scene,Home);
            var existing = EditorBuildSettings.scenes.Where(s=>s.path!=Home && s.path!=Level).ToList();
            existing.Insert(0,new EditorBuildSettingsScene(Level,true)); existing.Insert(0,new EditorBuildSettingsScene(Home,true));
            EditorBuildSettings.scenes = existing.ToArray();
            AssetDatabase.SaveAssets(); Debug.Log("[MVP] Scenes, definitions and prefabs generated.");
        }
        static GameObject SaveActor(CombatActor actor,string name)
        {
            var copy = Object.Instantiate(actor.gameObject); copy.name = name; copy.transform.SetParent(null);
            copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            copy.GetComponent<CombatActorView>().viewCamera = null;
            copy.GetComponent<CombatActor>().enforceFactions = true;
            var prefab = PrefabUtility.SaveAsPrefabAsset(copy,Root+"/Prefabs/MVP/"+name+".prefab"); Object.DestroyImmediate(copy); return prefab;
        }
        static void ConfigurePrefab<T>(GameObject prefab,EnemyDefinition definition) where T:EnemyController
        {
            string path = AssetDatabase.GetAssetPath(prefab); var root = PrefabUtility.LoadPrefabContents(path);
            root.AddComponent<T>().definition = definition;
            var combat = root.GetComponent<CombatActor>(); combat.lightAttack = definition.slash;
            if (definition.slow != null) combat.slowAttack = definition.slow;
            if (definition.perilous != null) combat.perilousAttack = definition.perilous;
            PrefabUtility.SaveAsPrefabAsset(root,path); PrefabUtility.UnloadPrefabContents(root);
        }
        static CombatActor Spawn(GameObject prefab,Vector3 position,string name,Camera camera)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab); root.name = name; root.transform.position = position;
            root.GetComponent<CombatActorView>().viewCamera = camera; return root.GetComponent<CombatActor>();
        }
        static Material Material(string name,Color color)
        {
            var result = new Material(Shader.Find("Universal Render Pipeline/Lit")); result.color = color;
            AssetDatabase.CreateAsset(result,Root+"/Materials/"+name+".mat"); return result;
        }
        static GameObject Cube(string name,Vector3 position,Vector3 scale,Material material)
        {
            var result = GameObject.CreatePrimitive(PrimitiveType.Cube); result.name = name;
            result.transform.position = position; result.transform.localScale = scale; result.GetComponent<Renderer>().sharedMaterial = material; return result;
        }
        static void Disc(string name,Vector3 position,float radius,Color color)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); disc.name = name;
            disc.transform.position = new Vector3(position.x,.006f,position.z); disc.transform.localScale = new Vector3(radius*2,.005f,radius*2);
            Object.DestroyImmediate(disc.GetComponent<Collider>()); disc.GetComponent<Renderer>().sharedMaterial = Material(name.Replace(" ",""),color);
        }
        public static void BuildPlayer()
        {
            string requested = Environment.GetEnvironmentVariable("MILKFROG_BUILD_OUTPUT");
            string path = Path.GetFullPath(string.IsNullOrEmpty(requested) ? "Builds/MilkfrogMVP/MilkfrogMVP.exe" : requested);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            bool previousResizable = PlayerSettings.resizableWindow;
            try
            {
                PlayerSettings.resizableWindow = true;
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[]{Home,Level,AnimatedDemoBuilder.ScenePath,CombatDemoBuilder.ScenePath},
                    locationPathName = path, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
                if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("MVP build failed: "+result.summary.result);
                File.Copy(Path.GetFullPath(Root + "/ThirdParty/NotoSansSC/OFL.txt"),
                    Path.Combine(Path.GetDirectoryName(path), "NotoSansSC-OFL.txt"), true);
            }
            finally { PlayerSettings.resizableWindow = previousResizable; }
            Debug.Log("[MVP] Windows build succeeded: "+path);
        }
    }
}
