# GitHub Copilot Instructions — Unity Local AI-Driven NPCs

## Project Overview
A Unity project delivering fully **local** AI NPCs using LLM inference (LLMUnity), TTS (Piper TTS), and STT (Whisper). No cloud APIs. All inference runs on-device.

---

## Architecture

### Runtime Layer (`Assets/Scripts/AISystem/`)
> **🚨 MANDATORY AI SYNC RULE**: Any script change in `Assets/Scripts/AISystem/` must also be synchronized with `AIPackageInstaller/Samples~/Scripts/AISystem/` (and vice-versa).
> In addition, always sync changes directly to the local test project `c:\Projects\AITest\` (both in `Library/PackageCache/com.yildizoguzhan.ai-driven-npcs@*/` and `Assets/Samples/AI Driven NPCs System/*/AI Driven NPCs System/Scripts/AISystem/`).

| Component | Role |
|-----------|------|
| `Core/AISystemManager.cs` | Scene singleton — coordinates all services; NPCAgents locate it via `AISystemManager.Instance` |
| `NPC/NPCAgent.cs` | Per-NPC component — holds `LLMAgent` ref, proximity trigger, interaction key |
| `Services/VoiceInputService.cs` | Whisper STT wrapper — fires `OnTranscription` event |
| `Services/VoiceOutputService.cs` | Piper TTS wrapper — splits text to sentences, queues speech |
| `UI/ChatUIController.cs` | Pure UI — no AI logic; fires `OnSendMessage` / `OnCloseChat` events |
| `Core/ModelBootstrapper.cs` | Warms up LLMAgents + Whisper on startup in parallel |
| `Core/NativePluginLoader.cs` | Pre-loads Whisper native DLLs via `kernel32` before splash screen (Windows only) |

### Communication Pattern
All cross-component communication uses **C# events**, not direct method calls:
- `VoiceInputService.OnTranscription` → `AISystemManager.HandleTranscription`
- `VoiceOutputService.OnSpeechStarted/Finished` → `AISystemManager`
- `ChatUIController.OnSendMessage` → `AISystemManager.HandleUserMessage`

### Auto-resolution Rule
Services are **never wired manually across prefabs**. `AISystemManager.Awake()` calls `GetComponentInChildren<T>(true)` on its children. `NPCAgent` uses `AISystemManager.Instance`. Do not add Inspector cross-prefab references.

---

## Prefabs (`Assets/Prefabs/`)
- **`AISystem.prefab`** — Drop once per scene. Contains all service components as children.
- **`NPC.prefab`** — `NPCAgent` + `LLMAgent` + `SphereCollider (isTrigger)`. The collider radius is synced to `interactionRange` at runtime.
- **`LLM.prefab`**, **`TTS.prefab`** — Standalone LLM/TTS components.
- **`Player.prefab`** — Must have the `Player` tag for NPC proximity detection.

---

## Key External Packages
| Package | ID | Notes |
|---------|----|-------|
| LLMUnity | `ai.undream.llm` | `LLMAgent` for per-NPC chat; `LLM` component for model hosting |
| Piper TTS | `ai.lookbe.piper` | ONNX-based TTS; needs `PiperTTS/` files in StreamingAssets |
| Whisper Unity | `com.whisper.unity` | STT; needs `Whisper/ggml-tiny.bin` in StreamingAssets |
| ONNX Runtime | `com.github.asus4.onnxruntime` **0.4.4 via npm** | **Must not use git URL** — git-URL version lacks LFS DLLs and causes native crashes |

---

## Model Files (`Assets/StreamingAssets/`)
Downloaded by `ModelDownloader.cs` (or manually via **Tools → AI Packages → Download Model Files**):
```
Whisper/ggml-tiny.bin
PiperTTS/model.onnx
PiperTTS/phoneme_dict.json
PiperTTS/tokenizer.json
PiperTTS/Amy/en_US-amy-low.onnx[.json]
PiperTTS/ibrahim/en_US-reza_ibrahim-medium.onnx[.json]
```
LLM model is downloaded separately: select the **LLM** GameObject → **Download Model** in Inspector.

---

## Editor Tooling (`AIPackageInstaller/Editor/`)
- **`AIPackageInstaller.cs`** — `[InitializeOnLoad]` installer. Uses `SessionState.GetBool("AIPackageInstaller.Done")` to **skip re-running on every Play** (domain reload guard). Patches `Packages/manifest.json` to pin ONNX 0.4.4 via npm.
- **`ModelDownloader.cs`** — `AutoStartDownloads()` opens the window **only when models are missing**. All models present → window stays closed.
- Menu shortcuts: **Tools → AI Packages → Force Install Dependencies**, **Download Model Files**.

---

## Critical Gotchas
- **`ModelBootstrapper`** saves and restores each `LLMAgent.numPredict` around `Warmup()` calls — concurrent warmup can corrupt `numPredict` to 0 (1-token output bug). Do not remove this guard.
- **ONNX must be npm version 0.4.4**, not a git URL. Old cached versions live in `Library/PackageCache/com.github.asus4.onnxruntime@*` — delete and restart if crashing.
- **`NativePluginLoader`** must run `BeforeSplashScreen` or Whisper DLL dependencies fail to resolve on Windows.
- Both **New Input System** and **Legacy Input Manager** are supported via `#if ENABLE_INPUT_SYSTEM` guards in `NPCAgent` and `ChatUIController`.

---

## Scenes
| Scene | Purpose |
|-------|---------|
| `ReadyScene.unity` | Primary demo — use this for manual testing |
| `AIOTest.unity` | All-in-one integration test |
| `TTSTest.unity` | Isolated TTS testing |
| `SampleScene.unity` | Scratch / experimental |
