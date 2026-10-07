# The end-of-layer panel's "Save and exit" still writes the native save

- Status: Todo
- Priority: Medium
- Category: Persistence / save system / session flow
- Source: found by the family sweep of the 2026-10-07 cycle that landed
  `review/layer-complete-choice-for-members.md` — NOT user-reported. The cycle was reading the
  end-of-layer panel's two entries and found that the second one reaches a native writer this repository
  believes is unreachable.
- Related: `review/save-layer-end-save-and-restore.md` (owns the save half; its acceptance row 5 claims
  decision 165 is enforced in both directions), `docs/decisions/active.md` (165, 167),
  `reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs`

## What is read

- The end-of-layer panel's other entry is `WorldGeneration.SaveAndExit`
  (`WorldGeneration.cs:1023-1030`): it runs `IncreaseDepthByLayer()`, then `SaveSystem.SaveGame()`, then
  `PlayerCamera.main.ToMainMenu()`. Like `ContinueRun`, it has NO caller anywhere in the decompiled
  assembly, so the scene panel is its only producer.
- This repository's S2 work re-pointed its OWN menu-return hook at a CUO cut and blocked the native
  READER of `save.sv` (`RunMenuReturnCoordinator` calling `IWorldSaveControl` instead of
  `SaveSystem.SaveGame()`; `SaveSystemHasSavePatch` and `SaveSystemTryLoadGamePatch` in the Game Adapter),
  with decision 165 ("CUO is independent of `save.sv`") cited as enforced in both directions.
- The evidence that S2 recorded for that claim is a grep over `src/` — which by construction cannot see a
  NATIVE button. So `save.sv` is still written by a player click at the layer's end, and on a member that
  click also drives `ToMainMenu()` out of the session's world.

## What is not known yet

- What the click does on each side of a live session: does the host's menu return take the CUO cut
  (decision 167's armed cut) as well, does a member's click leave the session or only its own world, and
  what each client logs — a reading (two clients, one click each), not a guess.
- What the button SHOULD mean in co-op: "save this session's world and leave" (the CUO cut plus a
  documented leave), or "leave without saving" with the native write blocked and named. That is the save
  half's decision, taken with `review/save-layer-end-save-and-restore.md` and decision 165/167 in hand.

## Required work

1. Attribute first, on two clients, in a live session: read what each click writes (the native save slot
   on disk, the CUO archive), what each client's world state does, and every log line on both sides.
2. Decide the meaning with the save half's contract in hand and write it down before implementing; the
   panel is native UI and stays the surface (no second CUO panel is built for it).
3. Implement in the save half's cycle, not this file's: whichever shape wins changes
   `RunMenuReturnCoordinator`'s family or adds a seam on the panel's entry, and it needs its own
   acceptance row.

## Non-goals

- The panel's "Continue" entry: landed in `review/layer-complete-choice-for-members.md` (the member's
  choice drives the host's advance).
- Re-deciding decision 165 itself: the question here is its ENFORCEMENT on a surface the original evidence
  could not see, not whether CUO should own its saves.
