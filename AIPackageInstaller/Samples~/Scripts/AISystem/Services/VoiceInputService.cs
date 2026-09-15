using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading.Tasks;
using Whisper;
using Whisper.Utils;

namespace AISystem
{
    /// <summary>
    /// Whisper STT wrapper  handles microphone recording and transcription.
    /// Other components do not call this directly; they subscribe to events.
    /// </summary>
    public class VoiceInputService : MonoBehaviour
    {
        //  Inspector 
        [Header("Whisper Components")]
        public WhisperManager whisperManager;
        public MicrophoneRecord microphoneRecord;

        [Header("Microphone Settings")]
        public float silenceThreshold           = 0.0015f;
        public bool  autoRestartAfterTranscribe = true;

        [Header("Optional UI")]
        [Tooltip("Microphone dropdown  optional, can be left empty.")]
        public Dropdown microphoneDropdown;
        public string defaultMicLabel = "Default Microphone";

        [Header("VAD Indicator")]
        [Tooltip("Image that shows mic state: yellow=ready, green=speaking, red=off")]
        public Image vadIndicator;
        public Color colorReady    = new Color(1f,  0.92f, 0f,   1f); // yellow
        public Color colorSpeaking = new Color(0.2f, 0.8f, 0.2f, 1f); // green
        public Color colorOff      = new Color(0.8f, 0.2f, 0.2f, 1f); // red

        //  Events 
        /// <summary>Fired when audio has been successfully transcribed to text.</summary>
        public event Action<string> OnTranscription;

        /// <summary>Fired when microphone mute state changes.</summary>
        public event Action<bool> OnMuteChanged;

        //  State 
        private bool _isListening;
        private bool _isPaused;
        private bool _isTranscribing;
        private bool _isMuted;

        public bool IsListening    => _isListening;
        public bool IsTranscribing => _isTranscribing;
        public bool IsMuted        => _isMuted;

        //  Lifecycle 
        void Awake()
        {
            if (whisperManager == null)
                whisperManager = GetComponent<WhisperManager>();

            if (microphoneRecord == null)
                microphoneRecord = GetComponent<MicrophoneRecord>();
        }

        void Start()
        {
            if (microphoneRecord != null)
            {
                microphoneRecord.OnRecordStop += OnMicRecordStop;
                microphoneRecord.OnVadChanged  += OnVadChanged;
            }

            SetupMicrophoneDropdown();
            SetVadIndicator(colorOff);
        }

        void OnDestroy()
        {
            if (microphoneRecord != null)
            {
                microphoneRecord.OnRecordStop -= OnMicRecordStop;
                microphoneRecord.OnVadChanged  -= OnVadChanged;
            }
        }

        //  Public API 

        /// <summary>Toggles microphone mute state.</summary>
        public void ToggleMute()
        {
            SetMuted(!_isMuted);
        }

        /// <summary>Sets microphone mute state.</summary>
        public void SetMuted(bool muted)
        {
            if (_isMuted == muted) return;
            _isMuted = muted;

            if (_isMuted)
            {
                if (microphoneRecord != null && microphoneRecord.IsRecording)
                    microphoneRecord.StopRecord();
                _isListening = false;
                SetVadIndicator(colorOff);
            }
            else
            {
                if (!_isPaused && !_isTranscribing)
                    StartListening();
            }

            OnMuteChanged?.Invoke(_isMuted);
            Debug.Log($"[VoiceInput] Microphone muted: {_isMuted}");
        }

        public void StartListening()
        {
            if (_isMuted)
            {
                Debug.Log("[VoiceInput] Cannot start listening: microphone is muted.");
                SetVadIndicator(colorOff);
                return;
            }

            if (microphoneRecord == null)
            {
                Debug.LogWarning("[VoiceInput] Cannot start: microphoneRecord is null");
                return;
            }
            if (_isListening) return;
            if (_isPaused)
            {
                Debug.LogWarning("[VoiceInput] Cannot start: paused (_isPaused=true)");
                return;
            }
            if (_isTranscribing)
            {
                Debug.LogWarning("[VoiceInput] Cannot start: transcribing (_isTranscribing=true)");
                return;
            }

            _isListening = true;
            microphoneRecord.StartRecord();
            SetVadIndicator(colorReady);
            Debug.Log("[VoiceInput] Listening started.");
        }

        public void StopListening()
        {
            _isPaused    = false;
            _isListening = false;
            SetVadIndicator(colorOff);
            if (microphoneRecord != null && microphoneRecord.IsRecording)
            {
                microphoneRecord.StopRecord();
                Debug.Log("[VoiceInput] Listening stopped.");
            }
        }

