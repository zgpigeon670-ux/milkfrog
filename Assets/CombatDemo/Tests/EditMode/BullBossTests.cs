using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Milkfrog.CombatDemo.Tests
{
    public class BullBossTests
    {
        [TearDown] public void Cleanup() => BullTrialSession.Clear();
        [Test] public void MemoryStoreClonesAndDoesNotCreateFiles()
        {
            var source=BullTrialSession.DefaultLoadout();var store=SaveService.CreateMemory(source);
            source.inventory.entries[0].quantity=99;
            Assert.That(store.TryLoad(out var loaded),Is.True);Assert.That(loaded.inventory.entries[0].quantity,Is.EqualTo(3));
            var a=SaveService.Clone(loaded);a.inventory.entries[0].quantity=1;Assert.That(store.TrySave(a),Is.True);a.inventory.entries[0].quantity=88;
            store.TryLoad(out var saved);Assert.That(saved.inventory.entries[0].quantity,Is.EqualTo(1));Assert.That(store.IsMemory,Is.True);
        }
        [Test] public void TrialRestartsFromEntryAndReturnsOriginalResourcesAndSlot()
        {
            var source=BullTrialSession.DefaultLoadout();source.position=new Vector3(5,.02f,-30);source.health=47;source.posture=28;source.vitality=3;source.bossDefeated=true;
            BullTrialSession.Begin(source,true,2);source.inventory.entries[0].quantity=99;
            var fresh=BullTrialSession.Fresh();Assert.That(fresh.health,Is.EqualTo(130));Assert.That(fresh.posture,Is.Zero);Assert.That(fresh.bossDefeated,Is.False);Assert.That(fresh.position.z,Is.EqualTo(-10));
            fresh.inventory.entries[0].quantity=0;Assert.That(BullTrialSession.Fresh().inventory.entries[0].quantity,Is.EqualTo(3));
            var returned=BullTrialSession.Return(out int slot);Assert.That(slot,Is.EqualTo(2));Assert.That(returned.health,Is.EqualTo(47));Assert.That(returned.position.x,Is.EqualTo(5));Assert.That(returned.bossDefeated,Is.True);Assert.That(BullTrialSession.Active,Is.False);
        }
        [Test] public void DefaultTrialHasSwordThreePotionsAndDistinctBindings()
        {
            var fresh=BullTrialSession.Fresh();var bag=new InventoryService();bag.Restore(fresh.inventory);
            Assert.That(bag.Equipment.Get(EquipmentSlot.Weapon),Is.EqualTo(EquipmentService.DefaultWeaponId));
            foreach(var id in new[]{"healing-medicine","calming-pill","attack-tonic"})Assert.That(bag.Quantity(id),Is.EqualTo(3));
            Assert.That(fresh.inventory.quickSlots,Is.EqualTo(new[]{"healing-medicine","calming-pill","attack-tonic"}));Assert.That(BullTrialSession.HasReturn,Is.False);
        }
        [Test] public void DiskFilesRemainUntouchedByMemoryTransactions()
        {
            string path=Path.Combine(Path.GetTempPath(),"BullIsolation-"+Guid.NewGuid());
            try
            {
                var disk=new SaveService(path);var source=BullTrialSession.DefaultLoadout();Assert.That(disk.TrySave(source),Is.True);source.health=75;Assert.That(disk.TrySave(source),Is.True);
                var primary=File.ReadAllBytes(Path.Combine(path,"save.json"));var backup=File.ReadAllBytes(Path.Combine(path,"save.backup.json"));
                var memory=SaveService.CreateMemory(source);source.health=50;memory.TrySave(source);memory.TryLoad(out _);
                Assert.That(File.ReadAllBytes(Path.Combine(path,"save.json")),Is.EqualTo(primary));Assert.That(File.ReadAllBytes(Path.Combine(path,"save.backup.json")),Is.EqualTo(backup));
            } finally {if(Directory.Exists(path))Directory.Delete(path,true);}
        }
        [Test] public void ConeAndSweptChargeHaveDirectionalAndDistanceBoundaries()
        {
            Assert.That(BullHitGeometry.Cone(Vector3.forward*3,Vector3.zero,Vector3.forward,3,65,.4f),Is.True);
            Assert.That(BullHitGeometry.Cone(Vector3.back*2,Vector3.zero,Vector3.forward,3,65,.4f),Is.False);
            Assert.That(BullHitGeometry.SegmentDistance(new Vector3(.5f,0,5),Vector3.zero,Vector3.forward*10),Is.EqualTo(.5f).Within(.001));
            Assert.That(BullHitGeometry.SegmentDistance(Vector3.forward*12,Vector3.zero,Vector3.forward*10),Is.EqualTo(2));
        }
        [Test] public void RingSweepsBetweenSamplesWithoutFillingTheInterior()
        {
            Assert.That(BullHitGeometry.Ring(Vector3.forward*4,Vector3.zero,3,5,.14f,.4f),Is.True);
            Assert.That(BullHitGeometry.Ring(Vector3.forward,Vector3.zero,3,5,.14f,.4f),Is.False);
            Assert.That(BullHitGeometry.Ring(Vector3.forward*6,Vector3.zero,3,5,.14f,.4f),Is.False);
        }
        [Test] public void IndependentHitGroupsHitOnceEachAndKeepEquipmentReduction()
        {
            var attack=new CombatCore(new CombatTuning());var player=new CombatCore(new CombatTuning{maxHealth=1000});player.ApplyEquipment(1,.2f,0);
            attack.RequestDefinedAttack(new AttackParameters{startup=.1f,active=2,damage=22,maxDamage=22,response=AttackResponse.DodgeOnly});attack.Tick(.11f);
            Assert.That(attack.TryHit(player,true,1),Is.EqualTo(HitResult.Hit));Assert.That(attack.TryHit(player,true,1),Is.EqualTo(HitResult.Ignore));
            Assert.That(attack.TryHit(player,true,2),Is.EqualTo(HitResult.Hit));Assert.That(player.Health,Is.EqualTo(1000-44*.8f).Within(.001));
        }
        [Test] public void ConfigContainsFiveIndependentMovesAndOriginalModel()
        {
            var d=AssetDatabase.LoadAssetAtPath<BullBossDefinition>("Assets/CombatDemo/Art/YellowBull/BullBoss.asset");Assert.That(d.IsValid,Is.True);
            foreach(var kind in new[]{BullMoveKind.Horn,BullMoveKind.Kick})Assert.That(d.Get(kind).attack.response,Is.EqualTo(AttackResponse.Deflectable));
            foreach(var kind in new[]{BullMoveKind.Charge,BullMoveKind.Slam,BullMoveKind.Wave})Assert.That(d.Get(kind).attack.response,Is.EqualTo(AttackResponse.DodgeOnly));
            Assert.That(d.Get(BullMoveKind.Wave).attack.active,Is.EqualTo(8f/6).Within(.001));Assert.That(d.Get(BullMoveKind.Charge).lockBeforeImpact,Is.EqualTo(.25f));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CombatDemo/Prefabs/MVP/YellowBull.prefab");Assert.That(prefab.GetComponent<CombatAnimationPresenter>(),Is.Null);Assert.That(prefab.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length,Is.GreaterThanOrEqualTo(14));
        }
    }
}
