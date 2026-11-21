using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AISystem
{
    /// <summary>
    /// Tracks the current quest stage of a StoryLore. Stages only move forward, one at a time,
    /// so an NPC action like [QUEST_COMPLETE] does nothing before the item is found.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        [Serializable]
        public class StageEvent
        {
            public string stageId;
            public UnityEvent onEntered = new UnityEvent();
        }

        [SerializeField] private StoryLore lore;

        [Tooltip("Scene reactions to stages, e.g. show the quest complete screen.")]
        [SerializeField] private List<StageEvent> stageEvents = new List<StageEvent>();

        private int _stageIndex;

        public StoryLore Lore => lore;
        public StoryLore.QuestStage CurrentStage => lore.stages[_stageIndex];

        public event Action<StoryLore.QuestStage> OnStageChanged;

        /// <summary>Moves to the stage with this id if it is the next one. Hook it to UnityEvents.</summary>
        public void Advance(string stageId)
        {
            int next = _stageIndex + 1;
            if (next >= lore.stages.Count || lore.stages[next].id != stageId)
            {
                Debug.Log($"[QuestManager] Ignored '{stageId}': current stage is '{CurrentStage.id}'.");
                return;
            }

            _stageIndex = next;
            Debug.Log($"[QuestManager] Stage: {stageId}");
            OnStageChanged?.Invoke(CurrentStage);
            foreach (var e in stageEvents)
                if (e.stageId == stageId) e.onEntered.Invoke();
        }

        public bool IsAtOrAfter(string stageId)
        {
            for (int i = 0; i <= _stageIndex; i++)
                if (lore.stages[i].id == stageId) return true;
            return false;
        }
    }
}
