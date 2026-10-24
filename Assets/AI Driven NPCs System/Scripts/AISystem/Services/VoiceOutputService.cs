using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using PiperTTS;

namespace AISystem
{
    /// <summary>
    /// PiperTTS wrapper — splits text into sentences and speaks them in sequence.
    /// Streamed text (AppendSpeech) starts speaking at the first full sentence; EndSpeech closes the stream.
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

        [Tooltip("Logs each queued and spoken sentence. PiperTTS logs its phonemes and clip lengths too.")]
        [SerializeField] private bool debugLogs;

        [Header("Optional status label")]
        public UnityEngine.UI.Text statusLabel;

        // ── Events ────────────────────────────────────────────────────────────────
        public event Action OnSpeechStarted;
        public event Action OnSpeechFinished;

        // ── Internal ──────────────────────────────────────────────────────────────
        private Queue<string> _sentenceQueue = new Queue<string>();
        private bool _isSpeaking;

        // Streamed text that does not end in a full sentence yet
        private readonly System.Text.StringBuilder _streamBuffer = new System.Text.StringBuilder();
        private bool _streamOpen;

        // Voice name (e.g. en_US-bryce-medium) -> its own PiperTTS copy. piperTts is the default voice.
        private readonly Dictionary<string, PiperTTS.PiperTTS> _voices = new Dictionary<string, PiperTTS.PiperTTS>();
        private readonly Dictionary<PiperTTS.PiperTTS, (PiperTTS.PiperTTS.StatusChangedDelegate status, Action playback)> _handlers =
            new Dictionary<PiperTTS.PiperTTS, (PiperTTS.PiperTTS.StatusChangedDelegate, Action)>();
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
            foreach (var pair in _handlers)
            {
                if (pair.Key == null) continue;
                pair.Key.OnStatusChanged    -= pair.Value.status;
                pair.Key.OnPlaybackFinished -= pair.Value.playback;
            }
            _handlers.Clear();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Speaks a complete text. Replaces any current speech.</summary>
        public void Speak(string text, string voiceModelName = null)
        {
            StopSpeaking();
            AppendSpeech(text, voiceModelName);
            EndSpeech();
        }

        /// <summary>
        /// Adds streamed text (e.g. new LLM tokens). The first call starts a new speech and replaces the current one.
        /// Each full sentence is spoken as soon as it arrives. Call EndSpeech when the text is complete.
        /// </summary>
        public void AppendSpeech(string chunk, string voiceModelName = null)
        {
            if (!voiceEnabled || string.IsNullOrEmpty(chunk)) return;

            if (!_streamOpen)
                BeginStream(voiceModelName);

            if (_activeTts.status == ModelStatus.Error)
            {
                SetStatus("Voice error");
                return;
            }

            _streamBuffer.Append(chunk);
            FlushSentences(false);
            TrySpeakNext();
        }

        /// <summary>Speaks the remaining text. OnSpeechFinished fires after the last sentence.</summary>
        public void EndSpeech()
        {
            if (!_streamOpen) return;

            FlushSentences(true);
            _streamOpen = false;
            Log($"Stream closed, {_sentenceQueue.Count} sentences queued");
            TrySpeakNext();

            // Nothing left to say: the last clip already ended, or the text was only *actions*
            if (_sentenceQueue.Count == 0 && _activeTts.status != ModelStatus.Generate && !_activeTts.IsPlaying)
                FinishSpeech();
        }

        public void StopSpeaking()
        {
            if (_isSpeaking || _sentenceQueue.Count > 0 || _streamBuffer.Length > 0)
                Log($"Stop: dropped {_sentenceQueue.Count} queued sentences and buffer \"{_streamBuffer}\"");

            _sentenceQueue.Clear();
            _streamBuffer.Clear();
            _streamOpen = false;
            _isSpeaking = false;

            _activeTts.StopPlayback();
            SetStatus("Idle");
        }

        private void BeginStream(string voiceModelName)
        {
            StopSpeaking();
            _activeTts  = GetVoice(voiceModelName);
            _streamOpen = true;

            // Sentences wait in the queue until the model reports Ready
            if (_activeTts.status == ModelStatus.Init)
                _activeTts.InitModel();
        }

