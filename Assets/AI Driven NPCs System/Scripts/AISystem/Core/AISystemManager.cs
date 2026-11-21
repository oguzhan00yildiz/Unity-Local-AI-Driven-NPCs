using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AISystem
{
    /// <summary>How the player talks to NPCs.</summary>
    public enum VoiceInputMode
    {
        /// <summary>Press the NPC's interaction key to open the chat panel. The NPC listens until the chat closes.</summary>
        OpenChat,
        /// <summary>Hold the push-to-talk key near an NPC to speak. The panel shows subtitles. The player keeps moving.</summary>
        PushToTalk
    }

    /// <summary>
    /// Central AI coordinator  added to the scene exactly once.
    /// NPCAgents locate it automatically via AISystemManager.Instance;
    /// no manual Inspector wiring required.
    /// </summary>
    public class AISystemManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        private class ManagerHolder
        {
            public AISystemManager Instance;
        }
        private static readonly ManagerHolder Holder = new ManagerHolder();

        public static AISystemManager Instance
        {
            get
            {
                if (Holder.Instance == null)
#if UNITY_2023_1_OR_NEWER
                    Holder.Instance = FindFirstObjectByType<AISystemManager>();
#else
                    Holder.Instance = FindObjectOfType<AISystemManager>();
#endif
                return Holder.Instance;
            }
        }

        // ── Service references (auto-resolved from children if left empty) ────
        [Header("Services — assigned to children of the AI System prefab")]
        [Tooltip("If left empty, resolved automatically via GetComponentInChildren.")]
        public ChatUIController chatUI;
        public VoiceInputService voiceInput;
        public VoiceOutputService voiceOutput;
        public ModelBootstrapper modelBootstrapper;
        [Tooltip("Bottom-of-screen subtitles used in Push To Talk mode.")]
        public SubtitleView subtitles;

        [Header("Voice Input")]
        [Tooltip("Open Chat: press the NPC's key to open the chat panel. Push To Talk: hold the key below near an NPC to speak.")]
        [SerializeField] private VoiceInputMode voiceInputMode = VoiceInputMode.OpenChat;
        [Tooltip("Key to hold while speaking in Push To Talk mode. The nearest NPC in range hears the player.")]
        [SerializeField] private KeyCode pushToTalkKey = KeyCode.E;

        public VoiceInputMode InputMode     => voiceInputMode;
        public KeyCode        PushToTalkKey => pushToTalkKey;

        /// <summary>Fires with the NPC and its full raw reply (action tags included) when the LLM finishes.</summary>
        public event Action<NPCAgent, string> OnNPCResponse;

        // ── Active NPC ────────────────────────────────────────────────────────
        private NPCAgent _currentNPC;
        private bool _isWaitingForResponse;
        private bool _playerLeftDuringChat;

        // Push-to-talk: NPCs whose trigger the player is inside
        private readonly List<NPCAgent> _npcsInRange = new();
        private Transform _player;

        private bool IsPushToTalk => voiceInputMode == VoiceInputMode.PushToTalk;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        void Awake()
        {
            if (Holder.Instance != null && Holder.Instance != this)
            {
                Debug.LogWarning("[AISystem] Multiple AISystemManagers found in scene. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Holder.Instance = this;

            // Auto-resolve services if not assigned in Inspector
            if (chatUI == null)            chatUI            = GetComponentInChildren<ChatUIController>(true);
            if (voiceInput == null)        voiceInput        = GetComponentInChildren<VoiceInputService>(true);
            if (voiceOutput == null)       voiceOutput       = GetComponentInChildren<VoiceOutputService>(true);
            if (modelBootstrapper == null) modelBootstrapper = GetComponentInChildren<ModelBootstrapper>(true);
            if (subtitles == null)         subtitles         = GetComponentInChildren<SubtitleView>(true);
        }

        void Start()
        {
            // Wire events
            if (voiceInput != null)
            {
                voiceInput.OnTranscription += HandleTranscription;
                voiceInput.OnMuteChanged   += HandleMuteChanged;
            }

            if (voiceOutput != null)
            {
                voiceOutput.OnSpeechStarted  += HandleSpeechStarted;
                voiceOutput.OnSpeechFinished += HandleSpeechFinished;
            }

            if (chatUI != null)
            {
                chatUI.OnSendMessage   += HandleUserMessage;
                chatUI.OnCloseChat     += CloseChat;
                chatUI.OnToggleMicMute += HandleToggleMicMute;
            }

            if (IsPushToTalk)
            {
                _player = GameObject.FindWithTag("Player").transform;
                voiceInput.autoRestartAfterTranscribe = false;
            }
        }

        void Update()
        {
            if (!IsPushToTalk) return;

            if (KeyInput.WasPressed(pushToTalkKey))
                BeginPushToTalk();
            else if (KeyInput.WasReleased(pushToTalkKey))
                EndPushToTalk();
        }

        void OnDestroy()
        {
            if (voiceInput != null)
            {
                voiceInput.OnTranscription -= HandleTranscription;
                voiceInput.OnMuteChanged   -= HandleMuteChanged;
            }
            if (voiceOutput != null)
            {
                voiceOutput.OnSpeechStarted  -= HandleSpeechStarted;
                voiceOutput.OnSpeechFinished -= HandleSpeechFinished;
            }
            if (chatUI != null)
            {
                chatUI.OnSendMessage   -= HandleUserMessage;
                chatUI.OnCloseChat     -= CloseChat;
                chatUI.OnToggleMicMute -= HandleToggleMicMute;
            }

            if (Holder.Instance == this) Holder.Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Holder.Instance = null;
        }

        //  Public API 

        /// <summary>Called when the player presses the interaction key near an NPC.</summary>
        public void OpenChat(NPCAgent npc)
        {
            if (npc == null) return;
            _currentNPC           = npc;
            _isWaitingForResponse = false;

            if (voiceInput != null && chatUI != null)
                chatUI.SetMicMuted(voiceInput.IsMuted);

            chatUI?.Open(npc.NPCName, npc);
            voiceInput?.StartListening();

            npc.SetChatActive(true);
            npc.SetPromptText(voiceInput != null && voiceInput.IsMuted ? "Muted" : "Listening");

            if (npc.AllowPlayerMovementInChat) return;

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
            chatUI?.SetWaiting(false);
            chatUI?.Close();
            subtitles?.HideAfterDelay();

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

        /// <summary>Called by NPCAgent when the player enters or leaves its trigger.</summary>
        public void SetNPCInRange(NPCAgent npc, bool inRange)
        {
            if (inRange)
            {
                if (!_npcsInRange.Contains(npc)) _npcsInRange.Add(npc);
            }
            else
            {
                _npcsInRange.Remove(npc);
            }
        }

        // ── Push-to-talk ──────────────────────────────────────────────────────

        private void BeginPushToTalk()
        {
            if (_isWaitingForResponse || _npcsInRange.Count == 0) return;

            var npc = GetNearestNPCInRange();
            if (npc != _currentNPC)
            {
                _currentNPC?.SetChatActive(false);
                _currentNPC = npc;
                npc.SetChatActive(true);
            }

            // Holding the key while the NPC talks interrupts it
            voiceOutput?.StopSpeaking();
            voiceInput?.StartListening();
            npc.SetPromptText(voiceInput != null && voiceInput.IsMuted ? "Muted" : "Listening");
        }

        private void EndPushToTalk()
        {
            if (voiceInput == null || !voiceInput.IsListening) return;

            // StopRecord hands the recording to Whisper; HandleTranscription sends the text to the NPC
            voiceInput.StopListening();
            // Back to the idle prompt; HandleUserMessage shows "Thinking" once Whisper returns text.
            // A silent recording returns no text, so the prompt must not wait for it.
            _currentNPC?.SetPromptText(null);
        }

        /// <summary>
        /// Lets an NPC react to a game event in its own words, e.g. a companion commenting on a fight.
        /// The line is spoken and shown in the subtitles; it is not added to the chat history.
        /// Returns false when a conversation or another speech is running.
        /// </summary>
        public bool RequestNPCComment(NPCAgent npc, string situation)
        {
            if (_isWaitingForResponse || (voiceOutput != null && voiceOutput.IsSpeaking)
                || (voiceInput != null && voiceInput.IsListening) || npc.Agent == null)
                return false;

            _isWaitingForResponse = true;
            string prompt = "(This is a game event, not the player speaking: " + situation +
                            " React out loud with one short sentence, in character. Do not use action tags.)";
            int spokenLength = 0;
            string full = string.Empty;

            _ = npc.Agent.Chat(prompt,
                partial =>
                {
                    full = partial;
                    subtitles?.ShowNPCLine(npc.NPCName, VoiceOutputService.CleanTextForDisplay(partial), true);
                    if (partial.Length > spokenLength)
                    {
                        voiceOutput?.AppendSpeech(partial.Substring(spokenLength), npc.voiceModelName);
                        spokenLength = partial.Length;
                    }
                },
                () =>
                {
                    _isWaitingForResponse = false;
                    if (string.IsNullOrWhiteSpace(full)) return;
                    voiceOutput?.EndSpeech();
                },
                addToHistory: false);
            return true;
        }

        // ── Conversation display: chat panel (Open Chat) or subtitles (Push To Talk) ──

        private void ShowPlayerMessage(string message)
        {
            if (IsPushToTalk)
            {
                subtitles?.ShowPlayerLine(message);
                return;
            }
            chatUI?.AddMessage(chatUI.playerSenderName, message);
            chatUI?.SetWaiting(true);
        }

        private void ShowNPCMessage(NPCAgent npc, string text, bool final)
        {
            string npcName = npc != null ? npc.NPCName : "AI";
            if (IsPushToTalk)
            {
                subtitles?.ShowNPCLine(npcName, text);
                return;
            }
            if (!final)
            {
                chatUI?.UpdateStreamingResponse(npcName, text, npc);
                return;
            }
            chatUI?.FinalizeResponse(npcName, text, npc);
            chatUI?.SetWaiting(false);
        }

        private NPCAgent GetNearestNPCInRange()
        {
            NPCAgent nearest = null;
            float bestSqr = float.MaxValue;
            foreach (var npc in _npcsInRange)
            {
                float sqr = (npc.transform.position - _player.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                nearest = npc;
            }
            return nearest;
        }

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

            ShowPlayerMessage(message);

            string fullResponse = string.Empty;
            int spokenLength     = 0;             // chars of the response already sent to TTS
            var npcForCallback   = _currentNPC;   // capture before await

            await _currentNPC.Agent.Chat(
                message,
                partial =>
                {
                    fullResponse = partial;
                    ShowNPCMessage(npcForCallback, VoiceOutputService.CleanTextForDisplay(partial), false);

                    // Speak each sentence as soon as it is generated; skip if the session was closed
                    if (_currentNPC != null && partial.Length > spokenLength)
                    {
                        voiceOutput?.AppendSpeech(partial.Substring(spokenLength), npcForCallback?.voiceModelName);
                        spokenLength = partial.Length;
                    }
                },
                () =>
                {
                    _isWaitingForResponse = false;
                    ShowNPCMessage(npcForCallback, VoiceOutputService.CleanTextForDisplay(fullResponse), true);

                    if (npcForCallback != null && !string.IsNullOrWhiteSpace(fullResponse))
                        OnNPCResponse?.Invoke(npcForCallback, fullResponse);

                    // Guard: session may have been closed while LLM was generating
                    // (e.g. player walked out of range during "Thinking" state)
                    if (_currentNPC == null) return;

                    if (!string.IsNullOrWhiteSpace(fullResponse))
                        voiceOutput?.EndSpeech();
                    else if (IsPushToTalk)
                    {
                        npcForCallback?.SetPromptText(null);
                        subtitles?.HideAfterDelay();
                    }
                    else
                    {
                        npcForCallback?.SetPromptText("Listening");
                        voiceInput?.StartListening();
                    }
                });
        }

        private void HandleSpeechStarted()
        {
            if (!IsPushToTalk)
                voiceInput?.PauseListening();
            _currentNPC?.SetPromptText("Talking");
        }

        private void HandleSpeechFinished()
        {
            // Subtitles fade after any speech, including NPC comments outside a conversation.
            // Speech also ends when the player interrupts; the subtitles stay while they talk.
            bool playerTalking = voiceInput != null && voiceInput.IsListening;
            if (IsPushToTalk && !playerTalking)
                subtitles?.HideAfterDelay();

            if (!IsChatOpen()) return;

            // If the player left the NPC's range while the NPC was still talking,
            // close the session now that speech has ended.
            if (_playerLeftDuringChat)
            {
                _playerLeftDuringChat = false;
                CloseChat();
                return;
            }

            if (IsPushToTalk)
            {
                if (!playerTalking)
                    _currentNPC?.SetPromptText(null);
                return;
            }

            _currentNPC?.SetPromptText(voiceInput != null && voiceInput.IsMuted ? "Muted" : "Listening");
            if (!_isWaitingForResponse)
                voiceInput?.ResumeListening();
        }

        private void HandleToggleMicMute()
        {
            voiceInput?.ToggleMute();
        }

        private void HandleMuteChanged(bool isMuted)
        {
            chatUI?.SetMicMuted(isMuted);
            if (_currentNPC != null && !_isWaitingForResponse && (voiceOutput == null || !voiceOutput.IsSpeaking))
            {
                _currentNPC.SetPromptText(isMuted ? "Muted" : "Listening");
            }
        }

        private static void SetPlayerMovement(bool enabled)
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;

            var tpp = player.GetComponentInChildren<ThirdPersonPlayer>();
            if (tpp != null)
                tpp.SetMovementEnabled(enabled);

            var pih = player.GetComponentInChildren<PlayerInputHandler>();
            if (pih != null)
            {
                pih.CursorLocked = !enabled ? false : pih.lockCursorOnStart;
                pih.enabled = enabled;
            }

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
