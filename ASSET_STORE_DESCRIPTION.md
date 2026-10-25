# Asset Store Page Text

Draft text for the publisher portal. Copy each section into its field.

---

## Summary

Talking NPCs that run fully on the player's PC. Local LLM, speech recognition and text-to-speech. No cloud service, no API key, no per-request cost.

---

## Description

AI Driven NPCs System adds NPCs that listen, think and answer with a voice. Everything runs on the player's PC. No internet connection is needed after setup, and no player data leaves the machine.

**How it works**
- The player speaks or types. Whisper turns speech into text.
- A local language model (llama.cpp through LLMUnity) writes the NPC's reply.
- Piper TTS speaks the reply. Speech starts after the first sentence, while the model still writes the rest.

**Features**
- Drag the AISystem prefab into a scene, then add the NPC prefab or the NPCAgent component to any character.
- No Inspector wiring between components.
- Personality presets as ScriptableObjects: name, system prompt and voice. Two sample presets are included.
- A different voice and a separate chat history for each NPC.
- Chat panel with text input, voice input, microphone selection and mute.
- Voice activity detection: the NPC hears the player when the player stops talking.
- Voice Browser window with 9 English voices, all public domain or CC0.
- System Health window shows GPU, VRAM and CPU, and turns GPU offload on or off.
- Use any GGUF model. The default is Qwen3.5-0.8B (Apache 2.0), about 500 MB.
- Demo scene with a third-person player and an NPC.

**Setup**
1. Import the package.
2. The setup window asks for permission and downloads the model files (about 770 MB).
3. Open the demo scene and press Play.

**Included**
LLMUnity 3.0.3 (Apache 2.0), Whisper.unity (MIT, modified), Piper TTS (MIT, modified) and ONNX Runtime 0.4.4 (MIT) are included in `ThirdParty/`. Do not import a second copy of these packages. See `ThirdPartyNotices.md` for licenses.

**Downloads**
- Model files come from Hugging Face during setup, after the user agrees: the Whisper tiny model, the phonemizer, 2 Piper voices and the Qwen3.5-0.8B model, about 770 MB in total.
- LLMUnity downloads the LlamaLib native libraries from GitHub the first time it runs.
- Builds include the downloaded model files from `StreamingAssets`, so players download nothing.

---

## Technical Details

```text
PACKAGE CONTENTS

- Full C# source code
- Services: AISystemManager, VoiceInputService, VoiceOutputService, NPCAgent, ChatUIController
- Prefabs: AISystem, NPC, LLM, Player
- Personality presets as ScriptableObjects (2 samples included)
- Chat panel with streaming text, voice input, microphone selection and mute
- Editor windows: AI System Setup (model downloads), Voice Browser (extra voices), System Health (GPU offload)
- Demo scene with a third-person player and an NPC
- Documentation: README.md and SETUP_GUIDE_EN.md


TECHNICAL SPECIFICATIONS

- LLM: llama.cpp through LLMUnity 3.0.3, GGUF models
- Speech to text: Whisper.unity, ggml models (default: tiny)
- Text to speech: Piper TTS on ONNX Runtime 0.4.4
- Language: English (speech recognition and voices)
- Unity version: Unity 6 (6000.0) or newer, tested on 6000.3
- Platform: Windows 10/11, 64-bit
- Required packages: Input System, uGUI, Newtonsoft Json, Collections
- Render pipeline: the demo scene and prefabs use URP. The scripts work in Built-in and HDRP with your own scene and characters.


HARDWARE REQUIREMENTS

- RAM: 8 GB minimum, 16 GB recommended
- CPU: 4 cores minimum. Runs on the CPU only.
- GPU: optional. NVIDIA or AMD with 4 GB VRAM or more for faster replies.
- Storage: about 1 GB for the models


MODELS AND DOWNLOADS

The setup window downloads these files to Assets/StreamingAssets after the user agrees:
- Whisper tiny (speech to text): 74 MB
- Piper phonemizer and 2 voices: 190 MB
- Qwen3.5-0.8B Q4_K_M (language model): 500 MB
LLMUnity downloads the LlamaLib native libraries from GitHub on first use.
Builds include these files, so players download nothing.
Any other GGUF model can be selected in the LLM component.


THIRD-PARTY SOFTWARE

Included in ThirdParty/. Do not import a second copy.
- LLMUnity 3.0.3 (Apache 2.0)
- Whisper.unity (MIT, modified)
- Piper TTS (MIT, modified)
- ONNX Runtime and ONNX Runtime Unity 0.4.4 (MIT)
Full license texts are in ThirdPartyNotices.md.
```

---

## AI Use Disclosure

```text
This package runs open-source AI models locally inside Unity. No cloud service is used.

* Language model: llama.cpp (MIT) through LLMUnity (Apache 2.0). The default model is Qwen3.5-0.8B (Apache 2.0). Users can load other GGUF models, which have their own licenses.
* Speech to text: whisper.cpp through Whisper.unity (MIT), with OpenAI Whisper weights (MIT).
* Text to speech: Piper TTS (MIT). The listed voices are public domain or CC0.


No models are included in the package. The setup window downloads them from Hugging Face after the user agrees. Model sources and licenses are in ThirdPartyNotices.md.

AI IN THE CREATION OF THE PACKAGE

AI coding assistants helped write and edit C# code, editor windows, documentation and license notices, and helped find bugs. The author chose the design and features, reviewed the changes, and tested the package in Unity. No AI-generated art, audio or 3D models are included. No proprietary data was used.
```
