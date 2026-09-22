// Adapted from firstkindgamer/QuaterniusUnityUtils, commit 62a4f8e (Unlicense).
// Scoped to our FBX, detects the actual root, checks the Avatar, and applies imports immediately.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Milkfrog.CombatDemo.Editor
{
    public static class QuaterniusAnimationImport
    {
        public const string ModelPath = "Assets/CombatDemo/ThirdParty/Quaternius/UAL1_Standard.fbx";
        public static void ImportAndInspect()
        {
            AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var root = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Equals("root", StringComparison.OrdinalIgnoreCase));
            importer.bakeAxisConversion = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = importer.importLights = false;
            importer.optimizeGameObjects = false;
            if (root != null) importer.motionNodeName = AnimationUtility.CalculateTransformPath(root, model.transform);
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = clip.name.EndsWith("Loop", StringComparison.OrdinalIgnoreCase) || clip.name.EndsWith("Sword_Idle", StringComparison.OrdinalIgnoreCase);
                clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var assets = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
            var avatar = assets.OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("UAL Humanoid Avatar is invalid.");
            Directory.CreateDirectory("Evidence");
            File.WriteAllLines("Evidence/animation-import.txt", assets.OfType<AnimationClip>().Select(c => c.name + " | " + c.length + "s | humanoid=" + c.humanMotion));
            Debug.Log("[CombatDemo] UAL Avatar valid; imported " + clips.Length + " clips. Root: " + importer.motionNodeName);
        }
    }
}
