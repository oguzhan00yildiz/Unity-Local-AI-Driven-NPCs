using System.Text;
using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Gives an NPC the scene's shared story, the current quest stage, live world facts and its own
    /// private facts. The NPC talks about them in its own words; nothing is a written dialogue line.
    /// </summary>
    [RequireComponent(typeof(NPCAgent))]
    public class NPCKnowledge : MonoBehaviour, INPCPromptSource
    {
        [Tooltip("Facts only this NPC knows, e.g. a hint about where the item is and when to share it.")]
        [TextArea(3, 10)]
        [SerializeField] private string privateFacts;

        private NPCAgent _agent;
        private QuestManager _quest;
        private WorldState _world;
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

        void OnDisable()
        {
            if (_quest != null) _quest.OnStageChanged -= HandleStageChanged;
            if (_world != null) _world.OnChanged -= _agent.RefreshSystemPrompt;
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
            _sb.Append("\n\nAlways speak as ").Append(_agent.NPCName)
               .Append(" in first person, directly to the traveller. Never narrate like a story. Never invent quest facts that are not listed here.");
            return _sb.ToString();
        }

        private void HandleStageChanged(StoryLore.QuestStage _) => _agent.RefreshSystemPrompt();
    }
}
