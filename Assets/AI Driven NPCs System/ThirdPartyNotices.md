# Third-Party Software & Model Notices

This package includes third-party open-source software and native libraries, and downloads machine learning models on request. Each component is subject to its own license terms as outlined below.

- Included in `ThirdParty/`: LLMUnity, Whisper Unity, Piper TTS, ONNX Runtime and ONNX Runtime Unity. Each folder contains its original license file.

---

## 1. LLMUnity 3.0.3 (included, unmodified)

- **Repository**: [https://github.com/undreamai/LLMUnity](https://github.com/undreamai/LLMUnity) (tag `v3.0.3`)
- **Author**: undreamai
- **License**: Apache License 2.0. The full text is in `ThirdParty/LLMUnity/LICENSE.md`.
- **Its own third-party notices**: `ThirdParty/LLMUnity/Third Party Notices.md` (llama.cpp MIT, llamafile Apache 2.0, USearch Apache 2.0).
- **Changes**: none. The files are copied from the tag as published. The `Tests`, `Samples~` and repository files are left out.
- **Native libraries**: LLMUnity downloads the LlamaLib libraries (Apache 2.0, built on llama.cpp) from its GitHub releases on first use.

"LLM for Unity" and "LLMUnity" are names of undreamai. This package is not made or endorsed by undreamai.

LLMUnity uses **llama.cpp**:
- **Author**: Georgi Gerganov and contributors
- **License**: MIT License (Copyright (c) 2023-2024 Georgi Gerganov)

---

## 2. Whisper.unity & whisper.cpp

- **Repository**: [https://github.com/Macoron/whisper.unity](https://github.com/Macoron/whisper.unity)
- **Author**: Macoron
- **License**: MIT License

```text
MIT License

Copyright (c) 2023 Macoron

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Whisper.unity bundles bindings and model architecture based on:
- **whisper.cpp**: Georgi Gerganov and contributors (MIT License)
- **OpenAI Whisper**: OpenAI (MIT License)

**Changes in this package**:
- `MicrophoneRecord.cs`: if `Microphone.Start` returns no clip (no microphone, or the device is busy), recording does not start and a warning is logged.

---

## 3. Piper TTS (piper-no-espeak-unity)

- **Repository**: [https://github.com/lookbe/piper-no-espeak-unity](https://github.com/lookbe/piper-no-espeak-unity)
- **Authors**: lookbe, Rhasspy / Michael Hansen
- **License**: MIT License

```text
MIT License

Copyright (c) 2023 lookbe
Copyright (c) 2023 Michael Hansen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

**Changes in this package**:
- `PiperTTS.cs`: synthesized clips play from a queue, so the next sentence is made while the current one plays. Adds `IsPlaying`, `OnPlaybackFinished`, `StopPlayback` and a `lengthScale` setting. Text with no letters or digits is skipped.
- `PiperModel.cs`: `Prompt` takes a length multiplier for the voice's `length_scale`.
- `PhonemizerModel.cs`: words are also split on quotes, semicolons and hyphens, and quote apostrophes are trimmed before the dictionary lookup.

---

## 4. ONNX Runtime Unity Binding

- **Repository**: [https://github.com/asus4/onnxruntime-unity](https://github.com/asus4/onnxruntime-unity)
- **Author**: Satoshi MAJIMA (asus4)
- **License**: MIT License

```text
MIT License

Copyright (c) 2020 Satoshi MAJIMA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

The underlying Microsoft ONNX Runtime binaries are licensed by Microsoft Corporation under the MIT License.

**Changes in this package**: version 0.4.4 copied from npm. The iOS framework (`Plugins/iOS~`) and its iOS post-build script (`Editor/OrtPostProcessBuild.cs`) are removed. Two plugin paths in `OrtUnityEnv.cs` point to `ThirdParty/OnnxRuntime`.

---

## 5. Model Attributions

- **Qwen3.5-0.8B** (default language model, downloaded on request as the `unsloth/Qwen3.5-0.8B-GGUF` quantization, not included): made by the Alibaba Qwen team, licensed under the **Apache License 2.0** ([model card](https://huggingface.co/Qwen/Qwen3.5-0.8B)). Other models from the LLMUnity download list have their own licenses.
- **Piper Voices** (downloaded on request from [rhasspy/piper-voices](https://huggingface.co/rhasspy/piper-voices), not included). Licenses as stated in each voice's MODEL_CARD:
  - Setup default: `en_US-ljspeech-medium` (public domain, LJ Speech dataset) and `en_US-bryce-medium` (public domain, recorded by the voice's creator; [MODEL_CARD](https://huggingface.co/rhasspy/piper-voices/blob/main/en/en_US/bryce/medium/MODEL_CARD)).
  - Voice Browser: `en_US-ljspeech-high`, `en_US-kristin-medium`, `en_US-john-medium`, `en_US-norman-medium` (public domain); `en_US-kathleen-low`, `en_US-joe-medium`, `en_US-reza_ibrahim-medium` (CC0).
- **Phonemizer**: `model.onnx`, `phoneme_dict.json` and `tokenizer.json` from [lookbe/open-phonemizer-onnx](https://huggingface.co/lookbe/open-phonemizer-onnx) (downloaded on request).
- **Whisper GGML Model**: `ggml-tiny.bin` weights converted from OpenAI Whisper (MIT License).

