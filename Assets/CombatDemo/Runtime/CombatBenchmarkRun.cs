using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;
namespace Milkfrog.CombatDemo
{
    // Explicit validation harness. Added only by a batch command or -combatBenchmark.
    public sealed class CombatBenchmarkRun : MonoBehaviour
    {
        public event Action Completed;
        CombatDemoSession session;
        CombatPerformanceSampler sampler;
        CombatDebugHud hud;
        CombatActorView playerView, enemyView;
        readonly Stopwatch wall = new Stopwatch();
        readonly StringBuilder csv = new StringBuilder("variant,repetition,frames,average_ms,p95_ms,gc_bytes_per_frame,gc_counter_valid\n");
        double last, stageStart;
        float scriptTime, accumulator;
        int stage, cue, oldRate, oldVsync;
        float oldVolume;
        bool oldBackground;
        bool sampling;
        string output;
        static readonly float[] cues = { .01f,.73f,1.45f,2.20f,2.32f,2.58f,3.30f,4.02f };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CommandLineStart()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-combatBenchmark") >= 0)
                new GameObject("Explicit Combat Benchmark").AddComponent<CombatBenchmarkRun>();
        }
        void Start()
        {
            session = FindFirstObjectByType<CombatDemoSession>();
            if (session == null || session.playerAnimation == null) throw new InvalidOperationException("Open the animated scene for benchmarking.");
            session.manualSimulation = true;
            hud = session.GetComponent<CombatDebugHud>(); playerView = session.player.GetComponent<CombatActorView>(); enemyView = session.enemy.GetComponent<CombatActorView>();
            oldRate = Application.targetFrameRate; oldVsync = QualitySettings.vSyncCount;
            oldVolume = AudioListener.volume; oldBackground = Application.runInBackground;
            AudioListener.volume = 0; Application.runInBackground = true;
            Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            output = Path.GetFullPath(Application.isEditor ? "Evidence/Performance-Editor.csv" : "Evidence/Performance-Standalone.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output + ".environment.txt", "UTC=" + DateTime.UtcNow.ToString("O") + "\nUnity=" + Application.unityVersion + "\nEditor=" + Application.isEditor + "\nDevelopment=" + Debug.isDebugBuild + "\nResolution=" + Screen.width + "x" + Screen.height + "\nQuality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "\nGPU=" + SystemInfo.graphicsDeviceName + "\nCPU=" + SystemInfo.processorType + "\nFrame cap=60; VSync=0 (harness only)\nWarmup=10s; sample=60s; 3 pairs; identical animated scene and deterministic 60Hz combat script.\nGC includes host/editor/rendering allocations; no attribution to combat alone.\n");
            sampler = new CombatPerformanceSampler(); wall.Start(); BeginStage();
        }
        void BeginStage()
        {
            bool optimized = (stage % 2) == 1;
            hud.cacheText = playerView.cacheLabels = enemyView.cacheLabels = optimized;
            sampling = false; stageStart = wall.Elapsed.TotalSeconds; sampler.Clear(); RestartScript();
            Debug.Log("[CombatBenchmark] Stage " + (stage+1) + "/6 " + (optimized ? "cached" : "uncached") + " warmup 10s, sample 60s");
        }
        void RestartScript() { session.SetMode(EnemyMode.Duel); scriptTime = accumulator = 0; cue = 0; }
        void Update()
        {
            if (sampler == null) return;
            double now = wall.Elapsed.TotalSeconds; float dt = (float)(now-last); last = now;
            double elapsed = now-stageStart;
            if (elapsed >= 10 && !sampling) { sampling = true; sampler.Clear(); }
            if (sampling) sampler.Sample(dt);
            accumulator += Mathf.Min(dt,.1f);
            while (accumulator >= 1f/60)
            {
                accumulator -= 1f/60; scriptTime += 1f/60;
                while (cue < cues.Length && scriptTime >= cues[cue])
                {
                    if (cue == 3) session.SubmitInput(Vector2.zero,true,true,false);
                    else if (cue == 4) session.SubmitInput(Vector2.zero,false,false,false);
                    else session.SubmitInput(Vector2.zero,false,false,true);
                    cue++;
                }
                session.Simulate(1f/60);
                if (scriptTime >= 6.5f) RestartScript();
            }
            if (elapsed < 70) return;
            csv.AppendLine(sampler.Csv(stage%2==1 ? "cached" : "uncached",stage/2+1)); File.WriteAllText(output,csv.ToString());
            stage++;
            if (stage < 6) { BeginStage(); return; }
            sampler.Dispose(); sampler = null;
            Debug.Log("[CombatBenchmark] Completed: " + output);
            Completed?.Invoke();
            if (!Application.isEditor) Application.Quit(0);
            enabled = false;
        }
        void OnDestroy()
        {
            sampler?.Dispose(); Application.targetFrameRate = oldRate; QualitySettings.vSyncCount = oldVsync;
            AudioListener.volume = oldVolume; Application.runInBackground = oldBackground;
        }
    }
}
