# Local AI Driven NPCs System — Documentation & Quick Start

Adds **100% local, on-device AI NPCs** (LLM + Text-to-Speech + Speech-to-Text) to any Unity project.  
Zero cloud subscriptions, zero network latency, complete data privacy.

---

## 📦 Installation

1. Open **Window → Package Manager** and switch to **Packages: My Assets**.
2. Import **Local AI Driven NPCs System**.
3. The **AI System Setup** window asks for permission, then downloads the required models.

**Included in this package** (in `ThirdParty/`, see `ThirdPartyNotices.md`):
- LLMUnity 3.0.3 (Apache 2.0), Piper TTS (MIT), Whisper Unity (MIT) and ONNX Runtime 0.4.4 (MIT).

**Required Unity packages** (install them from the Package Manager before you import):
- **Input System** (`com.unity.inputsystem`). The runtime assembly needs it. Set **Project Settings → Player → Active Input Handling** to **Input System Package** or **Both**.
- **uGUI** (`com.unity.ugui`). The chat UI uses it.
- **Universal RP** (`com.unity.render-pipelines.universal`). Only the demo scene and prefabs need it. See [Render Pipeline](#-compatibility--system-requirements).

- **Newtonsoft Json** (`com.unity.nuget.newtonsoft-json`). LLMUnity uses it.
- **Collections** (`com.unity.collections`). ONNX Runtime Unity uses it for Piper TTS.

> ⚠️ **Conflict:** Remove any separate LLM for Unity, Piper, Whisper or ONNX Runtime (`com.github.asus4.onnxruntime`) copies first. Two copies cause duplicate assembly errors.

> ⚠️ **Important:** Keep the Unity Editor open and focused during model downloads. Unity can pause background downloads when it loses focus.

---

## 🤖 Model Files

The **AI System Setup** window downloads the models (about 770 MB) into `Assets/StreamingAssets/`:

| Component | Model File | Size | Purpose |
| :--- | :--- | :--- | :--- |
| **STT** | `Whisper/ggml-tiny.bin` | 74 MB | On-device speech recognition (Required) |
| **TTS Core** | `PiperTTS/model.onnx` + dicts | 70 MB | Phonemizer and tokenizer dictionary (Required) |
| **Female Voice** | `PiperTTS/ljspeech/en_US-ljspeech-medium.onnx` | 61 MB | English female voice, public domain (Required) |
| **Male Voice** | `PiperTTS/bryce/en_US-bryce-medium.onnx` | 61 MB | English male voice, public domain (Required) |
| **LLM (Default)** | `Qwen3.5-0.8B-Q4_K_M.gguf` | ~500 MB | Fast, lightweight on-device language model |

If the window ever needs to be reopened: **Tools → AI Packages → AI System Setup** (or **Download Model Files**).

---

## 🎮 How to Test the Demo Scene

1. In the Project window, navigate to:  
   `Assets/AI Driven NPCs System/Scenes/AIOScene.unity`  
   *(Or if imported via UPM: `Assets/Samples/AI Driven NPCs System/<version>/Scenes/AIOScene.unity`)*
2. Open the scene and press **Play**.
3. Walk toward the NPC using **WASD**.
4. Press **`E`** to start a voice or text conversation.

For detailed instructions on adding NPCs to your own scenes, creating custom personality presets, or using different language models, please see [`SETUP_GUIDE_EN.md`](SETUP_GUIDE_EN.md).

---

## 🛠️ Menu Tools & Utilities

Access all tools via **Tools → AI Packages**:
- **AI System Setup**: Check that LLM for Unity is imported and manage model downloads.
- **Download Model Files**: Direct shortcut to trigger model verification and background download.
- **Voice Browser**: Browse and download additional Piper TTS voices. All listed voices are public domain or CC0.
- **System Health & GPU**: Inspect hardware specs (GPU, VRAM, CPU cores) and toggle GPU offloading layers for maximum LLM generation speed.
---

## 💻 Compatibility & System Requirements

- **Unity Version:** Unity 6 (6000.0) or newer. Tested on 6000.3.
- **Render Pipeline:** The demo scene and prefabs use URP. They use the URP `Lit` material and the URP camera and light components. In Built-in or HDRP projects the demo shows pink materials and missing scripts. The scripts work in all pipelines. In a Built-in or HDRP project, add `AISystem` to your own scene and put `NPCAgent` on your own characters.
- **Supported Operating System:** Windows 10/11 (64-bit)
- **Hardware Requirements:**
  - Minimum: 8 GB RAM, 4-core CPU (runs 100% on CPU if desired)
  - Recommended: 16 GB RAM, dedicated NVIDIA / AMD GPU (4+ GB VRAM) for accelerated inference

---

## 📄 Licensing & Third-Party Credits

This project includes integrations and bindings for open-source AI libraries. Please see [`ThirdPartyNotices.md`](ThirdPartyNotices.md) for full license details:
- **LLMUnity**: Apache 2.0 (undreamai). **llama.cpp**: MIT License (Georgi Gerganov)
- **Whisper.unity & whisper.cpp**: MIT License (Macoron, OpenAI, Georgi Gerganov)
- **Piper TTS**: MIT License (lookbe, Rhasspy)
- **ONNX Runtime Unity**: MIT License (asus4, Microsoft)
- **Qwen3.5-0.8B (default LLM, downloaded on request)**: Apache 2.0 (Alibaba Qwen team)

---

## 💬 Support & Contact

If you have questions, encounter issues, or need help integrating the system into your project:
- **GitHub Issues:** [Unity-Local-AI-Driven-NPCs Issues](https://github.com/oguzhan00yildiz/Unity-Local-AI-Driven-NPCs/issues)
- **Email Support:** [oguzhan00yildiz@gmail.com](mailto:oguzhan00yildiz@gmail.com)
- **Community Repository:** [GitHub Repository](https://github.com/oguzhan00yildiz/Unity-Local-AI-Driven-NPCs)
