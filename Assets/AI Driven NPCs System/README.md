# AI Driven NPCs System — Documentation & Quick Start

Adds **100% local, on-device AI NPCs** (LLM + Text-to-Speech + Speech-to-Text) to any Unity project.  
Zero cloud subscriptions, zero network latency, complete data privacy.

---

## 📦 Installation

1. Open **Window → Package Manager** and switch to **Packages: My Assets**.
2. Import **AI Driven NPCs System**.
3. The **AI System Setup** window asks for permission, then downloads the required models.

**Included in this package** (in `ThirdParty/`, see `ThirdPartyNotices.md`):
- LLMUnity 3.0.3 (Apache 2.0), Piper TTS (MIT), Whisper Unity (MIT) and ONNX Runtime 0.4.4 (MIT).

**Unity packages used:** Input System, Collections, Newtonsoft Json and uGUI.

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

- **Unity Version:** Unity 2022.3 LTS or Unity 6 (6000.x)+
- **Render Pipeline:** Universal Render Pipeline (URP) out of the box (Built-in and HDRP compatible by assigning pipeline-appropriate materials to the sample scene)
- **Supported Operating System:** Windows 10/11 (64-bit)
- **Hardware Requirements:**
  - Minimum: 8 GB RAM, 4-core CPU (runs 100% on CPU if desired)
  - Recommended: 16 GB RAM, dedicated NVIDIA / AMD GPU (4+ GB VRAM) for accelerated inference

---

## 📄 Licensing & Third-Party Credits

This project includes integrations and bindings for open-source AI libraries. Please see [`ThirdPartyNotices.md`](ThirdPartyNotices.md) for full license details:
- **LLMUnity & llama.cpp**: MIT License (undreamai, Georgi Gerganov)
- **Whisper.unity & whisper.cpp**: MIT License (Macoron, OpenAI, Georgi Gerganov)
- **Piper TTS**: MIT License (lookbe, Rhasspy)
- **ONNX Runtime Unity**: MIT License (asus4, Microsoft)
- **Qwen Language Models**: Apache 2.0 / Qwen Community License

---

## 💬 Support & Contact

If you have questions, encounter issues, or need help integrating the system into your project:
- **GitHub Issues:** [Unity-Local-AI-Driven-NPCs Issues](https://github.com/oguzhan00yildiz/Unity-Local-AI-Driven-NPCs/issues)
- **Email Support:** [oguzhan00yildiz@gmail.com](mailto:oguzhan00yildiz@gmail.com)
- **Community Repository:** [GitHub Repository](https://github.com/oguzhan00yildiz/Unity-Local-AI-Driven-NPCs)
