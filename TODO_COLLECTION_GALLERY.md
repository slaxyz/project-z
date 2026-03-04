# TODO - Collection Champion Gallery

## Goal
Ship a first playable `CollectionScene` with champion gallery, detail panel, unlock flow using `progressionPoints`, and persistent unlock state.

## Implementation checklist
- [x] Add champion data model with `ScriptableObject` catalog.
- [x] Add `ChampionDefinitionAsset` fields:
  - [x] `id`
  - [x] `displayName`
  - [x] `role`
  - [x] `unlockCost`
  - [x] `splashSprite`
  - [x] `shortLore`
  - [x] `baseHp`
  - [x] `baseAttack`
- [x] Add `Resources/Run/ChampionCatalog.asset` with 5 champions.
- [x] Keep robust fallback if catalog asset is missing/invalid.
- [x] Extend `MetaData` with `unlockedChampionIds`.
- [x] Add unlock helper methods (`IsChampionUnlocked`, `UnlockChampion`).
- [x] Initialize default unlocks (first 3 champions) for empty saves.
- [x] Add `GameFlowManager` collection APIs:
  - [x] `GetChampionCatalog()`
  - [x] `IsChampionUnlocked(string championId)`
  - [x] `TryUnlockChampion(string championId, out string reason)`
  - [x] `GetPlayerCoins()`
  - [x] `GetDefaultSelectedChampionIdForCollection()`
- [x] Auto-spawn `CollectionSceneController` in `CollectionScene`.
- [x] Build collection UI (runtime):
  - [x] Top detail panel (splash + infos + unlock button + feedback)
  - [x] Bottom horizontal carousel
  - [x] Visual states (`Selected`, `Unlocked`, `Locked`, `Locked but affordable`)
- [x] Enforce selected champion on page open (no empty selection state).
- [x] Add filter/sort foundations:
  - [x] `ChampionSortMode`
  - [x] `ChampionFilter`
  - [x] `ApplyFilterAndSort(...)`
  - [x] Placeholder text "Filters/Sort: coming soon"

## Test checklist (manual)
- [ ] New save: first 3 champions are unlocked.
- [ ] First champion auto-selected on open.
- [ ] Clicking a locked champion shows details + unlock button.
- [ ] Unlock succeeds when enough coins; coins decrease by cost.
- [ ] Unlock fails when not enough coins; no coin loss.
- [ ] Unlock state persists after relaunch.
- [ ] Missing catalog asset: warning log + no crash.

## Done criteria
- [x] Collection page is usable end-to-end with unlock persistence.
- [x] Unlock logic is centralized in `GameFlowManager`.
- [x] Data is editor-friendly via `ScriptableObject`.
- [ ] Manual tests validated in Unity play mode.
