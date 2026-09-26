using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Milkfrog.CombatDemo.Editor
{
    public sealed class MvpQuestUpgrade : IProcessSceneWithReport
    {
        const string Root = "Assets/CombatDemo";
        public int callbackOrder => 0;
        public void OnProcessScene(Scene scene, BuildReport report) { Validate(scene); }
        [MenuItem("Tools/Combat Demo/Upgrade MVP Quest")]
        public static void Upgrade()
        {
            MvpSceneBuilder.UpgradeBonfires();
            var scene = EditorSceneManager.GetActiveScene();
            var world = UnityEngine.Object.FindAnyObjectByType<MvpWorld>();
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(Root + "/Settings/GuardianTrial.asset");
            if (quest == null)
            {
                quest = ScriptableObject.CreateInstance<QuestDefinition>();
                AssetDatabase.CreateAsset(quest, Root + "/Settings/GuardianTrial.asset");
            }
            bool changed = false;
            if (world.quest == null) { world.quest = quest; EditorUtility.SetDirty(world); changed = true; }
            var camp = world.bonfires.First(c => c.stableId == BonfireCheckpoint.BossApproachId);
            var boss = world.enemies.First(c => c.IsBoss);
            changed |= Ensure(WorldStateService.KeyObjectId, "守门钥匙", WorldInteractionKind.KeyPickup,
                camp.transform.position + Vector3.right * 6);
            changed |= Ensure(WorldStateService.GateId, "封印门", WorldInteractionKind.SealedDoor,
                boss.transform.position + Vector3.back * (boss.definition.bossTriggerRadius + 1));
            Validate(scene);
            if (changed) EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(changed ? "[Quest] Incremental upgrade saved." : "[Quest] Already upgraded; preserved scene.");
        }
        static bool Ensure(string id, string label, WorldInteractionKind kind, Vector3 position)
        {
            if (UnityEngine.Object.FindObjectsByType<WorldInteractable>().Any(x => x.stableId == id)) return false;
            string path = Root + "/Prefabs/MVP/" + (kind == WorldInteractionKind.KeyPickup ? "GuardianKey" : "GuardianGate") + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                var root = new GameObject(label); var component = root.AddComponent<WorldInteractable>();
                component.stableId = id; component.displayName = label; component.kind = kind;
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube); visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.up * (kind == WorldInteractionKind.KeyPickup ? .7f : 1.5f);
                visual.transform.localScale = kind == WorldInteractionKind.KeyPickup ? new Vector3(.25f, .9f, .25f) : new Vector3(4, 3, .35f);
                visual.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/MvpArena.mat");
                if (kind == WorldInteractionKind.KeyPickup) visual.GetComponent<Collider>().isTrigger = true;
                component.visual = visual;
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root);
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;
            return true;
        }
        public static void Validate(Scene scene)
        {
            var components = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var world = components.OfType<MvpWorld>().FirstOrDefault(); if (world == null) return;
            var ids = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var component in components)
            {
                string id = component is IInteractable item ? item.StableId : component is EnemyController enemy ? enemy.stableId : null;
                if (!(component is IInteractable) && !(component is EnemyController)) continue;
                if (string.IsNullOrWhiteSpace(id)) throw new BuildFailedException("Missing stable ID: " + component.name);
                if (ids.TryGetValue(id, out var previous)) throw new BuildFailedException("Duplicate stable ID " + id + ": " + previous + " / " + component.name);
                ids.Add(id, component.name);
            }
            if (world.quest == null || world.quest.steps == null || world.quest.steps.Length == 0 || string.IsNullOrWhiteSpace(world.quest.stableId))
                throw new BuildFailedException("MVP quest definition is missing or empty.");
            var steps = new HashSet<string>();
            foreach (var step in world.quest.steps)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.id) || !steps.Add(step.id) || string.IsNullOrWhiteSpace(step.targetId))
                    throw new BuildFailedException("Missing or duplicate task step ID: " + world.quest.name);
                bool exists = step.condition == WorldEventKind.ItemAcquired ? components.OfType<WorldInteractable>().Any(x => x.kind == WorldInteractionKind.KeyPickup && x.itemId == step.targetId) : ids.ContainsKey(step.targetId);
                if (!exists) throw new BuildFailedException("Missing task target: " + step.targetId);
            }
        }
        public static void RepairStaleTraces()
        {
            // Rebuild derived data only; authored clips, markers and scene placement stay intact.
            EditorSceneManager.OpenScene(MvpSceneBuilder.Level);
            var presenter = UnityEngine.Object.FindAnyObjectByType<MvpWorld>().enemies.First(x => !x.IsBoss).GetComponent<CombatAnimationPresenter>();
            foreach (string guid in AssetDatabase.FindAssets("t:CombatAttackDefinition", new[] { Root + "/Settings" }))
            {
                var attack = AssetDatabase.LoadAssetAtPath<CombatAttackDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (attack.trace == null || attack.clip == null) continue;
                if (attack.trace.IsValid && attack.trace.bakedClip == attack.clip && Mathf.Approximately(attack.trace.bakedStart, attack.activeStart) && Mathf.Approximately(attack.trace.bakedEnd, attack.activeEnd)) continue;
                attack.trace = CombatBladeTraceBaker.Bake(presenter, attack, Root + "/Animations/" + attack.name + "ValidatedTrace.asset");
                EditorUtility.SetDirty(attack);
                Debug.Log("[Quest validation] Rebuilt stale derived trace: " + attack.name);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
