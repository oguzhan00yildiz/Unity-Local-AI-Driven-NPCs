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
        [Header("PiperTTS")]
        public PiperTTS.PiperTTS piperTts;

        [Header("Settings")]
        public bool voiceEnabled = true;
        public bool initOnStart  = true;

        [Header("Optional status label")]
        public UnityEngine.UI.Text statusLabel;

        //  Events 
        public event Action OnSpeechStarted;
        public event Action OnSpeechFinished;

        //  Internal 
        private Queue<string> _sentenceQueue = new Queue<string>();
        private bool   _isSpeaking;
        private string _pendingSpeech;

        public bool IsSpeaking => _isSpeaking;

        //  Lifecycle 

        // Awake runs before OnEnable, so piperTts is resolved before the first
        // OnEnable subscription attempt — prevents double-subscription when
        // piperTts is assigned in the Inspector.
        void Awake()
        {
            if (piperTts == null)
                piperTts = GetComponent<PiperTTS.PiperTTS>();
        }

        void Start()
        {
            if (!voiceEnabled) return;
            // piperTts already resolved in Awake and subscribed in OnEnable.
            // Do NOT subscribe again here — that would cause double-firing.
            if (initOnStart && piperTts != null && piperTts.status == ModelStatus.Init)
                piperTts.InitModel();

            SetStatus("Idle");
        }

        void OnEnable()
        {
            if (piperTts != null)
                piperTts.OnStatusChanged += OnPiperStatusChanged;
        }

        void OnDisable()
        {
            if (piperTts != null)
                piperTts.OnStatusChanged -= OnPiperStatusChanged;
        }

        void OnDestroy()
        {
            if (piperTts != null)
                piperTts.OnStatusChanged -= OnPiperStatusChanged;
        }

        //  Public API 

        public void Speak(string text)
        {
            if (!voiceEnabled || string.IsNullOrWhiteSpace(text)) return;

            if (piperTts == null)
            {
                SetStatus("No PiperTTS");
                return;
            }

            // Model not ready yet  queue and initialize
            if (piperTts.status == ModelStatus.Init || piperTts.status == ModelStatus.Loading)
            {
                _pendingSpeech = text;
                if (piperTts.status == ModelStatus.Init)
                    piperTts.InitModel();
                return;
            }

            if (piperTts.status == ModelStatus.Error)
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
                piperTts.Prompt(firstSentence);
            }
        }

        public void StopSpeaking()
        {
            _sentenceQueue.Clear();
            _isSpeaking    = false;
            _pendingSpeech = string.Empty;

            if (piperTts != null)
            {
                var audio = piperTts.GetComponent<AudioSource>();
                audio?.Stop();
            }
            SetStatus("Idle");
        }

        //  Internal 

        private void OnPiperStatusChanged(ModelStatus status)
        {
            if (!voiceEnabled) return;

            switch (status)
            {
                case ModelStatus.Loading:
                    SetStatus("Loading voice model...");
                    break;

                case ModelStatus.Ready:
                    if (_isSpeaking)
                    {
                        _isSpeaking = false;

                        // Speak next sentence in queue if available
                        if (_sentenceQueue.Count > 0)
                        {
                            var nextSentence = _sentenceQueue.Dequeue();
                            piperTts.Prompt(nextSentence);
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
                        Speak(txt);
                    }
                    break;

                case ModelStatus.Generate:
                    if (!_isSpeaking)
                    {
                        _isSpeaking = true;
                        OnSpeechStarted?.Invoke();
                    }
                    SetStatus("Speaking...");
                    break;

                case ModelStatus.Error:
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
