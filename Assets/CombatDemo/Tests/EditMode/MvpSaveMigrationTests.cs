using System;
using System.IO;
using Milkfrog.CombatDemo;
using NUnit.Framework;
using UnityEngine;

namespace Milkfrog.CombatDemo.Tests
{
    public sealed class MvpSaveMigrationTests
    {
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "MilkfrogMvpMigrationTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }

        [Test]
        public void VersionOneSavePreservesWorldAndVitalsAndDefaultsToStartCheckpoint()
        {
            WriteLegacySave(bossDefeated: false, health: 61.5f, posture: 37.25f);
            var service = new SaveService(_testDirectory);

            PlayerSnapshot loaded;
            Assert.That(service.TryLoad(out loaded), Is.True, service.LastMessage);

            Assert.That(loaded.version, Is.EqualTo(SaveService.CurrentVersion));
            Assert.That(loaded.levelId, Is.EqualTo("MVP_TestLevel"));
            Assert.That(loaded.position, Is.EqualTo(new Vector3(11, 2, -7)));
            Assert.That(loaded.yaw, Is.EqualTo(123.5f));
            Assert.That(loaded.health, Is.EqualTo(61.5f));
            Assert.That(loaded.posture, Is.EqualTo(37.25f));
            Assert.That(loaded.defeatedEnemyIds, Is.EqualTo(new[] { "mob-a", "mob-b" }));
            Assert.That(loaded.bossDefeated, Is.False);
            Assert.That(loaded.activeCheckpointId, Is.EqualTo(BonfireCheckpoint.StartId));
            Assert.That(loaded.experience, Is.Zero);
            Assert.That(loaded.vitality, Is.Zero);
            Assert.That(loaded.resolve, Is.Zero);
            Assert.That(loaded.power, Is.Zero);
        }

        [Test]
        public void BossDefeatedVersionOneSaveGrantsTwoHundredExperienceOnceAfterV2Commit()
        {
            WriteLegacySave(bossDefeated: true, health: 74, posture: 12);
            var service = new SaveService(_testDirectory);

            PlayerSnapshot migrated;
            Assert.That(service.TryLoad(out migrated), Is.True, service.LastMessage);
            Assert.That(migrated.bossDefeated, Is.True);
            Assert.That(migrated.experience, Is.EqualTo(200));
            Assert.That(migrated.activeCheckpointId, Is.EqualTo(BonfireCheckpoint.StartId));

            // The migrated representation becomes authoritative only when written successfully.
            Assert.That(service.TrySave(migrated), Is.True, service.LastMessage);
            string primaryPath = Path.Combine(_testDirectory, "save.json");
            string backupPath = Path.Combine(_testDirectory, "save.backup.json");
            Assert.That(JsonUtility.FromJson<PlayerSnapshot>(File.ReadAllText(primaryPath)).version,
                Is.EqualTo(SaveService.CurrentVersion));
            Assert.That(JsonUtility.FromJson<PlayerSnapshot>(File.ReadAllText(backupPath)).version,
                Is.EqualTo(SaveService.CurrentVersion));
            Assert.That(JsonUtility.FromJson<PlayerSnapshot>(File.ReadAllText(backupPath)).experience,
                Is.EqualTo(200), "the fallback is migrated once with the primary");

            PlayerSnapshot reloaded;
            Assert.That(service.TryLoad(out reloaded), Is.True, service.LastMessage);
            Assert.That(reloaded.experience, Is.EqualTo(200), "a v2 read must not apply the legacy reward again");
            File.WriteAllText(primaryPath, "corrupt primary");
            Assert.That(service.TryLoad(out reloaded), Is.True, service.LastMessage);
            Assert.That(reloaded.experience, Is.EqualTo(200), "a migrated backup must not award the boss twice");
        }

        [Test]
        public void V1RewardComesFromBossFlagAndNeverFromDefeatedMobList()
        {
            WriteLegacySave(bossDefeated: false, health: 90, posture: 0);
            var service = new SaveService(_testDirectory);

            PlayerSnapshot loaded;
            Assert.That(service.TryLoad(out loaded), Is.True, service.LastMessage);
            Assert.That(loaded.defeatedEnemyIds, Is.EqualTo(new[] { "mob-a", "mob-b" }));
            Assert.That(loaded.experience, Is.Zero);
        }

