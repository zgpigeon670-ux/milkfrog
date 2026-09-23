using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Milkfrog.CombatDemo.Editor
{
    // Explicit batch-only capture entry point; no automatic changes in the user's editor.
    [InitializeOnLoad]
    public static class CombatDemoCapture
    {
        const string Key = "Milkfrog.CombatDemo.Capture";
        static double started;
        static bool requested;
        static int stage;
        static double poseAt;
        static CombatDemoSession session;
        static string Output => Path.GetFullPath(SessionState.GetBool(Key + "Animated", false) ? "Evidence/CombatDemo_Animated" + (stage == 0 ? "" : "_" + stage) + ".png" : "Evidence/CombatDemo.png");

        public static void RunAnimated() { SessionState.SetBool(Key + "Animated", true); Run(); }

        static CombatDemoCapture()
        {
            if (Application.isBatchMode && SessionState.GetBool(Key, false)) Hook();
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Capture.Run is a batch-only validation command.");
            Directory.CreateDirectory("Evidence");
            if (File.Exists(Output)) File.Delete(Output);
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.OpenScene(SessionState.GetBool(Key + "Animated", false) ? AnimatedDemoBuilder.ScenePath : CombatDemoBuilder.ScenePath);
            var gameView = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            var window = ScriptableObject.CreateInstance(gameView) as EditorWindow;
            window.ShowUtility();
            window.position = new Rect(0, 0, 1280, 820);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
            Hook();
        }

        static void Hook()
        {
            started = EditorApplication.timeSinceStartup;
            ShaderUtil.allowAsyncCompilation = false;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        static void Update()
        {
            if (EditorApplication.timeSinceStartup - started > 60)
            { Finish(1); return; }
            if (!EditorApplication.isPlaying) return;
            if (session == null)
            {
                session = UnityEngine.Object.FindFirstObjectByType<CombatDemoSession>();
                if (session == null || session.player.Core == null) return;
                session.manualSimulation = true;
                session.ResetRound();
                session.Simulate(.2f);
                poseAt = EditorApplication.timeSinceStartup;
            }
            if (!requested && Time.frameCount > 60 && EditorApplication.timeSinceStartup - started > 12 && EditorApplication.timeSinceStartup - poseAt > .5 && !ShaderUtil.anythingCompiling)
            {
                requested = true;
                ScreenCapture.CaptureScreenshot(Output);
            }
            if (requested && File.Exists(Output) && new FileInfo(Output).Length > 1000)
            {
                if (!SessionState.GetBool(Key + "Animated", false) || stage >= 3) { Finish(0); return; }
                stage++; requested = false; poseAt = EditorApplication.timeSinceStartup;
                if (File.Exists(Output)) File.Delete(Output);
                session.SetMode(stage == 1 ? EnemyMode.Dummy : EnemyMode.Duel);
                session.Simulate(.01f);
                session.SubmitInput(Vector2.zero,false,false,true); session.Simulate(stage == 1 ? .3f : .72f);
                if (stage >= 2)
                {
                    session.SubmitInput(Vector2.zero,false,false,true); session.Simulate(.72f);
                    session.SubmitInput(Vector2.zero,false,false,true);
                    Until(() => session.player.Core.State == CombatState.DeflectedStun);
                    Until(() => session.enemy.Core.State == CombatState.AttackStartup && session.enemy.Core.Remaining <= .08f);
                    session.SubmitInput(Vector2.zero,true,true,false);
                    Until(() => session.enemy.Core.State == CombatState.DeflectedStun);
                }
                if (stage == 3)
                {
                    session.SubmitInput(Vector2.zero,false,false,false);
                    Until(() => session.enemy.Core.CanAct && !session.IsFrozen);
                    session.SubmitInput(Vector2.zero,false,false,true); session.Simulate(.72f);
                    session.SubmitInput(Vector2.zero,false,false,true); session.Simulate(.72f);
                }
                Debug.Log("[CombatDemo] Capture stage " + stage + ": player=" + session.player.Core.State + " enemy=" + session.enemy.Core.State);
            }
        }

        static void Until(Func<bool> condition)
        {
            for (int i = 0; i < 360 && !condition(); i++) session.Simulate(1f / 120);
            if (!condition()) throw new InvalidOperationException("Capture could not reach the expected combat state.");
        }

        static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Update;
            Debug.Log("[CombatDemo] Capture " + (code == 0 ? Output : "timed out"));
            EditorApplication.Exit(code);
        }
    }
}
