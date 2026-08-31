using UnityEngine;
using System;
using System.Threading.Tasks;

namespace AISystem
{
    /// <summary>
    /// Central AI coordinator  added to the scene exactly once.
    /// NPCAgents locate it automatically via AISystemManager.Instance;
    /// no manual Inspector wiring required.
    /// </summary>
    public class AISystemManager : MonoBehaviour
    {
        //  Singleton 
        public static AISystemManager Instance { get; private set; }

        //  Service references (auto-resolved from children if left empty) 
        [Header("Services  assigned to children of the AI System prefab")]
        [Tooltip("If left empty, resolved automatically via GetComponentInChildren.")]
        public ChatUIController chatUI;
        public VoiceInputService voiceInput;
        public VoiceOutputService voiceOutput;
        public ModelBootstrapper modelBootstrapper;

        //  Active NPC 
        private NPCAgent _currentNPC;
        private bool _isWaitingForResponse;
        private bool _playerLeftDuringChat;

        //  Lifecycle 
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[AISystem] Multiple AISystemManagers found in scene. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Auto-resolve services if not assigned in Inspector
            if (chatUI == null)            chatUI            = GetComponentInChildren<ChatUIController>(true);
            if (voiceInput == null)        voiceInput        = GetComponentInChildren<VoiceInputService>(true);
            if (voiceOutput == null)       voiceOutput       = GetComponentInChildren<VoiceOutputService>(true);
            if (modelBootstrapper == null) modelBootstrapper = GetComponentInChildren<ModelBootstrapper>(true);
        }

        void Start()
        {
            // Wire events
            if (voiceInput != null)
                voiceInput.OnTranscription += HandleTranscription;

            if (voiceOutput != null)
            {
                voiceOutput.OnSpeechStarted  += HandleSpeechStarted;
                voiceOutput.OnSpeechFinished += HandleSpeechFinished;
            }

            if (chatUI != null)
            {
                chatUI.OnSendMessage += HandleUserMessage;
                chatUI.OnCloseChat   += CloseChat;
            }
        }

        void OnDestroy()
        {
            if (voiceInput  != null) voiceInput.OnTranscription   -= HandleTranscription;
            if (voiceOutput != null)
            {
                voiceOutput.OnSpeechStarted  -= HandleSpeechStarted;
                voiceOutput.OnSpeechFinished -= HandleSpeechFinished;
            }
            if (chatUI != null)
            {
                chatUI.OnSendMessage -= HandleUserMessage;
                chatUI.OnCloseChat   -= CloseChat;
            }

            if (Instance == this) Instance = null;
        }

        //  Public API 

        /// <summary>Called when the player presses the interaction key near an NPC.</summary>
        public void OpenChat(NPCAgent npc)
        {
            if (npc == null) return;
            _currentNPC           = npc;
            _isWaitingForResponse = false;

            chatUI?.Open(npc.NPCName);
            voiceInput?.StartListening();

            npc.SetChatActive(true);
            npc.SetPromptText("Listening");

            // Unlock cursor for interaction
            Cursor.visible   = true;
            Cursor.lockState = CursorLockMode.None;
            SetPlayerMovement(false);
        }

        /// <summary>Closes the chat panel (close button or ESC key).</summary>
        public void CloseChat()
        {
            voiceInput?.StopListening();
            voiceOutput?.StopSpeaking();
            chatUI?.Close();

            _currentNPC?.SetChatActive(false);
            _currentNPC           = null;
            _isWaitingForResponse = false;
            _playerLeftDuringChat = false;

            // Re-lock cursor and re-enable player
            Cursor.visible   = false;
            Cursor.lockState = CursorLockMode.Locked;
            SetPlayerMovement(true);
        }

        public bool IsChatOpen() => _currentNPC != null;

        /// <summary>
        /// Called by NPCAgent when the player leaves the trigger range while a chat is active.
        /// Stops listening immediately; if the NPC is currently speaking the session will
        /// close automatically once speech finishes.
        /// </summary>
        public void HandlePlayerLeftRange()
        {
            if (_currentNPC == null) return;

            // Always stop listening — player is no longer in range
            voiceInput?.StopListening();

            if (voiceOutput != null && voiceOutput.IsSpeaking)
            {
                // NPC is mid-sentence — let it finish, then close in HandleSpeechFinished
                _playerLeftDuringChat = true;
                _currentNPC?.SetPromptText("Talking");
            }
            else
            {
                // Not speaking — close the session right away
                _playerLeftDuringChat = false;
                CloseChat();
            }
        }

        //  Internal event handlers 

        private void HandleTranscription(string text)
        {
            if (_isWaitingForResponse || _currentNPC == null) return;
            HandleUserMessage(text);
        }

        private async void HandleUserMessage(string message)
        {
            if (_currentNPC == null || _isWaitingForResponse) return;
            if (string.IsNullOrWhiteSpace(message)) return;

            _isWaitingForResponse = true;
            voiceInput?.StopListening();
            _currentNPC?.SetPromptText("Thinking");

            string fullResponse = string.Empty;
            var npcForCallback   = _currentNPC;   // capture before await

            await _currentNPC.Agent.Chat(
                message,
                partial => { fullResponse = partial; },
                () =>
                {
                    _isWaitingForResponse = false;

                    // Guard: session may have been closed while LLM was generating
                    // (e.g. player walked out of range during "Thinking" state)
                    if (_currentNPC == null) return;

                    if (!string.IsNullOrWhiteSpace(fullResponse))
                        voiceOutput?.Speak(fullResponse, npcForCallback?.voiceModelName);
                    else
                    {
                        npcForCallback?.SetPromptText("Listening");
                        voiceInput?.StartListening();
                    }
                });
        }

        private void HandleSpeechStarted()
        {
            voiceInput?.PauseListening();
            _currentNPC?.SetPromptText("Talking");
        }

        private void HandleSpeechFinished()
        {
            if (!IsChatOpen()) return;

            // If the player left the NPC's range while the NPC was still talking,
            // close the session now that speech has ended.
            if (_playerLeftDuringChat)
            {
                _playerLeftDuringChat = false;
                CloseChat();
                return;
            }

            _currentNPC?.SetPromptText("Listening");
            if (!_isWaitingForResponse)
                voiceInput?.ResumeListening();
        }

        private static void SetPlayerMovement(bool enabled)
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;
            foreach (var mb in player.GetComponentsInChildren<MonoBehaviour>())
            {
                if (mb == null) continue;
                var t = mb.GetType().Name;
                if (t is "StarterAssetsInputs" or "ThirdPersonController"
                       or "FirstPersonController" or "PlayerInput")
                    mb.enabled = enabled;
            }
        }
    }
}
