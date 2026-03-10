using UnityEngine;
using UnityEngine.UI;
using System;

namespace AISystem
{
    /// <summary>
    /// Minimal status display — shows a single UGUI Text label.
    /// No chat panel, no input field, no TMPro dependency.
    /// Shows NPC interaction state: Listening / Thinking / Talking.
    /// </summary>
    public class ChatUIController : MonoBehaviour
    {
        [Header("Status Text (UGUI Text — no TMPro)")]
        [Tooltip("Assign a UnityEngine.UI.Text component in the scene. Leave null to disable.")]
        public Text statusText;

        //  Events (kept for API compatibility — not used in voice-only mode) 
        public event Action<string> OnSendMessage;
        public event Action OnCloseChat;

        //  State 
        private bool   _isOpen;
        private string _npcName = string.Empty;
        public bool IsOpen => _isOpen;

        //  Lifecycle 
        void Start()
        {
            SetStatusRaw(string.Empty);
        }

        //  Public API — voice-only, no chat panel 

        public void Open(string npcName)
        {
            _npcName = npcName;
            _isOpen  = true;
            SetStatus("Listening");
        }

        public void Close()
        {
            _isOpen  = false;
            _npcName = string.Empty;
            SetStatusRaw(string.Empty);
        }

        /// <summary>Shows interaction state: "Listening", "Thinking", or "Talking".</summary>
        public void SetStatus(string state)
        {
            SetStatusRaw(string.IsNullOrEmpty(_npcName)
                ? state
                : $"{_npcName}  {state}");
        }

        // Stub — AI responds via voice, no text chat display needed.
        public void AddMessage(string sender, string text) { }
        public void UpdateStreamingResponse(string sender, string partial) { }
        public void FinalizeResponse(string sender, string finalText) { }
        public void SetInputText(string text) { }
        public void SetWaiting(bool waiting) { }

        /// <summary>Shows/hides the status text (e.g. "Loading AI models...").</summary>
        public void SetLoadingOverlay(bool visible, string message = "")
        {
            SetStatusRaw(visible ? message : string.Empty);
        }

        //  Internal 

        private void SetStatusRaw(string message)
        {
            if (statusText == null) return;
            statusText.text = message;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
    }
}
