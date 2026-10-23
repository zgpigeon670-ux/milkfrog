using System;
using System.IO;
using System.Linq;
using System.Globalization;
using Milkfrog.CombatDemo.PCG;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Milkfrog.CombatDemo.Editor
{
    public static class PcgAuthoring
    {
        public const string ScenePath = "Assets/CombatDemo/Scenes/PCG_Whitebox.unity";
        public const string ConfigPath = "Assets/CombatDemo/Settings/PCG/WhiteboxWFC.asset";
        public const string Evidence = "Assets/CombatDemo/Validation/PCG";
        [MenuItem("Tools/Combat Demo/PCG/Create Whitebox Scene")]
        public static void Create()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)); Directory.CreateDirectory(Evidence); AssetDatabase.Refresh();
            var config = AssetDatabase.LoadAssetAtPath<WfcConfig>(ConfigPath);
            if (config == null) { config = ScriptableObject.CreateInstance<WfcConfig>(); AssetDatabase.CreateAsset(config, ConfigPath); }
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.57f, .64f, .7f);
                var sun = new GameObject("Whitebox Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.4f; sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(48, -35, 0);
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(DemoCamera)).GetComponent<Camera>(); camera.tag = "MainCamera";
                camera.fieldOfView = 65; camera.nearClipPlane = .08f; camera.farClipPlane = 200; camera.backgroundColor = new Color(.12f, .17f, .22f); camera.clearFlags = CameraClearFlags.SolidColor;
                var rig = camera.GetComponent<DemoCamera>(); rig.freeOrbit = true; rig.avoidObstacles = true; rig.lockOn = false; rig.orbitDistance = 3.5f;
                var overview = new GameObject("Observation Camera").AddComponent<Camera>(); overview.orthographic = true; overview.orthographicSize = 55; overview.transform.SetPositionAndRotation(new Vector3(0, 100, 0), Quaternion.Euler(90, 0, 0)); overview.farClipPlane = 200;
                overview.clearFlags = CameraClearFlags.SolidColor; overview.backgroundColor = new Color(.055f, .085f, .11f); overview.gameObject.SetActive(false);
                var root = new GameObject("PCG Demo Session"); var demo = root.AddComponent<PcgDemo>();
                demo.config = config; demo.cameraRig = rig; demo.overviewCamera = overview;
                demo.combat = AssetDatabase.LoadAssetAtPath<CombatDemoSettings>("Assets/CombatDemo/Settings/AnimatedCombat.asset");
                demo.enemyDefinition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/CombatDemo/Settings/MobEnemy.asset");
                demo.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CombatDemo/Prefabs/MVP/Player.prefab");
                demo.enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CombatDemo/Prefabs/MVP/MobEnemy.prefab");
                demo.floorMaterial = Material("Floor", new Color(.71f, .76f, .78f)); demo.wallMaterial = Material("Wall", new Color(.82f, .85f, .84f));
                demo.startMaterial = Material("Start", new Color(.15f, .67f, .8f)); demo.goalMaterial = Material("Exit", new Color(.22f, .69f, .43f)); demo.encounterMaterial = Material("Encounter", new Color(.83f, .64f, .41f));
                var feedback = root.AddComponent<CombatFeedback>(); feedback.cameraRig = rig; feedback.flashMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/CombatDemo/Materials/Impact.mat"); demo.feedback = feedback;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            AssetDatabase.SaveAssets(); Debug.Log("[PCG] Scene and configuration ready. Existing scenes/build list unchanged.");
        }
        static Material Material(string name, Color color)
        {
            string path = "Assets/CombatDemo/Settings/PCG/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = color; material.SetFloat("_Smoothness", .1f); AssetDatabase.CreateAsset(material, path); return material;
        }
        [MenuItem("Tools/Combat Demo/PCG/Benchmark 100 Seeds")]
        public static void Benchmark()
        {
            Create(); var config = AssetDatabase.LoadAssetAtPath<WfcConfig>(ConfigPath);
            string csv = "mode,seed,success,localSolutions,attempts,backtracks,collapses,propagations,milliseconds,walkable,rooms,reachable,shortestCells,loops,error\n";
            int hybridPassed = 0, baselinePassed = 0;
            for (int mode = 0; mode < 2; mode++)
                for (int seed = 0; seed < 100; seed++)
                {
                    var result = new WfcSolver(config, seed, mode == 0).Solve(); var s = result.stats;
                    if (result.success) { if (mode == 0) hybridPassed++; else baselinePassed++; }
                    csv += string.Join(",", mode == 0 ? "hybrid" : "unconstrained", seed.ToString(), result.success ? "1" : "0", s.localSolutions.ToString(), s.attempts.ToString(), s.backtracks.ToString(), s.collapses.ToString(), s.propagations.ToString(), s.milliseconds.ToString("F3", CultureInfo.InvariantCulture), s.walkable.ToString(), s.rooms.ToString(), s.reachable.ToString(), s.shortestPath.ToString(), s.loops.ToString(), "\"" + (result.error ?? "").Replace("\"", "\"\"") + "\"") + "\n";
                    if (seed % 10 == 0) Debug.Log($"[PCG] Benchmark mode={mode} seed={seed} passed={result.success} ms={s.milliseconds:F1}");
                }
            File.WriteAllText(Evidence + "/benchmark.csv", csv, new System.Text.UTF8Encoding(false));
            AssetDatabase.Refresh(); Debug.Log($"[PCG] Benchmark complete: hybrid={hybridPassed}/100 baseline={baselinePassed}/100");
            if (hybridPassed != 100) throw new InvalidOperationException("Default hybrid generator did not pass all 100 seeds.");
        }
        public static void Build()
        {
            Create(); string path = Path.GetFullPath("Builds/Milkfrog-PCG/MilkfrogPCG.exe"); Directory.CreateDirectory(Path.GetDirectoryName(path));
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, target = BuildTarget.StandaloneWindows64, locationPathName = path, options = BuildOptions.Development });
            File.WriteAllText(Evidence + "/build.json", JsonUtility.ToJson(new BuildInfo { result = result.summary.result.ToString(), bytes = (long)result.summary.totalSize, seconds = result.summary.totalTime.TotalSeconds }, true));
            if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("PCG build failed.");
            File.Copy("Assets/CombatDemo/ThirdParty/NotoSansSC/OFL.txt", Path.Combine(Path.GetDirectoryName(path), "NotoSansSC-OFL.txt"), true);
            Debug.Log("[PCG] Windows build succeeded: " + path);
        }
        [Serializable] sealed class BuildInfo { public string result; public long bytes; public double seconds; }
    }
}
