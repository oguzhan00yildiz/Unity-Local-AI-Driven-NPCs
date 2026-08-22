using UnityEngine;
using System;
using System.Collections.Generic;
using PiperTTS;

namespace AISystem
{
    /// <summary>
    /// PiperTTS wrapper  splits text into sentences and speaks them in sequence.
    /// </summary>
    public class VoiceOutputService : MonoBehaviour
    {
        //  Inspector 
        [Header("Settings")]
        public bool voiceEnabled = true;
        public bool initOnStart  = true;

        [Header("Optional status label")]
        public UnityEngine.UI.Text statusLabel;

        //  Events 
        public event Action OnSpeechStarted;
        public event Action OnSpeechFinished;

        //  Internal 
        private Dictionary<string, PiperTTS.PiperTTS> _piperInstances = new Dictionary<string, PiperTTS.PiperTTS>();
        private PiperTTS.PiperTTS _activePiper;
        
        private Queue<string> _sentenceQueue = new Queue<string>();
        private bool   _isSpeaking;
        private string _pendingSpeech;

        public bool IsSpeaking => _isSpeaking;

        //  Lifecycle 

        // Awake runs before OnEnable.
        void Awake()
        {
        }

        void Start()
        {
            SetStatus("Idle");
        }

        void OnEnable()
        {
        }

        void OnDisable()
        {
            foreach (var piper in _piperInstances.Values)
            {
                piper.OnStatusChanged -= OnPiperStatusChanged;
            }
        }

        void OnDestroy()
        {
            foreach (var piper in _piperInstances.Values)
            {
                piper.OnStatusChanged -= OnPiperStatusChanged;
            }
        }

        private PiperTTS.PiperTTS GetOrCreatePiper(string voiceName)
        {
            if (string.IsNullOrEmpty(voiceName)) voiceName = "en_US-amy-low";
            
            if (_piperInstances.TryGetValue(voiceName, out var existing))
            {
                return existing;
            }

            // Create new PiperTTS instance
            GameObject go = new GameObject($"PiperTTS_{voiceName}");
            go.transform.SetParent(transform);
            
            var newPiper = go.AddComponent<PiperTTS.PiperTTS>();
            newPiper.piperModelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "PiperTTS", voiceName, $"{voiceName}.onnx");
            newPiper.piperConfigPath = System.IO.Path.Combine(Application.streamingAssetsPath, "PiperTTS", voiceName, $"{voiceName}.onnx.json");
            
            // Phonemizer uses the shared model
            newPiper.phonemizerModelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "PiperTTS", "model.onnx");
            newPiper.phonemizerConfigPath = System.IO.Path.Combine(Application.streamingAssetsPath, "PiperTTS", "model.onnx.json"); // fallback
            newPiper.phonemizerDictPath = System.IO.Path.Combine(Application.streamingAssetsPath, "PiperTTS", "phoneme_dict.json");
            
            newPiper.OnStatusChanged += OnPiperStatusChanged;
            _piperInstances[voiceName] = newPiper;
            
            if (initOnStart)
            {
                newPiper.InitModel();
            }

            return newPiper;
        }

        //  Public API 

        public void Speak(string text, string voiceModelName = "en_US-amy-low")
        {
            if (!voiceEnabled || string.IsNullOrWhiteSpace(text)) return;

            _activePiper = GetOrCreatePiper(voiceModelName);

            // Model not ready yet  queue and initialize
            if (_activePiper.status == PiperTTS.ModelStatus.Init || _activePiper.status == PiperTTS.ModelStatus.Loading)
            {
                _pendingSpeech = text;
                if (_activePiper.status == PiperTTS.ModelStatus.Init)
                    _activePiper.InitModel();
                return;
            }

            if (_activePiper.status == PiperTTS.ModelStatus.Error)
            {
                SetStatus("Voice error");
                return;
            }

            // Clear previous speech and enqueue new sentences
            _sentenceQueue.Clear();
            var sentences = SplitIntoSentences(text);
            foreach (var s in sentences)
                if (!string.IsNullOrWhiteSpace(s))
                    _sentenceQueue.Enqueue(s.Trim());

            if (_sentenceQueue.Count > 0)
            {
                var firstSentence = _sentenceQueue.Dequeue();
                _activePiper.Prompt(firstSentence);
            }
        }

        public void StopSpeaking()
        {
            _sentenceQueue.Clear();
            _isSpeaking    = false;
            _pendingSpeech = string.Empty;

            if (_activePiper != null)
            {
                var audio = _activePiper.GetComponent<AudioSource>();
                audio?.Stop();
            }
            SetStatus("Idle");
        }

        //  Internal 

        private void OnPiperStatusChanged(PiperTTS.ModelStatus status)
        {
            if (!voiceEnabled) return;

            switch (status)
            {
                case PiperTTS.ModelStatus.Loading:
                    SetStatus("Loading voice model...");
                    break;

                case PiperTTS.ModelStatus.Ready:
                    if (_isSpeaking)
                    {
                        _isSpeaking = false;

                        // Speak next sentence in queue if available
                        if (_sentenceQueue.Count > 0 && _activePiper != null)
                        {
                            var nextSentence = _sentenceQueue.Dequeue();
                            _activePiper.Prompt(nextSentence);
                            _isSpeaking = true;
                        }
                        else
                        {
                            SetStatus("Idle");
                            OnSpeechFinished?.Invoke();
                        }
                    }
                    else
                    {
                        SetStatus("Voice ready");
                    }

                    // Speak any pending text that arrived before the model was ready
                    if (!string.IsNullOrWhiteSpace(_pendingSpeech))
                    {
                        var txt = _pendingSpeech;
                        _pendingSpeech = string.Empty;
                        // Use the last active model name
                        Speak(txt, _activePiper?.gameObject.name.Replace("PiperTTS_", ""));
                    }
                    break;

                case PiperTTS.ModelStatus.Generate:
                    if (!_isSpeaking)
                    {
                        _isSpeaking = true;
                        OnSpeechStarted?.Invoke();
                    }
                    SetStatus("Speaking...");
                    break;

                case PiperTTS.ModelStatus.Error:
                    _isSpeaking = false;
                    _sentenceQueue.Clear();
                    SetStatus("Voice model error");
                    OnSpeechFinished?.Invoke();
                    break;
            }
        }

        private static List<string> SplitIntoSentences(string text)
        {
            var result  = new List<string>();
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                current.Append(c);

                bool isSentenceEnd = (c == '.' || c == '!' || c == '?');
                bool hasSpaceAfter  = i + 1 < text.Length && text[i + 1] == ' ';

                if (isSentenceEnd && hasSpaceAfter)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    i++; // skip the trailing space
                }
            }

            if (current.Length > 0)
                result.Add(current.ToString());

            return result;
        }

        private void SetStatus(string msg)
        {
            if (statusLabel != null) statusLabel.text = msg;
        }
    }
}
