using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Gives an NPC the scene's shared story, the current quest stage, live world facts and its own
    /// private facts. Per quest stage the NPC can have a goal (added to the prompt, keeps it on the story)
    /// and a written opening line (spoken when the player comes to talk). Everything else the LLM writes.
    /// </summary>
    [RequireComponent(typeof(NPCAgent))]
    public class NPCKnowledge : MonoBehaviour, INPCPromptSource
    {
        [Serializable]
        public class StageDialogue
        {
            [Tooltip("Quest stage id from the StoryLore, e.g. started.")]
            public string stageId;

            [Tooltip("What this NPC wants to do in this stage, e.g. 'send the traveller to Nora at the inn'. Added to the system prompt.")]
            [TextArea(2, 5)] public string goal;

            [Tooltip("Written line the NPC says first when the player comes to talk in this stage. Spoken once per stage. Leave empty to let the player speak first.")]
            [TextArea(2, 5)] public string openingLine;
        }

        [Tooltip("Facts only this NPC knows, e.g. a hint about where the item is and when to share it.")]
        [TextArea(3, 10)]
        [SerializeField] private string privateFacts;

        [Tooltip("Goal and opening line for each quest stage. Stages without an entry use only the facts.")]
        [SerializeField] private List<StageDialogue> stageDialogue = new List<StageDialogue>();

        private NPCAgent _agent;
        private QuestManager _quest;
        private WorldState _world;
        private AISystemManager _manager;
        private string _greetedStageId;
        private bool _greetingPending;
        private readonly StringBuilder _sb = new StringBuilder(1024);

        void Awake()
        {
            _agent = GetComponent<NPCAgent>();
            _quest = FindFirstObjectByType<QuestManager>();
            _world = FindFirstObjectByType<WorldState>();
        }

        void OnEnable()
        {
            if (_quest != null) _quest.OnStageChanged += HandleStageChanged;
            if (_world != null) _world.OnChanged += _agent.RefreshSystemPrompt;
        }

        void Start()
        {
            _manager = AISystemManager.Instance;
            _manager.OnConversationStarted += HandleConversationStarted;
            _manager.OnNPCResponse += HandleResponse;
        }

        void OnDisable()
        {
            if (_quest != null) _quest.OnStageChanged -= HandleStageChanged;
            if (_world != null) _world.OnChanged -= _agent.RefreshSystemPrompt;
        }

        void OnDestroy()
        {
            if (_manager == null) return;
            _manager.OnConversationStarted -= HandleConversationStarted;
            _manager.OnNPCResponse -= HandleResponse;
        }

        public string GetPromptText()
        {
            // Small local models follow short labelled facts better than long prose
            _sb.Clear();
            _sb.Append("\n\nFacts you know. Use them in your own words; never read them out:");
            if (_quest != null)
            {
                _sb.Append("\n- World: ").Append(_quest.Lore.worldText);
                _sb.Append("\n- Right now: ").Append(_quest.CurrentStage.promptText);
            }
            if (!string.IsNullOrWhiteSpace(privateFacts))
                _sb.Append("\n- Only you know: ").Append(privateFacts);
            if (_world != null)
                _sb.Append("\n- You can see: ").Append(_world.Describe());

            var stage = CurrentStageDialogue();
            if (stage != null && !string.IsNullOrWhiteSpace(stage.goal))
                _sb.Append("\n\nYour goal now: ").Append(stage.goal);

            _sb.Append("\n\nAlways speak as ").Append(_agent.NPCName)
               .Append(" in first person, directly to the traveller. Never narrate like a story. Never invent quest facts, places or people that are not listed here. ")
               .Append("Stay in this story. If the traveller talks about something else, answer in one short sentence, then lead back to your goal. ")
               .Append("Answer what the traveller asks. Never tell them what to ask you, and share a fact only when it fits what they said.");
            return _sb.ToString();
        }

        private StageDialogue CurrentStageDialogue()
        {
            if (_quest == null) return null;
            string id = _quest.CurrentStage.id;
            foreach (var stage in stageDialogue)
                if (stage.stageId == id) return stage;
            return null;
        }

        void Update()
        {
            // Another NPC was talking when the player arrived: say the opening line once it is quiet
            if (!_greetingPending) return;
            if (!_agent.PlayerInRange) { _greetingPending = false; return; }
            TryGreet();
        }

        private void HandleConversationStarted(NPCAgent npc)
        {
            if (npc != _agent) return;
            _greetingPending = true;
            TryGreet();
        }

        private void HandleResponse(NPCAgent npc, string _)
        {
            // The player already talks to this NPC, so a late opening line would interrupt
            if (npc == _agent) _greetingPending = false;
        }

        private void TryGreet()
        {
            var stage = CurrentStageDialogue();
            if (stage == null || string.IsNullOrWhiteSpace(stage.openingLine) || stage.stageId == _greetedStageId)
            {
                _greetingPending = false;
                return;
            }
            if (!_manager.SpeakNPCLine(_agent, stage.openingLine)) return;
            _greetedStageId = stage.stageId;
            _greetingPending = false;
        }

        private void HandleStageChanged(StoryLore.QuestStage _) => _agent.RefreshSystemPrompt();
    }
}
