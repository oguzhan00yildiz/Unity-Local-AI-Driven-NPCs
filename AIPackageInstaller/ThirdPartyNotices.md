# Third-Party Software & Model Notices

This package integrates or facilitates the installation of third-party open-source software, native libraries, and machine learning models. Each component is subject to its own respective license terms as outlined below.

---

## 1. LLMUnity & llama.cpp

- **Repository**: [https://github.com/undreamai/LLMUnity](https://github.com/undreamai/LLMUnity)
- **Author**: undreamai
- **License**: MIT License

```text
MIT License

Copyright (c) 2023 undreamai

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

LLMUnity bundles binaries and source from **llama.cpp**:
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

---

## 5. Model Attributions

- **Qwen Language Models**: Preconfigured default `Qwen3.5-0.8B` / `Qwen2.5` language models are developed by the Alibaba Qwen team and licensed under the **Apache 2.0 License** / **Qwen Community License**.
- **Piper Voice Checkpoints**: Piper neural voice checkpoints (e.g. `en_US-amy-low`, `en_US-reza_ibrahim-medium`, `en_US-jenny-dioco-medium`) are trained by the Rhasspy project using open datasets (LibriTTS, LJSpeech, VCTK) and released under open-access / Public Domain / CC0-compatible licenses.
- **Whisper GGML Model**: `ggml-tiny.bin` weights converted from OpenAI Whisper (MIT License).

