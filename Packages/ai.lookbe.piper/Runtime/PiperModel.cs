using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace PiperTTS
{
    public class PiperModel : BackgroundRunner
    {
        [Header("Model")]
        public string modelPath = string.Empty;
        public string configPath = string.Empty;

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
        public delegate void ResponseGeneratedDelegate(float[] response, int sampleRate);
        public event ResponseGeneratedDelegate OnResponseGenerated;

        InferenceSession _session;
        private PiperConfig _piperConfig;
        private float[] _inferenceParams;

        private bool _hasSidKey = false;
        private int _speakerId = 0;

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
                    throw new Exception($"Piper config not found at {configPath}");
                }

                string configJson = File.ReadAllText(configPath);
                _piperConfig = JsonConvert.DeserializeObject<PiperConfig>(configJson);

                if (_piperConfig == null)
                {
                    throw new Exception("Failed to deserialize Piper config.");
                }

                _inferenceParams = new float[3]
                {
                    _piperConfig.inference.noise_scale,
                    _piperConfig.inference.length_scale,
                    _piperConfig.inference.noise_w
                };

                _session = new InferenceSession(modelPath);
                if (_session == null)
                {
                    throw new System.Exception("unable to load model");
                }

                if (_session.InputMetadata.ContainsKey("sid"))
                {
                    _hasSidKey = true;
                    Debug.Log("Piper Model requires speaker ID (sid).");
                }

                PromptInternal("_", cts);

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
        }

        public void Prompt(string prompt)
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
            RunBackground(new PromptPayload() { Prompt = prompt }, RunPrompt);
        }

        void RunPrompt(PromptPayload payload, CancellationToken cts)
        {
            try
            {
                float[] response = PromptInternal(payload.Prompt, cts);
                PostResponse(response);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"An unexpected error occurred during RunDecode: {ex.Message}");
                PostResponse(new float[0]);
            }
            finally
            {
                PostStatus(ModelStatus.Ready);
            }
        }

        float[] PromptInternal(string prompt, CancellationToken cts)
        {
            // The OpenPhonemizer returns phonemes. We need to split them into individual characters for the tokenizer
            // e.g. "h@loU" -> ["h", "@", "l", "o", "U"]
            string[] phonemeArray = prompt.ToCharArray().Select(c => c.ToString()).ToArray();

            // Use internal tokenizer logic
            int[] phonemeTokensInt = Tokenize(phonemeArray);
            long[] phonemeTokens = phonemeTokensInt.Select(x => (long)x).ToArray();

            float[] scales = _inferenceParams;
            long[] inputLength = { phonemeTokens.Length };
            float[] audioData = new float[0];

            try
            {
                var inputs = new List<NamedOnnxValue>();

                // 1. input (phoneme IDs)
                var inputTensor = new DenseTensor<long>(phonemeTokens, new[] { 1, phonemeTokens.Length });
                inputs.Add(NamedOnnxValue.CreateFromTensor("input", inputTensor));

                // 2. input_lengths
                var lengthTensor = new DenseTensor<long>(inputLength, new[] { 1 });
                inputs.Add(NamedOnnxValue.CreateFromTensor("input_lengths", lengthTensor));

                // 3. scales
                var scalesTensor = new DenseTensor<float>(scales, new[] { 3 });
                inputs.Add(NamedOnnxValue.CreateFromTensor("scales", scalesTensor));

                // 4. sid (if needed)
                if (_hasSidKey)
                {
                    var sidTensor = new DenseTensor<long>(new long[] { _speakerId }, new[] { 1 });
                    inputs.Add(NamedOnnxValue.CreateFromTensor("sid", sidTensor));
                }

                using (var results = _session.Run(inputs))
                {
                    // Piper output is usually named "output"
                    var outputResult = results.FirstOrDefault();
                    if (outputResult == null)
                    {
                        throw new Exception("Model returned no results.");
                    }

                    var outputTensor = outputResult.AsTensor<float>();
                    float[] sample = outputTensor.ToArray();

                    if (sample == null || sample.Length == 0)
                    {
                        throw new Exception("Failed to generate audio data or the data is empty.");
                    }

                    audioData = sample;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Inference failed: {e.Message}");
            }

            return audioData;
        }

        int[] Tokenize(string[] phonemes)
        {
            if (_piperConfig == null)
            {
                return new int[0];
            }

            int estimatedCapacity = (phonemes != null ? phonemes.Length * 2 : 0) + 3;
            var tokenizedList = new List<int>(estimatedCapacity) { 1, 0 }; // Start tokens? check piper defaults. usually 1 (BOS)

            if (phonemes != null && phonemes.Length > 0)
            {
                foreach (string phoneme in phonemes)
                {
                    if (_piperConfig.PhonemeIdMap.TryGetValue(phoneme, out int[] ids) && ids.Length > 0)
                    {
                        tokenizedList.Add(ids[0]);
                        tokenizedList.Add(0); // Separator? Piper uses 0 as standard separator?
                    }
                    else
                    {
                        Debug.LogWarning($"Token not found for phoneme: '{phoneme}'. It will be skipped.");
                    }
                }
            }

            tokenizedList.Add(2); // End token?

            return tokenizedList.ToArray();
        }

        void PostResponse(float[] response)
        {
            unityContext?.Post(_ => OnResponseGenerated?.Invoke(response, _piperConfig.audio.sample_rate), null);
        }

        // Internal Config Classes
        [Serializable]
        public class AudioConfig
        {
            public int sample_rate { get; set; }
            public string quality { get; set; }
        }

        [Serializable]
        public class ESpeakConfig
        {
            public string voice { get; set; }
        }

        [Serializable]
        public class InferenceConfig
        {
            public float noise_scale { get; set; }
            public float length_scale { get; set; }
            public float noise_w { get; set; }
        }

        [Serializable]
        public class PiperConfig
        {
            public AudioConfig audio { get; set; }
            public ESpeakConfig espeak { get; set; }
            public InferenceConfig inference { get; set; }
            public string phoneme_type { get; set; }

            [JsonProperty("phoneme_id_map")]
            public Dictionary<string, int[]> PhonemeIdMap { get; set; }
        }
    }
}
