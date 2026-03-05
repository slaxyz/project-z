# TODO - Gameplay Loop V1 (Zone 1 -> Zone 2)

## Core Loop
- [ ] Keep current `Home -> Team Select` flow (3 champions required for Play).
- [ ] Add a `ZoneAnimationScene` (fake load) before each board.
- [ ] `StartRun` should route to `ZoneAnimationScene` then `BoardScene`.
- [ ] Build Board V1 with clickable tiles (at least one active tile).
- [ ] Clicking the active tile opens a small confirmation pop-in.
- [ ] Pop-in `Validate` button starts `FightScene`.
- [ ] Keep current fight resolution logic (kill monster).
- [ ] On victory, open win/result screen with coin rewards only.
- [ ] `Next` from win screen lights up the next board tile.
- [ ] While zone is not finished: loop `Board -> Fight -> Win -> Next`.
- [ ] When Zone 1 is finished: go to Zone 2 through `ZoneAnimationScene`.
- [ ] Zone 2 uses the same loop (`Board -> Fight -> Win -> Next`).
- [ ] End of Zone 2: run completion screen + return Home.
- [ ] Persist minimal run progression (zone index, tile index, coins gained).
- [ ] Add guard rails (cannot start fight without validating active tile).
- [ ] Add clear debug logs for each state transition.

## Data & Config
- [ ] Add simple zone config asset (tiles count per zone).
- [ ] Add coin reward config per victory.
- [ ] Extend `RunData` with `zoneIndex` and `tileIndex`.

## Manual Validation
- [ ] Full run from Zone 1 to Zone 2 works without blockers.
- [ ] Confirmation pop-in always appears on active tile.
- [ ] After victory, next tile lights up correctly.
- [ ] Zone 1 -> Zone 2 always passes through fake load screen.
- [ ] Coins are displayed and updated correctly on win screen.
- [ ] End-of-run returns cleanly to Home.
