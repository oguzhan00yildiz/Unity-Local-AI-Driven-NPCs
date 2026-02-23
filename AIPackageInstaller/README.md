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

That's it! ✅ The script will automatically:
- Modify your `Packages/manifest.json` (adds NPM registry)
- Install ONNX Runtime dependencies
- Download and install all AI packages
- Set everything up for you

## What Happens Next?

Open the **Console** (Window → General → Console) to see the progress:

```
[AI Package Installer] Initializing...
[AI Package Installer] Adding NPM scoped registry to manifest.json...
[AI Package Installer] Manifest.json updated successfully!
[AI Package Installer] Checking installed packages...
[AI Package Installer] Found 3 missing AI packages. Installing...
[AI Package Installer] Installing package: https://github.com/undreamai/LLMUnity.git...
[AI Package Installer] Successfully installed: ai.undream.llm@...
...
[AI Package Installer] All AI packages are successfully installed!
```

Installation typically takes 2-5 minutes depending on your internet connection.

## Optional: Download Model Files

Some packages work better with custom models:

### Whisper (Speech-to-Text)
- Download from [HuggingFace](https://huggingface.co/ggerganov/whisper.cpp)
- Place `.bin` files in `StreamingAssets` folder

### Piper (Text-to-Speech)
- Download voice models from [HuggingFace](https://huggingface.co/rhasspy/piper-voices)
- Place in `StreamingAssets` folder

## Troubleshooting

If you see errors in Console:

1. **Compile errors related to ONNX Runtime**: Wait a few seconds for packages to fully install
2. **"Package Manager is busy"**: Unity is still downloading packages. Wait and check Console again
3. **Any other error**: Restart Unity completely

For detailed logs, check `Packages/manifest.json` to verify all dependencies are listed.

## Manual Install (Last Resort)

If automatic installation fails completely, edit `Packages/manifest.json` manually and add:

```json
{
  "scopedRegistries": [
    {
      "name": "NPM",
      "url": "https://registry.npmjs.com",
      "scopes": ["com.github.asus4"]
    }
  ],
  "dependencies": {
    "com.github.asus4.onnxruntime": "0.4.4",
    "com.github.asus4.onnxruntime.unity": "0.4.4",
    "com.github.asus4.onnxruntime-extensions": "0.4.4",
    "ai.undream.llm": "https://github.com/undreamai/LLMUnity.git",
    "ai.lookbe.piper": "https://github.com/lookbe/piper-no-espeak-unity.git",
    "com.whisper.unity": "https://github.com/Macoron/whisper.unity.git?path=Packages/com.whisper.unity"
  }
}
```

Save and Unity will automatically resolve packages.

---

**Version**: 1.0.0  
**Last Updated**: February 23, 2026

