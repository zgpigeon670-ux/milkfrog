using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Milkfrog.CombatDemo.PCG
{
    // Explicit opt-in smoke test for the real Windows player; normal play never writes evidence.
    public sealed class PcgSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-pcg-smoke") >= 0)
            {
                Application.runInBackground = true;
                new GameObject("PCG smoke probe").AddComponent<PcgSmokeProbe>();
            }
        }
        IEnumerator Start()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "SmokeEvidence")); Directory.CreateDirectory(output);
            var deadline = Time.realtimeSinceStartup + 45;
            PcgDemo demo = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                demo = FindAnyObjectByType<PcgDemo>();
                if (demo != null && !demo.Generating && demo.Map != null) break;
                yield return null;
            }
            if (demo == null || demo.Map == null) { Debug.LogError("[PCG-SMOKE] No generated map."); Application.Quit(2); yield break; }
            demo.manualSimulation = true;
            demo.SetPaused(false); demo.cameraRig.Suspended = false;
            demo.cameraRig.ResetImpulse(); demo.cameraRig.Step(1); demo.cameraRig.Suspended = true;
            yield return new WaitForSecondsRealtime(2);
            yield return null; bool playerRendered = Capture(demo.cameraRig.GetComponent<Camera>(), Path.Combine(output, "player.png"));
            var start = demo.Player.transform.position;
            demo.SubmitInput(new CombatInputFrame()); demo.SubmitInput(new CombatInputFrame { move = Vector2.up }); demo.Simulate(.2f);
            bool moved = Vector3.Distance(start, demo.Player.transform.position) > .1f;
            demo.ClearInput(); demo.SetObserving(true);
            yield return new WaitForSecondsRealtime(.5f);
            yield return null; bool overviewRendered = Capture(demo.overviewCamera, Path.Combine(output, "observation.png"));
            bool passed = demo.Map.success && moved && playerRendered && overviewRendered && demo.Enemies.Length == 2 && demo.Player.transform.position.y > -.1f;
            File.WriteAllText(Path.Combine(output, "smoke.json"), JsonUtility.ToJson(new SmokeResult { passed = passed, seed = demo.Map.stats.seed, walkable = demo.Map.stats.walkable, enemies = demo.Enemies.Length, moved = moved, rendered = playerRendered && overviewRendered, tiles = demo.Map.tiles, unity = Application.unityVersion }, true));
            Debug.Log("[PCG-SMOKE] passed=" + passed + " seed=" + demo.Map.stats.seed);
            yield return new WaitForSecondsRealtime(1); Application.Quit(passed ? 0 : 3);
        }
        static bool Capture(Camera camera, string path)
        {
            // Hidden windows can have black backbuffers. Request a real URP camera render instead.
            // This records the Windows player's 3D output; GUI evidence is captured in the Editor.
            var target = RenderTexture.GetTemporary(1280, 720, 24); var previous = RenderTexture.active;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                var pixels = texture.GetPixels32(); var first = pixels[0];
                foreach (var pixel in pixels) if (Math.Abs(pixel.r - first.r) + Math.Abs(pixel.g - first.g) + Math.Abs(pixel.b - first.b) > 30) return true;
                return false;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Destroy(texture); }
        }
        [Serializable] sealed class SmokeResult { public bool passed, moved, rendered; public int seed, walkable, enemies; public int[] tiles; public string unity; }
    }
}
