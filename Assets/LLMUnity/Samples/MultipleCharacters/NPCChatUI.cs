using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LLMUnity;
using System.Collections.Generic;
using StarterAssets;
using System.Threading.Tasks;
using Whisper;
using Whisper.Utils;
using SparkTTS;
using SparkTTS.Models;
using SparkTTS.Utils;
using SparkLogLevel = SparkTTS.Utils.LogLevel;

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
        public TextMeshProUGUI loadingText;

        [Header("Voice Input - Ses Girişi")]
        public Button toggleMicButton;
        public Image micStatusIndicator;
        public Dropdown microphoneDropdown;

        [Header("Speech Recognition - Konuşma Tanıma")]
        public WhisperManager whisperManager;
        public MicrophoneRecord microphoneRecord;

        [Header("Voice Output - SparkTTS")]
        public bool enableVoiceOutput = true;
        public AudioSource npcVoiceAudioSource;
        public TextMeshProUGUI ttsStatusText;
        public string voiceGender = "male";
        public string voicePitch = "moderate";
        public string voiceSpeed = "moderate";
        public string voiceWarmupText = "Hello, I am ready to speak.";
        public bool preloadVoiceOnStart = true;
        public SparkLogLevel sparkLogLevel = SparkLogLevel.WARNING;
        public MemoryUsage sparkMemoryUsage = MemoryUsage.Balanced;
        public ExecutionProvider sparkExecutionProvider = ExecutionProvider.CPU;

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
        private bool isTranscribing = false;
        private string currentTranscription = "";
        private CharacterVoiceFactory voiceFactory;
        private CharacterVoice currentVoice;
        private bool isVoiceLoading = false;
        private bool isVoiceReady = false;
        private bool isSpeaking = false;
        private bool micPausedForTts = false;
        private string pendingSpeech = "";

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

            SetTtsStatus("Idle");

            if (enableVoiceOutput && preloadVoiceOnStart)
            {
                _ = LoadCharacterVoiceAsync();
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

        private async Task LoadCharacterVoiceAsync()
        {
            if (!enableVoiceOutput || isVoiceLoading)
                return;

            isVoiceLoading = true;
            SetTtsStatus("Preparing voice...");

            try
            {
                CharacterVoiceFactory.Initialize(sparkLogLevel, sparkMemoryUsage, sparkExecutionProvider);
                voiceFactory = CharacterVoiceFactory.Instance;

                if (sparkMemoryUsage == MemoryUsage.Performance)
                {
                    await CharacterVoiceFactory.WaitForModelsLoadedAsync();
                }

                currentVoice = await voiceFactory.CreateFromStyleAsync(
                    voiceGender,
                    voicePitch,
                    voiceSpeed,
                    voiceWarmupText);

                isVoiceReady = currentVoice != null;
                SetTtsStatus(isVoiceReady ? "Voice ready" : "Voice not ready");

                if (isVoiceReady && !string.IsNullOrWhiteSpace(pendingSpeech))
                {
                    var textToSpeak = pendingSpeech;
                    pendingSpeech = "";
                    _ = SpeakResponseAsync(textToSpeak);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"SparkTTS voice load error: {ex.Message}");
                SetTtsStatus("Voice load failed");
            }
            finally
            {
                isVoiceLoading = false;
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
            
            // Oyunu duraklat (karakter kontrol kapalı)
            Time.timeScale = 0f;
            
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

            if (npcVoiceAudioSource != null)
            {
                npcVoiceAudioSource.Stop();
            }

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
                
                currentNPCResponse = ""; // Sıfırla
                RefreshChatDisplay();

                if (enableVoiceOutput)
                {
                    TrySpeakResponse(finalResponse);
                }
                else if (enableVoiceInput && microphoneRecord != null && !microphoneRecord.IsRecording)
                {
                    StartMicrophoneRecording();
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
            if (isMicRecording)
            {
                StopMicrophoneRecording();
            }
            else
            {
                StartMicrophoneRecording();
            }
        }

        private void StartMicrophoneRecording()
        {
            if (!enableVoiceInput || microphoneRecord == null || isWaitingForResponse || isSpeaking)
                return;

            if (microphoneRecord.IsRecording)
                return;

            Debug.Log("Starting microphone recording...");
            microphoneRecord.StartRecord();
            isMicRecording = true;
            UpdateMicStatusUI(true);
        }

        private void StopMicrophoneRecording()
        {
            if (microphoneRecord == null || !microphoneRecord.IsRecording)
                return;

            Debug.Log("Stopping microphone recording...");
            microphoneRecord.StopRecord();
            isMicRecording = false;
            UpdateMicStatusUI(false);
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
            UpdateMicStatusUI(isSpeechDetected);
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
                if (enableVoiceInput && autoStartRecording)
                {
                    StartMicrophoneRecording();
                }
                return;
            }

            Debug.Log($"Microphone stopped. Audio length: {recordedAudio.Length}s");

            await TranscribeAudio(recordedAudio);

            // Mikrofonu yeniden başlat
            if (!isWaitingForResponse && enableVoiceInput && autoStartRecording)
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

            _ = currentAgent.Chat(userMessage, 
                (reply) => UpdateNPCResponse(reply),
                OnResponseComplete);
        }

        private void TrySpeakResponse(string responseText)
        {
            if (!enableVoiceOutput || string.IsNullOrWhiteSpace(responseText))
                return;

            if (!isVoiceReady || currentVoice == null)
            {
                pendingSpeech = responseText;
                if (!isVoiceLoading)
                {
                    _ = LoadCharacterVoiceAsync();
                }
                return;
            }

            _ = SpeakResponseAsync(responseText);
        }

        private async Task SpeakResponseAsync(string responseText)
        {
            if (npcVoiceAudioSource == null)
            {
                Debug.LogWarning("NPC voice AudioSource is not assigned.");
                SetTtsStatus("No audio source");
                return;
            }

            if (isSpeaking)
                return;

            isSpeaking = true;
            micPausedForTts = true;
            StopMicrophoneRecording();
            SetTtsStatus("Generating voice...");

            try
            {
                AudioClip generatedClip = await currentVoice.GenerateSpeechAsync(responseText);
                if (generatedClip == null)
                {
                    Debug.LogWarning("SparkTTS returned an empty audio clip.");
                    SetTtsStatus("Voice generation failed");
                    return;
                }

                npcVoiceAudioSource.Stop();
                npcVoiceAudioSource.clip = generatedClip;
                npcVoiceAudioSource.Play();
                SetTtsStatus("Speaking...");
                StartCoroutine(WaitForVoicePlayback());
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"SparkTTS speech generation error: {ex.Message}");
                SetTtsStatus("Voice error");
            }
            finally
            {
                if (npcVoiceAudioSource == null || !npcVoiceAudioSource.isPlaying)
                {
                    isSpeaking = false;
                    micPausedForTts = false;
                    SetTtsStatus("Idle");
                    if (enableVoiceInput && autoStartRecording && microphoneRecord != null && !isWaitingForResponse)
                    {
                        StartMicrophoneRecording();
                    }
                }
            }
        }

        private System.Collections.IEnumerator WaitForVoicePlayback()
        {
            if (npcVoiceAudioSource == null)
            {
                isSpeaking = false;
                yield break;
            }

            while (npcVoiceAudioSource.isPlaying)
            {
                yield return null;
            }

            isSpeaking = false;
            micPausedForTts = false;
            SetTtsStatus("Idle");

            if (enableVoiceInput && autoStartRecording && microphoneRecord != null && !isWaitingForResponse)
            {
                StartMicrophoneRecording();
            }
        }

        private void UpdateMicStatusUI(bool isActive)
        {
            if (micStatusIndicator != null)
            {
                micStatusIndicator.color = isActive ? Color.green : Color.red;
            }

            if (toggleMicButton != null)
            {
                var buttonText = toggleMicButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = isActive ? "Stop Mic" : "Start Mic";
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

        private void DisablePlayerController()
        {
            var inputScript = FindFirstObjectByType<StarterAssetsInputs>();
            if (inputScript != null)
            {
                inputScript.enabled = false;
            }
            
            var controllerScript = FindFirstObjectByType<ThirdPersonController>();
            if (controllerScript != null)
            {
                controllerScript.enabled = false;
            }
        }

        private void EnablePlayerController()
        {
            var inputScript = FindFirstObjectByType<StarterAssetsInputs>();
            if (inputScript != null)
            {
                inputScript.enabled = true;
            }
            
            var controllerScript = FindFirstObjectByType<ThirdPersonController>();
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

            currentVoice?.Dispose();
        }
    }
}