        /// <summary>
        /// Moves full sentences from the stream buffer to the queue.
        /// Sentence ends inside *actions* do not count. With final = true the rest is queued too.
        /// </summary>
        private void FlushSentences(bool final)
        {
            int start = 0;
            bool inAction = false;

            for (int i = 0; i < _streamBuffer.Length; i++)
            {
                char c = _streamBuffer[i];
                if (c == '*')
                {
                    inAction = !inAction;
                    continue;
                }

                bool isSentenceEnd = !inAction && (c == '.' || c == '!' || c == '?');
                bool hasSpaceAfter = i + 1 < _streamBuffer.Length && char.IsWhiteSpace(_streamBuffer[i + 1]);

                if (isSentenceEnd && hasSpaceAfter)
                {
                    EnqueueSentence(_streamBuffer.ToString(start, i + 1 - start));
                    start = i + 1;
                }
            }

            if (final && start < _streamBuffer.Length)
            {
                EnqueueSentence(_streamBuffer.ToString(start, _streamBuffer.Length - start));
                start = _streamBuffer.Length;
            }

            _streamBuffer.Remove(0, start);
        }

        private void EnqueueSentence(string sentence)
        {
            string cleaned = CleanTextForSpeech(sentence);
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                _sentenceQueue.Enqueue(cleaned);
                Log($"Queued: \"{cleaned}\"");
            }
            else
            {
                Log($"Skipped, nothing to speak: \"{sentence}\"");
            }
        }

        /// <summary>Prompts the next sentence when the voice is free. Otherwise the Ready status does it.</summary>
        private void TrySpeakNext()
        {
            if (_sentenceQueue.Count > 0 && _activeTts.status == ModelStatus.Ready)
                PromptNext(_activeTts);
        }

        private void PromptNext(PiperTTS.PiperTTS tts)
        {
            string sentence = _sentenceQueue.Dequeue();
            Log($"Speak: \"{sentence}\" ({_sentenceQueue.Count} left)");
            tts.Prompt(sentence);
        }

        private void Log(string msg)
        {
            if (debugLogs) Debug.Log($"[VoiceOutputService] {msg}");
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
            PiperTTS.PiperTTS.StatusChangedDelegate statusHandler = status => OnPiperStatusChanged(tts, status);
            Action playbackHandler = () => OnPiperPlaybackFinished(tts);
            tts.debugLogs = debugLogs;
            tts.OnStatusChanged    += statusHandler;
            tts.OnPlaybackFinished += playbackHandler;
            _handlers[tts] = (statusHandler, playbackHandler);
        }

        /// <summary>
        /// Cleans Markdown, asterisks/actions (*smiles*), contractions (I'm -> I am), and unusual symbols for TTS.
        /// </summary>
        public static string CleanTextForSpeech(string text)
        {
            // Expand contractions (I'm -> I am, don't -> do not)
            return NormalizeContractions(CleanTextForDisplay(text));
        }

        /// <summary>
        /// Removes asterisks/actions (*smiles*) and Markdown symbols. Used for the chat panel.
        /// An unclosed action at the end (*smi while streaming) is removed too.
        /// </summary>
        public static string CleanTextForDisplay(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // Remove text inside asterisks (roleplay cues like *laughs*, *sighs*)
            string result = System.Text.RegularExpressions.Regex.Replace(text, @"\*.*?\*", "");

            // Remove an unclosed action at the end
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\*[^*]*$", "");

            // Remove markdown symbols (#, _, ~, `, [, ])
            result = System.Text.RegularExpressions.Regex.Replace(result, @"[#_`~\[\]]", "");

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
                    // Ready comes when synthesis ends, while the clip still plays.
                    // Next sentence (synthesized during playback), or sentences queued while the model was loading
                    if (_sentenceQueue.Count > 0)
                    {
                        PromptNext(source);
                    }
                    else if (_isSpeaking && !_streamOpen && !source.IsPlaying)
                    {
                        FinishSpeech();
                    }
                    else if (!_isSpeaking)
                    {
                        SetStatus("Voice ready");
                    }
                    // Else wait for the playback to end or for the next streamed sentence
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

        private void OnPiperPlaybackFinished(PiperTTS.PiperTTS source)
        {
            if (!voiceEnabled || source != _activeTts) return;

            // Last clip ended and nothing else is coming
            if (_isSpeaking && !_streamOpen && _sentenceQueue.Count == 0 && source.status == ModelStatus.Ready)
                FinishSpeech();
        }

        private void FinishSpeech()
        {
            Log("Speech finished");
            _isSpeaking = false;
            SetStatus("Idle");
            OnSpeechFinished?.Invoke();
        }

        private void SetStatus(string msg)
        {
            if (statusLabel != null) statusLabel.text = msg;
        }
    }
}
