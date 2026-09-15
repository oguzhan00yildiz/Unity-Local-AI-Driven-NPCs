# AI Driven NPCs System — Setup Guide & User Manual

Welcome to the **AI Driven NPCs System**! This framework allows you to add fully local, on-device AI characters (Large Language Models, Speech-to-Text, and Text-to-Speech) into any Unity scene with zero manual cross-Inspector wiring.

---

## 🚀 Quick Start (Add AI NPCs to Any Scene)

Setting up an interactive AI NPC in your scene takes less than 60 seconds:

### Step 1: Add the AI System Manager
1. In the Project window, navigate to `Assets/AI Driven NPCs System/Prefabs/`.
2. Drag and drop the **`AISystem`** prefab into your scene hierarchy.
   - *This prefab houses the central coordinator (`AISystemManager`), UI canvas (`ChatUIController`), speech recognition (`VoiceInputService`), neural voice synthesizer (`VoiceOutputService`), and the model bootstrapper (`ModelBootstrapper`).*

### Step 2: Ensure Your Player is Tagged
1. Select your Player GameObject in the Hierarchy.
2. In the Inspector, ensure the **Tag** dropdown is set to **`Player`**.
   - *NPC proximity detection uses this tag to identify when the player walks into conversation range.*

### Step 3: Add an NPC
You have two easy ways to set up an NPC:

#### Option A: Drag & Drop the NPC Prefab (Fastest)
1. Drag and drop the **`NPC`** prefab from `Assets/AI Driven NPCs System/Prefabs/` into your scene.
2. Position it anywhere near your Player.

#### Option B: Turn Any Existing 3D Character into an AI NPC
1. Select your 3D character GameObject in the Hierarchy.
2. Click **Add Component** → search and add **`NPCAgent`**.
   - *A trigger `SphereCollider` is automatically configured to match the interaction range.*
3. Click **Add Component** → search and add **`LLMAgent`** (from the LLMUnity package).
4. Assign your character's name in `NPCAgent.npcName` (e.g., "Guard", "Merchant").

### Step 4: Hit Play!
1. Press **Play** in the Unity Editor.
2. Walk your Player up to the NPC.
3. When inside the interaction range, you will see the overhead 3D cue: `"Press E to interact"`.
4. Press **`E`** (or click) to open dialogue mode:
   - **Speak into your microphone**: Whisper STT captures your speech in real time with Voice Activity Detection (VAD).
   - **Or type your message**: Enter text in the chat input field and press Enter or Send.
   - The NPC will think and stream back an intelligent on-device reply while speaking in a natural neural voice via Piper TTS!
5. Press **`ESC`** or click **Close** to return to regular gameplay.

---

## 🎭 Creating Custom NPC Personalities

Personalities are configured via **ScriptableObject presets**, allowing you to create and swap personalities without writing code.

### How to Create a Preset:
1. In the Project window, right-click in any folder:  
   **Create → AI System → Personality Preset**
2. Name the asset (e.g., `PirateCaptain`, `Blacksmith`, `Shopkeeper`).
3. Select the asset to inspect its fields:
   - **NPC Name**: The in-game character name (e.g., `Captain Valerie`).
   - **System Prompt**: Instructions that shape the NPC's identity, knowledge, tone, and constraints:
     ```text
     You are Captain Valerie, a witty and fearless pirate captain.
     You speak with a maritime flair. Keep answers concise (under 2 sentences).
     ```
   - **Voice Model Name**: The Piper TTS voice identifier (e.g., `en_US-amy-low`, `en_US-reza_ibrahim-medium`).

### Applying the Preset:
1. Select your NPC GameObject in the Hierarchy.
2. In the `NPCAgent` component inspector, drag your new preset into the **Personality Template** field.
3. The prompt, name, and voice settings are instantly applied to both `NPCAgent` and `LLMAgent`!

---

## 🧠 Changing or Customizing LLM Models

The system is preconfigured with **`Qwen3.5-0.8B-Q4_K_M.gguf`** (~500 MB) for ultra-fast, lightweight inference. You can switch to any other GGUF model (e.g. Llama 3, Mistral, Phi-3, Gemma):

1. In the Hierarchy, expand the **`AISystem`** prefab instance and select the child GameObject named **`LLM`**.
2. In the Inspector, locate the **`LLM`** component:
   - **Download Presets**: Choose from curated models to download directly within Unity.
   - **Custom Model**: Select any `.gguf` file placed inside `Assets/StreamingAssets/` or on your local disk.
3. Press **Play** — all NPCs will converse using the new language model!

---

## 🎙️ Downloading Additional Character Voices

Piper TTS includes high-performance neural voice models. To download more character voices:
1. Open **Tools → AI Packages → Voice Browser**.
2. Browse available voices:
   - `Amy` (English Female, Low)
   - `Ibrahim` (English Male, Medium)
   - `LJSpeech` (English Female, High)
   - `Jenny` (English Female, Medium)
   - `Ryan` (English Male, High)
3. Click **Download** next to any voice. Models are saved directly to `Assets/StreamingAssets/PiperTTS/`.
4. Assign the voice name in your NPC's personality preset or `NPCAgent.voiceModelName`.

---

## ⚡ GPU Acceleration & Hardware Health

To check your hardware capabilities or enable GPU acceleration for near-instant responses:
1. Open **Tools → AI Packages → System Health & GPU**.
2. View detected GPU model, VRAM capacity, CPU threads, and system memory.
3. Click **Enable GPU (All LLMs)** to offload calculation layers to your graphics card for a 5x–10x speed boost.

---

## 🎮 Controls & Input Support

The system works out of the box with both the **New Input System** and the **Legacy Input Manager**:
- **WASD / Left Stick**: Character movement
- **Left Shift**: Sprint
- **Space**: Jump
- **E / F**: Interact with nearby NPC
- **Enter**: Submit message in chat
- **ESC**: Close chat panel and resume gameplay

---

## 🔧 Troubleshooting

| Symptom | Cause | Solution |
| :--- | :--- | :--- |
| NPC says "AISystemManager not found in scene" | Missing system prefab | Drag `AISystem` prefab into the scene. |
| NPC does not respond to proximity | Player missing tag | Ensure your Player GameObject has the tag `"Player"`. |
| Voice input not capturing speech | Microphone permissions or silent input | Verify your default microphone in Windows Sound Settings or select it in the chat UI dropdown. |
| Audio has no speech output | TTS voice model missing | Open **Tools → AI Packages → AI System Setup** or **Voice Browser** to download the voice files. |
| Re-run package or model installation | Need fresh dependencies | Open **Tools → AI Packages → AI System Setup** or click **Tools → AI Packages → Force Install Dependencies**. |

---

## 💬 Support & Contact

If you have questions, encounter issues, or need help integrating the system into your project:
- **GitHub Issues:** [Unity-Local-AI-Driven-NPCs Issues](https://github.com/oguzhan00yildiz/Unity-Local-AI-Driven-NPCs/issues)
- **Email Support:** [oguzhan00yildiz@gmail.com](mailto:oguzhan00yildiz@gmail.com)
- **Community Repository:** [GitHub Repository](https://github.com/oguzhan00yildiz/Unity-Local-AI-Driven-NPCs)
