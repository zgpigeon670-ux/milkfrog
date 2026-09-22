using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Milkfrog.CombatDemo.Editor
{
    public static class AnimatedDemoBuilder
    {
        public const string ScenePath = "Assets/CombatDemo/Scenes/CombatDemo_Animated.unity";
        const string Root = "Assets/CombatDemo";
        [MenuItem("Tools/Combat Demo/Create Missing Animated Scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath)) { Debug.Log("Animated scene already exists; preserved."); return; }
            QuaterniusAnimationImport.ImportAndInspect();
            Directory.CreateDirectory(Root + "/Animations");
            AssetDatabase.Refresh();
            var clips = AssetDatabase.LoadAllAssetsAtPath(QuaterniusAnimationImport.ModelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            AnimationClip Clip(string name) => clips.Single(c => c.name == "Armature|" + name);
            var profile = ScriptableObject.CreateInstance<CombatAnimationProfile>();
            profile.idle = Clip("Sword_Idle"); profile.walk = Clip("Walk_Loop"); profile.jog = Clip("Jog_Fwd_Loop");
            profile.attack = Clip("Sword_Attack"); profile.hit = Clip("Hit_Chest"); profile.death = Clip("Death01");
            profile.guard = Pose("Guard", profile.idle, .5f, false);
            profile.parry = Pose("Parry", profile.idle, .5f, true);
            profile.deflected = Pose("Deflected", Clip("Hit_Head"), .6f, false);
            profile.broken = Pose("Broken", Clip("Crouch_Idle_Loop"), .5f, false);
            AssetDatabase.CreateAsset(profile, Root + "/Animations/HumanoidCombat.asset");
            AnimationCalibration.Calibrate();
            var previous = SceneManager.GetActiveScene();
            bool additive = !Application.isBatchMode;
            var scene = EditorSceneManager.OpenScene(CombatDemoBuilder.ScenePath, additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            try
            {
                var session = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CombatDemoSession>()).Single();
                var settings = Object.Instantiate(session.settings); settings.name = "AnimatedCombat"; settings.logEvents = false;
                AssetDatabase.CreateAsset(settings, Root + "/Settings/AnimatedCombat.asset"); session.settings = settings;
                var bodyMaterial = Material("Mannequin", new Color(.6f,.7f,.8f));
                var swordMaterial = Material("SwordSteel", new Color(.8f,.9f,1));
                var gripMaterial = Material("SwordGrip", new Color(.08f,.1f,.14f));
                var flashMaterial = Material("Impact", new Color(1,.8f,.2f), true);
                session.playerAnimation = Humanoid(session.player, profile, bodyMaterial, swordMaterial, gripMaterial, flashMaterial);
                session.enemyAnimation = Humanoid(session.enemy, profile, bodyMaterial, swordMaterial, gripMaterial, flashMaterial);
                var rig = session.gameplayCamera.GetComponent<DemoCamera>(); rig.avoidObstacles = true; rig.offset = new Vector3(3.3f, 3.4f, -5.4f);
                session.gameplayCamera.fieldOfView = 48;
                var feedback = session.gameObject.AddComponent<CombatFeedback>();
                feedback.session = session; feedback.cameraRig = rig; feedback.flashMaterial = flashMaterial; session.feedback = feedback;
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[CombatDemo] Created animated humanoid scene.");
            }
            finally
            {
                if (additive) { if (previous.IsValid()) SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
            }
        }

        static AnimationClip Pose(string name, AnimationClip source, float sample, bool pulse)
        {
            // Humanoid muscle curves retain the imported root/retargeting data; no new skeleton is invented.
            var clip = Object.Instantiate(source); clip.name = name;
            var bindings = AnimationUtility.GetCurveBindings(source);
            Directory.CreateDirectory("Evidence");
            File.WriteAllLines("Evidence/animation-bindings.txt", bindings.Select(b => b.path + " | " + b.type.Name + " | " + b.propertyName));
            foreach (var binding in bindings)
            {
                var original = AnimationUtility.GetEditorCurve(source, binding);
                float value = original.Evaluate(source.length * sample);
                if (name == "Guard" || name == "Parry")
                {
                    switch (binding.propertyName)
                    {
                        case "Right Arm Front-Back": value = .55f; break;
                        case "Right Arm Down-Up": value = -.25f; break;
                        case "Right Forearm Stretch": value = -.55f; break;
                        case "Left Arm Front-Back": value = .5f; break;
                        case "Left Arm Down-Up": value = -.3f; break;
                        case "Left Forearm Stretch": value = -.65f; break;
                    }
                }
                if (name == "Broken" && binding.propertyName == "Spine Front-Back") value = .65f;
                if (name == "Deflected" && binding.propertyName == "Spine Front-Back") value = -.35f;
                var curve = AnimationCurve.Constant(0, source.length, value);
                if (pulse && binding.propertyName == "Right Arm Front-Back")
                    curve = new AnimationCurve(new Keyframe(0, value), new Keyframe(source.length * .3f, value + .35f), new Keyframe(source.length, value));
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
            AssetDatabase.CreateAsset(clip, Root + "/Animations/" + name + ".anim");
            return clip;
        }

        static CombatAnimationPresenter Humanoid(CombatActor actor, CombatAnimationProfile profile, Material body, Material steel, Material grip, Material trailMaterial)
        {
            Object.DestroyImmediate(actor.transform.Find("Body").gameObject);
            Object.DestroyImmediate(actor.transform.Find("Facing Marker").gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusAnimationImport.ModelPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, actor.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Humanoid"; model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            var animator = model.GetComponent<Animator>(); animator.applyRootMotion = false; animator.runtimeAnimatorController = null;
            if (!animator.avatar.isValid || !animator.avatar.isHuman) throw new InvalidOperationException("Humanoid avatar invalid.");
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            var bounds = renderers[0].bounds; foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); renderer.sharedMaterials = Enumerable.Repeat(body, renderer.sharedMaterials.Length).ToArray(); }
            model.transform.localScale = Vector3.one * (1.9f / bounds.size.y);
            var presenter = actor.gameObject.AddComponent<CombatAnimationPresenter>(); presenter.actor = actor; presenter.animator = animator; presenter.profile = profile;
            actor.GetComponent<CombatActorView>().body = renderers[0];
            actor.GetComponent<CombatActorView>().retainFactionColor = true;
            actor.GetComponent<CombatActorView>().label.transform.localPosition = Vector3.up * 2.2f;
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var sword = new GameObject("Training Sword"); sword.transform.SetParent(hand, false);
            sword.transform.localPosition = new Vector3(0, .04f, 0); sword.transform.localRotation = Quaternion.Euler(0, 0, 90);
            Piece(sword.transform, "Grip", Vector3.zero, new Vector3(.035f,.2f,.045f), grip);
            Piece(sword.transform, "Guard", Vector3.up*.12f, new Vector3(.24f,.035f,.065f), steel);
            Piece(sword.transform, "Blade", Vector3.up*.54f, new Vector3(.06f,.8f,.018f), steel);
            var tip = new GameObject("Sword Trail", typeof(TrailRenderer)); tip.transform.SetParent(sword.transform, false); tip.transform.localPosition = Vector3.up*.94f;
            var trail = tip.GetComponent<TrailRenderer>(); trail.sharedMaterial = trailMaterial; trail.time = .09f; trail.startWidth = .07f; trail.endWidth = 0; trail.emitting = false;
            presenter.swordTrail = trail;
            return presenter;
        }
        static void Piece(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
        }
        static Material Material(string name, Color color, bool unlit = false)
        {
            var material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, Root + "/Materials/" + name + ".mat"); return material;
        }
    }
}
