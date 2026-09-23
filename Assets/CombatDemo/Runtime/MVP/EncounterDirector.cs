using System.Collections.Generic;

namespace Milkfrog.CombatDemo
{
    public enum EncounterState { Exploration, NormalCombat, BossCombat }
    public sealed class EncounterDirector
    {
        readonly MvpWorld world;
        readonly HashSet<EnemyController> participants = new HashSet<EnemyController>();
        public EncounterState State { get; private set; }
        public float QuietTime { get; private set; }
        public float Remaining => System.Math.Max(0, 10 - QuietTime);
        public int Count => participants.Count;
        public EncounterDirector(MvpWorld owner) { world = owner; }

        public void Tick(float dt)
        {
            if (State == EncounterState.BossCombat) return;
            foreach (var enemy in world.enemies)
            {
                if (!enemy.Alive) { participants.Remove(enemy); continue; }
                if (enemy.IsBoss)
                {
                    if (EnemyController.FlatDistance(world.player.transform.position, enemy.Home) <= enemy.definition.bossTriggerRadius)
                    { StartBoss((BossEnemy)enemy); return; }
                    continue;
                }
                enemy.UpdateRearm();
                if (enemy.OutsideLeash(world.player.transform.position))
                {
                    if (participants.Remove(enemy)) enemy.ReturnHome(true);
                    continue;
                }
                if (!participants.Contains(enemy) && !enemy.SuppressProximity && enemy.Activity != EnemyActivity.Returning &&
                    EnemyController.FlatDistance(world.player.transform.position, enemy.transform.position) <= enemy.definition.alertRadius &&
                    enemy.Actor.HasLineOfSight(world.player, world.player.transform.position + UnityEngine.Vector3.up)) Engage(enemy);
            }
            if (participants.Count > 0)
            {
                QuietTime += dt;
                if (QuietTime >= 10 - .000001f)
                {
                    foreach (var enemy in participants) enemy.ReturnHome(false);
                    participants.Clear();
                }
            }
            if (participants.Count == 0 && State == EncounterState.NormalCombat)
            { State = EncounterState.Exploration; world.RequestSave(); }
        }
        public void Engage(EnemyController enemy)
        {
            if (State == EncounterState.BossCombat || enemy.IsBoss || !enemy.Alive || enemy.OutsideLeash(world.player.transform.position)) return;
            if (participants.Add(enemy)) enemy.Engage();
            if (State == EncounterState.Exploration) QuietTime = 0;
            State = EncounterState.NormalCombat;
        }
        public void OnInteraction(HitEvent hit)
        {
            if (hit.Result == HitResult.Ignore || State == EncounterState.BossCombat) return;
            foreach (var enemy in world.enemies)
                if (!enemy.IsBoss && (enemy.Actor.Core == hit.Attacker || enemy.Actor.Core == hit.Defender))
                { Engage(enemy); QuietTime = 0; break; }
        }
        void StartBoss(BossEnemy boss)
        {
            foreach (var enemy in world.enemies)
                if (!enemy.IsBoss && enemy.Alive) { enemy.ReturnHome(true); enemy.Actor.AcceptsDamage = false; }
            participants.Clear(); State = EncounterState.BossCombat;
            boss.Engage(); world.Lock.Force(boss);
            if (world.arena != null) world.arena.SetActive(true);
        }
        public void FinishBoss()
        {
            State = EncounterState.Exploration; participants.Clear();
            if (world.arena != null) world.arena.SetActive(false);
            world.Lock.Clear();
            foreach (var enemy in world.enemies) if (!enemy.IsBoss && enemy.Alive) enemy.Actor.AcceptsDamage = true;
        }
    }
}
