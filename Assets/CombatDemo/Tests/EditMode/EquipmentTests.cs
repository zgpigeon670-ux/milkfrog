using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using Milkfrog.CombatDemo.Editor;
using UnityEngine;

namespace Milkfrog.CombatDemo.Tests
{
    public class EquipmentTests
    {
        ItemDefinition Item(string id)=>AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/CombatDemo/Settings/Items/"+id+".asset");
        InventoryService Complete()
        {
            var bag=new InventoryService();foreach(var id in EquipmentAuthoring.Ids){var item=Item(id);Assert.That(item,Is.Not.Null);bag.TryAdd(id,1);bag.Equipment.TryEquip(item,true);}return bag;
        }
        [Test] public void FullSetCalculatesOnlyWornWeightAndSeparateDefense()
        {
            var bag=Complete();var stats=CharacterStatsCalculator.Calculate(bag,Item);
            Assert.That(stats.Weight,Is.EqualTo(8));Assert.That(stats.MovementMultiplier,Is.EqualTo(.94f).Within(.00001));Assert.That(stats.DamageReduction,Is.EqualTo(.2f));Assert.That(stats.GuardReduction,Is.EqualTo(1f/6).Within(.00001));
            bag.TryAdd("practice-armor",20);Assert.That(CharacterStatsCalculator.Calculate(bag,Item).Weight,Is.EqualTo(8));
            Assert.That(bag.Equipment.TryUnequip(EquipmentSlot.Body),Is.True);Assert.That(bag.Quantity("practice-armor"),Is.EqualTo(21));Assert.That(CharacterStatsCalculator.Calculate(bag,Item).Weight,Is.EqualTo(5));
            Assert.That(bag.Equipment.TryUnequip(EquipmentSlot.Weapon),Is.False);
        }
        [Test] public void MovementAndDefenseHaveFiniteCaps()
        {
            var heavy=new EquipmentStats(10000,10000,10000,0,10);Assert.That(heavy.MovementMultiplier,Is.EqualTo(.4f));Assert.That(heavy.DamageReduction,Is.EqualTo(.8f));Assert.That(heavy.GuardReduction,Is.EqualTo(.8f));
            Assert.That(new EquipmentStats(0,0,0,10,10).MovementMultiplier,Is.EqualTo(1.5f));
        }
        [Test] public void GearRejectsUnownedAndConsumableItemsWithoutChangingAnything()
        {
            var bag=Complete();string original=bag.Equipment.Get(EquipmentSlot.Head);
            Assert.That(bag.Equipment.TryEquip(Item("healing-medicine"),true),Is.False);Assert.That(bag.Equipment.TryEquip(Item("practice-helmet"),false),Is.False);Assert.That(bag.Equipment.Get(EquipmentSlot.Head),Is.EqualTo(original));
            var item=UnityEngine.Object.Instantiate(Item("practice-helmet"));try{item.weight=float.NaN;Assert.That(item.EquipmentValid,Is.False);item.weight=-1;Assert.That(item.EquipmentValid,Is.False);}finally{UnityEngine.Object.DestroyImmediate(item);}
        }
        [Test] public void EquipmentAndQuickSlotSnapshotsAreIndependent()
        {
            var bag=Complete();bag.TryAdd("healing-medicine",3);bag.TryAssignQuickSlot(2,"healing-medicine");var snapshot=bag.Capture();snapshot.equipment.slots[0]="future";snapshot.quickSlots[2]="other";
            Assert.That(bag.Equipment.Get(EquipmentSlot.Head),Is.EqualTo("practice-helmet"));Assert.That(bag.QuickItem(2),Is.EqualTo("healing-medicine"));
        }
        [Test] public void ThreeSlotsMoveBindingsKeepDepletedSelectionAndDoNotSpendOnSelection()
        {
            var bag=new InventoryService();bag.TryAdd("heal",1);bag.TryAdd("pill",2);Assert.That(bag.TryAssignQuickSlot(0,"heal"),Is.True);bag.TryAssignQuickSlot(1,"pill");bag.TryAssignQuickSlot(2,"heal");
            Assert.That(bag.QuickItem(0),Is.Empty);Assert.That(bag.QuickItem(2),Is.EqualTo("heal"));bag.SelectQuickSlot(2);bag.TryConsume("heal");bag.Restore(bag.Capture());
            Assert.That(bag.ActiveQuickSlot,Is.EqualTo(2));Assert.That(bag.EquippedItemId,Is.EqualTo("heal"));Assert.That(bag.TryAssignQuickSlot(0,"heal"),Is.False);Assert.That(bag.TryAssignQuickSlot(3,"pill"),Is.False);
            bag.TryAdd("heal",1);Assert.That(bag.EquippedItemId,Is.EqualTo("heal"));Assert.That(bag.ClearQuickSlot(2),Is.True);
        }
        [Test] public void WeaponGrowthAndTonicMultiplyOnlyHealthAndCommitASnapshot()
        {
            var core=new CombatCore(new CombatTuning());core.ApplyGrowth(100,100,1.5f);core.ApplyEquipment(2,0,0);core.TryApplyAttackBuff(1.25f,20);core.RequestAttack();
            Assert.That(core.ActiveAttack.Damage,Is.EqualTo(37.5f));Assert.That(core.ActiveAttack.Posture,Is.EqualTo(15));core.ClearAttackBuff();core.Tick(.25f);
            var target=new CombatCore(new CombatTuning());target.ApplyEquipment(1,.2f,1f/6);core.TryHit(target,false);Assert.That(target.Health,Is.EqualTo(70));Assert.That(target.Posture,Is.EqualTo(15));
        }
        [Test] public void WeaponGuardDoesNotChangeDeflectOrPerilousHitRules()
        {
            var attacker=new CombatCore(new CombatTuning());var target=new CombatCore(new CombatTuning());target.ApplyEquipment(1,.2f,1f/6);target.SetGuard(true,true);target.Tick(.2f);attacker.RequestAttack();attacker.Tick(.25f);
            Assert.That(attacker.TryHit(target,true),Is.EqualTo(HitResult.Block));Assert.That(target.Posture,Is.EqualTo(20f*5/6).Within(.0001));Assert.That(target.Health,Is.EqualTo(100));
            attacker.Reset();target.Reset();target.SetGuard(true,true);attacker.RequestAttack();attacker.Tick(.01f);attacker.Tick(.24f);Assert.That(attacker.TryHit(target,true),Is.EqualTo(HitResult.Deflect));Assert.That(attacker.Posture,Is.EqualTo(30));
            attacker.Reset();target.Reset();target.SetGuard(true,true);var perilous=AttackParameters.Thrust();perilous.response=AttackResponse.DodgeOnly;attacker.RequestDefinedAttack(perilous);attacker.Tick(perilous.startup);Assert.That(attacker.TryHit(target,true),Is.EqualTo(HitResult.Hit));Assert.That(target.Health,Is.EqualTo(92));
        }
        [Test] public void ApplyingEquipmentDoesNotResetVitalsWindowOrBuff()
        {
            var core=new CombatCore(new CombatTuning());core.RestoreVitals(40,20);core.SetGuard(true,true);core.TryApplyAttackBuff(1.25f,20);float window=core.DeflectRemaining;
            core.ApplyEquipment(1,.2f,1f/6);Assert.That(core.Health,Is.EqualTo(40));Assert.That(core.Posture,Is.EqualTo(20));Assert.That(core.DeflectRemaining,Is.EqualTo(window));Assert.That(core.AttackBuffRemaining,Is.EqualTo(20));
            Assert.Throws<ArgumentOutOfRangeException>(()=>core.ApplyEquipment(float.NaN,0,0));Assert.That(core.WeaponDamageMultiplier,Is.EqualTo(1));
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void MigrationPreservesQuantitiesAndMovesOnlyTheV5Binding(int version)
        {
            string path=Path.Combine(Path.GetTempPath(),"EquipmentMigration-"+Guid.NewGuid());Directory.CreateDirectory(path);
            try
            {
                var original=new PlayerSnapshot{version=version,health=64,inventory=new InventoryData{entries=new[]{new InventoryEntry{itemId="healing-medicine",quantity=2}},equippedItemId="healing-medicine"}};
                File.WriteAllText(Path.Combine(path,"save.json"),JsonUtility.ToJson(original));var store=new SaveService(path);Assert.That(store.TryLoad(out var loaded),Is.True,store.LastMessage);
                Assert.That(loaded.version,Is.EqualTo(6));var bag=new InventoryService();bag.Restore(loaded.inventory);Assert.That(bag.Quantity("healing-medicine"),Is.EqualTo(version>=4?2:0));Assert.That(bag.Quantity(EquipmentService.DefaultWeaponId),Is.EqualTo(1));
                Assert.That(bag.QuickItem(0),Is.EqualTo(version==5?"healing-medicine":""));Assert.That(bag.QuickItem(1),Is.Empty);Assert.That(bag.Equipment.Get(EquipmentSlot.Weapon),Is.EqualTo(EquipmentService.DefaultWeaponId));
                Assert.That(store.TrySave(loaded),Is.True);Assert.That(store.TryLoad(out loaded),Is.True);bag.Restore(loaded.inventory);Assert.That(bag.Quantity(EquipmentService.DefaultWeaponId),Is.EqualTo(1));Assert.That(bag.Quantity("practice-helmet"),Is.Zero);
            }
            finally{Directory.Delete(path,true);}
        }
        [Test] public void InvalidArraysAndDuplicatesAreRejectedAndUnknownBindingsRoundTrip()
        {
            var data=new InventoryData{quickSlots=new[]{"same","same",""}};Assert.That(InventoryService.IsValid(data),Is.False);data.quickSlots=new[]{""};Assert.That(InventoryService.IsValid(data),Is.False);
            data=new InventoryData();data.equipment.slots=new[]{""};Assert.That(InventoryService.IsValid(data),Is.False);
            var bag=new InventoryService();data=new InventoryData{equipment=new EquipmentData{slots=new[]{"future-helmet","","","","future-sword"}},quickSlots=new[]{"future-drug","",""}};bag.Restore(data);Assert.That(bag.Equipment.Get(EquipmentSlot.Head),Is.EqualTo("future-helmet"));Assert.That(CharacterStatsCalculator.Calculate(bag,Item).ArmorDefense,Is.Zero);
        }
        [Test] public void UpgradeIsIdempotentAndProtectsTheModelAndAttackTraces()
        {
            string scene=Milkfrog.CombatDemo.Editor.MvpSceneBuilder.Level;byte[] before=File.ReadAllBytes(scene);Milkfrog.CombatDemo.Editor.MvpInventoryUpgrade.Upgrade();CollectionAssert.AreEqual(before,File.ReadAllBytes(scene));
            var world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();Assert.That(world.items.Length,Is.GreaterThanOrEqualTo(9));Assert.That(world.GetComponent<GameFlowController>().inventoryView.Configured,Is.True);
            Assert.That(world.player.GetComponent<EquipmentVisuals>().modelRoot,Is.Not.Null);Assert.That(LayerMask.NameToLayer("InventoryPreview"),Is.GreaterThanOrEqualTo(8));
        }
        [TestCase("quickSlots")] [TestCase("equipment")] [TestCase("slots")]
        public void MissingV6FieldsRecoverTheCompleteBackupInsteadOfSilentlyLosingEquipment(string field)
        {
            string path=Path.Combine(Path.GetTempPath(),"EquipmentSchema-"+Guid.NewGuid());
            try
            {
                var store=new SaveService(path);var snapshot=new PlayerSnapshot{inventory=Complete().Capture()};snapshot.inventory.quickSlots[2]="future-drug";
                Assert.That(store.TrySave(snapshot),Is.True);Assert.That(store.TrySave(snapshot),Is.True);
                string primary=Path.Combine(path,"save.json");File.WriteAllText(primary,File.ReadAllText(primary).Replace("\""+field+"\"","\"removedField\""));
                Assert.That(store.TryLoad(out var loaded),Is.True);Assert.That(store.LastMessage,Does.Contain("backup"));Assert.That(loaded.inventory.quickSlots[2],Is.EqualTo("future-drug"));Assert.That(loaded.inventory.equipment.slots[1],Is.EqualTo("practice-armor"));
            }
            finally{if(Directory.Exists(path))Directory.Delete(path,true);}
        }
    }
}

