# AI Instructions — Unity Local AI-Driven NPCs

## 🚨 MANDATORY REPOSITORY & AITEST SYNCHRONIZATION RULES

### 1. Dual-Script Synchronization within this Repository
The runtime scripts in this project exist in two mirrored locations:
1. **Development Source:** `Assets/Scripts/AISystem/` (used by development scenes in this repository)
2. **Package Sample Distribution:** `AIPackageInstaller/Samples~/Scripts/AISystem/` (imported by end users when installing the UPM package)

> **CRITICAL INSTRUCTION FOR AI AGENTS:**
> Whenever you create, modify, or delete any script in `Assets/Scripts/AISystem/`, you **MUST** immediately apply the exact same changes to `AIPackageInstaller/Samples~/Scripts/AISystem/` (or vice-versa). Never leave one location updated while the other is outdated.

### 2. Live Synchronization to Local Test Project (`c:\Projects\AITest\`)
To allow immediate testing in `AITest` without requiring re-pulling or reinstalling the package:
- **Package & Editor Changes:** Whenever any file in `AIPackageInstaller/` (such as `Editor/`, `package.json`, `README.md`, etc.) is modified, immediately copy it to the active package cache directory in `c:\Projects\AITest\Library\PackageCache\com.yildizoguzhan.ai-driven-npcs@*\`.
- **Runtime AISystem Scripts:** Whenever runtime scripts in `Assets/Scripts/AISystem/` are modified, sync them directly to:
  1. `c:\Projects\AITest\Library\PackageCache\com.yildizoguzhan.ai-driven-npcs@*\Samples~\Scripts\AISystem\`
  2. `c:\Projects\AITest\Assets\Samples\AI Driven NPCs System\*\AI Driven NPCs System\Scripts\AISystem\` (if present).

### Quick Sync Command (PowerShell)
Whenever changes are made, run this sync command to keep all targets up to date:
```powershell
# 1. Sync internal package samples
Copy-Item -Path "Assets\Scripts\AISystem\*" -Destination "AIPackageInstaller\Samples~\Scripts\AISystem\" -Recurse -Force

# 2. Sync to AITest PackageCache (if present)
$aiTestPkg = Get-Item "c:\Projects\AITest\Library\PackageCache\com.yildizoguzhan.ai-driven-npcs@*" -ErrorAction SilentlyContinue
if ($aiTestPkg) {
    Copy-Item -Path "AIPackageInstaller\Editor\*" -Destination "$($aiTestPkg.FullName)\Editor\" -Recurse -Force
    Copy-Item -Path "AIPackageInstaller\README.md" -Destination "$($aiTestPkg.FullName)\README.md" -Force
    Copy-Item -Path "Assets\Scripts\AISystem\*" -Destination "$($aiTestPkg.FullName)\Samples~\Scripts\AISystem\" -Recurse -Force
}

# 3. Sync to AITest imported samples (if present)
$aiTestImported = Get-Item "c:\Projects\AITest\Assets\Samples\AI Driven NPCs System\*\AI Driven NPCs System\Scripts\AISystem" -ErrorAction SilentlyContinue
if ($aiTestImported) {
    Copy-Item -Path "Assets\Scripts\AISystem\*" -Destination $aiTestImported.FullName -Recurse -Force
}
```

---

## Project Overview & Architecture
- Local AI NPCs running on-device: LLM inference (LLMUnity), TTS (Piper TTS), STT (Whisper).
- Cross-component communication uses C# events (`VoiceInputService.OnTranscription`, `ChatUIController.OnSendMessage`, etc.).
- Services are auto-resolved via `GetComponentInChildren<T>(true)` or `AISystemManager.Instance`. Do not wire cross-prefab references manually in inspectors.
- Editor installers live in `AIPackageInstaller/Editor/`.
