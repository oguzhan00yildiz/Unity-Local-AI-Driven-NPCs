using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System;
using System.Collections;
using System.Collections.Generic;

namespace AISystem
{
    /// <summary>
    /// Pure UI component  unaware of AI logic.
    /// Displays messages, forwards user input, and opens/closes the panel.
    /// </summary>
    public class ChatUIController : MonoBehaviour
    {
        //  Inspector 
        [Header("Panel")]
        public GameObject chatPanel;

        [Header("Text Fields")]
        public Text npcNameText;
        public Text chatDisplayText;
        public Text loadingOverlayText;

        [Header("Input")]
        public InputField playerInputField;
        public Button     sendButton;
        public Button     closeButton;

        [Header("Scroll")]
        public ScrollRect chatScrollRect;
        public float      autoScrollDelay = 0.1f;

        [Header("Microphone Controls")]
        public Button micToggleButton;
        public Text   micToggleText;
        public string micUnmutedLabel = "Mute";
        public string micMutedLabel   = "Unmute";

        [Header("Settings")]
        [Tooltip("Messages kept per NPC conversation")]
        public int maxDisplayedMessages = 10;

        [Header("Message Style")]
        [Tooltip("Sender name used by AISystemManager for the player's messages")]
        public string playerSenderName = "Player";
        public Color  playerNameColor  = new Color(0.45f, 0.75f, 1f);
        public Color  npcNameColor     = new Color(1f, 0.8f, 0.4f);
        [Tooltip("Text placed between two messages")]
        public string messageSeparator = "\n\n";

        //  Events 
        /// <summary>Fired when the user submits a message (text field or Enter key).</summary>
        public event Action<string> OnSendMessage;

        /// <summary>Fired when the panel is closed via the close button or ESC key.</summary>
        public event Action OnCloseChat;

        /// <summary>Fired when the user clicks the mute/unmute button.</summary>
        public event Action OnToggleMicMute;

        //  State 
        private bool               _isOpen;
        private bool               _isMicMuted;
        private string             _currentNPCName  = string.Empty;
        private string             _streamingResponse = string.Empty;

        // One history per conversation partner. The key is passed to Open (AISystemManager uses the NPCAgent).
        private readonly Dictionary<object, List<string>> _histories = new();
        private object       _currentKey;
        private List<string> _chatHistory = new();
        private readonly System.Text.StringBuilder _displayBuilder = new();

        public bool IsOpen     => _isOpen;
        public bool IsMicMuted => _isMicMuted;

        //  Lifecycle 
        void Awake() { /* intentionally empty — panel is hidden in Start after UI children initialise */ }

        void Start()
        {
            EnsureEventSystem();
            EnsureInputFieldWiring();
            EnsureMicButtonWiring();

            if (chatPanel != null) chatPanel.SetActive(false);

            if (sendButton      != null) sendButton.onClick.AddListener(OnSendClicked);
            if (closeButton     != null) closeButton.onClick.AddListener(OnCloseClicked);
            if (micToggleButton != null) micToggleButton.onClick.AddListener(OnMicToggleClicked);
            if (playerInputField != null)
                playerInputField.onEndEdit.AddListener(OnInputEndEdit);

            UpdateMicButtonDisplay();
        }

        void Update()
        {
            if (_isOpen && IsEscapePressed())
                OnCloseClicked();
        }

        private bool IsEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private bool IsEnterPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
        }

