using System;
using System.IO;
using Milkfrog.CombatDemo;
using NUnit.Framework;
using UnityEngine;

namespace Milkfrog.CombatDemo.Tests
{
    public class MvpSaveTests
    {
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "MilkfrogMvpSaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }

        [Test]
        public void SaveAndLoadRoundTripPreservesSnapshot()
        {
            var service = new SaveService(_testDirectory);
            var expected = new PlayerSnapshot
            {
                position = new Vector3(12.5f, -3f, 27.25f),
                yaw = 137.5f,
                health = 74.25f,
                posture = 31.5f,
                defeatedEnemyIds = new[] { "guard-a", "guard-b" },
                bossDefeated = true
            };

            Assert.That(service.TrySave(expected), Is.True, service.LastMessage);
            Assert.That(service.HasSave, Is.True);

            var reloadedService = new SaveService(_testDirectory);
            PlayerSnapshot actual;
            Assert.That(reloadedService.TryLoad(out actual), Is.True, reloadedService.LastMessage);
            Assert.That(actual.version, Is.EqualTo(expected.version));
            Assert.That(actual.levelId, Is.EqualTo(expected.levelId));
            Assert.That(actual.position, Is.EqualTo(expected.position));
            Assert.That(actual.yaw, Is.EqualTo(expected.yaw));
            Assert.That(actual.health, Is.EqualTo(expected.health));
            Assert.That(actual.posture, Is.EqualTo(expected.posture));
            Assert.That(actual.defeatedEnemyIds, Is.EqualTo(expected.defeatedEnemyIds));
            Assert.That(actual.bossDefeated, Is.EqualTo(expected.bossDefeated));
        }

        [Test]
        public void CorruptPrimaryLoadsBackupWithoutChangingPrimary()
        {
            var service = new SaveService(_testDirectory);
            var first = new PlayerSnapshot { position = new Vector3(1, 2, 3), health = 90 };
            var second = new PlayerSnapshot { position = new Vector3(4, 5, 6), health = 80 };
            Assert.That(service.TrySave(first), Is.True, service.LastMessage);
            Assert.That(service.TrySave(second), Is.True, service.LastMessage);

            string primaryPath = Path.Combine(_testDirectory, "save.json");
            const string corruptPrimary = "{ not a snapshot";
            File.WriteAllText(primaryPath, corruptPrimary);

            PlayerSnapshot loaded;
            Assert.That(service.TryLoad(out loaded), Is.True, service.LastMessage);
            Assert.That(loaded.position, Is.EqualTo(first.position));
            Assert.That(loaded.health, Is.EqualTo(first.health));
            Assert.That(File.ReadAllText(primaryPath), Is.EqualTo(corruptPrimary));

            var third = new PlayerSnapshot { position = new Vector3(7, 8, 9), health = 70 };
            Assert.That(service.TrySave(third), Is.True, service.LastMessage);
            Assert.That(File.ReadAllText(Path.Combine(_testDirectory, "save.backup.json")),
                Is.EqualTo(JsonUtility.ToJson(first, true)));
            PlayerSnapshot afterSave;
            Assert.That(service.TryLoad(out afterSave), Is.True, service.LastMessage);
            Assert.That(afterSave.position, Is.EqualTo(third.position));

            string[] archives = Directory.GetFiles(_testDirectory, "save.corrupt.*.json");
            Assert.That(archives, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(archives[0]), Is.EqualTo(corruptPrimary));
        }

        [Test]
        public void HasSaveIsTrueWhenOnlyBackupExists()
        {
            string backupPath = Path.Combine(_testDirectory, "save.backup.json");
            File.WriteAllText(backupPath, JsonUtility.ToJson(new PlayerSnapshot(), true));

            var service = new SaveService(_testDirectory);
            Assert.That(service.HasSave, Is.True);
        }

        [Test]
        public void UnreadablePrimaryFallsBackToValidBackup()
        {
            string primaryPath = Path.Combine(_testDirectory, "save.json");
            string backupPath = Path.Combine(_testDirectory, "save.backup.json");
            File.WriteAllText(primaryPath, JsonUtility.ToJson(new PlayerSnapshot { health = 50 }, true));
            var backupSnapshot = new PlayerSnapshot { position = new Vector3(11, 12, 13), health = 85 };
            File.WriteAllText(backupPath, JsonUtility.ToJson(backupSnapshot, true));

            var service = new SaveService(_testDirectory);
            using (new FileStream(primaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                PlayerSnapshot loaded;
                Assert.That(service.HasSave, Is.True);
                Assert.That(service.TryLoad(out loaded), Is.True, service.LastMessage);
                Assert.That(loaded.position, Is.EqualTo(backupSnapshot.position));
                Assert.That(loaded.health, Is.EqualTo(backupSnapshot.health));
            }
        }

        [Test]
        public void BothCorruptFilesFailToLoadAndRemainUntouched()
        {
            string primaryPath = Path.Combine(_testDirectory, "save.json");
            string backupPath = Path.Combine(_testDirectory, "save.backup.json");
            const string corruptPrimary = "primary corruption";
            const string corruptBackup = "backup corruption";
            File.WriteAllText(primaryPath, corruptPrimary);
            File.WriteAllText(backupPath, corruptBackup);

            var service = new SaveService(_testDirectory);
            PlayerSnapshot loaded;
            Assert.That(service.TryLoad(out loaded), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(service.HasSave, Is.True);
            Assert.That(File.ReadAllText(primaryPath), Is.EqualTo(corruptPrimary));
            Assert.That(File.ReadAllText(backupPath), Is.EqualTo(corruptBackup));
        }

        [Test]
        public void UnsupportedVersionIsRejectedAndPreserved()
        {
            string primaryPath = Path.Combine(_testDirectory, "save.json");
            string unsupported = JsonUtility.ToJson(new PlayerSnapshot { version = SaveService.CurrentVersion + 1 });
            File.WriteAllText(primaryPath, unsupported);

            var service = new SaveService(_testDirectory);
            PlayerSnapshot loaded;
            Assert.That(service.TryLoad(out loaded), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(service.LastMessage, Is.EqualTo("Unsupported save version."));
            Assert.That(File.ReadAllText(primaryPath), Is.EqualTo(unsupported));
            Assert.That(service.TrySave(new PlayerSnapshot { version = SaveService.CurrentVersion + 1 }), Is.False);
            Assert.That(service.LastMessage, Is.EqualTo("Unsupported save version."));
        }

        [Test]
        public void InvalidNumericValuesAreRejected()
        {
            var service = new SaveService(_testDirectory);
            Assert.That(service.TrySave(new PlayerSnapshot { health = float.NaN }), Is.False);
            Assert.That(service.TrySave(new PlayerSnapshot { posture = -1 }), Is.False);
            Assert.That(service.TrySave(new PlayerSnapshot { health = 0 }), Is.False);
            Assert.That(service.HasSave, Is.False);
        }

        [Test]
        public void WriteFailureReturnsFalseWithDisplayableMessage()
        {
            string directoryBlockedByFile = Path.Combine(_testDirectory, "not-a-directory");
            File.WriteAllText(directoryBlockedByFile, "block directory creation");

            var service = new SaveService(directoryBlockedByFile);
            Assert.That(service.TrySave(new PlayerSnapshot()), Is.False);
            Assert.That(service.LastMessage, Is.Not.Null.And.Not.Empty);
        }
    }
}
