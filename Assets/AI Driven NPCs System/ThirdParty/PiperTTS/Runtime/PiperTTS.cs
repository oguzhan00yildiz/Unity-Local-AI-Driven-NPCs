using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PiperTTS
{
    public static class NumberExtensions
    {
        public static string ToWords(this int number) => NumberToText.Convert(number);
        public static string ToWords(this long number) => NumberToText.Convert(number);
        public static string ToWords(this double number) => NumberToText.Convert(number);
    }

    public class PiperTTS : MonoBehaviour
    {
        [Header("Piper")]
        public string piperModelPath = string.Empty;
        public string piperConfigPath = string.Empty;

        [Header("Phonemizer")]
        public string phonemizerModelPath = string.Empty;
        public string phonemizerConfigPath = string.Empty;
        public string phonemizerDictPath = string.Empty;


        [Header("Config")]
        [Range(0.0f, 1.0f)]
        public float commaDelay = 0.1f;

        [Range(0.0f, 1.0f)]
        public float periodDelay = 0.5f;

        [Range(0.0f, 1.0f)]
        public float questionExclamationDelay = 0.6f;

        protected PiperModel piper;
        protected PhonemizerModel phonemizer;
        protected AudioSource audioSource;

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

        public void InitModel()
        {
            if (string.IsNullOrEmpty(piperModelPath) || string.IsNullOrEmpty(piperConfigPath))
            {
                return;
            }

            if (string.IsNullOrEmpty(phonemizerModelPath) || string.IsNullOrEmpty(phonemizerConfigPath))
            {
                return;
            }

            if (_status != ModelStatus.Init)
            {
                Debug.LogError("invalid status");
                return;
            }

            status = ModelStatus.Loading;
            StartCoroutine(RunInitModel());
        }

        IEnumerator RunInitModel()
        {
            Debug.Log($"Load piper tts model");

            piper.modelPath = piperModelPath;
            piper.configPath = piperConfigPath;
            piper.InitModel();

            phonemizer.modelPath = phonemizerModelPath;
            phonemizer.configPath = phonemizerConfigPath;
            phonemizer.dictPath = phonemizerDictPath;
            phonemizer.InitModel();

            yield return new WaitWhile(() => phonemizer.status != ModelStatus.Ready);
            yield return new WaitWhile(() => piper.status != ModelStatus.Ready);

            Debug.Log("Load model done");

            status = ModelStatus.Ready;
        }

        public void Prompt(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                return;
            }

            if (status != ModelStatus.Ready)
            {
                Debug.LogError("invalid status");
                return;
            }

            status = ModelStatus.Generate;
            StartCoroutine(SynthesizeAndPlay(prompt));
        }

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
                text = Regex.Replace(text, pair.Key, match =>
                {
                    string replacement = pair.Value;
                    if (char.IsUpper(match.Value[0]))
                    {
                        return char.ToUpper(replacement[0]) + replacement.Substring(1);
                    }
                    return replacement;
                }, RegexOptions.IgnoreCase);
            }

            return text;
        }

        string PreProcessText(string text)
        {
            // 1. Expand contractions (e.g. "I'm" -> "I am", "don't" -> "do not")
            text = NormalizeContractions(text);

            // 2. Expand numbers to words
            text = Regex.Replace(text, @"\d+(\.\d+)?", match =>
            {
                try
                {
                    if (double.TryParse(match.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double number))
                    {
                        return number.ToWords();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e.Message);
                }
                return match.Value;
            });

            return text;
        }

        IEnumerator SynthesizeAndPlay(string prompt)
        {
            string text = PreProcessText(prompt);
            
            // Clean non-standard characters while preserving letters, digits, whitespace, and punctuation [. , ? ! ; : ' - " ]
            string cleanedChunk = Regex.Replace(text, @"[^\w\s,.?!;:'""-]", " ").Trim();

            if (!string.IsNullOrEmpty(cleanedChunk))
            {
                phonemizer.Phonemize(cleanedChunk);

                yield return new WaitUntil(() => phonemizer.status == ModelStatus.Ready);
                yield return new WaitUntil(() => piper.status == ModelStatus.Ready);

                // Wait for all audio samples to finish playing
                yield return new WaitUntil(() => !audioSource.isPlaying);
            }

            status = ModelStatus.Ready;
        }

        private void Awake()
        {
            piper = GetComponentInChildren<PiperModel>();
            phonemizer = GetComponentInChildren<PhonemizerModel>();
            audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            piper.OnStatusChanged += OnModelStatusChanged;
            phonemizer.OnStatusChanged += OnModelStatusChanged;

            phonemizer.OnResponseGenerated += OnPhonemeResponse;
            piper.OnResponseGenerated += OnResponseGenerated;
        }

        private void OnDisable()
        {
            piper.OnStatusChanged -= OnModelStatusChanged;
            phonemizer.OnStatusChanged -= OnModelStatusChanged;

            phonemizer.OnResponseGenerated -= OnPhonemeResponse;
            piper.OnResponseGenerated -= OnResponseGenerated;
        }

        void OnModelStatusChanged(ModelStatus status)
        {
            if (status == ModelStatus.Error)
            {
                StopAllCoroutines();
                this.status = ModelStatus.Error;
            }
        }

        void OnPhonemeResponse(string phonemeString)
        {
            piper.Prompt(phonemeString);
        }

        void OnResponseGenerated(float[] audioChunk, int sampleRate)
        {
            AudioClip clip = AudioClip.Create("GeneratedSpeech", audioChunk.Length, 1, sampleRate, false);
            clip.SetData(audioChunk, 0);

            audioSource.PlayOneShot(clip);
        }
    }
}