        private void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
#else
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
#endif
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
            var uiModule = esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            uiModule.AssignDefaultActions();
#else
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }

        private void EnsureInputFieldWiring()
        {
            if (playerInputField == null) return;

            if (playerInputField.textComponent == null)
            {
                var texts = playerInputField.GetComponentsInChildren<Text>(true);
                foreach (var t in texts)
                {
                    if (t.gameObject.name.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (playerInputField.placeholder == null)
                            playerInputField.placeholder = t;
                    }
                    else
                    {
                        if (playerInputField.textComponent == null)
                            playerInputField.textComponent = t;
                    }
                }
            }
        }

        private void EnsureMicButtonWiring()
        {
            if (micToggleButton == null)
            {
                var buttons = GetComponentsInChildren<Button>(true);
                foreach (var btn in buttons)
                {
                    if (btn.gameObject.name.IndexOf("mic", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        micToggleButton = btn;
                        break;
                    }
                }
            }

            if (micToggleButton != null && micToggleText == null)
            {
                micToggleText = micToggleButton.GetComponentInChildren<Text>(true);
            }
        }

        //  Public API 

        /// <summary>Opens the panel and shows the history stored for historyKey (npcName when null).</summary>
        public void Open(string npcName, object historyKey = null)
        {
            EnsureEventSystem();
            EnsureInputFieldWiring();
            EnsureMicButtonWiring();
            UpdateMicButtonDisplay();

            _currentNPCName    = npcName;
            _streamingResponse = string.Empty;
            _currentKey        = historyKey ?? npcName;
            _chatHistory       = GetHistory(_currentKey);

            if (npcNameText != null) npcNameText.text = npcName;
            if (chatPanel   != null)
            {
                chatPanel.SetActive(true);
                // Force UI components inside the ScrollRect to fully initialize before
                // Unity's culling/clipping pass runs, preventing NullReferenceException
                // in ScrollRect.LateUpdate.
                Canvas.ForceUpdateCanvases();
            }
            _isOpen = true;

            if (playerInputField != null)
            {
                playerInputField.text = string.Empty;
                playerInputField.interactable = true;
                playerInputField.Select();
                playerInputField.ActivateInputField();
            }

            RefreshDisplay();
            // Note: Cursor lock and player movement are managed by AISystemManager.
        }

        public void Close()
        {
            if (chatPanel != null) chatPanel.SetActive(false);
            _isOpen            = false;
            _streamingResponse = string.Empty;

            // Note: Cursor lock and player movement are managed by AISystemManager.
        }

        /// <summary>Adds a completed message to the open conversation.</summary>
        public void AddMessage(string sender, string text) => AddMessage(sender, text, _currentKey);

        /// <summary>
        /// Adds a completed message to the conversation for historyKey.
        /// A reply that finishes after the player switched NPCs goes to its own NPC's history.
        /// </summary>
        public void AddMessage(string sender, string text, object historyKey)
        {
            var history = GetHistory(historyKey ?? _currentKey);
            history.Add(FormatLine(sender, text));
            if (history.Count > maxDisplayedMessages)
                history.RemoveAt(0);

            if (history != _chatHistory) return;
            _streamingResponse = string.Empty;
            RefreshDisplay();
        }

        /// <summary>Updates the NPC response in real-time during LLM streaming.</summary>
        public void UpdateStreamingResponse(string sender, string partial, object historyKey = null)
        {
            if (historyKey != null && !Equals(historyKey, _currentKey)) return;
            _currentNPCName    = sender;
            _streamingResponse = partial;
            RefreshDisplay();
        }

        /// <summary>Finalizes the LLM response  closes the stream and adds it to history.</summary>
        public void FinalizeResponse(string sender, string finalText, object historyKey = null)
        {
            bool isCurrent = historyKey == null || Equals(historyKey, _currentKey);
            if (isCurrent) _streamingResponse = string.Empty;

            if (!string.IsNullOrWhiteSpace(finalText))
                AddMessage(sender, finalText, historyKey);
            else if (isCurrent)
                RefreshDisplay();
        }

        /// <summary>Deletes the stored conversation for historyKey.</summary>
        public void ClearHistory(object historyKey)
        {
            _histories.Remove(historyKey);
            if (Equals(historyKey, _currentKey))
            {
                _chatHistory = GetHistory(historyKey);
                RefreshDisplay();
            }
        }

        /// <summary>Sets the input field text (used for transcription preview).</summary>
        public void SetInputText(string text)
        {
            if (playerInputField != null) playerInputField.text = text;
        }

        /// <summary>Enables or disables the send button based on waiting state.</summary>
        public void SetWaiting(bool waiting)
        {
            if (sendButton != null) sendButton.interactable = !waiting;
        }

        /// <summary>Shows or hides the loading overlay (used by ModelBootstrapper).</summary>
        public void SetLoadingOverlay(bool visible, string message = "")
        {
            if (loadingOverlayText == null) return;
            // Guard: only manipulate text while the canvas hierarchy is active.
            if (visible)
            {
                loadingOverlayText.gameObject.SetActive(true);
                loadingOverlayText.text = message;
            }
            else
            {
                loadingOverlayText.text = string.Empty;
                loadingOverlayText.gameObject.SetActive(false);
            }
        }

        /// <summary>Updates the mute/unmute button state and label.</summary>
        public void SetMicMuted(bool isMuted)
        {
            _isMicMuted = isMuted;
            UpdateMicButtonDisplay();
        }

        public void UpdateMicButtonDisplay()
        {
            if (micToggleText != null)
                micToggleText.text = _isMicMuted ? micMutedLabel : micUnmutedLabel;
        }

        //  Internal 

        private void OnMicToggleClicked()
        {
            OnToggleMicMute?.Invoke();
        } 

        private void OnSendClicked()
        {
            if (playerInputField == null) return;
            string msg = playerInputField.text.Trim();
            if (!string.IsNullOrEmpty(msg))
            {
                playerInputField.text = string.Empty;
                playerInputField.ActivateInputField();
                OnSendMessage?.Invoke(msg);
            }
        }

        private void OnInputEndEdit(string value)
        {
            if (playerInputField != null && !playerInputField.wasCanceled && IsEnterPressed())
                OnSendClicked();
        }

        private void OnCloseClicked() => OnCloseChat?.Invoke();

        private List<string> GetHistory(object key)
        {
            if (key == null) return _chatHistory;
            if (!_histories.TryGetValue(key, out var history))
            {
                history = new List<string>();
                _histories[key] = history;
            }
            return history;
        }

        private string FormatLine(string sender, string text)
        {
            if (chatDisplayText == null || !chatDisplayText.supportRichText)
                return $"{sender}: {text}";

            Color color = sender == playerSenderName ? playerNameColor : npcNameColor;
            return $"<b><color=#{ColorUtility.ToHtmlStringRGB(color)}>{sender}</color></b>\n{text}";
        }

        private void RefreshDisplay()
        {
            if (chatDisplayText == null) return;

            _displayBuilder.Clear();
            foreach (var line in _chatHistory)
                _displayBuilder.Append(line).Append(messageSeparator);

            if (!string.IsNullOrEmpty(_streamingResponse))
                _displayBuilder.Append(FormatLine(_currentNPCName, _streamingResponse));

            chatDisplayText.text = _displayBuilder.ToString();

            if (chatScrollRect != null)
                StartCoroutine(ScrollToBottom());
        }

        private IEnumerator ScrollToBottom()
        {
            yield return new WaitForSecondsRealtime(autoScrollDelay);
            if (chatScrollRect != null)
                chatScrollRect.verticalNormalizedPosition = 0f;  // 0 = bottom, 1 = top
        }

    }
}
