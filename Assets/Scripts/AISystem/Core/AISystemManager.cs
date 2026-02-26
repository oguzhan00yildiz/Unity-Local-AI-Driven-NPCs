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
        }

        /// <summary>Closes the chat panel (close button or ESC key).</summary>
        public void CloseChat()
        {
            voiceInput?.StopListening();
            voiceOutput?.StopSpeaking();
            chatUI?.Close();
            _currentNPC           = null;
            _isWaitingForResponse = false;
        }

        public bool IsChatOpen() => chatUI != null && chatUI.IsOpen;

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

            chatUI?.AddMessage("You", message);
            chatUI?.SetInputText(string.Empty);
            chatUI?.SetWaiting(true);

            string fullResponse = string.Empty;

            await _currentNPC.Agent.Chat(
                message,
                partial =>
                {
                    fullResponse = partial;
                    chatUI?.UpdateStreamingResponse(_currentNPC.NPCName, partial);
                },
                () =>
                {
                    chatUI?.FinalizeResponse(_currentNPC.NPCName, fullResponse);
                    chatUI?.SetWaiting(false);
                    _isWaitingForResponse = false;

                    if (!string.IsNullOrWhiteSpace(fullResponse))
                        voiceOutput?.Speak(fullResponse);
                    else
                        voiceInput?.StartListening();
                });
        }

        private void HandleSpeechStarted()  => voiceInput?.PauseListening();

        private void HandleSpeechFinished()
        {
            if (IsChatOpen() && !_isWaitingForResponse)
                voiceInput?.ResumeListening();
        }
    }
}
