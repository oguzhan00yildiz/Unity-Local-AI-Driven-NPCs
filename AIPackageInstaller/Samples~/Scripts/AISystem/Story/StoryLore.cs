using System;
using System.Collections.Generic;
using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Shared story for a scene: world facts every NPC knows and the quest stages in order.
    /// NPCKnowledge adds the world text and the current stage to each NPC's system prompt.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStoryLore", menuName = "AI System/Story Lore")]
    public class StoryLore : ScriptableObject
    {
        [Serializable]
        public class QuestStage
        {
            [Tooltip("Short id used by QuestManager.Advance, e.g. 'started'.")]
            public string id = "intro";

            [Tooltip("Objective shown on the HUD. Leave empty to hide the HUD line.")]
            public string objective;

            [Tooltip("What NPCs know about this stage. Added to every NPC's system prompt.")]
            [TextArea(2, 6)]
            public string promptText;
        }

        [Tooltip("Facts every NPC in the scene knows.")]
        [TextArea(4, 12)]
        public string worldText;

        [Tooltip("Quest stages in order. The first one is active when the scene starts.")]
        public List<QuestStage> stages = new List<QuestStage>();
    }
}
