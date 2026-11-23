using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static PiperTTS.PiperModel;

namespace PiperTTS
{
    public class TokenizerConfig
    {
        [JsonProperty("text_symbols")]
        public Dictionary<string, int> TextSymbols { get; set; }

        [JsonProperty("phoneme_symbols")]
        public Dictionary<string, string> PhonemeSymbols { get; set; }

        [JsonProperty("char_repeats")]
        public int CharRepeats { get; set; }

        [JsonProperty("languages")]
        public List<string> Languages { get; set; }
    }

    public class Tokenizer
    {
        private readonly TokenizerConfig _config;

        public Tokenizer(TokenizerConfig config)
        {
            _config = config;
        }

        public long[] Tokenize(string text, string lang = "en_us")
        {
            var charRepeats = _config.CharRepeats;
            var textSymbols = _config.TextSymbols;

            // Basic lowercasing
            var chars = text.ToLowerInvariant().ToCharArray();
            var sequence = new List<long>();

            // Add start token: <lang>
            string startToken = $"<{lang}>";
            if (textSymbols.TryGetValue(startToken, out int startIndex))
            {
                sequence.Add(startIndex);
            }

            foreach (var c in chars)
            {
                string s = c.ToString();
                if (textSymbols.TryGetValue(s, out int idx))
                {
                    for (int i = 0; i < charRepeats; i++)
                    {
                        sequence.Add(idx);
                    }
                }
            }

            // Add end token: <end>
            if (textSymbols.TryGetValue("<end>", out int endIndex))
            {
                sequence.Add(endIndex);
            }

            return sequence.ToArray();
        }

        public string Decode(long[] tokens)
        {
            // Simple decode
            var phonemes = new List<string>();
            foreach (var t in tokens)
            {
                string sKey = t.ToString();
                if (_config.PhonemeSymbols.TryGetValue(sKey, out string ph))
                {
                    // Filter specials
                    if (!ph.StartsWith("<") && ph != "_")
                    {
                        phonemes.Add(ph);
                    }
                }
            }
            return string.Join("", phonemes);
        }
    }

    public class PhonemizerModel : BackgroundRunner
    {
        [Header("Model")]
        public string modelPath = string.Empty;
        public string configPath = string.Empty;
        public string dictPath = string.Empty;

        // Define a delegate (or use Action<T>)
        public delegate void StatusChangedDelegate(ModelStatus status);
        public event StatusChangedDelegate OnStatusChanged;

        private ModelStatus _status = ModelStatus.Init;

        // Public getter, no public setter
        public ModelStatus status
        {
            get => _status;
            protected set
            {
                if (_status != value)
                {
                    _status = value;
                    OnStatusChanged?.Invoke(_status);
                }
            }
        }

        protected void PostStatus(ModelStatus newStatus)
        {
            unityContext?.Post(_ => status = newStatus, null);
        }

        void OnDestroy()
        {
            BackgroundStop();
            FreeModel();
        }

        // Define a delegate (or use Action<T>)
        public delegate void ResponseGeneratedDelegate(string response);
        public event ResponseGeneratedDelegate OnResponseGenerated;

        InferenceSession _session;
        Tokenizer _tokenizer;
        Dictionary<string, Dictionary<string, string>> _dict;

        // The dictionary is large (275k words), so all voices share one copy per file
        static readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> SharedDicts =
            new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

        /// <summary>
        /// Lines of "word: pronunciation" set before InitModel. Lines starting with # are comments.
        /// The pronunciation is eSpeak IPA (ˈiːdə), or plain English words that sound right ("ee duh").
        /// </summary>
        [NonSerialized] public string lexiconText;
        Dictionary<string, string> _lexicon = new Dictionary<string, string>();

        public void InitModel()
        {
            if (string.IsNullOrEmpty(modelPath) || string.IsNullOrEmpty(configPath))
            {
                Debug.LogError("path not set");
                return;
            }

            if (_status != ModelStatus.Init)
            {
                Debug.LogError("invalid status");
                return;
            }

            status = ModelStatus.Loading;
            RunBackground(RunInitModel);
        }

