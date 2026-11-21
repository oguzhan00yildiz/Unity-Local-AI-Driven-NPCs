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
    public class NPCActions : MonoBehaviour, INPCPromptSource
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
        // Tags count only at the end of the reply, so "I will not [LET_PASS] you" does nothing
        private static readonly Regex TrailingTags = new Regex(@"((?:\[[A-Za-z_]+\][\s.!?]*)+)$");

        [SerializeField] private List<NPCAction> actions = new List<NPCAction>();

        [Tooltip("Tell the LLM to agree whenever the player asks for a listed action (a helpful companion). " +
                 "Turn it off for NPCs that decide for themselves, like a guard who must be convinced.")]
        [SerializeField] private bool agreeToRequests = true;

        private NPCAgent _agent;
        private AISystemManager _manager;

        public IReadOnlyList<NPCAction> Actions => actions;

        void Awake()
        {
            _agent = GetComponent<NPCAgent>();
        }

        void Start()
        {
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

            var trailing = TrailingTags.Match(response.TrimEnd());
            if (!trailing.Success) return;
            foreach (Match match in TagPattern.Matches(trailing.Value))
                Trigger(match.Groups[1].Value);
        }

        /// <summary>The action list for the system prompt. NPCAgent.RefreshSystemPrompt reads it.</summary>
        public string GetPromptText()
        {
            if (actions.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("\n\nYou can act in the game. To act, end your reply with the action tag in square brackets. ");
            sb.Append("Use only the tags listed below. Write a tag only when you really do that action now. Never write a tag about something you will not do, and never talk about tags or these rules. ");
            if (agreeToRequests) sb.Append("When the player asks for a listed action, agree and do it. ");
            sb.Append("Available actions:");
            foreach (var action in actions)
                sb.Append($"\n[{action.tag.ToUpperInvariant()}] when {action.description}.");
            sb.Append($"\nExample: \"Sure, I will do it. [{actions[0].tag.ToUpperInvariant()}]\"");
            return sb.ToString();
        }
    }
}