        /// <summary>Temporarily pauses listening while TTS is speaking (prevents silent captures).</summary>
        public void PauseListening()
        {
            if (_isMuted) return;

            if (!_isListening)
            {
                Debug.LogWarning("[VoiceInput] Cannot pause: not listening (_isListening=false)");
                return;
            }
            _isPaused    = true;
            _isListening = false;
            SetVadIndicator(colorOff);
            if (microphoneRecord != null && microphoneRecord.IsRecording)
                microphoneRecord.StopRecord();
            Debug.Log("[VoiceInput] Listening paused (TTS active).");
        }

        /// <summary>Resumes listening after a pause.</summary>
        public void ResumeListening()
        {
            if (!_isPaused)
            {
                if (_isMuted) return;
                Debug.LogWarning("[VoiceInput] Cannot resume: not paused (_isPaused=false)");
                return;
            }
            Debug.Log("[VoiceInput] Resuming from pause...");
            _isPaused = false;
            if (_isMuted)
            {
                SetVadIndicator(colorOff);
                return;
            }
            StartListening();
        }

        //  Internal 

        private void SetupMicrophoneDropdown()
        {
            if (microphoneDropdown == null || microphoneRecord == null) return;

            var devices = new System.Collections.Generic.List<string> { defaultMicLabel };
            devices.AddRange(Microphone.devices);
            microphoneDropdown.ClearOptions();
            microphoneDropdown.AddOptions(devices);
            microphoneDropdown.value = 0;
            microphoneDropdown.onValueChanged.AddListener(OnMicrophoneDropdownChanged);
        }

        private void OnMicrophoneDropdownChanged(int index)
        {
            if (microphoneDropdown == null || microphoneRecord == null) return;
            string selected = microphoneDropdown.options[index].text;
            microphoneRecord.SelectedMicDevice = selected == defaultMicLabel ? null : selected;
            Debug.Log($"[VoiceInput] Microphone changed: {selected}");
        }

        private async void OnMicRecordStop(AudioChunk chunk)
        {
            // Skip recordings that arrived from a TTS pause or when muted
            if (_isPaused || _isMuted) return;

            _isListening = false;

            if (chunk.Data == null || chunk.Data.Length == 0)
            {
                if (autoRestartAfterTranscribe && !_isPaused && !_isMuted)
                    StartListening();
                return;
            }

            if (IsSilent(chunk.Data, out float avgEnergy))
            {
                Debug.Log($"[VoiceInput] Silent audio (avg energy: {avgEnergy:F5} < threshold: {silenceThreshold:F5}), transcription skipped.");
                if (autoRestartAfterTranscribe && !_isPaused && !_isMuted)
                    StartListening();
                return;
            }

            await Transcribe(chunk);

            if (autoRestartAfterTranscribe && !_isPaused && !_isMuted)
                StartListening();
        }

        private async Task Transcribe(AudioChunk chunk)
        {
            if (whisperManager == null || !whisperManager.IsLoaded)
            {
                Debug.LogError("[VoiceInput] Whisper is not loaded!");
                return;
            }

            _isTranscribing = true;
            try
            {
                var result = await whisperManager.GetTextAsync(chunk.Data, chunk.Frequency, chunk.Channels);

                if (result == null || string.IsNullOrWhiteSpace(result.Result)) return;

                string text = result.Result.Trim();

                if (string.Equals(text, "[blank audio]", StringComparison.OrdinalIgnoreCase)) return;
                if (text.Length <= 1) return;

                OnTranscription?.Invoke(text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceInput] Transcription error: {ex.Message}");
            }
            finally
            {
                _isTranscribing = false;
            }
        }

        private void OnVadChanged(bool speechDetected)
        {
            if (_isListening)
                SetVadIndicator(speechDetected ? colorSpeaking : colorReady);
        }

        private void SetVadIndicator(Color color)
        {
            if (vadIndicator != null)
                vadIndicator.color = color;
        }

        private bool IsSilent(float[] samples, out float avgEnergy)
        {
            if (samples == null || samples.Length == 0)
            {
                avgEnergy = 0f;
                return true;
            }
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++) sum += Mathf.Abs(samples[i]);
            avgEnergy = sum / samples.Length;
            return avgEnergy < silenceThreshold;
        }

        private bool IsSilent(float[] samples)
        {
            return IsSilent(samples, out _);
        }
    }
}
