using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using Milkfrog.CombatDemo.Editor;

namespace Milkfrog.CombatDemo.Tests
{
    public class InventoryTests
    {
        string directory;
        [SetUp] public void Setup() => directory = Path.Combine(Path.GetTempPath(), "InventoryTests-" + Guid.NewGuid());
        [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        [Test] public void StackConsumeAndSnapshotAreIndependent()
        {
            var bag = new InventoryService(); int events = 0; bag.Changed += () => events++;
            Assert.That(bag.TryAdd("medicine", 3), Is.True); Assert.That(bag.TryAdd("medicine", 2), Is.True);
            var old = bag.Capture(); Assert.That(bag.TryConsume("medicine", 5), Is.True);
            Assert.That(bag.HasItem("medicine"), Is.False); Assert.That(bag.Capture().entries, Is.Empty);
            Assert.That(old.entries[0].quantity, Is.EqualTo(5)); bag.Restore(old); old.entries[0].quantity = 99;
            Assert.That(bag.Quantity("medicine"), Is.EqualTo(5)); Assert.That(events, Is.EqualTo(4));
        }
        [Test] public void InvalidOperationsAndIntegerOverflowDoNotChangeCountsOrEmitEvents()
        {
            var bag = new InventoryService(); bag.TryAdd("item", int.MaxValue); int events = 0; bag.Changed += () => events++;
            Assert.That(bag.TryAdd("item", 1), Is.False); Assert.That(bag.TryAdd("", 1), Is.False);
            Assert.That(bag.TryAdd("item", 0), Is.False); Assert.That(bag.TryConsume("item", -1), Is.False);
            Assert.That(bag.TryConsume("absent"), Is.False); Assert.That(bag.Quantity("item"), Is.EqualTo(int.MaxValue)); Assert.That(events, Is.Zero);
        }
        [Test] public void CaptureSortsIdsAndPreservesUnknownItems()
        {
            var bag = new InventoryService(); bag.TryAdd("future-item", 7); bag.TryAdd("a", 2);
            var copy = new InventoryService(); copy.Restore(bag.Capture());
            Assert.That(copy.Capture().entries[0].itemId, Is.EqualTo("a")); Assert.That(copy.Quantity("future-item"), Is.EqualTo(7));
        }
        [Test] public void MalformedRestoreDoesNotEraseExistingInventory()
        {
            var bag = new InventoryService(); bag.TryAdd("item", 2);
            Assert.Throws<ArgumentException>(() => bag.Restore(new InventoryData { entries = new[] {
                new InventoryEntry {itemId="same",quantity=1}, new InventoryEntry {itemId="same",quantity=2} } }));
            Assert.That(bag.Quantity("item"), Is.EqualTo(2));
        }
        [Test] public void ItemRecoveryClampsVitalsWithoutResettingGuardOrDeflectWindow()
        {
            var core = new CombatCore(new CombatTuning()); core.RestoreVitals(80,20); core.SetGuard(true,true);
            float window = core.DeflectRemaining; int attack = core.AttackId;
            Assert.That(core.TryRecoverWithItem(40,40), Is.True);
            Assert.That(core.Health, Is.EqualTo(100)); Assert.That(core.Posture, Is.Zero);
            Assert.That(core.State, Is.EqualTo(CombatState.Guard)); Assert.That(core.DeflectRemaining, Is.EqualTo(window)); Assert.That(core.AttackId, Is.EqualTo(attack));
            Assert.That(core.TryRecoverWithItem(40,40), Is.False);
        }
        [Test] public void RecoveryCannotCancelAnAttackOrReviveDeadActor()
        {
            var core = new CombatCore(new CombatTuning()); core.RestoreVitals(50,30); core.RequestAttack();
            var state = core.State; var remaining = core.Remaining;
            Assert.That(core.TryRecoverWithItem(40,40), Is.False); Assert.That(core.State, Is.EqualTo(state)); Assert.That(core.Remaining, Is.EqualTo(remaining));
            core.RestoreVitals(0,0); Assert.That(core.TryRecoverWithItem(40,0), Is.False); Assert.That(core.Health, Is.Zero);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void InvalidRecoveryAmountIsRejected(float amount)
        { var core = new CombatCore(new CombatTuning()); core.RestoreVitals(50,20); Assert.That(core.TryRecoverWithItem(amount,0),Is.False); Assert.That(core.Health,Is.EqualTo(50)); }
        [TestCase(CombatState.Dodge)] [TestCase(CombatState.HitStun)] [TestCase(CombatState.PostureBroken)]
        public void RecoveryCannotBypassDodgeHitStunOrPostureBreak(CombatState state)
        {
            var core=new CombatCore(new CombatTuning()); core.RestoreVitals(50,state==CombatState.PostureBroken?95:30);
            if(state==CombatState.Dodge)core.RequestDodge();
            else {var attacker=new CombatCore(new CombatTuning()); attacker.RequestAttack(); attacker.Tick(.26f); attacker.TryHit(core,false);}
            Assert.That(core.State,Is.EqualTo(state)); float health=core.Health,posture=core.Posture,remaining=core.Remaining;
            Assert.That(core.TryRecoverWithItem(40,40),Is.False); Assert.That(core.Health,Is.EqualTo(health));
            Assert.That(core.Posture,Is.EqualTo(posture)); Assert.That(core.Remaining,Is.EqualTo(remaining));
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void LegacyMigrationRetainsProgressAndAddsExactlyOneKey(int version)
        {
            var source = new PlayerSnapshot { version=version, health=61, experience=37, bossDefeated=version<3 };
            source.worldState.keyItems = new[] {WorldStateService.KeyItemId}; source.worldState.openedDoors = new[] {WorldStateService.GateId};
            source.worldState.collectedObjects = new[] {WorldStateService.KeyObjectId};
            source.questProgress.completedSteps = new[] {"find-key","open-gate"};
            Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory,"save.json"),JsonUtility.ToJson(source));
            var store = new SaveService(directory); Assert.That(store.TryLoad(out var loaded),Is.True,store.LastMessage);
            Assert.That(loaded.version,Is.EqualTo(4)); Assert.That(loaded.health,Is.EqualTo(61));
            Assert.That(loaded.worldState.openedDoors,Does.Contain(WorldStateService.GateId));
            Assert.That(loaded.inventory.entries,Has.Length.EqualTo(1)); Assert.That(loaded.inventory.entries[0].quantity,Is.EqualTo(1));
            if(version==3) { Assert.That(loaded.experience,Is.EqualTo(37)); Assert.That(loaded.questProgress.completedSteps,Is.EqualTo(source.questProgress.completedSteps)); }
            Assert.That(store.TrySave(loaded),Is.True); Assert.That(store.TryLoad(out loaded),Is.True);
            Assert.That(loaded.inventory.entries,Has.Length.EqualTo(1)); Assert.That(loaded.inventory.entries[0].quantity,Is.EqualTo(1));
        }
        [Test] public void LegacyWithoutKeyDoesNotReceiveFreeItems()
        {
            Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory,"save.json"),JsonUtility.ToJson(new PlayerSnapshot{version=3}));
            Assert.That(new SaveService(directory).TryLoad(out var loaded),Is.True); Assert.That(loaded.inventory.entries,Is.Empty);
        }
        [TestCase(0)] [TestCase(-1)] public void InvalidSavedQuantitiesAreRejected(int count)
        {
            var snapshot = new PlayerSnapshot {inventory=new InventoryData {entries=new[]{new InventoryEntry{itemId="item",quantity=count}}}};
            var store=new SaveService(directory); Assert.That(store.TrySave(snapshot),Is.False);
            Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory,"save.json"),JsonUtility.ToJson(snapshot)); Assert.That(store.TryLoad(out _),Is.False);
        }
        [Test] public void MissingInventoryFieldAndDuplicateIdsAreRejected()
        {
            Directory.CreateDirectory(directory); string path=Path.Combine(directory,"save.json");
            File.WriteAllText(path,JsonUtility.ToJson(new PlayerSnapshot()).Replace("\"inventory\"","\"removedInventory\""));
            var store=new SaveService(directory); Assert.That(store.TryLoad(out _),Is.False);
            var bad=new PlayerSnapshot { inventory=new InventoryData {entries=new[]{new InventoryEntry{itemId="x",quantity=1},new InventoryEntry{itemId="x",quantity=1}}}};
            Assert.That(store.TrySave(bad),Is.False); bad.inventory=null; Assert.That(store.TrySave(bad),Is.False);
        }
        [Test] public void BackupRecoversLastCommittedInventoryAndUnknownItems()
        {
            var store=new SaveService(directory); var snapshot=new PlayerSnapshot(); var bag=new InventoryService(); bag.TryAdd("future-item",3); bag.WriteTo(snapshot);
            Assert.That(store.TrySave(snapshot),Is.True); bag.TryConsume("future-item"); bag.WriteTo(snapshot); Assert.That(store.TrySave(snapshot),Is.True);
            File.WriteAllText(Path.Combine(directory,"save.json"),"corrupt"); Assert.That(store.TryLoad(out var recovered),Is.True);
            Assert.That(recovered.inventory.entries[0].itemId,Is.EqualTo("future-item")); Assert.That(recovered.inventory.entries[0].quantity,Is.EqualTo(3));
        }
        [Test] public void CatalogAndPickupValidationBlocksMissingAndDuplicateConfiguration()
        {
            var scene=EditorSceneManager.OpenScene(MvpSceneBuilder.Level); var world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();
            var original=world.items;
            try
            {
                Assert.DoesNotThrow(()=>MvpInventoryUpgrade.Validate(scene));
                world.items=new[]{original[0],original[0]}; Assert.Throws<BuildFailedException>(()=>MvpInventoryUpgrade.Validate(scene));
                world.items=original;
                var pickup=UnityEngine.Object.FindAnyObjectByType<InventoryPickup>(); var item=pickup.item;
                try {pickup.item=null; Assert.Throws<BuildFailedException>(()=>MvpInventoryUpgrade.Validate(scene));} finally {pickup.item=item;}
            }
            finally {world.items=original;}
        }
        [Test] public void UpgradeIsIdempotentAndPreservesAuthoredSceneBytes()
        {
            byte[] before=File.ReadAllBytes(MvpSceneBuilder.Level); MvpInventoryUpgrade.Upgrade();
            Assert.That(File.ReadAllBytes(MvpSceneBuilder.Level),Is.EqualTo(before));
        }
    }
}
