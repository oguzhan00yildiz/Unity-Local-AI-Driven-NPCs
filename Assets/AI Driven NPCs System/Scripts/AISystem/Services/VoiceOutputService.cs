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

        [Header("3D voice")]
        [Tooltip("1 = the voice comes from the speaking NPC in the world. 0 = flat 2D voice for every NPC.")]
        [Range(0f, 1f)]
        [SerializeField] private float spatialBlend = 1f;
        [Tooltip("Full volume up to this distance from the listener (the camera). The camera is a few metres behind the player, so keep it above 6.")]
        [SerializeField] private float minDistance = 8f;
        [Tooltip("Silent beyond this distance.")]
        [SerializeField] private float maxDistance = 30f;
        [Tooltip("Height of the voice above the NPC's feet (mouth height).")]
        [SerializeField] private float mouthHeight = 1.6f;

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
        private Transform _speaker;   // NPC the current voice follows; null = 2D

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
        public void Speak(string text, string voiceModelName = null, Transform speaker = null)
        {
            StopSpeaking();
            AppendSpeech(text, voiceModelName, speaker);
            EndSpeech();
        }

        /// <summary>
        /// Adds streamed text (e.g. new LLM tokens). The first call starts a new speech and replaces the current one.
        /// Each full sentence is spoken as soon as it arrives. Call EndSpeech when the text is complete.
        /// With a speaker, the voice plays in 3D from that NPC's position and follows it.
        /// </summary>
        public void AppendSpeech(string chunk, string voiceModelName = null, Transform speaker = null)
        {
            if (!voiceEnabled || string.IsNullOrEmpty(chunk)) return;

            if (!_streamOpen)
                BeginStream(voiceModelName, speaker);

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
            TrySpeakNext();

            // Nothing left to say: the last clip already ended, or the text was only *actions*
            if (_sentenceQueue.Count == 0 && _activeTts.status != ModelStatus.Generate && !_activeTts.IsPlaying)
                FinishSpeech();
        }

        public void StopSpeaking()
        {
            _sentenceQueue.Clear();
            _streamBuffer.Clear();
            _streamOpen = false;
            _isSpeaking = false;

            _activeTts.StopPlayback();
            SetStatus("Idle");
        }

        private void BeginStream(string voiceModelName, Transform speaker)
        {
            StopSpeaking();
            _activeTts  = GetVoice(voiceModelName);
            _streamOpen = true;

            _speaker = speaker;
            var source = _activeTts.GetComponent<AudioSource>();
            source.spatialBlend = speaker != null ? spatialBlend : 0f;
            source.rolloffMode  = AudioRolloffMode.Logarithmic;
            source.minDistance  = minDistance;
            source.maxDistance  = maxDistance;
            FollowSpeaker();

            // Sentences wait in the queue until the model reports Ready
            if (_activeTts.status == ModelStatus.Init)
                _activeTts.InitModel();
        }

        void LateUpdate()
        {
            if (_isSpeaking || _streamOpen) FollowSpeaker();
        }

        /// <summary>Keeps the active voice at the speaking NPC's mouth, so it moves with a walking NPC.</summary>
        private void FollowSpeaker()
        {
            if (_speaker != null)
                _activeTts.transform.position = _speaker.position + Vector3.up * mouthHeight;
        }

        /// <summary>
        /// Moves full sentences from the stream buffer to the queue.
        /// Sentence ends inside *actions* do not count. A line break ends a sentence. With final = true the rest is queued too.
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
                    // Wait for the next character to know what this asterisk is
                    if (i + 1 >= _streamBuffer.Length) { if (!final) break; continue; }
                    char next = _streamBuffer[i + 1];
                    if (next == '*') { i++; continue; }                 // **bold** marker
                    if (!inAction && char.IsWhiteSpace(next)) continue; // list bullet
                    inAction = !inAction;
                    continue;
                }

                if (c == '\n' && !inAction)
                {
                    EnqueueSentence(_streamBuffer.ToString(start, i + 1 - start));
                    start = i + 1;
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
                _sentenceQueue.Enqueue(cleaned);
        }

        /// <summary>Prompts the next sentence when the voice is free. Otherwise the Ready status does it.</summary>
        private void TrySpeakNext()
        {
            if (_sentenceQueue.Count > 0 && _activeTts.status == ModelStatus.Ready)
                _activeTts.Prompt(_sentenceQueue.Dequeue());
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
                Preload(npc.voiceModelName);
            foreach (var bark in FindObjectsByType<AmbientBark>(FindObjectsSortMode.None))
                Preload(bark.VoiceModelName);
        }

        private void Preload(string voiceModelName)
        {
            var tts = GetVoice(voiceModelName);
            if (tts.status == ModelStatus.Init)
                tts.InitModel();
        }

        private void Subscribe(PiperTTS.PiperTTS tts)
        {
            PiperTTS.PiperTTS.StatusChangedDelegate statusHandler = status => OnPiperStatusChanged(tts, status);
            Action playbackHandler = () => OnPiperPlaybackFinished(tts);
            tts.OnStatusChanged    += statusHandler;
            tts.OnPlaybackFinished += playbackHandler;
            _handlers[tts] = (statusHandler, playbackHandler);
        }

        /// <summary>
        /// Cleans Markdown, asterisks/actions (*smiles*) and unusual symbols for TTS.
        /// Contractions stay as written; the phonemizer speaks them contracted.
        /// </summary>
        public static string CleanTextForSpeech(string text)
        {
            // Curly apostrophes to ASCII, so "don’t" matches the pronunciation rules
            return CleanTextForDisplay(text).Replace('’', '\'').Replace('‘', '\'');
        }

        /// <summary>
        /// Removes asterisks/actions (*smiles*) and Markdown symbols. Used for the chat panel.
        /// An unclosed action at the end (*smi while streaming) is removed too.
        /// </summary>
        public static string CleanTextForDisplay(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // Keep **bold** text, drop the markers
            string result = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");

            // Remove roleplay cues like *laughs*. A cue starts with * and a non-space and stays on one line,
            // so list bullets ("* item") are not cues.
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\*[^*\s][^*\n]*?\*", "");

            // Remove an unclosed cue at the end
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\*[^*\s][^*\n]*$", "");

            // Remove list bullets and other leftover asterisks
            result = result.Replace("*", "");

            // Remove action tags like [FOLLOW] (read by NPCActions), and an unclosed tag at the end while streaming
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\[[A-Za-z_]+\]", "");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\[[A-Za-z_]*$", "");

            // Remove markdown symbols (#, _, ~, `, [, ])
            result = System.Text.RegularExpressions.Regex.Replace(result, @"[#_`~\[\]]", "");

            // Normalize multiple whitespace to single space
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();

            return result;
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
                        source.Prompt(_sentenceQueue.Dequeue());
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
