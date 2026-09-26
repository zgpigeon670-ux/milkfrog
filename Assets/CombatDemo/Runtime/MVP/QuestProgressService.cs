using System;
using System.Collections.Generic;
using System.Text;

namespace Milkfrog.CombatDemo
{
    [Serializable] public sealed class QuestProgressData
    {
        public string questId = WorldStateService.QuestId;
        public string[] completedSteps = Array.Empty<string>();
        public bool completed;
    }
    public sealed class QuestProgressService
    {
        readonly QuestDefinition definition;
        public QuestProgressService(QuestDefinition definition) { this.definition = definition; }
        // Facts own progress: saved task flags never override a missing prerequisite.
        public QuestProgressData Evaluate(PlayerSnapshot snapshot)
        {
            var result = new QuestProgressData { questId = definition.stableId };
            var done = new List<string>();
            var state = new WorldStateService(); state.Restore(snapshot.worldState);
            foreach (var step in definition.steps)
            {
                bool met = step.condition == WorldEventKind.BonfireLit ? Array.IndexOf(snapshot.unlockedCheckpointIds ?? Array.Empty<string>(), step.targetId) >= 0 :
                    step.condition == WorldEventKind.ItemAcquired ? state.HasItem(step.targetId) :
                    step.condition == WorldEventKind.DoorOpened ? state.IsDoorOpen(step.targetId) :
                    snapshot.bossDefeated && Array.IndexOf(snapshot.defeatedEnemyIds ?? Array.Empty<string>(), step.targetId) >= 0;
                if (met) done.Add(step.id);
            }
            result.completedSteps = done.ToArray(); result.completed = done.Count == definition.steps.Length;
            return result;
        }
        public string Objective(PlayerSnapshot snapshot)
        {
            var progress = Evaluate(snapshot);
            for (int i = 0; i < definition.steps.Length; i++)
                if (Array.IndexOf(progress.completedSteps, definition.steps[i].id) < 0)
                    return $"主线 {i + 1}/{definition.steps.Length} · {definition.steps[i].text}";
            return "主线完成 · 守门人的试炼";
        }
        public string Journal(PlayerSnapshot snapshot)
        {
            var progress = Evaluate(snapshot); var text = new StringBuilder();
            foreach (var step in definition.steps)
                text.Append(Array.IndexOf(progress.completedSteps, step.id) >= 0 ? "已完成 · " : "未完成 · ").AppendLine(step.text);
            return text.ToString();
        }
        public void WriteTo(PlayerSnapshot snapshot) => snapshot.questProgress = Evaluate(snapshot);
    }
}
