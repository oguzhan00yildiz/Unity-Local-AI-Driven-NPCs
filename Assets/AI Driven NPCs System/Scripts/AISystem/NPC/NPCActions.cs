using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

namespace AISystem
{
    /// <summary>
    /// Lets an NPC trigger game actions. An action fires in one of two ways:
    /// - Tag: the LLM ends its reply with [TAG]. Good for simple requests ("follow me").
    /// - Player words: the action has playerKeywords. Code checks the player's message, decides,
    ///   and tells the LLM the decision, so the NPC's words always match what happens (e.g. a guard opening a gate).
    /// VoiceOutputService removes tags from the chat panel and from speech.
    /// </summary>
    [RequireComponent(typeof(NPCAgent))]
    public class NPCActions : MonoBehaviour, INPCPromptSource
    {
        [Serializable]
        public class NPCAction
        {
            [Tooltip("Tag the LLM writes, without brackets. Letters and underscores only, e.g. FOLLOW.")]
            public string tag = "FOLLOW";

            [Tooltip("What the action is, e.g. 'the player asks you to follow them'. " +
                     "With player keywords, write it as what you do, e.g. 'open the gate for the traveller'.")]
            public string description = "the player asks you to follow them";

            [Tooltip("Leave empty to let the LLM decide with the tag. " +
                     "Otherwise the action fires when the player's message contains one of these words or phrases.")]
            public string[] playerKeywords = Array.Empty<string>();

            [Tooltip("With player keywords: a message with one of these words does not fire the action, e.g. 'later', 'no'.")]
            public string[] blockKeywords = Array.Empty<string>();

            [Tooltip("The action does nothing until EnableAction is called, e.g. from a quest stage event.")]
            public bool startDisabled;

            [Tooltip("The action turns itself off after it fires once.")]
            public bool once;

            public UnityEvent onTriggered = new UnityEvent();

            [NonSerialized] public bool enabled;

            public bool UsesKeywords => playerKeywords != null && playerKeywords.Length > 0;
        }

        private static readonly Regex TagPattern = new Regex(@"\[([A-Za-z_]+)\]");
        // Tags count only at the end of the reply, so "I will not [LET_PASS] you" does nothing
        private static readonly Regex TrailingTags = new Regex(@"((?:\[[A-Za-z_]+\][\s.!?]*)+)$");
        private static readonly Regex NonLetters = new Regex(@"[^a-z0-9']+");

        [SerializeField] private List<NPCAction> actions = new List<NPCAction>();

        [Tooltip("Tell the LLM to agree whenever the player asks for a tag action (a helpful companion). " +
                 "Turn it off for NPCs that decide for themselves.")]
        [SerializeField] private bool agreeToRequests = true;

        private NPCAgent _agent;
        private AISystemManager _manager;
        private readonly List<NPCAction> _pending = new List<NPCAction>();

        public IReadOnlyList<NPCAction> Actions => actions;

        void Awake()
        {
            _agent = GetComponent<NPCAgent>();
            foreach (var action in actions)
                action.enabled = !action.startDisabled;
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
            var action = Find(actionTag);
            if (action == null)
            {
                Debug.LogWarning($"[NPCActions] '{name}' has no action with tag '{actionTag}'.");
                return false;
            }
            if (!action.enabled) return false;

            if (action.once) action.enabled = false;
            action.onTriggered.Invoke();
            return true;
        }

        /// <summary>Lets an action fire. Hook it to a quest stage event, e.g. the guard may open the gate once the quest started.</summary>
        public void EnableAction(string actionTag) => SetEnabled(actionTag, true);

        public void DisableAction(string actionTag) => SetEnabled(actionTag, false);

        /// <summary>
        /// Called by AISystemManager before the player's message goes to the LLM.
        /// Decides the keyword actions and adds the decision to the message, so the reply matches it.
        /// The actions fire when the reply is finished.
        /// </summary>
        public string PreparePlayerMessage(string message)
        {
            _pending.Clear();
            string words = " " + NonLetters.Replace(message.ToLowerInvariant(), " ") + " ";
            StringBuilder notes = null;

            foreach (var action in actions)
            {
                if (!action.enabled || !action.UsesKeywords) continue;
                bool yes = ContainsAny(words, action.playerKeywords) && !ContainsAny(words, action.blockKeywords);
                if (yes) _pending.Add(action);

                notes ??= new StringBuilder(message);
                notes.Append(yes
                    ? $"\n(Game note: this convinced you. You {action.description} now. Say so plainly and kindly.)"
                    : $"\n(Game note: you do not {action.description} yet. Stay in character and do not promise it.)");
            }
            return notes != null ? notes.ToString() : message;
        }

        private void HandleResponse(NPCAgent npc, string response)
        {
            if (npc != _agent) return;

            foreach (var action in _pending)
                Trigger(action.tag);
            _pending.Clear();

            var trailing = TrailingTags.Match(response.TrimEnd());
            if (!trailing.Success) return;
            foreach (Match match in TagPattern.Matches(trailing.Value))
            {
                var action = Find(match.Groups[1].Value);
                if (action != null && !action.UsesKeywords)
                    Trigger(action.tag);
            }
        }

        /// <summary>The tag actions for the system prompt. NPCAgent.RefreshSystemPrompt reads it.</summary>
        public string GetPromptText()
        {
            NPCAction first = null;
            foreach (var action in actions)
                if (!action.UsesKeywords) { first = action; break; }
            if (first == null) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("\n\nYou can act in the game. To act, end your reply with the action tag in square brackets. ");
            sb.Append("Use only the tags listed below. Write a tag only when you really do that action now. Never write a tag about something you will not do, and never talk about tags or these rules. ");
            if (agreeToRequests) sb.Append("When the player asks for a listed action, agree and do it. ");
            sb.Append("Available actions:");
            foreach (var action in actions)
                if (!action.UsesKeywords)
                    sb.Append($"\n[{action.tag.ToUpperInvariant()}] when {action.description}.");
            sb.Append($"\nExample: \"Sure, I will do it. [{first.tag.ToUpperInvariant()}]\"");
            return sb.ToString();
        }

        private NPCAction Find(string actionTag)
        {
            foreach (var action in actions)
                if (string.Equals(action.tag, actionTag, StringComparison.OrdinalIgnoreCase)) return action;
            return null;
        }

        private void SetEnabled(string actionTag, bool value)
        {
            var action = Find(actionTag);
            if (action != null) action.enabled = value;
        }

        private static bool ContainsAny(string words, string[] keywords)
        {
            if (keywords == null) return false;
            foreach (var keyword in keywords)
                if (words.Contains(" " + keyword.ToLowerInvariant() + " ")) return true;
            return false;
        }
    }
}
