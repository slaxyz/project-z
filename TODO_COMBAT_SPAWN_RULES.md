# TODO - Combat Step 3 (Spawn Rules)

## Goal
Move enemy spawn selection to data-driven rules (biome/tier/progression), without hardcoded selection logic in `FightMockController`.

## Implementation checklist
- [x] Create `CombatSpawnRulesAsset` (`ScriptableObject`).
- [x] Add required data:
  - [x] Rules by biome (`Zone1`, `Zone2`)
  - [x] Rules by progression window (example: `boardNodeIndex` ranges)
  - [x] Tier weights (`Minion`, `Elite`, `Champion`, `Boss`, `Apex`)
- [x] Load `CombatSpawnRulesAsset` from `Resources/Combat/SpawnRules`.
- [x] Replace hardcoded biome/tier selection in `FightMockController` with asset rules.
- [x] Keep a robust fallback when asset is missing/invalid.
- [x] Keep existing biome/tier debug overrides.
- [x] Add explicit logs for rules errors.

## Test checklist
- [x] `SpawnRules.asset` exists and is editable in Inspector.
- [x] `SpawnRules.asset` present + valid: spawn follows expected biome/tier behavior.
- [x] `SpawnRules.asset` missing: no crash, fallback active.
- [x] Invalid rule data: clear warning in Console + combat continues.
- [x] Debug overrides for `Biome/Tier` still work.
- [x] No red errors in Unity Console.

## Done criteria
- [x] No hardcoded spawn decision (biome/tier/progression) left in `FightMockController`.
- [x] Spawn rates can be tuned from Inspector only (no code edit required).
- [x] Fallback behavior validated.
- [x] Manual tests validated across multiple runs.
