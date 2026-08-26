using UnityEngine;
using System;
using System.Collections.Generic;
using PiperTTS;

namespace AISystem
{
    /// <summary>
    /// PiperTTS wrapper — splits text into sentences and speaks them in sequence.
    /// </summary>
    public class VoiceOutputService : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────
        [Header("PiperTTS")]
        public PiperTTS.PiperTTS piperTts;

        [Header("Settings")]
        public bool voiceEnabled = true;
        public bool initOnStart  = true;

        [Header("Optional status label")]
        public UnityEngine.UI.Text statusLabel;

        // ── Events ────────────────────────────────────────────────────────────────
        public event Action OnSpeechStarted;
        public event Action OnSpeechFinished;

        // ── Internal ──────────────────────────────────────────────────────────────
        private Queue<string> _sentenceQueue = new Queue<string>();
        private bool   _isSpeaking;
        private string _pendingSpeech;

        public bool IsSpeaking => _isSpeaking;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        void Awake()
        {
            if (piperTts == null)
                piperTts = GetComponentInChildren<PiperTTS.PiperTTS>();
        }

        void Start()
        {
            if (!voiceEnabled) return;
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

        // ── Public API ────────────────────────────────────────────────────────────

        public void Speak(string text, string voiceModelName = null)
        {
            if (!voiceEnabled || string.IsNullOrWhiteSpace(text)) return;

            string cleanedText = CleanTextForSpeech(text);
            if (string.IsNullOrWhiteSpace(cleanedText)) return;

            if (piperTts == null)
            {
                piperTts = GetComponentInChildren<PiperTTS.PiperTTS>();
                if (piperTts == null)
                {
                    SetStatus("No PiperTTS");
                    return;
                }
            }

            // Model not ready yet — queue and initialize
            if (piperTts.status == ModelStatus.Init || piperTts.status == ModelStatus.Loading)
            {
                _pendingSpeech = cleanedText;
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
            var sentences = SplitIntoSentences(cleanedText);
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

        /// <summary>
        /// Cleans Markdown, asterisks/actions (*smiles*), contractions (I'm -> I am), and unusual symbols for TTS.
        /// </summary>
        public static string CleanTextForSpeech(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // Remove text inside asterisks (roleplay cues like *laughs*, *sighs*)
            string result = System.Text.RegularExpressions.Regex.Replace(text, @"\*.*?\*", "");

            // Remove markdown symbols (#, _, ~, `, [, ])
            result = System.Text.RegularExpressions.Regex.Replace(result, @"[#_`~\[\]]", "");

            // Expand contractions (I'm -> I am, don't -> do not)
            result = PiperTTS.PiperTTS.NormalizeContractions(result);

            // Normalize multiple whitespace to single space
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();

            return result;
        }

        // ── Internal ──────────────────────────────────────────────────────────────

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
                bool hasSpaceAfter  = i + 1 < text.Length && (text[i + 1] == ' ' || text[i + 1] == '\n');

                if (isSentenceEnd && hasSpaceAfter)
                {
                    string sentence = current.ToString().Trim();
                    if (!string.IsNullOrEmpty(sentence))
                        result.Add(sentence);
                    current.Clear();
                    i++; // skip the whitespace
                }
            }

            if (current.Length > 0)
            {
                string sentence = current.ToString().Trim();
                if (!string.IsNullOrEmpty(sentence))
                    result.Add(sentence);
            }

            return result;
        }

        private void SetStatus(string msg)
        {
            if (statusLabel != null) statusLabel.text = msg;
        }
    }
}
