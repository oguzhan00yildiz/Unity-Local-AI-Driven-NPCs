using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LLMUnity;
using System.Collections.Generic;
using StarterAssets;

namespace LLMUnitySamples
{
    public class NPCChatUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public GameObject chatPanel;
        public TextMeshProUGUI npcNameText;
        public TextMeshProUGUI chatDisplayText;
        public TMP_InputField playerInputField;
        public Button sendButton;
        public Button closeButton;
        public ScrollRect chatScrollRect;

        [Header("Settings")]
        public int maxDisplayMessages = 10;
        public float autoScrollDelay = 0.1f;

        private LLMAgent currentAgent;
        private string currentNPCName;
        private bool isWaitingForResponse = false;
        private List<string> chatHistory = new List<string>();
        private string currentNPCResponse = ""; // Son NPC yanıtını izlemek için

        void Start()
        {
            // UI Event Listener'ları bağla
            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendMessage);
            
            if (closeButton != null)
                closeButton.onClick.AddListener(CloseChat);

            if (playerInputField != null)
                playerInputField.onSubmit.AddListener((message) => OnSendMessage());

            // Chat panelini başlangıçta kapat
            if (chatPanel != null)
                chatPanel.SetActive(false);
        }

        public void OpenChat(LLMAgent agent, string npcName)
        {
            currentAgent = agent;
            currentNPCName = npcName;
            
            // Yeni NPC'ye geçilmişse geçmişi temizle, aynı NPC ise sakla
            if (!npcNameText.text.Equals(npcName))
            {
                chatHistory.Clear();
            }
            
            if (npcNameText != null)
                npcNameText.text = npcName;

            // Paneli aç - önceki mesajlar kalacak
            if (chatPanel != null)
                chatPanel.SetActive(true);

            if (playerInputField != null)
            {
                playerInputField.text = "";
                playerInputField.Select();
                playerInputField.ActivateInputField();
            }

            // Cursor'u göster ve Unlock et
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            
            // Oyunu duraklat (karakter kontrol kapalı)
            Time.timeScale = 0f;
            
            // Third Person Controller'ı deaktif et
            DisablePlayerController();
        }

        public void CloseChat()
        {
            if (chatPanel != null)
                chatPanel.SetActive(false);

            currentAgent = null;
            isWaitingForResponse = false;
            
            // Cursor'u gizle ve Lock et
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            
            // Oyunu devam ettir
            Time.timeScale = 1f;
            
            // Third Person Controller'ı aktif et
            EnablePlayerController();
        }

        private void OnSendMessage()
        {
            if (currentAgent == null || isWaitingForResponse)
                return;

            string userMessage = playerInputField.text.Trim();
            if (string.IsNullOrEmpty(userMessage))
                return;

            // Oyuncunun mesajını görüntüle
            AddToChatDisplay($"Siz: {userMessage}");

            // Input'u temizle
            playerInputField.text = "";

            // NPC'den yanıt al
            isWaitingForResponse = true;
            sendButton.interactable = false;
            currentNPCResponse = ""; // Yeni yanıtı başlat

            _ = currentAgent.Chat(userMessage, 
                (reply) => UpdateNPCResponse(reply),
                OnResponseComplete);
        }

        private void UpdateNPCResponse(string fullResponse)
        {
            currentNPCResponse = fullResponse;
            RefreshChatDisplay();
        }

        private void RefreshChatDisplay()
        {
            if (chatDisplayText != null)
            {
                chatDisplayText.text = "";
                foreach (string msg in chatHistory)
                {
                    chatDisplayText.text += msg + "\n";
                }
                
                // Son NPC yanıtını ekle (streaming olarak güncelleniyor)
                if (!string.IsNullOrEmpty(currentNPCResponse))
                {
                    chatDisplayText.text += $"{currentNPCName}: {currentNPCResponse}\n";
                }
            }

            // Scroll aşağıya git
            if (chatScrollRect != null)
            {
                StartCoroutine(ScrollToBottom());
            }
        }

        private void AddToChatDisplay(string message)
        {
            chatHistory.Add(message);

            // Son N mesajı göster
            if (chatHistory.Count > maxDisplayMessages)
            {
                chatHistory.RemoveAt(0);
            }

            RefreshChatDisplay();
        }

        private void OnResponseComplete()
        {
            isWaitingForResponse = false;
            sendButton.interactable = true;
            
            // Son yanıtı history'ye ekle
            if (!string.IsNullOrEmpty(currentNPCResponse))
            {
                chatHistory.Add($"{currentNPCName}: {currentNPCResponse}");
                
                // Son N mesajı göster
                if (chatHistory.Count > maxDisplayMessages)
                {
                    chatHistory.RemoveAt(0);
                }
                
                currentNPCResponse = ""; // Sıfırla
                RefreshChatDisplay();
            }
            
            if (playerInputField != null)
            {
                playerInputField.Select();
                playerInputField.ActivateInputField();
            }
        }

        private System.Collections.IEnumerator ScrollToBottom()
        {
            yield return new WaitForSeconds(autoScrollDelay);
            if (chatScrollRect != null)
                chatScrollRect.verticalNormalizedPosition = 0f;
        }

        private void DisablePlayerController()
        {
            // StarterAssetsInputs ve ThirdPersonController'ı deaktif et
            var inputScript = FindObjectOfType<StarterAssetsInputs>(true);
            if (inputScript != null)
            {
                inputScript.enabled = false;
            }
            
            var controllerScript = FindObjectOfType<ThirdPersonController>(true);
            if (controllerScript != null)
            {
                controllerScript.enabled = false;
            }
        }

        private void EnablePlayerController()
        {
            // StarterAssetsInputs ve ThirdPersonController'ı aktif et
            var inputScript = FindObjectOfType<StarterAssetsInputs>(true);
            if (inputScript != null)
            {
                inputScript.enabled = true;
            }
            
            var controllerScript = FindObjectOfType<ThirdPersonController>(true);
            if (controllerScript != null)
            {
                controllerScript.enabled = true;
            }
        }

        void Update()
        {
            // ESC tuşuyla da pencereyi kapatabilme
            if (Input.GetKeyDown(KeyCode.Escape) && chatPanel.activeSelf)
            {
                CloseChat();
            }
        }
    }
}
