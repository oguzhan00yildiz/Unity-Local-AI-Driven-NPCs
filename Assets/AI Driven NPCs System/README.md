# AI Driven NPCs System — Documentation & Quick Start

Adds **100% local, on-device AI NPCs** (LLM + Text-to-Speech + Speech-to-Text) to any Unity project.  
Zero cloud subscriptions, zero network latency, complete data privacy.

---

## 📦 Installation Options

### Option A: Unity Asset Store Import (Recommended)
1. Open **Window → Package Manager**.
2. Switch to **Packages: My Assets**.
3. Locate **AI Driven NPCs System** and click **Download / Import**.
4. Import all package assets into your project.
5. The **AI System Setup** window will automatically appear to configure dependencies and download models.

### Option B: Add via Git URL (UPM Package)
1. Open **Window → Package Manager**.
2. Click **+** → **Add package from git URL…**
3. Paste:
   ```text
   https://github.com/oguzhan00yildiz/Unity-Local-AI-Driven-NPCs.git?path=AIPackageInstaller
   ```
4. Click **Add**.

**What happens automatically:**
- `manifest.json` is configured with an NPM scoped registry for ONNX Runtime (preventing native stub crashes).
- ONNX Runtime 0.4.4, LLMUnity, Piper TTS, and Whisper STT are installed automatically.
- The **AI System Setup** window opens to verify packages and download the required local models.

> ⚠️ **Important:** Keep the Unity Editor open and focused during initial setup. Switching away can cause Unity to pause background tasks, interrupting native binary resolution or model downloads.

---

## 🤖 Model Files

The **AI System Setup** window automatically downloads the required lightweight voice and speech models (~265 MB) into `Assets/StreamingAssets/`:

| Component | Model File | Size | Purpose |
| :--- | :--- | :--- | :--- |
| **STT** | `Whisper/ggml-tiny.bin` | 74 MB | On-device speech recognition (Required) |
| **TTS Core** | `PiperTTS/model.onnx` + dicts | 70 MB | Phonemizer and tokenizer dictionary (Required) |
| **Female Voice** | `PiperTTS/Amy/en_US-amy-low.onnx` | 60 MB | English female voice (Required) |
| **Male Voice** | `PiperTTS/ibrahim/en_US-reza_ibrahim-medium.onnx` | 61 MB | English male voice (Required) |
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
- **AI System Setup**: View package status, verify ONNX/Whisper/Piper/LLM installations, and manage model downloads.
- **Download Model Files**: Direct shortcut to trigger model verification and background download.
- **Voice Browser**: Browse and download additional Piper TTS voices (Amy, Ibrahim, LJSpeech, Jenny, Ryan, etc.).
- **System Health & GPU**: Inspect hardware specs (GPU, VRAM, CPU cores) and toggle GPU offloading layers for maximum LLM generation speed.
- **Force Install Dependencies**: Re-run the automated dependency installer and registry patcher.

---

## 📄 Licensing & Third-Party Credits

This project includes integrations and bindings for open-source AI libraries. Please see [`ThirdPartyNotices.md`](ThirdPartyNotices.md) for full license details:
- **LLMUnity & llama.cpp**: MIT License (undreamai, Georgi Gerganov)
- **Whisper.unity & whisper.cpp**: MIT License (Macoron, OpenAI, Georgi Gerganov)
- **Piper TTS**: MIT License (lookbe, Rhasspy)
- **ONNX Runtime Unity**: MIT License (asus4, Microsoft)
- **Qwen Language Models**: Apache 2.0 / Qwen Community License
