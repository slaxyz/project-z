# TODO - Gameplay Loop V1 (Zone 1 -> Zone 2)

## Core Loop
- [ ] Keep current `Home -> Team Select` flow (3 champions required for Play).
- [x] Add a `ZoneAnimationScene` (fake load) before each board.
- [x] `StartRun` should route to `ZoneAnimationScene` then `BoardScene`.
- [x] Build Board V1 with clickable tiles (at least one active tile).
- [x] Clicking the active tile opens a small confirmation pop-in.
- [x] Pop-in `Validate` button starts `FightScene`.
- [ ] Keep current fight resolution logic (kill monster).
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
- [ ] Full run from Zone 1 to Zone 2 works without blockers.
- [ ] Confirmation pop-in always appears on active tile.
- [ ] After victory, next tile lights up correctly.
- [ ] Zone 1 -> Zone 2 always passes through fake load screen.
- [ ] Coins are displayed and updated correctly on win screen.
- [ ] End-of-run returns cleanly to Home.
