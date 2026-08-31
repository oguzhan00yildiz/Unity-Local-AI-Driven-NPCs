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
            result = NormalizeContractions(result);

            // Normalize multiple whitespace to single space
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();

            return result;
        }

        /// <summary>
        /// Expands common English contractions to their full forms for smoother phonemization/TTS.
        /// </summary>
        public static string NormalizeContractions(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // Normalize curly apostrophes / accents to standard ASCII apostrophe
            text = text.Replace('’', '\'').Replace('‘', '\'').Replace('`', '\'');

            var contractionMap = new Dictionary<string, string>
            {
                { @"\bi'm\b", "I am" },
                { @"\bi've\b", "I have" },
                { @"\bi'll\b", "I will" },
                { @"\bi'd\b", "I would" },
                { @"\byou're\b", "you are" },
                { @"\byou've\b", "you have" },
                { @"\byou'll\b", "you will" },
                { @"\byou'd\b", "you would" },
                { @"\bhe's\b", "he is" },
                { @"\bhe'll\b", "he will" },
                { @"\bhe'd\b", "he would" },
                { @"\bshe's\b", "she is" },
                { @"\bshe'll\b", "she will" },
                { @"\bshe'd\b", "she would" },
                { @"\bit's\b", "it is" },
                { @"\bit'll\b", "it will" },
                { @"\bwe're\b", "we are" },
                { @"\bwe've\b", "we have" },
                { @"\bwe'll\b", "we will" },
                { @"\bwe'd\b", "we would" },
                { @"\bthey're\b", "they are" },
                { @"\bthey've\b", "they have" },
                { @"\bthey'll\b", "they will" },
                { @"\bthey'd\b", "they would" },
                { @"\bthat's\b", "that is" },
                { @"\bwhat's\b", "what is" },
                { @"\bwho's\b", "who is" },
                { @"\bwhere's\b", "where is" },
                { @"\bhow's\b", "how is" },
                { @"\bthere's\b", "there is" },
                { @"\blet's\b", "let us" },
                { @"\bcan't\b", "cannot" },
                { @"\bwon't\b", "will not" },
                { @"\bdon't\b", "do not" },
                { @"\bdoesn't\b", "does not" },
                { @"\bdidn't\b", "did not" },
                { @"\bisn't\b", "is not" },
                { @"\baren't\b", "are not" },
                { @"\bwasn't\b", "was not" },
                { @"\bweren't\b", "were not" },
                { @"\bhaven't\b", "have not" },
                { @"\bhasn't\b", "has not" },
                { @"\bhadn't\b", "had not" },
                { @"\bwouldn't\b", "would not" },
                { @"\bshouldn't\b", "should not" },
                { @"\bcouldn't\b", "could not" }
            };

            foreach (var pair in contractionMap)
            {
                text = System.Text.RegularExpressions.Regex.Replace(text, pair.Key, match =>
                {
                    string replacement = pair.Value;
                    if (char.IsUpper(match.Value[0]))
                    {
                        return char.ToUpper(replacement[0]) + replacement.Substring(1);
                    }
                    return replacement;
                }, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return text;
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
