using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Milkfrog.CombatDemo.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Milkfrog.CombatDemo.Tests
{
    public class QuickConsumableTests
    {
        [Test]
        public void AttackBuffMultipliesGrowthButOnlyChangesHealthDamage()
        {
            var core = new CombatCore(new CombatTuning());
            core.ApplyGrowth(100,100,1.2f); Assert.That(core.TryApplyAttackBuff(1.25f,20),Is.True);
            core.RequestAttack(); core.Tick(.25f);
            var target = new CombatCore(new CombatTuning()); Assert.That(core.TryHit(target,false),Is.EqualTo(HitResult.Hit));
            Assert.That(target.Health,Is.EqualTo(85).Within(.001f)); Assert.That(target.Posture,Is.EqualTo(12).Within(.001f));
            Assert.That(core.ActiveAttack.Block,Is.EqualTo(24).Within(.001f)); Assert.That(core.ActiveAttack.DeflectPosture,Is.EqualTo(30));
        }
        [Test]
        public void RefreshReplacesRemainingTimeWithoutStackingOrResettingGuard()
        {
            var core=new CombatCore(new CombatTuning()); core.TryApplyAttackBuff(1.25f,20); core.Tick(6);
            core.SetGuard(true,true); float deflect=core.DeflectRemaining;
            Assert.That(core.TryApplyAttackBuff(1.25f,20),Is.True);
            Assert.That(core.State,Is.EqualTo(CombatState.Guard)); Assert.That(core.DeflectRemaining,Is.EqualTo(deflect));
            Assert.That(core.AttackBuffRemaining,Is.EqualTo(20)); Assert.That(core.AttackBuffMultiplier,Is.EqualTo(1.25f));
        }
        [Test]
        public void ExpiredBuffDoesNotChangeAnAlreadyCommittedAttack()
        {
            var core=new CombatCore(new CombatTuning()); core.TryApplyAttackBuff(1.25f,.1f); core.RequestAttack(); core.Tick(.25f);
            Assert.That(core.AttackBuffRemaining,Is.Zero); Assert.That(core.AttackBuffMultiplier,Is.EqualTo(1));
            Assert.That(core.ActiveAttack.Damage,Is.EqualTo(12.5f));
            core.Tick(1); core.RequestAttack(); Assert.That(core.ActiveAttack.Damage,Is.EqualTo(10));
        }
        [Test]
        public void ChargeReleaseAndFollowupCaptureDamageBuff()
        {
            var slash=new AttackParameters{CanComboFrom=true}; var follow=new AttackParameters{kind=AttackKind.Followup,damage=12};
            var core=new CombatCore(new CombatTuning(),slash,null,follow); core.TryApplyAttackBuff(1.25f,20);
            core.SetAttackHeld(true);core.BeginPreparation();core.Tick(.8f);core.ReleaseAttack(); Assert.That(core.ActiveAttack.Damage,Is.EqualTo(27.5f));
            core.Tick(1);core.RequestAttack();core.Tick(.7f);Assert.That(core.RequestFollowup(),Is.True);Assert.That(core.ActiveAttack.Damage,Is.EqualTo(15));
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void BuffClockEndsAtTheSameSimulationTime(int fps)
        {
            var core=new CombatCore(new CombatTuning()); core.TryApplyAttackBuff(1.25f,1);
            for(int i=0;i<fps;i++)core.Tick(1f/fps);
            Assert.That(core.AttackBuffRemaining,Is.LessThan(.00001f)); core.Tick(.001f); Assert.That(core.AttackBuffMultiplier,Is.EqualTo(1));
        }
        [Test]
        public void RecoveryDoesNotClearBuffButResetRestoreAndDeathDo()
        {
            var core=new CombatCore(new CombatTuning());core.RestoreVitals(50,20);core.TryApplyAttackBuff(1.25f,20);
            core.TryRecoverWithItem(40,40); Assert.That(core.AttackBuffRemaining,Is.EqualTo(20));
            core.Reset();Assert.That(core.AttackBuffRemaining,Is.Zero);
            core.TryApplyAttackBuff(1.25f,20);core.RestoreVitals(50,0);Assert.That(core.AttackBuffRemaining,Is.Zero);
            core.TryApplyAttackBuff(1.25f,20); var attacker=new CombatCore(new CombatTuning{hitDamage=100});attacker.RequestAttack();attacker.Tick(.25f);attacker.TryHit(core,false);
            Assert.That(core.State,Is.EqualTo(CombatState.Dead));Assert.That(core.AttackBuffRemaining,Is.Zero);
        }
        [TestCase(float.NaN,20)] [TestCase(float.PositiveInfinity,20)] [TestCase(1,20)] [TestCase(1.25f,0)]
        public void InvalidBuffCannotChangeCurrentEffect(float multiplier,float duration)
        {
            var core=new CombatCore(new CombatTuning());core.TryApplyAttackBuff(1.25f,20);
            Assert.That(core.TryApplyAttackBuff(multiplier,duration),Is.False);Assert.That(core.AttackBuffRemaining,Is.EqualTo(20));
        }
        [Test]
        public void ActionsCannotApplyBuffAndExistingAttackRemainsUnchanged()
        {
            var core=new CombatCore(new CombatTuning());core.RequestAttack(); var id=core.AttackId;var remaining=core.Remaining;
            Assert.That(core.TryApplyAttackBuff(1.25f,20),Is.False);Assert.That(core.AttackId,Is.EqualTo(id));Assert.That(core.Remaining,Is.EqualTo(remaining));
        }
        [Test]
        public void EquippedSelectionSurvivesDepletionCaptureAndReplenishment()
        {
            var bag=new InventoryService();Assert.That(bag.TryEquip("heal"),Is.False);bag.TryAdd("heal",1);Assert.That(bag.TryEquip("heal"),Is.True);
            bag.TryConsume("heal");var data=bag.Capture();Assert.That(data.entries,Is.Empty);Assert.That(data.equippedItemId,Is.EqualTo("heal"));
            bag.Restore(data);bag.TryAdd("heal",3);Assert.That(bag.EquippedItemId,Is.EqualTo("heal"));Assert.That(bag.Quantity("heal"),Is.EqualTo(3));
            data.equippedItemId="other";Assert.That(bag.EquippedItemId,Is.EqualTo("heal"));bag.Unequip();Assert.That(bag.EquippedItemId,Is.Empty);
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void LegacySaveMigratesToUnassignedSlotWithoutGrantingConsumables(int version)
        {
            string directory=Path.Combine(Path.GetTempPath(),"QuickMigration-"+Guid.NewGuid());Directory.CreateDirectory(directory);
            try
            {
                var source=new PlayerSnapshot{version=version,health=64};
                if(version==4)source.inventory=new InventoryData{entries=new[]{new InventoryEntry{itemId="healing-medicine",quantity=2}},equippedItemId="healing-medicine"};
                File.WriteAllText(Path.Combine(directory,"save.json"),JsonUtility.ToJson(source));var store=new SaveService(directory);
                Assert.That(store.TryLoad(out var loaded),Is.True,store.LastMessage);Assert.That(loaded.version,Is.EqualTo(SaveService.CurrentVersion));Assert.That(loaded.inventory.equippedItemId,Is.Empty);
                Assert.That(loaded.inventory.entries.Length,Is.EqualTo(version==4?2:1));Assert.That(loaded.health,Is.EqualTo(64));
                Assert.That(store.TrySave(loaded),Is.True);Assert.That(store.TryLoad(out loaded),Is.True);Assert.That(loaded.inventory.entries.Length,Is.EqualTo(version==4?2:1));
                if(version==4)Assert.That(loaded.inventory.entries[0].quantity,Is.EqualTo(2));
            }
            finally {Directory.Delete(directory,true);}
        }
        [Test]
        public void BackupPreservesEquippedUnknownItemEvenWhenEmpty()
        {
            string directory=Path.Combine(Path.GetTempPath(),"QuickBackup-"+Guid.NewGuid());
            try
            {
                var store=new SaveService(directory);var snapshot=new PlayerSnapshot{inventory=new InventoryData{equippedItemId="future-item"}};
                Assert.That(store.TrySave(snapshot),Is.True);snapshot.inventory.equippedItemId="newer";Assert.That(store.TrySave(snapshot),Is.True);
                File.WriteAllText(Path.Combine(directory,"save.json"),"broken");Assert.That(store.TryLoad(out var loaded),Is.True);Assert.That(loaded.inventory.equippedItemId,Is.EqualTo("future-item"));
            }
            finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test]
        public void ConsumableConfigurationRejectsMixedAndInvalidEffects()
        {
            var item=ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                item.restoreHealth=40;Assert.That(item.Usable,Is.True);item.effectType=ItemEffectType.TemporaryAttack;item.attackDamageMultiplier=1.25f;item.attackBuffDuration=20;
                Assert.That(item.EffectsValid,Is.False);item.restoreHealth=0;Assert.That(item.Usable,Is.True);
                item.category=ItemCategory.Quest;Assert.That(item.Usable,Is.False);item.attackBuffDuration=float.NaN;Assert.That(item.EffectsValid,Is.False);
            }
            finally {UnityEngine.Object.DestroyImmediate(item);}
        }
        [Test]
        public void QuickSlotUpgradeIsIdempotentAndPreservesExistingScene()
        {
            var bytes=File.ReadAllBytes(MvpSceneBuilder.Level);MvpInventoryUpgrade.Upgrade();
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(MvpSceneBuilder.Level));
            var scene=EditorSceneManager.GetActiveScene();var world=UnityEngine.Object.FindAnyObjectByType<MvpWorld>();
            Assert.That(world.FindItem("attack-tonic").attackBuffDuration,Is.EqualTo(20));Assert.That(world.GetComponent<GameFlowController>().inventoryView.Configured,Is.True);
            Assert.DoesNotThrow(()=>MvpInventoryUpgrade.Validate(scene));
        }
    }
}
