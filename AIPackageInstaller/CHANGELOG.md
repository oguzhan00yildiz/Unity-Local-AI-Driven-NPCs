# Changelog

## [2.5.0] - 2026-11-21

### Added
- Push-to-talk mode: hold a key near an NPC to speak, with on-screen subtitles. The player keeps moving.
- NPCActions: the LLM triggers UnityEvents with tags such as [FOLLOW]; tags are hidden from chat and speech.
- 10 more personality presets for common NPC roles.
- NPC name and status (Listening, Thinking, Talking) shown above each NPC.
- DemoQuest sample scene: a short quest in the village of Elmford with an elder, an innkeeper, a gate guard, a hunter, the companion Aada, 10 walking villagers and skeleton enemies.
- Story system: StoryLore, QuestManager, NPCKnowledge (shared facts, per-stage goals and written opening lines), WorldState, ItemPickup, TriggerZone, QuestHUD.
- NPCActions keyword actions: code decides from the player's words and tells the LLM the decision, so replies match game events. Actions can start disabled and be enabled by quest stages.
- Combat: Health, MeleeAttacker, MeleeCombatant, EnemyAI, PlayerCombat, CompanionController fight mode and CompanionReactions.
- Village life: VillagerWander and AmbientBark (short spoken lines without the LLM).
- Audio: MusicDirector (calm and fight music crossfade, ducks while NPCs speak), FootstepAudio, CombatAudio.
- 3D speech: NPC voices play from the speaking NPC and follow it.
- Setup window: optional DemoQuest models (Qwen3.5-4B, Whisper small.en, 6 Piper voices), offered only when the DemoQuest scene is in the project.

### Changed
- Push to talk picks the NPC in front of the camera when several are in range.
- Voice preload includes villager voices.

### Fixed
- Opening lines wait until other speech ends.
