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
        public int maxDisplayedMessages = 10;

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
        private readonly List<string> _chatHistory      = new();
        private string             _streamingResponse = string.Empty;

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

        public void Open(string npcName)
        {
            EnsureEventSystem();
            EnsureInputFieldWiring();
            EnsureMicButtonWiring();
            UpdateMicButtonDisplay();

            _currentNPCName    = npcName;
            _streamingResponse = string.Empty;

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

        /// <summary>Adds a completed message to the chat history.</summary>
        public void AddMessage(string sender, string text)
        {
            _chatHistory.Add($"{sender}: {text}");
            if (_chatHistory.Count > maxDisplayedMessages)
                _chatHistory.RemoveAt(0);

            _streamingResponse = string.Empty;
            RefreshDisplay();
        }

        /// <summary>Updates the NPC response in real-time during LLM streaming.</summary>
        public void UpdateStreamingResponse(string sender, string partial)
        {
            _currentNPCName    = sender;
            _streamingResponse = partial;
            RefreshDisplay();
        }

        /// <summary>Finalizes the LLM response  closes the stream and adds it to history.</summary>
        public void FinalizeResponse(string sender, string finalText)
        {
            _streamingResponse = string.Empty;
            if (!string.IsNullOrWhiteSpace(finalText))
                AddMessage(sender, finalText);
            else
                RefreshDisplay();
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

        private void RefreshDisplay()
        {
            if (chatDisplayText == null) return;

            chatDisplayText.text = string.Empty;
            foreach (var line in _chatHistory)
                chatDisplayText.text += line + "\n";

            if (!string.IsNullOrEmpty(_streamingResponse))
                chatDisplayText.text += $"{_currentNPCName}: {_streamingResponse}\n";

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