        [Test]
        public void V2SaveRequiresGrowthAndCheckpointFieldsAndRejectsInvalidRanks()
        {
            string path = Path.Combine(_testDirectory, "save.json");
            const string truncatedV2 = "{\"version\":2,\"levelId\":\"MVP_TestLevel\",\"position\":{\"x\":0,\"y\":0,\"z\":0},\"yaw\":0,\"health\":100,\"posture\":0,\"defeatedEnemyIds\":[],\"bossDefeated\":false}";
            File.WriteAllText(path, truncatedV2);
            var service = new SaveService(_testDirectory);

            PlayerSnapshot loaded;
            Assert.That(service.TryLoad(out loaded), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(File.ReadAllText(path), Is.EqualTo(truncatedV2));

            Assert.That(service.TrySave(new PlayerSnapshot { vitality = PlayerProgression.MaximumRank + 1 }), Is.False);
            Assert.That(service.TrySave(new PlayerSnapshot { experience = -1 }), Is.False);
            Assert.That(service.TrySave(new PlayerSnapshot { activeCheckpointId = "" }), Is.False);
        }

        [Test]
        public void V1BackupCanBeRecoveredAndMigratedAfterPrimaryCorruption()
        {
            string backupPath = Path.Combine(_testDirectory, "save.backup.json");
            string legacyJson = LegacyJson(bossDefeated: true, health: 55, posture: 15);
            File.WriteAllText(backupPath, legacyJson);
            File.WriteAllText(Path.Combine(_testDirectory, "save.json"), "corrupt primary");
            var service = new SaveService(_testDirectory);

            PlayerSnapshot recovered;
            Assert.That(service.TryLoad(out recovered), Is.True, service.LastMessage);
            Assert.That(recovered.version, Is.EqualTo(SaveService.CurrentVersion));
            Assert.That(recovered.experience, Is.EqualTo(200));
            Assert.That(recovered.health, Is.EqualTo(55));
            Assert.That(recovered.bossDefeated, Is.True);
            Assert.That(File.ReadAllText(backupPath), Is.EqualTo(legacyJson), "recovery must not rewrite the source backup");
        }

        [Test]
        public void EarlyV2SaveWithoutUnlockListRecoversStartAndActiveBonfires()
        {
            const string earlyV2 = "{\"version\":2,\"levelId\":\"MVP_TestLevel\",\"position\":{\"x\":0,\"y\":0,\"z\":0}," +
                                   "\"yaw\":0,\"health\":100,\"posture\":0,\"defeatedEnemyIds\":[],\"bossDefeated\":false," +
                                   "\"activeCheckpointId\":\"camp-before-boss\",\"experience\":40,\"vitality\":0,\"resolve\":0,\"power\":0}";
            File.WriteAllText(Path.Combine(_testDirectory, "save.json"), earlyV2);
            var service = new SaveService(_testDirectory);
            Assert.That(service.TryLoad(out var loaded), Is.True, service.LastMessage);
            Assert.That(loaded.unlockedCheckpointIds, Does.Contain(BonfireCheckpoint.StartId));
            Assert.That(loaded.unlockedCheckpointIds, Does.Contain(BonfireCheckpoint.BossApproachId));
            Assert.That(service.TrySave(loaded), Is.True, service.LastMessage);
            Assert.That(service.TryLoad(out loaded), Is.True, service.LastMessage);
            Assert.That(loaded.unlockedCheckpointIds, Has.Length.EqualTo(2));
        }

        private string WriteLegacySave(bool bossDefeated, float health, float posture)
        {
            string json = LegacyJson(bossDefeated, health, posture);
            File.WriteAllText(Path.Combine(_testDirectory, "save.json"), json);
            return json;
        }

        private static string LegacyJson(bool bossDefeated, float health, float posture)
        {
            return "{\"version\":1,\"levelId\":\"MVP_TestLevel\",\"position\":{\"x\":11,\"y\":2,\"z\":-7},\"yaw\":123.5,\"health\":" +
                   health.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"posture\":" +
                   posture.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                   ",\"defeatedEnemyIds\":[\"mob-a\",\"mob-b\"],\"bossDefeated\":" +
                   (bossDefeated ? "true" : "false") + "}";
        }
    }
}
