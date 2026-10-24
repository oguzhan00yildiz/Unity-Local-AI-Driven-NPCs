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
LLMUnity 3.0.3 (Apache 2.0), Whisper.unity (MIT), Piper TTS (MIT, modified) and ONNX Runtime 0.4.4 (MIT) are included in `ThirdParty/`. Do not import a second copy of these packages. See `ThirdPartyNotices.md` for licenses.

**Downloads**
- Model files come from Hugging Face during setup, after the user agrees: the Whisper tiny model, the phonemizer, 2 Piper voices and the Qwen3.5-0.8B model, about 770 MB in total.
- LLMUnity downloads the LlamaLib native libraries from GitHub the first time it runs.
- Builds include the downloaded model files from `StreamingAssets`, so players download nothing.

---

## Technical Details

- Unity 6 (6000.0) or newer. Tested on 6000.3.
- Windows 10/11, 64-bit.
- Required packages: Input System, uGUI, Newtonsoft Json, Collections.
- The demo scene and prefabs use URP. The scripts work in any render pipeline. In Built-in or HDRP, use your own scene and characters.
- Speech recognition and voices: English.
- Minimum hardware: 8 GB RAM, 4-core CPU. Runs on the CPU only.
- Recommended hardware: 16 GB RAM, NVIDIA or AMD GPU with 4 GB VRAM or more.
- Documentation: `README.md` and `SETUP_GUIDE_EN.md` in the package folder.
- Full C# source code.
