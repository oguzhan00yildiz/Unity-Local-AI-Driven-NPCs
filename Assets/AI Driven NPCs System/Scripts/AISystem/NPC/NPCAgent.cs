using UnityEngine;
using LLMUnity;

namespace AISystem
{
    /// <summary>
    /// Attach one per NPC  holds the LLMAgent reference and interaction settings.
    /// Locates AISystemManager automatically; no cross-prefab Inspector wiring required.
    ///
    /// Prefab layout:
    ///   [NPC]
    ///      NPCAgent        (this script)
    ///      LLMAgent        (or LLMCharacter)
    ///      SphereCollider  (Is Trigger = true, Radius = interactionRange)
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NPCAgent : MonoBehaviour
    {
        [Header("Personality Template")]
        [Tooltip("Optional preset template for personality and voice settings.")]
        public NPCPersonalityPreset personalityPreset;

        [Header("NPC Identity")]
        public string npcName = "NPC";

        [Header("AI Component")]
        [Tooltip("LLMAgent or LLMCharacter on the same prefab.")]
        public LLMAgent llmAgent;

        [Header("Interaction")]
        public float   interactionRange = 3f;
        public KeyCode interactionKey   = KeyCode.E;

        [Tooltip("Keep player movement and the locked cursor while this NPC's chat is open. Use it for companions that take voice commands on the move.")]
        [SerializeField] private bool allowPlayerMovementInChat;

        [Header("Voice")]
        [Tooltip("Piper voice for this NPC. Download more voices in Tools > AI Packages > Voice Browser.")]
        [VoiceName]
        public string voiceModelName = "en_US-ljspeech-medium";

        [Header("Interaction Prompt")]
        [Tooltip("TextMesh (3D Text) shown above NPC. Auto-found in children if left null.")]
        public TextMesh promptText;

        [Tooltip("Label above the NPC. {0} = NPC name, {1} = status (Hold E to talk, Listening, Thinking, Talking).")]
        [SerializeField] private string promptFormat = "{0}\n({1})";

        //  Internal 
        private bool _playerInRange;
        private bool _chatActive;
        private string _basePrompt;   // personality prompt without the INPCPromptSource parts

        /// <summary>Read by AISystemManager.</summary>
        public LLMAgent Agent   => llmAgent;
        public string   NPCName => npcName;
        public bool     AllowPlayerMovementInChat => allowPlayerMovementInChat;

        //  Lifecycle 
        void Awake()
        {
            // Fall back to same-object LLMAgent if not assigned in Inspector
            if (llmAgent == null)
                llmAgent = GetComponent<LLMAgent>();

            if (personalityPreset != null)
            {
                ApplyPreset(personalityPreset);
            }

            // Sync trigger collider radius to interactionRange
            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
                if (col is SphereCollider sphere)
                    sphere.radius = interactionRange;
            }

            // Auto-find prompt TextMesh from children if not assigned
            if (promptText == null)
                promptText = GetComponentInChildren<TextMesh>(true);

            // Set default text — always visible
            SetPromptText(null);
        }

        void Start()
        {
            if (llmAgent == null)
                Debug.LogError($"[NPCAgent] No LLMAgent found on '{gameObject.name}'!");

            if (AISystemManager.Instance == null)
                Debug.LogWarning("[NPCAgent] AISystemManager not found in scene. Add the 'AI System' prefab.");

            // Prompt sources (actions, knowledge) are initialised in their Awake, so compose here
            RefreshSystemPrompt();
        }

        void Update()
        {
            if (!_playerInRange) return;

            var manager = AISystemManager.Instance;
            if (manager != null && (manager.IsChatOpen() || manager.InputMode == VoiceInputMode.PushToTalk)) return;

            if (KeyInput.WasPressed(interactionKey))
                TriggerChat();
        }

        /// <summary>Prompt text when nobody talks: "Press E to interact", or "Hold E to talk" in push-to-talk mode.</summary>
        private string IdlePrompt
        {
            get
            {
                var manager = AISystemManager.Instance;
                return manager != null && manager.InputMode == VoiceInputMode.PushToTalk
                    ? $"Hold {manager.PushToTalkKey} to talk"
                    : $"Press {interactionKey} to interact";
            }
        }

        //  Trigger

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = true;
            AISystemManager.Instance?.SetNPCInRange(this, true);
            SetPromptText(null);
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = false;
            AISystemManager.Instance?.SetNPCInRange(this, false);

            if (_chatActive)
            {
                // Player left while interacting — stop listening but let NPC finish speaking
                AISystemManager.Instance?.HandlePlayerLeftRange();
            }
            else
            {
                SetPromptText(null);
            }
        }

        /// <summary>
        /// Shows the NPC name and a status (Listening / Thinking / Talking) above the NPC.
        /// Null or empty shows the idle hint ("Hold E to talk").
        /// </summary>
        public void SetPromptText(string state)
        {
            if (promptText == null) return;
            promptText.text = string.Format(promptFormat, npcName, string.IsNullOrEmpty(state) ? IdlePrompt : state);
        }

        /// <summary>Called by AISystemManager on open/close.</summary>
        public void SetChatActive(bool active)
        {
            _chatActive = active;
            if (!active)
                SetPromptText(null);
        }

        /// <summary>Applies personality template settings to this NPC and its LLMAgent.</summary>
        public void ApplyPreset(NPCPersonalityPreset preset)
        {
            if (preset == null) return;
            personalityPreset = preset;
            npcName = preset.npcName;
            voiceModelName = preset.voiceModelName;

            if (llmAgent == null)
                llmAgent = GetComponent<LLMAgent>();

            _basePrompt = preset.systemPrompt;
            RefreshSystemPrompt();
        }

        /// <summary>
        /// Rebuilds the system prompt: the personality prompt plus the text of every INPCPromptSource
        /// on this NPC (actions, story knowledge, world state). Call it when a source's text changes.
        /// </summary>
        public void RefreshSystemPrompt()
        {
            if (llmAgent == null) return;
            _basePrompt ??= llmAgent.systemPrompt;

            var sb = new System.Text.StringBuilder(_basePrompt);
            foreach (var source in GetComponents<INPCPromptSource>())
                sb.Append(source.GetPromptText());
            llmAgent.systemPrompt = sb.ToString();
        }

        private void TriggerChat()
        {
            var manager = AISystemManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[NPCAgent] AISystemManager.Instance is null  add the 'AI System' prefab to the scene.");
                return;
            }
            manager.OpenChat(this);
        }

        //  Editor Gizmo 
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}

