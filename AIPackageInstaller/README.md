# AI Package Installer

This is a fully automatic Unity package that installs all AI-related dependencies:
- ✅ **LLMUnity** - Local LLM integration
- ✅ **Piper TTS** - Text-to-Speech synthesis  
- ✅ **Whisper STT** - Speech recognition

**Everything is automatic. No manual configuration needed!**

## Installation (One Step)

1. In Unity Package Manager, click **+** → **Add package from git URL...**
2. Paste this URL:
   ```
   https://github.com/oguzhan00yildiz/Unity-Local-AI-Driven-NPCs.git?path=AIPackageInstaller#packagetest
   ```
3. Click **Add**

That's it! ✅ The script will automatically install in this order:
1. **ONNX Runtime** (required by Piper TTS)
2. **LLMUnity** - Local LLM
3. **Piper TTS** - Text-to-Speech
4. **Whisper STT** - Speech recognition

## What Happens Next?

Open the **Console** (Window → General → Console) to see the progress:

```
[AI Package Installer] Initializing...
[AI Package Installer] Checking installed packages...
[AI Package Installer] Installing 5 missing packages...
[AI Package Installer] Installing: https://github.com/asus4/onnxruntime-unity.git?path=com.github.asus4.onnxruntime...
[AI Package Installer] ✅ Installed: com.github.asus4.onnxruntime@...
[AI Package Installer] Installing: https://github.com/undreamai/LLMUnity.git...
[AI Package Installer] ✅ Installed: ai.undream.llm@...
...
[AI Package Installer] All AI packages installed successfully! ✅
```

Installation typically takes 2-5 minutes depending on your internet connection.

## Step 2 — Download Model Files

After packages are installed, open the model downloader:

**Tools → AI Packages → Download Model Files**

This opens a window where you can download everything with one click:

| Model | Size | Purpose |
|-------|------|---------|
| `ggml-tiny.bin` | 74 MB | Whisper speech recognition |
| `model.onnx` | 59 MB | Piper TTS phonemizer |
| `phoneme_dict.json` | 10 MB | Piper TTS phonemizer |
| `en_US-amy-low.onnx` | 60 MB | Piper voice – Amy (female) |
| `en_US-reza_ibrahim-medium.onnx` | 61 MB | Piper voice – Ibrahim (male) |

All files are saved automatically to `StreamingAssets/`.

> **LLM model** is managed separately by LLMUnity.
> Open your scene → select the **LLM** GameObject → click **Download Model** in the Inspector.

## Troubleshooting

If you see errors in Console:

1. **Compile errors after install**: Wait for all packages to finish — ONNX Runtime must fully compile before Piper TTS compiles. Check the Console for `[AI Package Installer] All AI packages installed successfully!`
2. **"Package Manager is busy"**: Unity is still downloading packages. Wait and check Console again.
3. **A package failed**: Use **Tools → AI Packages → Force Install Dependencies** in the menu bar to retry.
4. **Any other error**: Restart Unity completely.

## Manual Install (Last Resort)

If automatic installation fails, add these to `Packages/manifest.json` manually:

```json
{
  "dependencies": {
    "com.github.asus4.onnxruntime": "https://github.com/asus4/onnxruntime-unity.git?path=com.github.asus4.onnxruntime",
    "com.github.asus4.onnxruntime.unity": "https://github.com/asus4/onnxruntime-unity.git?path=com.github.asus4.onnxruntime.unity",
    "ai.undream.llm": "https://github.com/undreamai/LLMUnity.git",
    "ai.lookbe.piper": "https://github.com/lookbe/piper-no-espeak-unity.git",
    "com.whisper.unity": "https://github.com/Macoron/whisper.unity.git?path=Packages/com.whisper.unity"
  }
}
```

Save and Unity will automatically resolve packages.

---

**Version**: 1.0.1  
**Last Updated**: February 24, 2026

