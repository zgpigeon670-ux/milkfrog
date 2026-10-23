using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Milkfrog.CombatDemo.BellCrossing
{
    public sealed class BellCrossingSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-bell-smoke")<0)return;
            Application.runInBackground=true;new GameObject("BellCrossing opt-in Windows validation").AddComponent<BellCrossingSmokeProbe>();
        }
        IEnumerator Start()
        {
            yield return null;var level=FindAnyObjectByType<BellCrossingLevel>();
            if(level==null){Application.Quit(2);yield break;}
            level.manualSimulation=true;level.SetPaused(false);level.feedback.sound=false;
            level.cameraRig.ResetImpulse();level.cameraRig.Step(1);level.cameraRig.Suspended=true;
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","SmokeEvidence"));Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(2);bool playerRendered=Capture(level.cameraRig.GetComponent<Camera>(),Path.Combine(folder,"player.png"));
            var start=level.Player.transform.position;level.SubmitInput(new CombatInputFrame());level.SubmitInput(new CombatInputFrame{move=Vector2.up});level.Simulate(.3f);
            bool moved=Vector3.Distance(start,level.Player.transform.position)>.2f&&level.Player.transform.position.y>-.1f;
            level.ClearInput();level.overviewCamera.gameObject.SetActive(true);yield return null;
            bool overviewRendered=Capture(level.overviewCamera,Path.Combine(folder,"overview.png"));
            // Fixture progression is scripted here; combat damage and traversal are checked in PlayMode.
            for(int i=0;i<level.Enemies.Length;i++)level.Enemies[i].Core.RestoreVitals(0,0);
            BellCrossingLevel.Place(level.Player,BellCrossingLayout.WestPoint);bool west=level.Interact();
            BellCrossingLevel.Place(level.Player,BellCrossingLayout.EastPoint);bool east=level.Interact();
            BellCrossingLevel.Place(level.Player,BellCrossingLayout.LeverPoint);bool shortcut=level.Interact();
            BellCrossingLevel.Place(level.Player,BellCrossingLayout.BellPoint);bool finale=level.Interact();
            bool passed=moved&&playerRendered&&overviewRendered&&west&&east&&shortcut&&finale&&level.Completed&&level.Enemies.Length==8;
            File.WriteAllText(Path.Combine(folder,"smoke.json"),JsonUtility.ToJson(new Result{passed=passed,moved=moved,playerRendered=playerRendered,overviewRendered=overviewRendered,scriptedProgression=west&&east&&shortcut&&finale,enemies=level.Enemies.Length,unity=Application.unityVersion},true));
            Debug.Log("[BellCrossing-SMOKE] passed="+passed);Application.Quit(passed?0:3);
        }
        static bool Capture(Camera camera,string path)
        {
            var rt=RenderTexture.GetTemporary(1280,720,24);var old=RenderTexture.active;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
                var pixels=texture.GetPixels32();var first=pixels[0];foreach(var p in pixels)if(Math.Abs(p.r-first.r)+Math.Abs(p.g-first.g)+Math.Abs(p.b-first.b)>40)return true;
                return false;
            }
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Destroy(texture);}
        }
        [Serializable]sealed class Result{public bool passed,moved,playerRendered,overviewRendered,scriptedProgression;public int enemies;public string unity;}
    }
}
