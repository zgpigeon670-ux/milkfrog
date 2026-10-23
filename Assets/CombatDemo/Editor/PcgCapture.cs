using System;
using System.IO;
using System.Reflection;
using Milkfrog.CombatDemo.PCG;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Milkfrog.CombatDemo.Editor
{
    [InitializeOnLoad]
    public static class PcgCapture
    {
        const string Key = "Milkfrog.PcgCapture";
        static EditorWindow view;
        static PcgDemo demo;
        static int stage;
        static bool posed, requested;
        static double started, poseAt;
        static string Folder => Path.GetFullPath(PcgAuthoring.Evidence + "/Screenshots");
        static string Output => Path.Combine(Folder, (stage + 1).ToString("00") + ".png");
        static PcgCapture() { if (Application.isBatchMode && SessionState.GetBool(Key, false)) Hook(); }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch capture only.");
            Directory.CreateDirectory(Folder); SessionState.SetBool(Key, true); EditorSceneManager.OpenScene(PcgAuthoring.ScenePath);
            ShaderUtil.allowAsyncCompilation = false;
            view = ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")) as EditorWindow;
            view.ShowUtility(); view.position = new Rect(0, 0, 1600, 921); SetSize(1600, 900);
            Hook(); EditorApplication.isPlaying = true;
        }
        static void Hook() { started = EditorApplication.timeSinceStartup; EditorApplication.update -= Update; EditorApplication.update += Update; }
        static void Update()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 240) throw new TimeoutException("PCG capture stage=" + stage);
                if (!EditorApplication.isPlaying) return;
                if (demo == null) demo = UnityEngine.Object.FindAnyObjectByType<PcgDemo>(); if (demo == null) return;
                demo.manualSimulation = true;
                if (!posed)
                {
                    if (view == null) view = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                    SetSize(1600, 900); demo.SetPaused(false); demo.SetObserving(false); demo.BeginGeneration(stage == 5 ? 42 : 18); demo.FinishGeneration();
                    if (demo.Map == null) throw new InvalidOperationException(demo.Message);
                    demo.feedback.hitStop = demo.feedback.cameraImpulse = false;
                    if (stage == 1 || stage == 5)
                    {
                        demo.cameraRig.Suspended = true; demo.cameraRig.transform.position = new Vector3(35, 75, -45); demo.cameraRig.transform.LookAt(Vector3.zero);
                    }
                    else if (stage == 2)
                    {
                        demo.SetObserving(true);
                    }
                    else if (stage == 3)
                    {
                        Vector3 home = demo.EnemyHomes[0]; Place(home + Vector3.back * 2.6f);
                        demo.SubmitInput(new CombatInputFrame()); demo.SubmitInput(new CombatInputFrame { guardHeld = true, guardPressed = true }); demo.Simulate(.2f);
                        demo.cameraRig.Suspended = true; demo.cameraRig.transform.position = home + new Vector3(3.4f, 3.1f, -4.5f); demo.cameraRig.transform.LookAt(home + new Vector3(0, 1.1f, -.8f));
                    }
                    else if (stage == 4)
                    {
                        demo.SetObserving(true); demo.BeginGeneration(18); for (int i = 0; i < 45 && demo.Generating; i++) demo.AdvanceGeneration();
                    }
                    else if (stage == 6)
                    {
                        Vector3 goal = PcgGeometry.Center(demo.config, demo.Map.goal); Place(goal + Vector3.up * .02f); demo.Simulate(.01f);
                        demo.cameraRig.Suspended = true; demo.cameraRig.transform.position = goal + new Vector3(3.2f, 3.2f, -4); demo.cameraRig.transform.LookAt(goal + Vector3.up);
                    }
                    else { demo.cameraRig.ResetImpulse(); demo.cameraRig.Step(1); demo.cameraRig.Suspended = true; }
                    posed = true; requested = false; poseAt = EditorApplication.timeSinceStartup;
                    if (File.Exists(Output)) File.Delete(Output);
                    Debug.Log("[PCG-CAPTURE] stage=" + stage);
                }
                view.Repaint();
                if (!requested && EditorApplication.timeSinceStartup - poseAt > 2 && (!ShaderUtil.anythingCompiling || EditorApplication.timeSinceStartup - poseAt > 20)) { ScreenCapture.CaptureScreenshot(Output); requested = true; }
                if (requested && File.Exists(Output) && new FileInfo(Output).Length > 2000) { stage++; posed = false; if (stage >= 7) Finish(0); }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        static void Place(Vector3 value) { demo.Player.Motor.enabled = false; demo.Player.transform.position = value; demo.Player.Motor.enabled = true; Physics.SyncTransforms(); }
        static void SetSize(int width, int height)
        {
            var assembly = typeof(UnityEditor.Editor).Assembly; var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), new object[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), width, height, "PCG evidence" });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null); group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, index);
        }
        static void Finish(int code) { SessionState.SetBool(Key, false); EditorApplication.update -= Update; EditorApplication.Exit(code); }
    }
}