        void RunInitModel(CancellationToken cts)
        {
            try
            {
                Debug.Log($"Load model at {modelPath}");

                if (!File.Exists(configPath))
                {
                    throw new Exception($"Phonemizer config not found at {configPath}");
                }

                string jsonString = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<TokenizerConfig>(jsonString);
                _tokenizer = new Tokenizer(config);
                if (_tokenizer == null)
                {
                    throw new Exception("Failed to deserialize tokenizer config.");
                }

                _session = new InferenceSession(modelPath);
                if (_session == null)
                {
                    throw new System.Exception("unable to load model");
                }

                lock (SharedDicts)
                {
                    if (!SharedDicts.TryGetValue(dictPath ?? string.Empty, out _dict))
                    {
                        if (File.Exists(dictPath))
                        {
                            string dictJson = File.ReadAllText(dictPath);
                            _dict = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(dictJson);
                            Debug.Log($"[OpenPhonemizer] Loaded dictionary from {dictPath}");
                        }
                        else
                        {
                            Debug.LogWarning($"[OpenPhonemizer] Dictionary file not found at {dictPath}. Proceeding without dictionary.");
                            _dict = new Dictionary<string, Dictionary<string, string>>();
                        }
                        SharedDicts[dictPath ?? string.Empty] = _dict;
                    }
                }

                LoadLexicon();

                PhonemizeInternal("warmup", "en_us", cts);

                Debug.Log("Load model done");
                PostStatus(ModelStatus.Ready);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"An unexpected error occurred: {ex.Message}");

                FreeModel();
                PostStatus(ModelStatus.Init);
            }
        }

        void FreeModel()
        {
            _session?.Dispose();
        }

        private class PromptPayload : IBackgroundPayload
        {
            public string Prompt;
            public string Lang;
        }

        public void Phonemize(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                Debug.LogError("empty prompt");
                return;
            }

            if (_session == null)
            {
                Debug.LogError("model not loaded");
                return;
            }

            if (status != ModelStatus.Ready)
            {
                Debug.LogError("invalid status");
                return;
            }

