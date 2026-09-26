using NUnit.Framework;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Milkfrog.CombatDemo.Editor;

namespace Milkfrog.CombatDemo.Tests
{
    public class WorldIdValidationTests
    {
        Scene scene;
        QuestDefinition quest;
        BonfireCheckpoint camp;
        [SetUp] public void Setup()
        {
            scene = EditorSceneManager.NewPreviewScene();
            quest = ScriptableObject.CreateInstance<QuestDefinition>();
            Create("World").AddComponent<MvpWorld>().quest = quest;
            camp = Create("Camp").AddComponent<BonfireCheckpoint>(); camp.stableId = BonfireCheckpoint.BossApproachId;
            var key = Create("Key").AddComponent<WorldInteractable>(); key.stableId = WorldStateService.KeyObjectId;
            var gate = Create("Gate").AddComponent<WorldInteractable>(); gate.stableId = WorldStateService.GateId; gate.kind = WorldInteractionKind.SealedDoor;
            Create("Boss").AddComponent<BossEnemy>().stableId = WorldStateService.BossId;
        }
        GameObject Create(string name)
        { var root = new GameObject(name); SceneManager.MoveGameObjectToScene(root, scene); return root; }
        [TearDown] public void Cleanup()
        { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); Object.DestroyImmediate(quest); }
        [Test] public void UniqueWorldWithResolvableTaskTargetsIsAccepted() => Assert.DoesNotThrow(() => MvpQuestUpgrade.Validate(scene));
        [Test] public void DuplicateOrMissingObjectIdsBlockBuild()
        {
            var duplicate = Create("Duplicate").AddComponent<BonfireCheckpoint>(); duplicate.stableId = camp.stableId;
            Assert.Throws<BuildFailedException>(() => MvpQuestUpgrade.Validate(scene)); duplicate.stableId = "";
            Assert.Throws<BuildFailedException>(() => MvpQuestUpgrade.Validate(scene));
        }
        [Test] public void MissingTaskTargetBlocksBuild()
        { quest.steps[0].targetId = "missing-camp"; Assert.Throws<BuildFailedException>(() => MvpQuestUpgrade.Validate(scene)); }
        [Test] public void DoorKeyRequirementDoesNotCountAsAnAvailablePickup()
        {
            foreach (var root in scene.GetRootGameObjects()) if (root.name == "Key") Object.DestroyImmediate(root);
            Assert.Throws<BuildFailedException>(() => MvpQuestUpgrade.Validate(scene));
        }
    }
}
