# TODO - Gameplay Loop V1 (Zone 1 -> Zone 2)

## Core Loop
- [x] Keep `Home -> Team Select` for new runs (3 champions required), and direct resume when a run is already active.
- [x] Add a `ZoneAnimationScene` (fake load) before each board.
- [x] `StartRun` should route to `ZoneAnimationScene` then `BoardScene`.
- [x] Build Board V1 with clickable tiles (at least one active tile).
- [x] Clicking the active tile opens a small confirmation pop-in.
- [x] Pop-in `Validate` button starts `FightScene`.
- [x] Keep current fight resolution logic (kill monster).
- [x] On victory, open win/result screen with coin rewards only.
- [x] `Next` from win screen lights up the next board tile.
- [x] While zone is not finished: loop `Board -> Fight -> Win -> Next`.
- [x] When Zone 1 is finished: go to Zone 2 through `ZoneAnimationScene`.
- [x] Zone 2 uses the same loop (`Board -> Fight -> Win -> Next`).
- [x] End of Zone 2: run completion screen + return Home.
- [x] Persist minimal run progression (zone index, tile index, coins gained).
- [x] Add guard rails (cannot start fight without validating active tile).
- [x] Add clear debug logs for each state transition.

## Data & Config
- [x] Add simple zone config asset (tiles count per zone).
- [x] Add coin reward config per victory.
- [x] Extend `RunData` with `zoneIndex` and `tileIndex`.

## Manual Validation
- [x] Full run from Zone 1 to Zone 2 works without blockers.
- [x] Confirmation pop-in always appears on active tile.
- [x] After victory, next tile lights up correctly.
- [x] Zone 1 -> Zone 2 always passes through `ZoneAnimationScene`.
- [x] Coins are displayed and updated correctly on win screen.
- [x] End-of-run returns cleanly to Home.

## Vision V2 (Run Depth)
- [x] Add board tile categories (`Fight`, `Event`, `Shop`, `Boss`).
- [x] Guarantee `Boss` encounter on last tile of each zone.
- [x] Add non-combat tile resolution flow (`Event` and `Shop`) from Board.
- [x] Add run spell reward choice after combat victory.
- [x] Enforce run spell cap (max 4) with replacement choice flow.
- [x] Add run shop interface with 3 spell offers.
- [x] Add boss celebration screen before returning to board flow.
- [x] Remove meta shop from MVP flow.
- [x] Reward flow: choose spell -> choose champion -> choose spell to replace.
- [x] Shop flow: choose priced spell -> choose champion -> choose spell to replace.
- [x] Board composition per zone: 5 fights + 1 event + 1 shop (always before boss) + 1 boss.
- [x] Add branch system after step 2.

## Vision V2 Validation
- [x] Last tile of each zone always spawns a boss.
- [x] Event tile grants coins and advances correctly.
- [x] Shop tile shows 3 spells and supports buy/skip.
- [x] Choosing reward spell with full deck requires selecting a spell to replace.
- [x] Boss victory shows celebration screen before next board flow.

## FightScene V1 Next Steps
- [ ] Configure the enemy setup for the fight scene.
- [ ] Make spells consume gems when played.
- [ ] Make spells deal damage.
- [ ] Clamp the spell spawn to a maximum of 4 visible spells.
- [ ] After `End Turn`, refresh spells. If there are more than 4 available, draw 4 at random.