            status = ModelStatus.Generate;
            RunBackground(new PromptPayload() { Prompt = prompt, Lang = "en_us" }, RunPhonemize);
        }

        void RunPhonemize(PromptPayload payload, CancellationToken cts)
        {
            try
            {
                string response = PhonemizeInternal(payload.Prompt, payload.Lang, cts);
                PostResponse(response);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"An unexpected error occurred during RunDecode: {ex.Message}");
                PostResponse(string.Empty);
            }
            finally
            {
                PostStatus(ModelStatus.Ready);
            }
        }

        string PhonemizeInternal(string prompt, string lang, CancellationToken cts)
        {
            // Simple split by punctuation
            string pattern = @"([().,:;?!/""\-\s])"; // Punctuation, quotes, hyphens + whitespace
            string[] parts = Regex.Split(prompt, pattern);

            var resultParts = new List<string>();
            var punctSet = new HashSet<string>() { "(", ")", ".", ",", ":", ";", "?", "!", "/", " " };

            // Check dictionary availability for lang
            Dictionary<string, string> langDict = null;
            if (_dict != null && _dict.ContainsKey(lang))
            {
                langDict = _dict[lang];
            }

            // Tokens: words, spaces and punctuation, in order
            var tokens = new List<string>();
            var isWord = new List<bool>();
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                if (punctSet.Contains(part))
                {
                    tokens.Add(part); isWord.Add(false);
                    continue;
                }

                // "well-known" is read as two words; quotes are not spoken
                if (part == "-") { tokens.Add(" "); isWord.Add(false); continue; }
                if (part == "\"") continue;

                // Quoted words ('Halt') keep only inner apostrophes (guard's)
                string word = part.Trim('\'');
                if (word.Length == 0) continue;
                tokens.Add(word); isWord.Add(true);
            }

            // Words are read with their neighbours known ("close the gate" / "close to home")
            for (int i = 0; i < tokens.Count; i++)
            {
                if (!isWord[i]) { resultParts.Add(tokens[i]); continue; }
                int prev = NeighbourWord(tokens, isWord, i, -1);
                int next = NeighbourWord(tokens, isWord, i, 1);
                resultParts.Add(PhonemizeWord(tokens[i], langDict, lang, cts, true,
                    prev >= 0 ? tokens[prev].ToLowerInvariant() : null,
                    next >= 0 ? tokens[next].ToLowerInvariant() : null));
            }

            // "the" and "to" change with the next sound: "thee apple", "tuh the gate"
            for (int i = 0; i < tokens.Count; i++)
            {
                if (!isWord[i]) continue;
                int next = NeighbourWord(tokens, isWord, i, 1);
                if (next < 0) continue;
                string weak = EnglishPronunciation.WeakForm(tokens[i].ToLowerInvariant(), resultParts[next]);
                if (weak != null) resultParts[i] = weak;
            }

            // A sentence without an end mark is spoken with a cut-off tone
            string result = string.Join("", resultParts).TrimEnd();
            if (result.Length > 0 && ".?!".IndexOf(result[result.Length - 1]) < 0)
                result += ".";
            return result;
        }

        /// <summary>Index of the word next to token i in direction dir, or -1 when punctuation comes first.</summary>
        static int NeighbourWord(List<string> tokens, List<bool> isWord, int i, int dir)
        {
            for (int j = i + dir; j >= 0 && j < tokens.Count; j += dir)
            {
                if (isWord[j]) return j;
                if (tokens[j] != " ") return -1;
            }
            return -1;
        }

        /// <summary>Lexicon, then context rules, dictionary and English rules, then the neural model.</summary>
        string PhonemizeWord(string word, Dictionary<string, string> langDict, string lang, CancellationToken cts, bool useLexicon,
            string prevWord = null, string nextWord = null)
        {
            if (useLexicon && _lexicon.TryGetValue(word.ToLowerInvariant(), out string custom))
                return custom;

            string homograph = EnglishPronunciation.InContext(word.ToLowerInvariant(), prevWord, nextWord);
            if (homograph != null)
                return homograph;

            string ruled = EnglishPronunciation.Pronounce(word,
                w => langDict != null && langDict.TryGetValue(w, out string p) ? p : null);
            if (ruled != null)
                return ruled;

            return GuessWord(word, lang);
        }

        /// <summary>Reads lexiconText. Plain-letter pronunciations are phonemized like normal text.</summary>
        void LoadLexicon()
        {
            _lexicon = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(lexiconText)) return;

            _dict.TryGetValue("en_us", out var langDict);
            foreach (string raw in lexiconText.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;

                string word = line.Substring(0, colon).Trim().ToLowerInvariant();
                string value = line.Substring(colon + 1).Trim();
                if (value.Length == 0) continue;

                if (Regex.IsMatch(value, @"^[A-Za-z' \-]+$"))
                {
                    var parts = value.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => PhonemizeWord(p, langDict, "en_us", CancellationToken.None, false));
                    value = string.Join("", parts);
                }
                _lexicon[word] = value;
            }
        }

        string GuessWord(string word, string lang)
        {
            // Model Inference
            try
            {
                long[] inputTokens = _tokenizer.Tokenize(word, lang);

                var inputTensor = new DenseTensor<long>(inputTokens, new[] { 1, inputTokens.Length });
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("text", inputTensor)
                };

                using var results = _session.Run(inputs);
                var logits = results.First().AsTensor<float>();

                // Argmax & Dedup
                var decodedTokens = new List<long>();
                int sequenceLength = logits.Dimensions[1];
                int vocabSize = logits.Dimensions[2];

                long lastToken = -1;

                for (int t = 0; t < sequenceLength; t++)
                {
                    float maxVal = float.MinValue;
                    int maxIdx = 0;
                    for (int v = 0; v < vocabSize; v++)
                    {
                        float val = logits[0, t, v];
                        if (val > maxVal)
                        {
                            maxVal = val;
                            maxIdx = v;
                        }
                    }

                    if (t == 0 || maxIdx != lastToken)
                    {
                        decodedTokens.Add(maxIdx);
                        lastToken = maxIdx;
                    }
                }

                return _tokenizer.Decode(decodedTokens.ToArray());
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpenPhonemizer] Inference error for word '{word}': {e.Message}");
                return string.Empty;
            }
        }

        void PostResponse(string response)
        {
            unityContext?.Post(_ => OnResponseGenerated?.Invoke(response), null);
        }
    }
}
