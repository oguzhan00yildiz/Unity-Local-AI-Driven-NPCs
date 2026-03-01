// ============================================================
// DEPRECATED — Bu script artık kullanılmıyor.
// Yeni modüler sistem: Assets/Scripts/AISystem/
//   • AISystemManager  → merkez koordinatör (singleton)
//   • ChatUIController → saf UI bileşeni
//   • VoiceInputService → Whisper STT
//   • VoiceOutputService → PiperTTS
//   • ModelBootstrapper → model ısıtma
//   • NPCAgent         → NPC başına bileşen
//
// Sahnede yalnızca [AI System] prefabını ve [NPC] prefablarını
// kullanın; Inspector'da çapraz bağlantı gerekmez.
// ============================================================

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.UI;
using LLMUnity;
using System.Collections.Generic;
using System.Threading.Tasks;
using Whisper;
using Whisper.Utils;
using PiperTTS;

namespace LLMUnitySamples
{
    public class NPCChatUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public GameObject chatPanel;
        public Text npcNameText;
        public Text chatDisplayText;
        public InputField playerInputField;
        public Button sendButton;
        public Button closeButton;
        public ScrollRect chatScrollRect;
        public Text loadingText;

        [Header("Voice Input - Ses Girişi")]
        public Button toggleMicButton;
        public Image micStatusIndicator;
        public Dropdown microphoneDropdown;
        public Color micOnButtonColor = new Color(0.2f, 0.7f, 0.2f);
        public Color micOffButtonColor = new Color(0.75f, 0.2f, 0.2f);

        [Header("Speech Recognition - Konuşma Tanıma")]
        public WhisperManager whisperManager;
        public MicrophoneRecord microphoneRecord;

        [Header("Voice Output - PiperTTS")]
        public bool enableVoiceOutput = true;
        public PiperTTS.PiperTTS piperTts;
        public Text ttsStatusText;
        public bool initPiperOnStart = true;

        [Header("Settings")]
        public int maxDisplayMessages = 10;
        public float autoScrollDelay = 0.1f;
        public bool enableVoiceInput = true;
        public bool autoStartRecording = true;
        public bool showTranscriptionInRealtime = true;
        public string microphoneDefaultLabel = "Default Microphone";
        public float micSilenceThreshold = 0.0015f;

        private LLMAgent currentAgent;
        private string currentNPCName;
        private bool isWaitingForResponse = false;
        private bool isModelWarming = false;
        private bool allModelsWarmed = false;
        private List<string> chatHistory = new List<string>();
        private string currentNPCResponse = "";
        private bool isMicRecording = false;
        private bool isMicMuted = false;
        private bool isTranscribing = false;
        private string currentTranscription = "";
        private bool isSpeaking = false;
        private bool micPausedForTts = false;
        private string pendingSpeech = "";
        private int lastSpokenLength = 0;
        private Queue<string> speechQueue = new Queue<string>();

        void Start()
        {
            // UI Event Listener'ları bağla
            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendMessage);
            
            if (closeButton != null)
                closeButton.onClick.AddListener(CloseChat);

            if (toggleMicButton != null)
                toggleMicButton.onClick.AddListener(OnToggleMicrophone);

            if (playerInputField != null)
                playerInputField.onSubmit.AddListener((message) => OnSendMessage());

            // Chat panelini başlangıçta kapat
            if (chatPanel != null)
                chatPanel.SetActive(false);
            
            // Loading text'i başlangıçta göster
            if (loadingText != null)
            {
                loadingText.gameObject.SetActive(true);
                loadingText.text = "Loading AI models...";
            }
            
            InitializeMicrophone();

            UpdateMicButtonUI();

            SetTtsStatus("Idle");

            if (enableVoiceOutput && initPiperOnStart)
            {
                InitializePiperTts();
            }
            
