# AI Instructions — Unity Local AI-Driven NPCs

## 🚨 MANDATORY REPOSITORY & AITEST SYNCHRONIZATION RULES

### 1. Dual-Script Synchronization within this Repository
The runtime scripts and package assets exist in two mirrored locations:
1. **Development Source & Asset Store Package:** `Assets/AI Driven NPCs System/` (used by development scenes in this repository and selected in Asset Store Tools)
2. **Package Sample Distribution:** `AIPackageInstaller/` (imported by end users when installing via Git UPM package)

> **CRITICAL INSTRUCTION FOR AI AGENTS:**
> Whenever you create, modify, or delete any script in `Assets/AI Driven NPCs System/`, you **MUST** immediately apply the exact same changes to `AIPackageInstaller/` (or run `.\sync.ps1`). Never leave one location updated while the other is outdated.

### 2. Live Synchronization to Local Test Project (`c:\Projects\AITest\`)
To allow immediate testing in `AITest` without requiring re-pulling or reinstalling the package:
- **Package & Editor Changes:** Whenever any file in `Assets/AI Driven NPCs System/Editor/` or `AIPackageInstaller/` is modified, immediately sync it to the active package cache directory in `c:\Projects\AITest\Library\PackageCache\com.yildizoguzhan.ai-driven-npcs@*\`.
- **Runtime AISystem Scripts & Assets:** Sync them directly to:
  1. `c:\Projects\AITest\Library\PackageCache\com.yildizoguzhan.ai-driven-npcs@*\Samples~\`
  2. `c:\Projects\AITest\Assets\Samples\AI Driven NPCs System\*\AI Driven NPCs System\` (if present).

### Quick Sync Command (PowerShell)
Whenever changes are made, run `.\sync.ps1` or this sync snippet:
```powershell
.\sync.ps1
```

---

## Project Overview & Architecture
- Local AI NPCs running on-device: LLM inference (LLMUnity), TTS (Piper TTS), STT (Whisper).
- Cross-component communication uses C# events (`VoiceInputService.OnTranscription`, `ChatUIController.OnSendMessage`, etc.).
- Services are auto-resolved via `GetComponentInChildren<T>(true)` or `AISystemManager.Instance`. Do not wire cross-prefab references manually in inspectors.
- Editor installers live in `AIPackageInstaller/Editor/`.
