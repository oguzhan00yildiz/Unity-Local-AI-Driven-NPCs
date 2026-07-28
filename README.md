# 🤖 Unity Local AI-Driven NPCs

A modern, fully **on-device, local AI NPC framework** for Unity. Bring your Non-Player Characters to life with real-time **voice-to-voice interaction** — powered by local Large Language Models (LLM), Speech-to-Text (STT), and Text-to-Speech (TTS).

> 🔒 **100% Local & Offline**: No cloud APIs, no monthly subscriptions, no latency from network requests, and zero data leaves the user's machine.

---

## 🌟 Key Features

- 💬 **Local LLM Integration**: Powered by [LLMUnity](https://github.com/undreamai/LLMUnity), running GGUF-quantized models (Llama 3, Mistral, Phi-3, etc.) locally on CPU/GPU via llama.cpp.
- 🎙️ **Voice-to-Text (STT)**: Powered by [Whisper Unity](https://github.com/Macoron/whisper.unity) (Whisper `ggml-tiny.bin` model) with real-time Voice Activity Detection (VAD).
- 🗣️ **Text-to-Voice (TTS)**: High-quality neural speech synthesis using [Piper TTS](https://github.com/lookbe/piper-no-espeak-unity) with multiple natural voice options (e.g., Amy, Ibrahim).
- ⚡ **Zero Manual Wiring**: Clean, event-driven architecture with automatic runtime service discovery and singleton management.
- 🎯 **Proximity-Based Interaction**: Dynamic 3D UI indicators ("Press E to interact", "Listening", "Thinking", "Talking") and distance-based trigger management.
- 🛠️ **Automated Package Installer & Model Downloader**: Includes a custom installer editor tool that handles ONNX Runtime npm scoping, dependency resolution, and automated download of necessary model files (~265 MB).
- 🎮 **Cross-Input System Support**: Built-in support for both legacy Unity Input Manager and the New Input System package.

---

## 🏗️ Architecture & How It Works

The system is decoupled into pure services, UI components, and NPC agent wrappers that interact exclusively via C# `event` delegates.

```
                   +------------------------+
                   |     NPCAgent           |
                   | (Proximity / Trigger)  |
                   +-----------+------------+
                               | Interacts
                               v
                   +------------------------+
                   |    AISystemManager     | (Central Controller / Singleton)
                   +----+------+-------+----+
                        |      |       |
      +-----------------+      |       +-------------------+
      |                        v                           |
      v               +-----------------+                  v
+------------------+  | ChatUIController|        +-------------------+
|VoiceInputService |  |   (Pure UI)     |        |VoiceOutputService |
|  (Whisper STT)   |  +-----------------+        |   (Piper TTS)     |
+------------------+                             +-------------------+
```

### Core Components (`Assets/Scripts/AISystem/`)

| Path | Component | Description |
|------|-----------|-------------|
| [`Core/AISystemManager.cs`](file:///c:/Projects/Unity-Local-AI-Driven-NPCs/Assets/Scripts/AISystem/Core/AISystemManager.cs) | `AISystemManager` | Central scene singleton. Coordinates interaction states, binds STT/LLM/TTS flows, and manages player movement/cursor lock states. |
| [`NPC/NPCAgent.cs`](file:///c:/Projects/Unity-Local-AI-Driven-NPCs/Assets/Scripts/AISystem/NPC/NPCAgent.cs) | `NPCAgent` | Attached to NPC game objects. References `LLMAgent`, handles player proximity triggers, keybinds, and 3D TextMesh status prompts. |
| [`Services/VoiceInputService.cs`](file:///c:/Projects/Unity-Local-AI-Driven-NPCs/Assets/Scripts/AISystem/Services/VoiceInputService.cs) | `VoiceInputService` | Manages microphone capture via Whisper. Handles VAD state transitions, silence detection, and asynchronous speech transcription. |
| [`Services/VoiceOutputService.cs`](file:///c:/Projects/Unity-Local-AI-Driven-NPCs/Assets/Scripts/AISystem/Services/VoiceOutputService.cs) | `VoiceOutputService` | Manages Piper TTS. Splits LLM outputs into sentence chunks for smooth, sequential voice synthesis and audio playback. |
| [`UI/ChatUIController.cs`](file:///c:/Projects/Unity-Local-AI-Driven-NPCs/Assets/Scripts/AISystem/UI/ChatUIController.cs) | `ChatUIController` | UGUI chat panel interface. Displays real-time streaming LLM text, historical chat turns, input field, and loading overlays. |
| [`Core/ModelBootstrapper.cs`](file:///c:/Projects/Unity-Local-AI-Driven-NPCs/Assets/Scripts/AISystem/Core/ModelBootstrapper.cs) | `ModelBootstrapper` | Startup initializer. Asynchronously warms up LLM and Whisper models in parallel while showing a loading overlay. Includes `numPredict` race condition safety guards. |
| [`Core/NativePluginLoader.cs`](file:///c:/Projects/Unity-Local-AI-Driven-NPCs/Assets/Scripts/AISystem/Core/NativePluginLoader.cs) | `NativePluginLoader` | Windows pre-loader (`[RuntimeInitializeOnLoadMethod]`). Pre-loads `ggml` and `libwhisper` DLLs via `kernel32` before splash screen to resolve native binary dependencies. |

---

## 📋 Requirements & Dependencies

- **Unity**: 2022.3 LTS or newer (Tested on Unity 6 / Universal Render Pipeline)
- **Target Platforms**: Windows Standalone / Editor (Native DLLs configured for x86_64)
- **Packages**:
  - `ai.undream.llm` (LLMUnity)
  - `ai.lookbe.piper` (Piper TTS)
  - `com.whisper.unity` (Whisper Unity STT)
  - `com.github.asus4.onnxruntime` (**v0.4.4 pinned via NPM**, required for Piper TTS execution)

---

## 🚀 Quick Start & Usage Guide

### Method A — Installing via Git URL (Existing Projects)

1. Open Unity and navigate to **Window → Package Manager**.
2. Click the **+** button in the top left and select **Add package from git URL...**.
3. Enter the following package installer URL:
   ```text
   https://github.com/oguzhan00yildiz/Unity-Local-AI-Driven-NPCs.git?path=AIPackageInstaller#packagetest
   ```
4. Click **Add**.

#### What Happens Automatically:
- `Packages/manifest.json` is updated with an NPM scoped registry (`com.github.asus4`) to download ONNX Runtime `0.4.4` (prevents native crashes caused by Git LFS pointer files).
- LLMUnity, Piper TTS, and Whisper STT are installed automatically.
- The **AI Model Downloader** window will launch to fetch the required Whisper & Piper TTS model binaries (~265 MB) into `Assets/StreamingAssets/`.

---

### Method B — Running the Sample Scene (This Repository)

1. Clone or open this repository in Unity Hub.
2. Ensure model files are present under `Assets/StreamingAssets/`. If missing, run **Tools → AI Packages → Download Model Files**.
3. Select your LLM model:
   - Click on the `LLM` GameObject in the scene hierarchy.
   - In the Inspector, click **Download Model** (or assign your own `.gguf` model file).
4. Open the primary demo scene: `Assets/Scenes/ReadyScene.unity`.
5. Press **Play**!

---

## 🎮 In-Game Controls & Interaction Flow

1. **Approach NPC**: Walk up to any NPC with an `NPCAgent` component until the 3D prompt displays `Press E to interact`.
2. **Start Chat**: Press **E**. The chat UI will open, the player movement will lock, mouse cursor will unlock, and speech recognition starts.
3. **Voice Input**: Speak into your microphone. The VAD indicator turns **Green** while speaking. Once you pause, Whisper transcribes your speech.
4. **Text Input**: Alternatively, type a message into the input field at the bottom and press **Enter** or click **Send**.
5. **AI Response**: The NPC enters the `Thinking` state. As the LLM stream returns text, it appears live in the chat box.
6. **Voice Synthesis**: The NPC state changes to `Talking`. Piper TTS synthesizes the response sentence-by-sentence. Microphone listening is temporarily paused to avoid hearing its own voice output.
7. **Exit Chat**: Press **Esc**, click the **X** close button on the UI, or walk outside the NPC's interaction radius.

---

## ⚙️ How to Add a New AI NPC to Your Scene

1. **Add `AISystem.prefab`**:
   - Drag `Assets/Prefabs/AISystem.prefab` into your scene root. (Required once per scene).
2. **Add `NPC.prefab`**:
   - Drag `Assets/Prefabs/NPC.prefab` into your scene.
3. **Customize NPC**:
   - Set `Npc Name` on `NPCAgent` component (e.g. "Merchant Bob").
   - Set the System Prompt on the `LLMAgent` component to define the character's backstory, personality, and instructions.
4. **Tag Player**:
   - Ensure your Player object has the **`Player`** tag set in the Inspector.

---

## 🎓 New User Tutorial: Integrating into Your Custom Scene

You do **not** need the sample scene to use this package. Integration in a brand-new project or existing custom scene takes **less than 5 minutes**. 

You can use the package in two ways:
1. **Prefab Mode (Zero Code)**: Drag & drop ready-made prefabs into your scene.
2. **C# API Mode (Custom Integration)**: Trigger chats, transcribe voice, generate LLM text, and speak TTS programmatically in your own scripts.

---

### Method 1: Component & Prefab Setup (Recommended)

#### Step 1: Install the Package
In Unity, open **Window → Package Manager → + → Add package from git URL...**:
```text
https://github.com/oguzhan00yildiz/Unity-Local-AI-Driven-NPCs.git?path=AIPackageInstaller#packagetest
```
*(Wait 2–3 minutes for dependencies and model files to install automatically).*

#### Step 2: Add `AISystem` to Your Scene
1. Open your custom scene.
2. Drag `Assets/Prefabs/AISystem.prefab` into the Hierarchy.
   * *This prefab contains all services (Whisper STT, Piper TTS, Chat UI, Model Bootstrapper).*

#### Step 3: Add `NPC` to Your Character
1. Select your NPC GameObject in the scene (or instantiate `Assets/Prefabs/NPC.prefab`).
2. Add the [`NPCAgent`](file:///c:/Projects/Unity-Local-AI-Driven-NPCs/Assets/Scripts/AISystem/NPC/NPCAgent.cs) script to your NPC:
   - Set **NPC Name** (e.g. `"Guard Commander"`).
   - Set **Interaction Range** (e.g. `3` meters).
3. Add an `LLMAgent` component to the same GameObject:
   - Configure the **System Prompt** (e.g. `"You are a stern castle guard commander. Keep answers concise."`).
4. Ensure your player character has the tag **`Player`** and a Collider.

**Done!** When your player approaches the NPC and presses **E**, interaction starts automatically.

---

### Method 2: Programmatic C# API Usage (Custom UI / Logic)

If you want to trigger AI NPC chats programmatically from your own scripts (or replace the built-in UI/trigger system), use the C# API:

#### 1. Triggering an NPC Chat via Code
```csharp
using AISystem;
using UnityEngine;

public class CustomTrigger : MonoBehaviour
{
    public NPCAgent targetNPC;

    // Call this from your own UI button, quest system, or cutscene trigger
    public void StartNPCConversation()
    {
        if (AISystemManager.Instance != null && targetNPC != null)
        {
            // Opens UI, locks player controls, starts STT listening
            AISystemManager.Instance.OpenChat(targetNPC);
        }
    }

    public void EndNPCConversation()
    {
        if (AISystemManager.Instance != null)
        {
            AISystemManager.Instance.CloseChat();
        }
    }
}
```

#### 2. Using LLM Generation directly (Without UI)
```csharp
using LLMUnity;
using UnityEngine;

public class DirectLLMExample : MonoBehaviour
{
    public LLMAgent npcLLM;

    async void AskNPC(string question)
    {
        Debug.Log("Asking NPC: " + question);

        // Chat returns text chunks via streaming callback
        await npcLLM.Chat(
            question,
            partialResponse => {
                Debug.Log("Streaming token: " + partialResponse);
            },
            () => {
                Debug.Log("Conversation turn completed!");
            }
        );
    }
}
```

#### 3. Using Speech-to-Text (Whisper STT) Programmatically
```csharp
using AISystem;
using UnityEngine;

public class CustomVoiceInput : MonoBehaviour
{
    public VoiceInputService voiceInput;

    void Start()
    {
        // Subscribe to transcription events
        voiceInput.OnTranscription += HandlePlayerSpeech;
    }

    void OnDestroy()
    {
        voiceInput.OnTranscription -= HandlePlayerSpeech;
    }

    public void StartMic() => voiceInput.StartListening();
    public void StopMic()  => voiceInput.StopListening();

    private void HandlePlayerSpeech(string transcribedText)
    {
        Debug.Log($"Player said: {transcribedText}");
    }
}
```

#### 4. Using Text-to-Speech (Piper TTS) Programmatically
```csharp
using AISystem;
using UnityEngine;

public class CustomVoiceOutput : MonoBehaviour
{
    public VoiceOutputService voiceOutput;

    void Start()
    {
        voiceOutput.OnSpeechStarted  += () => Debug.Log("NPC started talking");
        voiceOutput.OnSpeechFinished += () => Debug.Log("NPC finished talking");
    }

    public void MakeNPCSpeak(string text)
    {
        // Automatically splits into sentences and speaks sequentially
        voiceOutput.Speak(text);
    }
}
```

---

## 🔍 Scenes Breakdown

| Scene | Purpose |
|-------|---------|
| [`ReadyScene.unity`](file:///c:/Projects/Unity-Local-AI-Driven-NPCs/Assets/Scenes/ReadyScene.unity) | **Primary Demo**: Full integration of Player, AI System, NPC, Whisper STT, LLM, and Piper TTS. |
| [`AIOTest.unity`](file:///c:/Projects/Unity-Local-AI-Driven-NPCs/Assets/Scenes/AIOTest.unity) | Integration test scene for AI systems and UI components. |
| [`TTSTest.unity`](file:///c:/Projects/Unity-Local-AI-Driven-NPCs/Assets/Scenes/TTSTest.unity) | Isolated scene for testing Piper TTS phonemization and voice audio playback. |

---

## 💡 Technical Gotchas & Best Practices

> [!IMPORTANT]
> **ONNX Runtime NPM Requirement**
> ONNX Runtime **must** be installed via NPM (`com.github.asus4.onnxruntime: 0.4.4`). Importing ONNX Runtime directly from a Git URL causes native crashes because Git LFS stub files are downloaded instead of full binary DLLs.

> [!NOTE]
> **LLM Model Prediction Guard**
> `ModelBootstrapper` saves and restores `LLMAgent.numPredict` during startup warmups. This prevents concurrent warmup calls from resetting `numPredict` to `0`, which would truncate LLM responses to a single word.

---

## 📄 License & Credits

- Created by **Oguzhan Yildiz**.
- Core Packages: [LLMUnity](https://github.com/undreamai/LLMUnity), [Whisper Unity](https://github.com/Macoron/whisper.unity), [Piper TTS Unity](https://github.com/lookbe/piper-no-espeak-unity).
