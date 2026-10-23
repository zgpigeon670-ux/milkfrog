using UnityEngine;

namespace Milkfrog.CombatDemo
{
    // The session never owns a disk store. Restart always clones Entry, not the spent inventory.
    public static class BullTrialSession
    {
        public const string SceneName = "BullTrial";
        static PlayerSnapshot entry, returnSnapshot;
        static int returnSlot;
        public static bool Active { get; private set; }
        public static bool HasReturn => returnSnapshot != null;
        public static void Begin(PlayerSnapshot source, bool returnToWorld, int selectedSlot = 0)
        {
            returnSnapshot = returnToWorld ? SaveService.Clone(source) : null; returnSlot = selectedSlot;
            entry = SaveService.Clone(source) ?? DefaultLoadout(); Active = true;
        }
        public static PlayerSnapshot DefaultLoadout()
        {
            var value = new PlayerSnapshot(); var bag = new InventoryService();
            string[] ids = {"healing-medicine","calming-pill","attack-tonic"};
            for(int i=0;i<3;i++) { bag.TryAdd(ids[i],3); bag.TryAssignQuickSlot(i,ids[i]); }
            value.inventory = bag.Capture(); EquipmentService.EnsureDefaultWeapon(value.inventory); return value;
        }
        public static PlayerSnapshot Fresh()
        {
            if (!Active) Begin(null,false);
            var value = SaveService.Clone(entry);
            value.position = new Vector3(0,.02f,-10); value.yaw = 0; value.health = 100 + value.vitality*10; value.posture = 0;
            value.bossDefeated = false; value.defeatedEnemyIds = System.Array.Empty<string>();
            value.worldState = new WorldStateData(); value.questProgress = new QuestProgressData();
            value.activeCheckpointId = BonfireCheckpoint.StartId; value.unlockedCheckpointIds = new[]{BonfireCheckpoint.StartId};
            EquipmentService.EnsureDefaultWeapon(value.inventory); return value;
        }
        public static PlayerSnapshot Return(out int slot)
        {
            var value = SaveService.Clone(returnSnapshot); slot = returnSlot; Clear(); return value;
        }
        public static void Clear() { Active = false; entry = returnSnapshot = null; returnSlot = 0; }
    }
}
