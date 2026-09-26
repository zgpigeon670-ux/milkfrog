using System;
using UnityEngine;

namespace Milkfrog.CombatDemo
{
    [Serializable] public sealed class QuestStep
    {
        public string id, text, targetId;
        public WorldEventKind condition;
    }
    [CreateAssetMenu(menuName = "Combat Demo/Quest")]
    public sealed class QuestDefinition : ScriptableObject
    {
        public string stableId = WorldStateService.QuestId;
        public string displayName = "守门人的试炼";
        public QuestStep[] steps = {
            new QuestStep { id="light-camp", text="点亮守门篝火", condition=WorldEventKind.BonfireLit, targetId=BonfireCheckpoint.BossApproachId },
            new QuestStep { id="find-key", text="取得守门钥匙（篝火东侧）", condition=WorldEventKind.ItemAcquired, targetId=WorldStateService.KeyItemId },
            new QuestStep { id="open-gate", text="开启封印门", condition=WorldEventKind.DoorOpened, targetId=WorldStateService.GateId },
            new QuestStep { id="defeat-boss", text="击败守门人", condition=WorldEventKind.BossDefeated, targetId=WorldStateService.BossId }
        };
    }
}
