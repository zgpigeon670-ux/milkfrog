using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Milkfrog.CombatDemo.Editor
{
    public static class CombatPresentationUpgrade
    {
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run in a validation copy.");
            var scene = EditorSceneManager.OpenScene(AnimatedDemoBuilder.ScenePath);
            Configure(UnityEngine.Object.FindFirstObjectByType<CombatDemoSession>());
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        public static void Configure(CombatDemoSession session)
        {
            var rig = session.gameplayCamera.GetComponent<DemoCamera>();
            rig.followBehindPlayer = true; rig.avoidObstacles = true;
            rig.offset = new Vector3(1.45f, 1.25f, -4.2f);
            rig.enemyFocusWeight = .2f; rig.headingSmooth = 7; rig.smooth = 10;
            session.gameplayCamera.fieldOfView = 52;
            var hud = session.GetComponent<CombatHud>();
            if (hud == null) hud = session.gameObject.AddComponent<CombatHud>();
            hud.session = session;
            var debug = UnityEngine.Object.FindFirstObjectByType<CombatDebugHud>();
            if (debug != null) debug.showDebug = false;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/CombatDemo/Materials/Mannequin.mat");
            var shader = Shader.Find("Milkfrog/Combat Fighter");
            if (shader == null) throw new InvalidOperationException("Missing combat flash shader.");
            material.shader = shader; material.SetFloat("_FlashAmount", 0); EditorUtility.SetDirty(material);
            foreach (var actor in new[] { session.player, session.enemy })
            {
                var view = actor.GetComponent<CombatActorView>();
                view.retainFactionColor = true; view.diagnosticsOnly = true;
            }
        }
    }
}
