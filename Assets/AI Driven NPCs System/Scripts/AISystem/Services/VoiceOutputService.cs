using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using PiperTTS;

namespace AISystem
{
    /// <summary>
    /// PiperTTS wrapper — splits text into sentences and speaks them in sequence.
    /// Each voice gets its own PiperTTS copy, loaded on first use and cached.
    /// Voices used by NPCs in the scene are loaded in Start.
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
        private string _pendingVoice;

        // Voice name (e.g. en_US-bryce-medium) -> its own PiperTTS copy. piperTts is the default voice.
        private readonly Dictionary<string, PiperTTS.PiperTTS> _voices = new Dictionary<string, PiperTTS.PiperTTS>();
        private readonly Dictionary<PiperTTS.PiperTTS, PiperTTS.PiperTTS.StatusChangedDelegate> _statusHandlers =
            new Dictionary<PiperTTS.PiperTTS, PiperTTS.PiperTTS.StatusChangedDelegate>();
        private PiperTTS.PiperTTS _activeTts;

        public bool IsSpeaking => _isSpeaking;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        void Awake()
        {
            if (piperTts == null)
                piperTts = GetComponentInChildren<PiperTTS.PiperTTS>();

            // Prefab paths are project-relative ("Assets/StreamingAssets/..."), which only exist in the Editor
            piperTts.piperModelPath       = ToStreamingAssetsPath(piperTts.piperModelPath);
            piperTts.piperConfigPath      = ToStreamingAssetsPath(piperTts.piperConfigPath);
            piperTts.phonemizerModelPath  = ToStreamingAssetsPath(piperTts.phonemizerModelPath);
            piperTts.phonemizerConfigPath = ToStreamingAssetsPath(piperTts.phonemizerConfigPath);
            piperTts.phonemizerDictPath   = ToStreamingAssetsPath(piperTts.phonemizerDictPath);

            _activeTts = piperTts;
            _voices[Path.GetFileNameWithoutExtension(piperTts.piperModelPath)] = piperTts;
            Subscribe(piperTts);
        }

        void Start()
        {
            if (!voiceEnabled) return;
            if (initOnStart)
            {
                if (piperTts.status == ModelStatus.Init)
                    piperTts.InitModel();
                PreloadSceneVoices();
            }

            SetStatus("Idle");
        }

        void OnDestroy()
        {
            foreach (var pair in _statusHandlers)
                if (pair.Key != null)
                    pair.Key.OnStatusChanged -= pair.Value;
            _statusHandlers.Clear();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void Speak(string text, string voiceModelName = null)
        {
            if (!voiceEnabled || string.IsNullOrWhiteSpace(text)) return;

            string cleanedText = CleanTextForSpeech(text);
            if (string.IsNullOrWhiteSpace(cleanedText)) return;

            var tts = GetVoice(voiceModelName);
            if (tts != _activeTts)
            {
                StopSpeaking();
                _activeTts = tts;
            }

            // Model not ready yet — queue and initialize
            if (tts.status == ModelStatus.Init || tts.status == ModelStatus.Loading)
            {
                _pendingSpeech = cleanedText;
                _pendingVoice  = voiceModelName;
                if (tts.status == ModelStatus.Init)
                    tts.InitModel();
                return;
            }

            if (tts.status == ModelStatus.Error)
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
                tts.Prompt(firstSentence);
            }
        }

        public void StopSpeaking()
        {
            _sentenceQueue.Clear();
            _isSpeaking    = false;
            _pendingSpeech = string.Empty;

            _activeTts.GetComponent<AudioSource>().Stop();
            SetStatus("Idle");
        }

        // ── Voices ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the PiperTTS for a voice, creating a copy of the default one on first use.
        /// Falls back to the default voice when the name is empty or its files are not downloaded.
        /// </summary>
        private PiperTTS.PiperTTS GetVoice(string voiceModelName)
        {
            if (string.IsNullOrEmpty(voiceModelName))
                return piperTts;

            if (_voices.TryGetValue(voiceModelName, out var cached))
                return cached;

            string modelPath  = GetVoiceModelPath(voiceModelName);
            string configPath = modelPath + ".json";
            if (!File.Exists(modelPath) || !File.Exists(configPath))
            {
                Debug.LogWarning($"[VoiceOutputService] Voice '{voiceModelName}' not found at {modelPath}. Using the default voice. Download it in Tools → AI Packages → Voice Browser.");
                _voices[voiceModelName] = piperTts;
                return piperTts;
            }

            var copy = Instantiate(piperTts.gameObject, piperTts.transform.parent);
            copy.name = $"{piperTts.gameObject.name} ({voiceModelName})";
            var tts = copy.GetComponent<PiperTTS.PiperTTS>();
            tts.piperModelPath  = modelPath;
            tts.piperConfigPath = configPath;

            _voices[voiceModelName] = tts;
            Subscribe(tts);
            return tts;
        }

        /// <summary>Piper names are "locale-speaker-quality"; files are in StreamingAssets/PiperTTS/speaker/.</summary>
        private static string GetVoiceModelPath(string voiceModelName)
        {
            string[] parts = voiceModelName.Split('-');
            string folder = parts.Length >= 3 ? parts[1] : voiceModelName;
            return Path.Combine(Application.streamingAssetsPath, "PiperTTS", folder, voiceModelName + ".onnx");
        }

        private static string ToStreamingAssetsPath(string path)
        {
            const string prefix = "Assets/StreamingAssets/";
            if (string.IsNullOrEmpty(path) || !path.Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return path;
            return Path.Combine(Application.streamingAssetsPath, path.Substring(prefix.Length));
        }

        private void PreloadSceneVoices()
        {
            foreach (var npc in FindObjectsByType<NPCAgent>(FindObjectsSortMode.None))
            {
                var tts = GetVoice(npc.voiceModelName);
                if (tts.status == ModelStatus.Init)
                    tts.InitModel();
            }
        }

        private void Subscribe(PiperTTS.PiperTTS tts)
        {
            PiperTTS.PiperTTS.StatusChangedDelegate handler = status => OnPiperStatusChanged(tts, status);
            tts.OnStatusChanged += handler;
            _statusHandlers[tts] = handler;
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

        private void OnPiperStatusChanged(PiperTTS.PiperTTS source, ModelStatus status)
        {
            // Preloading voices report status too; only the speaking voice drives the queue
            if (!voiceEnabled || source != _activeTts) return;

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
                            source.Prompt(nextSentence);
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
                        Speak(txt, _pendingVoice);
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
