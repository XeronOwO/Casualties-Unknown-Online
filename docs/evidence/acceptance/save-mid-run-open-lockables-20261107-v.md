# A mid-run cut with opened lockables: the lockpick fixture and the restore

- Date: 2026-11-07
- Build: `0.1.0+7f3c5cfe` (acceptance package; the plain deploy's production package carries the same
  code, the acceptance channel only adds the driver)
- Ticket: `docs/backlog/review/save-mid-run-consistent-cut.md`, row 3's opened-lockables half
- Why: the ticket's status line said "one row's fixture (a save made with open lockables) has no drive
  path on this machine (the lockpick minigame cannot be completed in-process)". This batch built that
  drive path and took the reading.

## The drive path

The lockpick minigame's hand is `MinigameBase.handPos`, mapped from the pointer by
`(Input.mousePosition - minigameScreen.transform.position) / PlayerCamera.uiScale`, and its
`LockpingMinigame.Update` only sets `clickingInside` from the attack bind while the hand is inside the
game's own band (`HandAngle` in `(0,180)`, `HandDistance` in `(195,247)`), capped at
`MaxTurnProgress = 1` when the hand is within `anglePrecision` of the rolled `correctAngle`. The drive
path is therefore three calls:

1. `lockpick-open` — finds the nearest `Openable`, stands the body beside it and runs the game's own
   `Openable.OnUse()`, which starts `LockpingMinigame` with the carried kit.
2. `lockpick-aim` — reads `correctAngle`, `anglePrecision`, `pickLevel` and the minigame screen by
   reflection (the evaluator cannot name the game's private minigame type; a typed cast compiles to no
   value) and returns the screen point for the correct angle at distance 220.
3. `virtual-input mode=move` at that point, then `mode=press-key button=0` — the `press` mode stages
   the MOUSE BUTTON while the minigame reads `Input.GetKey(attack)` through the transpiled call site,
   so the key state is the one it asks for. Hold, then read `minigame-live`.

All coordinates are integers: the recipe arguments arrive as `Double` and a decimal value fails the
`Single` cast ("Object of type 'System.Double' cannot be converted to type 'System.Single'").

## The reading

- `/give @p lockpickingkit` → `gave 76561198281246659 lockpickingkit (#1271631300547), placed in slot 0` (translated; the console prints Chinese).
- `lockpick-open` → `ok:true, distance:7.7, minigame:"LockpingMinigame"`.
- `lockpick-aim` → `correctAngle:25.108, anglePrecision:3.125, pickLevel:-1, screenX:772.809, screenY:419.489, health:1000`.
- After `virtual-input mode=move x=773 y=419`: `handAngle:23.316, handMagnitude:226.815` — inside the
  band and 1.79° from `correctAngle` (inside `anglePrecision`).
- `virtual-input mode=press-key button=0` then `minigame-live`: `handClicking:true, clickingInside:true,
  lockProgress:0.683`; three seconds later the minigame was gone (`kind:"none"`).
- `sound-log mode=read match=unlock` → `raw=718|unlock@310.1;unlock@310.1` — the game's own completion
  sound plays only in the `lockProgress == 1` branch that sets `toDestroy.health = 0` and ends the
  minigame.
- First `/save`: `cut written (world w-20261107-a9fc at revision 715, layer 0 (9 file(s), 0
  world-block row(s), 142 transient row(s), …))`. Its `live/world-entities.json` (SHA-256
  `40051BA43F92F58D091130E075E337F7F02FB08A4E07427921E07D3D5EB7D5F2`) carries two `opened-entity`
  rows and two `building-health {health: 0}` rows at `(-33,480)` and `(-37,476)` — the two picks.
- `continue-run` → `inWorld:true`; the log shows the regenerated layer's restore (`[GenStream] restore
  before=… after=… expect=…` lines).
- Second `/save` after the Continue: `cut written … revision 925`; its `live/world-entities.json` still
  carries the same two `opened-entity` rows and the same two `building-health {health: 0}` rows at the
  same positions, plus one damaged-building row at `(-21,457)` (`health 95.32`).
- `entity-probe-at` at `(-33,480)` and `(-37,476)` on the restored world: `found:false` — the opened
  buildings are not live entities in the regenerated layer (an opened door is removed from the world;
  the row's "the scene shows it" half stays the user's in-game check).

## What this settles

- The lockpick minigame is completable in-process: the drive path exists and produces the game's own
  `unlock` sound and the `toDestroy.health = 0` fact.
- A mid-run cut taken with opened lockables carries those facts in `world-entities.json`, and a
  Continue restores a kernel whose next cut carries the same facts — the restore does not lose the
  opened lockables and does not resurrect them as closed buildings.

## Limits

- The live scene's visual (the open doorway as the player sees it) is not claimed here; the entity probe
  only says the building entity is no longer present at the saved position.
- The trap/fluid/enemy halves of row 3 were read in an earlier session (the ticket's own record); this
  batch adds only the opened-lockables half.
