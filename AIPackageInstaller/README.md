# AI NPC Package – Setup Guide

Adds **local AI-driven NPCs** (LLM + TTS + STT) to any Unity project via a single git URL.  
No manual dependency management required.

---

## Step 1 — Add the Package

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL…**
3. Paste:
   ```
   https://github.com/oguzhan00yildiz/Unity-Local-AI-Driven-NPCs.git?path=AIPackageInstaller#packagetest
   ```
4. Click **Add**

**What happens automatically:**
- `manifest.json` is patched to add a npm scoped registry for ONNX Runtime (prevents native crashes caused by git-URL LFS stub DLLs)
- ONNX Runtime 0.4.4 is pinned in `manifest.json` via npm
- LLMUnity, Piper TTS, and Whisper STT are installed via their git URLs
- Unity reloads once (after the manifest patch), then installs remaining packages

Watch **Window → General → Console** for progress:
```
[AI Package Installer] Initializing…
[AI Package Installer] manifest.json patched (npm ONNX registry added). Unity will reload…
[AI Package Installer] Checking git packages…
[AI Package Installer] Installing: https://github.com/undreamai/LLMUnity.git…
[AI Package Installer] ✅ Installed: ai.undream.llm@…
…
[AI Package Installer] All AI packages installed successfully! ✅
```

Installation takes **2–5 minutes** depending on connection speed.

---

## Step 2 — Model Files (automatic)

After all packages finish installing, the **AI Model Downloader** window opens automatically and begins downloading all missing model files (~765 MB total).

| File | Size | Purpose |
|------|------|---------|
| `Whisper/ggml-tiny.bin` | 74 MB | Speech recognition (Whisper) |
| `PiperTTS/model.onnx` | 59 MB | TTS phonemizer |
| `PiperTTS/phoneme_dict.json` | 10 MB | TTS phonemizer dictionary |
| `PiperTTS/tokenizer.json` | ~1 MB | TTS tokenizer |
| `PiperTTS/Amy/en_US-amy-low.onnx` | 60 MB | English female voice |
| `PiperTTS/ibrahim/en_US-reza_ibrahim-medium.onnx` | 61 MB | English male voice |
| `Qwen3.5-0.8B-Q4_K_M.gguf` | ~500 MB | LLM language model (Qwen3.5 0.8B) |

All files save to `Assets/StreamingAssets/` automatically.  
If the window doesn't open, trigger it manually: **Tools → AI Packages → Download Model Files**

> The LLM model is automatically registered with LLMUnity's model manager and configured on the `LLM.prefab` — no manual setup needed.

---

## Step 3 — Import the Ready Scene (Optional)

1. **Window → Package Manager** → select **AI NPC Package** → **Samples** tab
2. Click **Import** next to **NPC AI Setup**
3. Open `Assets/Samples/AI NPC Package/…/Scenes/ReadyScene.unity`
4. Press **Play**

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Compile errors right after install | Wait — ONNX must compile before Piper. Watch Console for the ✅ message. |
| "Package Manager is busy" | Packages still downloading. Wait and check Console. |
| A package failed to install | **Tools → AI Packages → Force Install Dependencies** |
| ONNX native crash on Play | Old git-URL ONNX is cached. Delete `Library/PackageCache/com.github.asus4.onnxruntime@*` and restart Unity. |
| Any other error | Restart Unity completely. |

---

## Manual Install (Last Resort)

If automatic installation fails entirely, add this to `Packages/manifest.json` by hand:

```json
{
  "scopedRegistries": [
    {
      "name": "NPM",
      "url": "https://registry.npmjs.org",
      "scopes": ["com.github.asus4"]
    }
  ],
  "dependencies": {
    "com.github.asus4.onnxruntime":       "0.4.4",
    "com.github.asus4.onnxruntime.unity": "0.4.4",
    "ai.undream.llm":   "https://github.com/undreamai/LLMUnity.git",
    "ai.lookbe.piper":  "https://github.com/lookbe/piper-no-espeak-unity.git",
    "com.whisper.unity":"https://github.com/Macoron/whisper.unity.git?path=Packages/com.whisper.unity"
  }
}
```

> ⚠️ **Do NOT use git URLs for ONNX Runtime.** The repository uses git LFS for DLLs; Unity will download only the pointer stubs, causing a native crash at runtime. Always use the npm version (`0.4.4`).

---

**Version**: 2.2.0  
**Last Updated**: February 24, 2026

