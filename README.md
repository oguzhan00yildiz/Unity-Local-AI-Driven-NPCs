# Local AI Driven NPCs System

[![Unity Version](https://img.shields.io/badge/Unity-6-blue.svg?logo=unity)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-lightgrey.svg?logo=windows)](https://unity.com/)
[![Package Version](https://img.shields.io/badge/Package-v2.4.2-orange.svg)](AIPackageInstaller/package.json)

A free Unity package for NPCs that listen, think and talk. Three models run on the player's computer:

- **Whisper** turns the player's speech into text ([whisper.unity](https://github.com/Macoron/whisper.unity)).
- **An LLM** writes the answer ([LLMUnity](https://github.com/undreamai/LLMUnity), any GGUF model).
- **Piper** speaks the answer ([piper-no-espeak-unity](https://github.com/lookbe/piper-no-espeak-unity)).

No cloud API and no API fees. The package collects no data. It only connects to Hugging Face to download the models.

## Install

### Asset Store (recommended)

LLMUnity, Whisper, Piper and ONNX Runtime come bundled in `ThirdParty/`.

1. Import the package from the [Unity Asset Store](https://assetstore.unity.com/packages/slug/405036). Confirm the Unity packages when asked.
2. Click **Import**.
3. Click **Download** in the **AI System Setup** window. You can cancel the download.
4. Open `Assets/AI Driven NPCs System/Scenes/AIOScene.unity`.
5. Press **Play**, walk to an NPC and press **E**.

Remove other copies of LLMUnity, Piper, Whisper or ONNX Runtime first. Two copies cause duplicate assembly errors.

### Git URL

1. In **Window → Package Manager**, click **+** → **Add package from git URL…** and paste:
   ```text
   https://github.com/oguzhan00yildiz/Unity-Local-AI-Driven-NPCs.git?path=AIPackageInstaller
   ```
2. The installer adds the npm registry for ONNX Runtime and installs LLMUnity, Piper and Whisper from Git.
3. Import the **AI Driven NPCs System** sample, then open `Assets/Samples/AI Driven NPCs System/2.4.2/Scenes/AIOScene.unity`.

Keep Unity focused during setup. Unity can pause downloads in the background.

### Downloaded models

The setup downloads 9 files to `Assets/StreamingAssets/`:

| Stage | Files | Size |
| :--- | :--- | :--- |
| STT | `Whisper/ggml-tiny.bin` | 74 MB |
| TTS core | `PiperTTS/model.onnx`, `phoneme_dict.json`, `tokenizer.json` | ~70 MB |
| Voices | `en_US-bryce-medium`, `en_US-ljspeech-medium` (+ `.json`) | 61 MB each |
| LLM | `Qwen3.5-0.8B-Q4_K_M.gguf` | 508 MB |

The default LLM is small and runs on the CPU. Small models drop out of their role more often. For a real game, use a bigger model: select the `LLM` object in the `AISystem` prefab and pick a model in the **Model** dropdown.

## How it works

```mermaid
graph TD
    Player -->|Press E| NPCAgent
    NPCAgent -->|OpenChat| Manager[AISystemManager]
    VoiceIn[VoiceInputService / Whisper] -->|OnTranscription| Manager
    UI[ChatUIController] -->|Typed text| Manager
    Manager -->|Prompt + preset| LLM[LLMAgent]
    LLM -->|Streamed tokens| Manager
    Manager --> UI
    Manager --> VoiceOut[VoiceOutputService / Piper]
    VoiceOut -->|Finished| Manager
```

- The chat panel shows the NPC state: **Listening**, **Thinking** or **Talking**.
- Recording stops after 1 second of silence. The microphone is off while the NPC thinks and talks.
- Piper speaks each sentence while the LLM still writes the next one.
- Whisper and the LLM load when the scene starts, so the first answer has no extra delay.
- The player can type in the chat panel. The chat panel works without the microphone.
- Walking away ends the conversation.

| Script | Job |
| :--- | :--- |
| [`AISystemManager`](Assets/AI%20Driven%20NPCs%20System/Scripts/AISystem/Core/AISystemManager.cs) | Passes data between the services. Handles the chat, cursor and player movement. |
| [`ModelBootstrapper`](Assets/AI%20Driven%20NPCs%20System/Scripts/AISystem/Core/ModelBootstrapper.cs) | Loads Whisper and the LLM when the scene starts. |
| [`NPCAgent`](Assets/AI%20Driven%20NPCs%20System/Scripts/AISystem/NPC/NPCAgent.cs) | Interaction area, prompt cue and personality preset of one NPC. |
| [`VoiceInputService`](Assets/AI%20Driven%20NPCs%20System/Scripts/AISystem/Services/VoiceInputService.cs) | Microphone, silence detection and Whisper. |
| [`VoiceOutputService`](Assets/AI%20Driven%20NPCs%20System/Scripts/AISystem/Services/VoiceOutputService.cs) | Splits text at `.` `!` `?` and plays it through Piper. |
| [`ChatUIController`](Assets/AI%20Driven%20NPCs%20System/Scripts/AISystem/UI/ChatUIController.cs) | Chat history, text input, microphone choice and mute. |

## NPC personalities

1. **Create → AI System → Personality Preset**.
2. Set **NPC Name**, **System Prompt** and **Voice**:
   ```text
   You are Captain Valerie, a sky pirate. Speak with confidence.
   Keep your answers under 2 sentences. Do not use emojis.
   ```
3. Drag the preset into the **Personality Preset** field of `NPCAgent`. It sets the prompt and the voice.

The package ships two presets: Grumpy Guard (voice bryce) and Helpful Guide (voice ljspeech). Give each NPC a name in the prompt. Without one, small models answer with their own model name.

## C# API

```csharp
// Open a conversation with an NPC
AISystemManager.Instance.OpenChat(targetNPC);

// Ask the LLM directly, with streaming
string reply = await llmAgent.Chat(prompt,
    callback: partial => Debug.Log(partial),
    completionCallback: () => Debug.Log("Done"));

// Speak any text with a Piper voice
voiceOutput.Speak("Welcome, traveller.", "en_US-bryce-medium");
```

## Editor tools

Menu **Tools → AI Packages**:

- **AI System Setup**: setup status and model downloads. Opens after import.
- **Download Model Files**: downloads missing models.
- **Voice Browser**: downloads or deletes 9 Piper voices. All are public domain or CC0, so they can ship in commercial games.
- **System Health & GPU**: shows GPU, VRAM, CPU and RAM. One switch moves the LLM to the GPU.
- **Force Install Dependencies**: Git URL version only.

## Requirements

- Unity 6 (6000.0) or newer.
- Windows 10/11 64-bit. Other platforms are not supported.
- English. Whisper and Piper are set up for English only.
- URP for the demo scene. The scripts also work in Built-in and HDRP.
- 8 GB RAM, 16 GB recommended.
- Optional GPU with 4 GB+ VRAM: NVIDIA (CUDA) or AMD (Vulkan). Without one, the LLM runs on the CPU.

## Responsible use

The package does not filter model answers. Put your rules in the preset's system prompt or add a filter. Tell players that NPC answers come from an LLM.

## Roadmap

- NPC actions: the LLM turns the player's words into game commands, for example "give drink".

## License

MIT. Bundled tools keep their own licences, listed in [ThirdPartyNotices.md](Assets/AI%20Driven%20NPCs%20System/ThirdPartyNotices.md):
[LLMUnity](https://github.com/undreamai/LLMUnity) (Apache 2.0),
[whisper.unity](https://github.com/Macoron/whisper.unity) (MIT),
[piper-no-espeak-unity](https://github.com/lookbe/piper-no-espeak-unity) (MIT) with [Piper](https://github.com/rhasspy/piper) voices,
[ONNX Runtime Unity](https://github.com/asus4/onnxruntime-unity) (MIT),
[Qwen3.5-0.8B GGUF](https://huggingface.co/unsloth/Qwen3.5-0.8B-GGUF) (Apache 2.0).

## Contact

[GitHub Issues](https://github.com/oguzhan00yildiz/Unity-Local-AI-Driven-NPCs/issues) · [oguzhan00yildiz@gmail.com](mailto:oguzhan00yildiz@gmail.com) · Oguzhan Yildiz
