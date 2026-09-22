using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Milkfrog.CombatDemo.Editor
{
    [InitializeOnLoad]
    public static class CombatBenchmarkBatch
    {
        const string Key = "Milkfrog.CombatDemo.Benchmark";
        static bool attached;
        static CombatBenchmarkBatch() { if (Application.isBatchMode && SessionState.GetBool(Key,false)) EditorApplication.update += Attach; }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch only.");
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.OpenScene(AnimatedDemoBuilder.ScenePath);
            var view = ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")) as EditorWindow;
            view.ShowUtility(); view.position = new Rect(0,0,1280,820);
            SessionState.SetBool(Key,true); EditorApplication.isPlaying = true;
            EditorApplication.update -= Attach; EditorApplication.update += Attach;
        }
        static void Attach()
        {
            if (!EditorApplication.isPlaying || attached) return;
            var session = UnityEngine.Object.FindFirstObjectByType<CombatDemoSession>();
            if (session == null || session.player.Core == null) return;
            attached = true; var run = new GameObject("Explicit Editor Benchmark").AddComponent<CombatBenchmarkRun>();
            run.Completed += () => { SessionState.SetBool(Key,false); EditorApplication.Exit(0); };
            EditorApplication.update -= Attach;
        }
        public static void BuildPlayer()
        {
            EditorSceneManager.OpenScene(AnimatedDemoBuilder.ScenePath);
            foreach (var view in UnityEngine.Object.FindObjectsByType<CombatActorView>())
            { view.retainFactionColor = true; view.label.transform.localPosition = Vector3.up * 2.2f; }
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            string output = Path.GetFullPath("Builds/CombatDemo/CombatDemo.exe"); Directory.CreateDirectory(Path.GetDirectoryName(output));
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { AnimatedDemoBuilder.ScenePath, CombatDemoBuilder.ScenePath }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build failed: " + result.summary.result);
            Debug.Log("[CombatDemo] Standalone development build: " + output);
        }
        public static void BuildAndCapture() { BuildPlayer(); CombatDemoCapture.RunAnimated(); }
        public static void BuildAndBenchmark() { BuildPlayer(); Run(); }
    }
}

