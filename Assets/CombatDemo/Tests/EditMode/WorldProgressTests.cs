using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Milkfrog.CombatDemo.Tests
{
    public class WorldProgressTests
    {
        QuestDefinition definition;
        string directory;
        [SetUp] public void Setup() { definition = ScriptableObject.CreateInstance<QuestDefinition>(); directory = Path.Combine(Path.GetTempPath(), "WorldTests-" + Guid.NewGuid()); Directory.CreateDirectory(directory); }
        [TearDown] public void Cleanup() { UnityEngine.Object.DestroyImmediate(definition); Directory.Delete(directory, true); }
        [Test] public void EarlyKeyIsRetainedButFirstUnmetObjectiveRemainsCamp()
        {
            var state = new WorldStateService(); var snapshot = new PlayerSnapshot(); var quests = new QuestProgressService(definition);
            int events = 0; state.Changed += (_, __) => events++;
            state.Apply(WorldEventKind.ItemAcquired, WorldStateService.KeyObjectId, WorldStateService.KeyItemId);
            state.Apply(WorldEventKind.ItemAcquired, WorldStateService.KeyObjectId, WorldStateService.KeyItemId);
            state.WriteTo(snapshot);
            Assert.That(events, Is.EqualTo(1)); Assert.That(quests.Evaluate(snapshot).completedSteps, Is.EquivalentTo(new[] { "find-key" }));
            Assert.That(quests.Objective(snapshot), Does.Contain("1/4"));
            snapshot.unlockedCheckpointIds = new[] { BonfireCheckpoint.BossApproachId }; quests.WriteTo(snapshot);
            Assert.That(quests.Objective(snapshot), Does.Contain("3/4"));
        }
        [Test] public void SavedCompletionFlagsCannotBypassMissingWorldFacts()
        {
            var snapshot = new PlayerSnapshot(); snapshot.questProgress.completed = true;
            var progress = new QuestProgressService(definition).Evaluate(snapshot);
            Assert.That(progress.completed, Is.False); Assert.That(progress.completedSteps, Is.Empty);
        }
        [TestCase(false)] [TestCase(true)] public void VersionTwoMigrationPreservesGrowthAndCompletesOnlyWonGames(bool won)
        {
            var snapshot = new PlayerSnapshot { version = 2, bossDefeated = won, experience = 37, vitality = 2, health = 91,
                unlockedCheckpointIds = new[] { BonfireCheckpoint.StartId, BonfireCheckpoint.BossApproachId } };
            File.WriteAllText(Path.Combine(directory, "save.json"), JsonUtility.ToJson(snapshot));
            var store = new SaveService(directory); Assert.That(store.TryLoad(out var loaded), Is.True, store.LastMessage);
            Assert.That(loaded.version, Is.EqualTo(3)); Assert.That(loaded.experience, Is.EqualTo(37)); Assert.That(loaded.vitality, Is.EqualTo(2));
            var state = new WorldStateService(); state.Restore(loaded.worldState);
            Assert.That(state.IsDoorOpen(WorldStateService.GateId), Is.EqualTo(won));
            Assert.That(new QuestProgressService(definition).Evaluate(loaded).completed, Is.EqualTo(won));
            Assert.That(store.TrySave(loaded), Is.True); Assert.That(store.TryLoad(out loaded), Is.True); Assert.That(loaded.experience, Is.EqualTo(37));
        }
        [Test] public void WorldStateRoundTripsAndBackupRetainsLastCommittedProgress()
        {
            var store = new SaveService(directory); var snapshot = new PlayerSnapshot();
            var state = new WorldStateService(); state.Apply(WorldEventKind.ItemAcquired, WorldStateService.KeyObjectId, WorldStateService.KeyItemId);
            state.WriteTo(snapshot); new QuestProgressService(definition).WriteTo(snapshot); Assert.That(store.TrySave(snapshot), Is.True);
            state.Apply(WorldEventKind.DoorOpened, WorldStateService.GateId); state.WriteTo(snapshot); Assert.That(store.TrySave(snapshot), Is.True);
            File.WriteAllText(Path.Combine(directory, "save.json"), "invalid"); Assert.That(store.TryLoad(out var recovered), Is.True);
            Assert.That(recovered.worldState.keyItems, Does.Contain(WorldStateService.KeyItemId)); Assert.That(recovered.worldState.openedDoors, Is.Empty);
        }
        [Test] public void V3MissingWorldDataIsRejectedInsteadOfLosingProgress()
        {
            var snapshot = new PlayerSnapshot(); snapshot.worldState = null;
            Assert.That(new SaveService(directory).TrySave(snapshot), Is.False);
            string json = JsonUtility.ToJson(new PlayerSnapshot());
            json = json.Replace("\"worldState\"", "\"removedState\""); File.WriteAllText(Path.Combine(directory, "save.json"), json);
            Assert.That(new SaveService(directory).TryLoad(out _), Is.False);
        }
    }
}
