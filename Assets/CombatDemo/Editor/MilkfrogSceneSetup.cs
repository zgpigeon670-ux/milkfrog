using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Milkfrog.CombatDemo.Editor
{
    public static class MilkfrogSceneSetup
    {
        const string ModelPath = "Assets/CombatDemo/Animations/milkfrog/milkfrog.fbx";
        const string ScenePath = "Assets/CombatDemo/Scenes/milkfrog.unity";

        public static void Inspect()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            var assets = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
            foreach (var asset in assets.OrderBy(a => a.GetType().Name))
                Debug.Log($"ASSET {asset.GetType().Name} '{asset.name}' id={Unsupported.GetLocalIdentifierInFileForPersistentObject(asset)}");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var bounds = renderer.bounds;
                Debug.Log($"BOUNDS center={bounds.center} size={bounds.size} extents={bounds.extents}");
            }
            foreach (var animator in instance.GetComponentsInChildren<Animator>())
                Debug.Log($"ANIMATOR {animator.name} avatar={(animator.avatar != null ? animator.avatar.name : "none")} human={animator.isHuman}");
            Object.DestroyImmediate(instance);
            EditorApplication.Exit(0);
        }

        public static void Build()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__")).ToArray();
            var idle = clips.FirstOrDefault(c => c.name.EndsWith("idle") || c.name == "idle");
            var walk = clips.FirstOrDefault(c => c.name.EndsWith("walk") || c.name == "walk");
            if (model == null || idle == null || walk == null)
            {
                Debug.LogError($"missing assets model={model != null} idle={idle != null} walk={walk != null} clips={string.Join(",", clips.Select(c => c.name))}");
                EditorApplication.Exit(1);
                return;
            }
            SetLoop(walk);

            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var existing in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToArray())
                if (existing != null && (existing.name == "Ground" || existing.name == "milkfrog")) Object.DestroyImmediate(existing);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4, 1, 4);
            var groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMaterial.color = new Color(.45f, .5f, .42f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;

            var actor = new GameObject("milkfrog");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(actor.transform, false);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var visual = actor.transform.GetChild(0).gameObject;
            var renderers = visual.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.localPosition = new Vector3(0, -bounds.min.y, 0);

            var controller = actor.AddComponent<CharacterController>();
            var height = bounds.size.y;
            controller.height = height;
            controller.radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * .6f, .15f, height * .4f);
            controller.center = new Vector3(0, height * .5f, 0);

            if (visual.GetComponentsInChildren<Transform>(true).All(t => t.name != "metarig"))
            {
                Debug.LogError("metarig missing");
                EditorApplication.Exit(1);
                return;
            }
            visual = actor.transform.GetChild(0).gameObject;
            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = ObjectFactory.AddComponent<Animator>(visual);
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            animator.applyRootMotion = false;
            Debug.Log($"ANIMATOR created={animator != null} avatar={animator.avatar != null}");
            var locomotion = actor.AddComponent<MilkfrogLocomotion>();
            locomotion.animator = animator;
            locomotion.idle = idle;
            locomotion.walk = walk;

            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0, height * .8f, -height * 2.2f);
                camera.transform.LookAt(new Vector3(0, height * .45f, 0));
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"BUILT height={height:0.###} radius={controller.radius:0.###} idle={idle.name} {idle.length:0.###}s walk={walk.name} {walk.length:0.###}s");
            EditorApplication.Exit(0);
        }

        static void SetLoop(AnimationClip clip)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            var changed = false;
            foreach (var description in clips)
            {
                if (!description.name.EndsWith("walk")) continue;
                description.loopTime = true;
                changed = true;
            }
            if (!changed) return;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }
}
