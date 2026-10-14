using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Milkfrog.CombatDemo.Editor
{
    public static class MilkfrogAnimationUpgrade
    {
        const string Root = "Assets/CombatDemo/";
        const string Model = Root + "Animations/milkfrog/milkfrog.fbx";
        const string Player = Root + "Prefabs/MVP/Player.prefab";
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated validation project.");
            ConfigureImporter();
            var clips = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            if (clips.Count != 28) throw new InvalidOperationException("Expected 28 Milkfrog clips, found " + clips.Count);
            var profile = AssetDatabase.LoadAssetAtPath<CombatAnimationProfile>(Root + "Animations/MilkfrogCombat.asset");
            var serialized = new SerializedObject(profile);
            foreach (string name in new[] { "idle", "walk", "jog", "hit", "death", "guard", "parry", "deflected", "broken", "walkBack", "jogBack", "walkLeft", "jogLeft", "walkRight", "jogRight", "dodgeForward", "dodgeBack", "dodgeLeft", "dodgeRight", "charge", "thrust", "jump", "fall", "land" })
                serialized.FindProperty(name).objectReferenceValue = clips[name];
            serialized.FindProperty("attack").objectReferenceValue = clips["light"];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // Walk is kept unchanged. The new in-place gaits are calibrated to their authored stride.
            profile.walkStrideLengths = new Vector4(profile.walkStrideLengths.x, .76f, .76f, .76f);
            profile.jogStrideLengths = new Vector4(1.2f, 1.2f, 1.2f, 1.2f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);

            var instance = PrefabUtility.LoadPrefabContents(Player);
            var report = new StringBuilder();
            try
            {
                var presenter = instance.GetComponent<CombatAnimationPresenter>();
                var actor = instance.GetComponent<CombatActor>();
                var model = presenter.animator.transform;
                presenter.profile = profile;
                presenter.animator.applyRootMotion = false;
                presenter.animator.runtimeAnimatorController = null;
                if (presenter.animator.isHuman) throw new InvalidOperationException("Milkfrog must remain Generic.");
                // Recenter in actor space after sampling the protected idle, keeping the established scale.
                model.localPosition = Vector3.zero;
                clips["idle"].SampleAnimation(model.gameObject, 0);
                var body = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "tripo_node_02f756cc");
                Vector3 min = Vector3.one * float.PositiveInfinity, max = Vector3.one * float.NegativeInfinity;
                var mesh = body.sharedMesh;
                // Skin bind matrices give world-space geometry without BakeMesh's prefab-stage scale cache.
                var matrices = body.bones.Select((bone,i)=>bone.localToWorldMatrix * mesh.bindposes[i]).ToArray();
                var vertices = mesh.vertices; var weights = mesh.boneWeights;
                for (int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];var v=vertices[i];
                    var world = matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0 + matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1 + matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2 + matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                    var point = instance.transform.InverseTransformPoint(world);
                    min = Vector3.Min(min, point); max = Vector3.Max(max, point);
                }
                Vector3 center = (min + max) * .5f;
                model.localPosition = new Vector3(-center.x, .02f - min.y, -center.z);
                instance.GetComponent<CombatActorView>().body = body;
                report.AppendLine("Idle actor-space body bounds before centering: " + min + " / " + max);
                report.AppendLine("Model offset: " + model.localPosition);
                var sword = model.GetComponentsInChildren<Transform>(true).Single(t => t.name == "sword");
                // The source sword's blade is along local negative Z; +Z is the handle.
                presenter.bladeRoot = Endpoint(sword, "Blade Root", new Vector3(0, 0, -.02f));
                presenter.bladeTip = Endpoint(sword, "Blade Tip", new Vector3(0, 0, -.78657365f));
                var trail = presenter.bladeTip.GetComponent<TrailRenderer>();
                if (trail == null) trail = presenter.bladeTip.gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = TrailMaterial();
                trail.time = .085f; trail.startWidth = .075f; trail.endWidth = 0;
                trail.minVertexDistance = .015f; trail.emitting = false; trail.autodestruct = false;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false; trail.Clear();
                presenter.swordTrail = trail;
                foreach (var pair in new[] { (actor.lightAttack, "light", "MilkfrogSlashTrace"), (actor.thrustAttack, "thrust", "MilkfrogThrustTrace"), (actor.followupAttack, "followup", "MilkfrogFollowupTrace") })
                {
                    var definition = pair.Item1;
                    definition.clip = clips[pair.Item2];
                    // Keep gameplay timing and normalized active markers; the source actions match them.
                    definition.trace = CombatBladeTraceBaker.Bake(presenter, definition, Root + "Animations/" + pair.Item3 + ".asset");
                    definition.trace.name = pair.Item3;
                    AssetDatabase.SaveAssetIfDirty(definition.trace);
                    EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
                    report.AppendLine(definition.name + " -> " + definition.clip.name + " / " + definition.trace.name);
                }
                actor.bladeTrace = actor.lightAttack.trace;
                clips["idle"].SampleAnimation(model.gameObject, 0);
                // Sampling may contain model-level bindings; reapply the calibrated controller offset.
                model.localPosition = new Vector3(-center.x, .02f - min.y, -center.z);
                PrefabUtility.SaveAsPrefabAsset(instance, Player);
                report.AppendLine("Sword endpoints: " + presenter.bladeRoot.localPosition + " / " + presenter.bladeTip.localPosition);
                foreach (var c in clips.Values.OrderBy(c => c.name)) report.AppendLine(c.name + " length=" + c.length + " fps=" + c.frameRate + " loop=" + c.isLooping);
            }
            finally { PrefabUtility.UnloadPrefabContents(instance); }
            Directory.CreateDirectory("Evidence/AnimationRepair");
            File.WriteAllText("Evidence/AnimationRepair/import-and-prefab.txt", report.ToString());
            Debug.Log("[MilkfrogAnimationUpgrade] Complete\n" + report);
        }

        static void ConfigureImporter()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            if (importer.animationType != ModelImporterAnimationType.Generic) throw new InvalidOperationException("Unexpected rig type.");
            var existing = importer.clipAnimations.ToDictionary(c => c.name);
            var defaults = importer.defaultClipAnimations;
            if (defaults.Length != 28) throw new InvalidOperationException("FBX takes: " + string.Join(",", defaults.Select(c => c.name)));
            var loops = new[] { "idle", "walk", "jog", "walkBack", "jogBack", "walkLeft", "jogLeft", "walkRight", "jogRight", "guard", "fall" };
            importer.clipAnimations = defaults.Select(take =>
            {
                var clip = existing.TryGetValue(take.name, out var old) ? old : take;
                clip.takeName = take.takeName; clip.firstFrame = take.firstFrame; clip.lastFrame = take.lastFrame;
                clip.loopTime = loops.Contains(clip.name); clip.loopPose = false;
                clip.lockRootRotation = true; clip.lockRootHeightY = true; clip.lockRootPositionXZ = true;
                return clip;
            }).ToArray();
            importer.importCameras = false; importer.importLights = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
        }
        static Transform Endpoint(Transform parent, string name, Vector3 local)
        {
            var endpoint = parent.Find(name);
            if (endpoint == null) { endpoint = new GameObject(name).transform; endpoint.SetParent(parent, false); }
            endpoint.localPosition = local; endpoint.localRotation = Quaternion.identity; endpoint.localScale = Vector3.one;
            return endpoint;
        }
        static Material TrailMaterial()
        {
            const string path = Root + "Animations/milkfrog/MilkfrogSwordTrail.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) throw new InvalidOperationException("Missing URP particles shader.");
            material = new Material(shader) { name = "MilkfrogSwordTrail" };
            material.SetColor("_BaseColor", new Color(.65f, .95f, 1f, .6f));
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            AssetDatabase.CreateAsset(material, path); return material;
        }
    }
}
