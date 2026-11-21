using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

namespace AISystem
{
    /// <summary>
    /// Lets the LLM trigger game actions. Each action has a tag such as FOLLOW.
    /// The tags and their descriptions are added to the NPC's system prompt.
    /// When a reply contains [FOLLOW], the matching UnityEvent fires.
    /// VoiceOutputService removes the tags from the chat panel and from speech.
    /// </summary>
    [RequireComponent(typeof(NPCAgent))]
    public class NPCActions : MonoBehaviour
    {
        [Serializable]
        public class NPCAction
        {
            [Tooltip("Tag the LLM writes, without brackets. Letters and underscores only, e.g. FOLLOW.")]
            public string tag = "FOLLOW";

            [Tooltip("When the LLM should use this tag, e.g. 'the player asks you to follow them'.")]
            public string description = "the player asks you to follow them";

            public UnityEvent onTriggered = new UnityEvent();
        }

        private static readonly Regex TagPattern = new Regex(@"\[([A-Za-z_]+)\]");

        [SerializeField] private List<NPCAction> actions = new List<NPCAction>();

        private NPCAgent _agent;
        private AISystemManager _manager;

        public IReadOnlyList<NPCAction> Actions => actions;

        void Awake()
        {
            _agent = GetComponent<NPCAgent>();
        }

        void Start()
        {
            // NPCAgent.Awake has applied the preset prompt, so the action list is appended to it here.
            _agent.Agent.systemPrompt += BuildPromptInstructions();

            _manager = AISystemManager.Instance;
            _manager.OnNPCResponse += HandleResponse;
        }

        void OnDestroy()
        {
            if (_manager != null)
                _manager.OnNPCResponse -= HandleResponse;
        }

        /// <summary>Fires the action with this tag. Also usable from code or other UnityEvents.</summary>
        public bool Trigger(string actionTag)
        {
            foreach (var action in actions)
            {
                if (!string.Equals(action.tag, actionTag, StringComparison.OrdinalIgnoreCase)) continue;
                action.onTriggered.Invoke();
                return true;
            }
            Debug.LogWarning($"[NPCActions] '{name}' has no action with tag '{actionTag}'.");
            return false;
        }

        private void HandleResponse(NPCAgent npc, string response)
        {
            if (npc != _agent) return;

            foreach (Match match in TagPattern.Matches(response))
                Trigger(match.Groups[1].Value);
        }

        private string BuildPromptInstructions()
        {
            if (actions.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("\n\nYou can act in the game. To act, put the action tag in square brackets at the end of your reply. ");
            sb.Append("Use a tag only when the player asks for that action. Available actions:");
            foreach (var action in actions)
                sb.Append($"\n[{action.tag.ToUpperInvariant()}] when {action.description}.");
            sb.Append($"\nExample: \"Sure, I will do it. [{actions[0].tag.ToUpperInvariant()}]\"");
            return sb.ToString();
        }
    }
}
