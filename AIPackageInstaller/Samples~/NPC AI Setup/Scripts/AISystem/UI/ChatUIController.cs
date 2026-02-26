using UnityEngine;
using UnityEngine.UI;
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

        [Header("Settings")]
        public int maxDisplayedMessages = 10;

        //  Events 
        /// <summary>Fired when the user submits a message (text field or Enter key).</summary>
        public event Action<string> OnSendMessage;

        /// <summary>Fired when the panel is closed via the close button or ESC key.</summary>
        public event Action OnCloseChat;

        //  State 
        private bool               _isOpen;
        private string             _currentNPCName  = string.Empty;
        private readonly List<string> _chatHistory      = new();
        private string             _streamingResponse = string.Empty;

        public bool IsOpen => _isOpen;

        //  Lifecycle 
        void Awake() { /* intentionally empty — panel is hidden in Start after UI children initialise */ }

        void Start()
        {
            if (chatPanel != null) chatPanel.SetActive(false);

            if (sendButton  != null) sendButton.onClick.AddListener(OnSendClicked);
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
            if (playerInputField != null)
                playerInputField.onEndEdit.AddListener(OnInputEndEdit);
        }

        void Update()
        {
            if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
                OnCloseClicked();
        }

        //  Public API 

        public void Open(string npcName)
        {
            _currentNPCName    = npcName;
            _streamingResponse = string.Empty;

            if (npcNameText != null) npcNameText.text = npcName;
            if (chatPanel   != null) chatPanel.SetActive(true);
            _isOpen = true;

            if (playerInputField != null)
            {
                playerInputField.text = string.Empty;
                playerInputField.Select();
                playerInputField.ActivateInputField();
            }

            SetPlayerControllerEnabled(false);
            Cursor.visible   = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void Close()
        {
            if (chatPanel != null) chatPanel.SetActive(false);
            _isOpen            = false;
            _streamingResponse = string.Empty;

            SetPlayerControllerEnabled(true);
            Cursor.visible   = false;
            Cursor.lockState = CursorLockMode.Locked;
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

        //  Internal 

        private void OnSendClicked()
        {
            if (playerInputField == null) return;
            string msg = playerInputField.text.Trim();
            if (!string.IsNullOrEmpty(msg))
                OnSendMessage?.Invoke(msg);
        }

        private void OnInputEndEdit(string value)
        {
            if (!playerInputField.wasCanceled)
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

        /// <summary>
        /// Enables or disables player controller components without hard assembly references.
        /// Compatible with StarterAssets, custom controllers, and any project.
        /// </summary>
        private void SetPlayerControllerEnabled(bool state)
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;

            foreach (var mb in player.GetComponentsInChildren<MonoBehaviour>())
            {
                if (mb == null) continue;
                var typeName = mb.GetType().Name;
                if (typeName is "StarterAssetsInputs"
                             or "ThirdPersonController"
                             or "FirstPersonController"
                             or "PlayerInput")
                {
                    mb.enabled = state;
                }
            }
        }
    }
}
