# ⚠️ Samples~ Sync Warning

The scripts in `Samples~/Scripts/AISystem/` are **copies** of the canonical source at `Assets/Scripts/AISystem/`.

When you modify any runtime script (AISystemManager, NPCAgent, VoiceInputService, VoiceOutputService, ChatUIController, ModelBootstrapper, NativePluginLoader), **you must copy the changes to both locations**:

1. `Assets/Scripts/AISystem/` — the development source (used by this repository's scenes)
2. `AIPackageInstaller/Samples~/Scripts/AISystem/` — the UPM sample (imported by end users)

### Quick Sync Command (PowerShell)

```powershell
# From project root
Copy-Item -Path "Assets\Scripts\AISystem\*" -Destination "AIPackageInstaller\Samples~\Scripts\AISystem\" -Recurse -Force
```

### Why Not Symlinks?

Unity does not reliably support symlinks inside `Samples~` folders across all OS/editor combinations. A manual copy is safer until a build-script solution is set up.