            // Sahnedeki tüm modelleri arka planda yükle
            _ = WarmupAllModels();
        }

        private void InitializeMicrophone()
        {
            if (!enableVoiceInput || microphoneRecord == null)
                return;

            // Setup microphone dropdown
            if (microphoneDropdown != null)
            {
                var micDevices = new List<string> { microphoneDefaultLabel };
                micDevices.AddRange(Microphone.devices);
                
                microphoneDropdown.ClearOptions();
                microphoneDropdown.AddOptions(micDevices);
                microphoneDropdown.value = 0;
                microphoneDropdown.onValueChanged.AddListener(OnMicrophoneChanged);
            }

            // Setup microphone callbacks
            microphoneRecord.OnRecordStop += OnMicrophoneRecordStop;
            microphoneRecord.OnVadChanged += OnVadChanged;
        }
        
        private async Task WarmupAllModels()
        {
            if (isModelWarming) return;
            
            isModelWarming = true;
            
            try
            {
                // Sahnedeki tüm LLMAgent'ları bul
                LLMAgent[] allAgents = FindObjectsByType<LLMAgent>(FindObjectsSortMode.None);
                
                if (allAgents.Length > 0)
                {
                    Debug.Log($"Warming up {allAgents.Length} AI model(s)...");
                    
                    // Tüm modelleri parallel olarak warmup et
                    var warmupTasks = new List<Task>();
                    foreach (var agent in allAgents)
                    {
                        warmupTasks.Add(agent.Warmup());
                    }
                    
                    await Task.WhenAll(warmupTasks);
                    
                    Debug.Log("All AI models are ready!");
                }

                // Whisper modelini de yükle
                if (enableVoiceInput && whisperManager != null && !whisperManager.IsLoaded)
                {
                    Debug.Log("Loading Whisper model...");
                    await whisperManager.InitModel();
                    Debug.Log("Whisper model is ready!");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Model warmup error: {ex.Message}");
            }
            finally
            {
                isModelWarming = false;
                allModelsWarmed = true;
                
                // Loading göstergesini gizle
                if (loadingText != null)
                    loadingText.gameObject.SetActive(false);
            }
        }

        private void InitializePiperTts()
        {
            if (!enableVoiceOutput)
                return;

            if (piperTts == null)
            {
                SetTtsStatus("No PiperTTS");
                return;
            }

            if (piperTts.status == ModelStatus.Init)
            {
                SetTtsStatus("Preparing voice...");
                piperTts.InitModel();
            }
            else if (piperTts.status == ModelStatus.Ready)
            {
                SetTtsStatus("Voice ready");
            }
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
            
            // Third Person Controller'ı deaktif et
            DisablePlayerController();

            // Mikrofon kaydını başlat
            if (enableVoiceInput && autoStartRecording && microphoneRecord != null)
            {
                StartMicrophoneRecording();
            }
        }

        public void CloseChat()
        {
            StopMicrophoneRecording();

            if (piperTts != null)
            {
                var piperAudio = piperTts.GetComponent<AudioSource>();
                if (piperAudio != null)
                {
                    piperAudio.Stop();
                }
            }

            if (chatPanel != null)
                chatPanel.SetActive(false);

            currentAgent = null;
            isWaitingForResponse = false;
            
            // Cursor'u gizle ve Lock et
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            
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

            SendChatMessage(userMessage);
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
                string finalResponse = currentNPCResponse;
                chatHistory.Add($"{currentNPCName}: {finalResponse}");
                
                // Son N mesajı göster
                if (chatHistory.Count > maxDisplayMessages)
                {
                    chatHistory.RemoveAt(0);
                }
                
                // Speak the complete response
                if (enableVoiceOutput)
                {
                    TrySpeakResponse(finalResponse);
                }
                
                currentNPCResponse = ""; // Sıfırla
                lastSpokenLength = 0; // Reset for next response
                RefreshChatDisplay();

                if (!enableVoiceOutput)
                {
                    if (enableVoiceInput && microphoneRecord != null && !microphoneRecord.IsRecording)
                    {
                        StartMicrophoneRecording();
                    }
                }
            }
            else if (enableVoiceInput && microphoneRecord != null && !microphoneRecord.IsRecording)
            {
                StartMicrophoneRecording();
            }
            
            if (playerInputField != null)
            {
                playerInputField.Select();
                playerInputField.ActivateInputField();
            }
        }

        private System.Collections.IEnumerator ScrollToBottom()
        {
            yield return new WaitForSecondsRealtime(autoScrollDelay);
            if (chatScrollRect != null)
                chatScrollRect.verticalNormalizedPosition = 1f;
        }

        private void OnToggleMicrophone()
        {
            isMicMuted = !isMicMuted;

            if (isMicMuted)
            {
                StopMicrophoneRecording();
            }
            else
            {
                StartMicrophoneRecording();
            }

            UpdateMicButtonUI();
        }

        private void StartMicrophoneRecording()
        {
            if (!enableVoiceInput || microphoneRecord == null || isWaitingForResponse || isSpeaking || isMicMuted)
                return;

            if (microphoneRecord.IsRecording)
                return;

            Debug.Log("Starting microphone recording...");
            microphoneRecord.StartRecord();
            isMicRecording = true;
            UpdateMicStatusIndicator(true);
            UpdateMicButtonUI();
        }

        private void StopMicrophoneRecording()
        {
            if (microphoneRecord == null || !microphoneRecord.IsRecording)
                return;

            Debug.Log("Stopping microphone recording...");
            microphoneRecord.StopRecord();
            isMicRecording = false;
            UpdateMicStatusIndicator(false);
            UpdateMicButtonUI();
        }

        private void OnMicrophoneChanged(int index)
        {
            if (microphoneDropdown == null || microphoneRecord == null)
                return;

            string selectedDevice = microphoneDropdown.options[index].text;
            
            if (selectedDevice == microphoneDefaultLabel)
            {
                microphoneRecord.SelectedMicDevice = null;
                Debug.Log("Switched to default microphone");
            }
            else
            {
                microphoneRecord.SelectedMicDevice = selectedDevice;
                Debug.Log($"Switched to microphone: {selectedDevice}");
            }
        }

        private void OnVadChanged(bool isSpeechDetected)
        {
            Debug.Log($"Speech detected: {isSpeechDetected}");
            UpdateMicStatusIndicator(isSpeechDetected);
        }

        private async void OnMicrophoneRecordStop(AudioChunk recordedAudio)
        {
            if (micPausedForTts)
            {
                micPausedForTts = false;
                return;
            }

            if (isWaitingForResponse || !enableVoiceInput)
                return;

            if (recordedAudio.Data == null || recordedAudio.Data.Length == 0 || IsSilentAudio(recordedAudio.Data))
            {
                Debug.Log("Microphone input is silent. Skipping transcription.");
                if (enableVoiceInput && autoStartRecording && !isMicMuted)
                {
                    StartMicrophoneRecording();
                }
                return;
            }

            Debug.Log($"Microphone stopped. Audio length: {recordedAudio.Length}s");

            await TranscribeAudio(recordedAudio);

            // Mikrofonu yeniden başlat
            if (!isWaitingForResponse && enableVoiceInput && autoStartRecording && !isMicMuted)
            {
                StartMicrophoneRecording();
            }
        }

        private async Task TranscribeAudio(AudioChunk audioChunk)
        {
            if (whisperManager == null || !whisperManager.IsLoaded)
            {
                Debug.LogError("Whisper is not initialized!");
                return;
            }

            try
            {
                isTranscribing = true;
                currentTranscription = "";

                Debug.Log("Starting transcription...");

                var result = await whisperManager.GetTextAsync(
                    audioChunk.Data,
                    audioChunk.Frequency,
                    audioChunk.Channels
                );

                if (result != null && !string.IsNullOrEmpty(result.Result))
                {
                    currentTranscription = result.Result.Trim();
                    
                    // Check if transcription is not empty or just whitespace
                    if (!string.IsNullOrWhiteSpace(currentTranscription) && currentTranscription.Length > 1)
                    {
                        Debug.Log($"Transcription: {currentTranscription}");

                        if (showTranscriptionInRealtime && playerInputField != null)
                        {
                            playerInputField.text = currentTranscription;
                        }

                        if (!isWaitingForResponse)
                        {
                            if (string.Equals(currentTranscription, "[blank audio]", System.StringComparison.OrdinalIgnoreCase))
                            {
                                Debug.Log("Transcription is [blank audio]. Skipping message send.");
                            }
                            else
                            {
                                SendChatMessage(currentTranscription);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Transcription is empty or too short, skipping message send.");
                    }
                }
                else
                {
                    Debug.LogWarning("No text transcribed from audio");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Transcription error: {ex.Message}");
            }
            finally
            {
                isTranscribing = false;
            }
        }

        private bool IsSilentAudio(float[] samples)
        {
            if (samples == null || samples.Length == 0)
                return true;

            float sumAbs = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                sumAbs += Mathf.Abs(samples[i]);
            }

            float meanAbs = sumAbs / samples.Length;
            return meanAbs < micSilenceThreshold;
        }

        private void SendChatMessage(string userMessage)
        {
            if (currentAgent == null || isWaitingForResponse)
                return;

            if (string.IsNullOrEmpty(userMessage))
                return;

            AddToChatDisplay($"Siz: {userMessage}");
            playerInputField.text = "";
            isWaitingForResponse = true;
            sendButton.interactable = false;
            currentNPCResponse = "";
            lastSpokenLength = 0;

            _ = currentAgent.Chat(userMessage, 
                (reply) => UpdateNPCResponse(reply),
                OnResponseComplete);
        }

        private void TrySpeakResponse(string responseText)
        {
            if (!enableVoiceOutput || string.IsNullOrWhiteSpace(responseText))
                return;

            if (piperTts == null)
            {
                SetTtsStatus("No PiperTTS");
                return;
            }

            // Allow prompting when Ready or Generate (PiperTTS has a queue)
            if (piperTts.status == ModelStatus.Init || piperTts.status == ModelStatus.Loading)
            {
                pendingSpeech = responseText;
                if (piperTts.status == ModelStatus.Init)
                {
                    InitializePiperTts();
                }
                return;
            }

            if (piperTts.status == ModelStatus.Error)
            {
                SetTtsStatus("Voice error");
                return;
            }

            // Split response into sentences to handle commas and other punctuation properly
            var sentences = SplitIntoSentences(responseText);
            
            // Clear and queue all sentences
            speechQueue.Clear();
            foreach (var sentence in sentences)
            {
                if (!string.IsNullOrWhiteSpace(sentence))
                {
                    speechQueue.Enqueue(sentence.Trim());
                }
            }

            // Speak the first sentence
            if (speechQueue.Count > 0)
            {
                string firstSentence = speechQueue.Dequeue();
                piperTts.Prompt(firstSentence);
            }
        }

        private List<string> SplitIntoSentences(string text)
        {
            var sentences = new List<string>();
            var currentSentence = "";
            
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                currentSentence += c;
                
                // Check for sentence-ending punctuation
                if ((c == '.' || c == '!' || c == '?') && i + 1 < text.Length && text[i + 1] == ' ')
                {
                    // This is a sentence boundary
                    sentences.Add(currentSentence);
                    currentSentence = "";
                    i++; // Skip the space after punctuation
                }
                else if ((c == ',' || c == ';' || c == ':') && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
                {
                    // For commas, semicolons, and colons followed by space, continue in same sentence
                    // This ensures commas don't break up the speech
                    i++; // Skip the space
                    currentSentence += ' ';
                }
            }
            
            // Add any remaining text
            if (!string.IsNullOrWhiteSpace(currentSentence))
            {
                sentences.Add(currentSentence);
            }
            
            return sentences;
        }

        private void UpdateMicStatusIndicator(bool isActive)
        {
            if (micStatusIndicator != null)
            {
                micStatusIndicator.color = isActive ? Color.green : Color.red;
            }
        }

        private void UpdateMicButtonUI()
        {
            if (toggleMicButton != null)
            {
                var buttonText = toggleMicButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = isMicMuted ? "Mic Off" : "Mic On";
                }

                var buttonImage = toggleMicButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = isMicMuted ? micOffButtonColor : micOnButtonColor;
                }
            }
        }

        private void SetTtsStatus(string status)
        {
            if (ttsStatusText == null)
                return;

            if (!enableVoiceOutput)
            {
                ttsStatusText.text = "Voice off";
                return;
            }

            ttsStatusText.text = status;
        }

        // Disables/enables player controller components without requiring StarterAssets assembly.
        // Works with StarterAssets, custom controllers, or any project.
        private void DisablePlayerController() => SetPlayerControllerEnabled(false);
        private void EnablePlayerController() => SetPlayerControllerEnabled(true);

        private void SetPlayerControllerEnabled(bool enabled)
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;

            foreach (var mb in player.GetComponentsInChildren<MonoBehaviour>())
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "StarterAssetsInputs" ||
                    typeName == "ThirdPersonController" ||
                    typeName == "FirstPersonController" ||
                    typeName == "PlayerInput")
                {
                    mb.enabled = enabled;
                }
            }
        }

        void Update()
        {
            // Close chat panel with Escape key
#if ENABLE_INPUT_SYSTEM
            bool escPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            bool escPressed = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (escPressed && chatPanel.activeSelf)
            {
                CloseChat();
            }
        }

        public bool IsChatOpen()
        {
            return chatPanel.activeSelf;
        }

        private void OnDestroy()
        {
            if (microphoneRecord != null)
            {
                microphoneRecord.OnRecordStop -= OnMicrophoneRecordStop;
                microphoneRecord.OnVadChanged -= OnVadChanged;
            }
        }

        private void OnEnable()
        {
            if (piperTts != null)
            {
                piperTts.OnStatusChanged += OnPiperStatusChanged;
                OnPiperStatusChanged(piperTts.status);
            }
        }

        private void OnDisable()
        {
            if (piperTts != null)
            {
                piperTts.OnStatusChanged -= OnPiperStatusChanged;
            }
        }

        private void OnPiperStatusChanged(ModelStatus status)
        {
            if (!enableVoiceOutput)
                return;

            switch (status)
            {
                case ModelStatus.Loading:
                    SetTtsStatus("Preparing voice...");
                    break;
                case ModelStatus.Ready:
                    if (isSpeaking)
                    {
                        isSpeaking = false;
                        micPausedForTts = false;
                        SetTtsStatus("Idle");
                        
                        // Check if there are more sentences in the queue
                        if (speechQueue.Count > 0)
                        {
                            string nextSentence = speechQueue.Dequeue();
                            piperTts.Prompt(nextSentence);
                            isSpeaking = true;
                            micPausedForTts = true;
                        }
                        else if (enableVoiceInput && autoStartRecording && microphoneRecord != null && !isWaitingForResponse && !isMicMuted)
                        {
                            StartMicrophoneRecording();
                        }
                    }
                    else
                    {
                        SetTtsStatus("Voice ready");
                    }

                    if (!string.IsNullOrWhiteSpace(pendingSpeech))
                    {
                        var textToSpeak = pendingSpeech;
                        pendingSpeech = "";
                        TrySpeakResponse(textToSpeak);
                    }
                    break;
                case ModelStatus.Generate:
                    isSpeaking = true;
                    micPausedForTts = true;
                    StopMicrophoneRecording();
                    SetTtsStatus("Speaking...");
                    break;
                case ModelStatus.Error:
                    isSpeaking = false;
                    micPausedForTts = false;
                    speechQueue.Clear();
                    SetTtsStatus("Voice error");
                    break;
            }
        }
    }
}
