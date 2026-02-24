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

        //  Events 
        /// <summary>Fired when audio has been successfully transcribed to text.</summary>
        public event Action<string> OnTranscription;

        //  State 
        private bool _isListening;
        private bool _isPaused;
        private bool _isTranscribing;

        public bool IsListening    => _isListening;
        public bool IsTranscribing => _isTranscribing;

        //  Lifecycle 
        void Awake()
        {
            if (microphoneRecord == null)
                microphoneRecord = GetComponent<MicrophoneRecord>();
            if (whisperManager == null)
                whisperManager = GetComponentInParent<WhisperManager>(true);
        }

        void Start()
        {
            if (microphoneRecord != null)
            {
                microphoneRecord.OnRecordStop += OnMicRecordStop;
                microphoneRecord.OnVadChanged  += OnVadChanged;
            }

            SetupMicrophoneDropdown();
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

        public void StartListening()
        {
            if (microphoneRecord == null || _isListening || _isPaused || _isTranscribing) return;
            _isListening = true;
            microphoneRecord.StartRecord();
            Debug.Log("[VoiceInput] Listening started.");
        }

        public void StopListening()
        {
            _isPaused    = false;
            _isListening = false;
            if (microphoneRecord != null && microphoneRecord.IsRecording)
            {
                microphoneRecord.StopRecord();
                Debug.Log("[VoiceInput] Listening stopped.");
            }
        }

        /// <summary>Temporarily pauses listening while TTS is speaking (prevents silent captures).</summary>
        public void PauseListening()
        {
            if (!_isListening) return;
            _isPaused = true;
            if (microphoneRecord != null && microphoneRecord.IsRecording)
                microphoneRecord.StopRecord();
            Debug.Log("[VoiceInput] Listening paused (TTS active).");
        }

        /// <summary>Resumes listening after a pause.</summary>
        public void ResumeListening()
        {
            if (!_isPaused) return;
            _isPaused = false;
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
            // Skip recordings that arrived from a TTS pause
            if (_isPaused) return;

            _isListening = false;

            if (chunk.Data == null || chunk.Data.Length == 0 || IsSilent(chunk.Data))
            {
                Debug.Log("[VoiceInput] Silent audio  transcription skipped.");
                if (autoRestartAfterTranscribe && !_isPaused)
                    StartListening();
                return;
            }

            await Transcribe(chunk);

            if (autoRestartAfterTranscribe && !_isPaused)
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

                Debug.Log($"[VoiceInput] Transcription: {text}");
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

        private void OnVadChanged(bool speechDetected) { /* Override for UI indicators */ }

        private bool IsSilent(float[] samples)
        {
            float sum = 0f;
            foreach (float s in samples) sum += Mathf.Abs(s);
            return (sum / samples.Length) < silenceThreshold;
        }
    }
}
