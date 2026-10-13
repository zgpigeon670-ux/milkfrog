using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Milkfrog.CombatDemo.Editor
{
    public static class MilkfrogAnimationCapture
    {
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use the validation project.");
            ShaderUtil.allowAsyncCompilation = false;
            var outDir = Environment.GetEnvironmentVariable("MILKFROG_ANIMATION_EVIDENCE") ?? Path.GetFullPath("Evidence/AnimationRepair/Unity");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var player = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CombatDemo/Prefabs/MVP/Player.prefab")) as GameObject;
            var presenter = player.GetComponent<CombatAnimationPresenter>();
            UnityEngine.Object.DestroyImmediate(player.GetComponent<CombatActorView>().label.gameObject);
            UnityEngine.Object.DestroyImmediate(player.GetComponent<CombatActorView>().attackMarker.gameObject);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.transform.position = Vector3.down*.15f;ground.transform.localScale=new Vector3(20,.25f,20);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(.14f,.20f,.23f);ground.GetComponent<Renderer>().sharedMaterial=material;
            var light = new GameObject("Preview sun",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(40,-30,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.7f);
            var camera = new GameObject("Preview camera",typeof(Camera)).GetComponent<Camera>();camera.transform.position=new Vector3(2.8f,2.1f, -4);camera.transform.LookAt(Vector3.up*.9f);
            camera.transform.position=new Vector3(2.8f,2.1f,4);camera.transform.LookAt(Vector3.up*.9f);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.14f,.18f);camera.orthographic=true;camera.orthographicSize=1.25f;
            var rt=new RenderTexture(640,640,24);rt.Create();
            var clips=AssetDatabase.LoadAllAssetsAtPath("Assets/CombatDemo/Animations/milkfrog/milkfrog.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).OrderBy(c=>c.name).ToArray();
            Vector3 modelPosition=presenter.animator.transform.localPosition;Quaternion modelRotation=presenter.animator.transform.localRotation;
            foreach(var clip in clips)
            {
                int sampleCount=clip.name=="idle"?1:5;
                for(int i=0;i<sampleCount;i++)
                {
                    float t=sampleCount==1?0:(float)i/(sampleCount-1);
                    clip.SampleAnimation(presenter.animator.gameObject,t*clip.length);
                    presenter.animator.transform.localPosition=modelPosition;presenter.animator.transform.localRotation=modelRotation;
                    Save(camera,rt,Path.Combine(outDir,clip.name+"-"+i+".png"));
                }
            }
            rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            // Render the actual MVP level with the calibrated prefab inherited by its scene instance.
            EditorSceneManager.OpenScene("Assets/CombatDemo/Scenes/MVP_TestLevel.unity");
            rt=new RenderTexture(1280,720,24);rt.Create();
            var world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();
            Save(world.gameplayCamera,rt,Path.Combine(outDir,"MVP-Level.png"));
            rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("[MilkfrogAnimationCapture] Rendered " + clips.Length + " clips and MVP scene into " + outDir);
        }
        static void Save(Camera camera,RenderTexture rt,string path)
        {
            var request=new RenderPipeline.StandardRequest{destination=rt};
            RenderPipeline.SubmitRenderRequest(camera,request);
            var previous=RenderTexture.active;RenderTexture.active=rt;
            var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;
        }
    }
}
