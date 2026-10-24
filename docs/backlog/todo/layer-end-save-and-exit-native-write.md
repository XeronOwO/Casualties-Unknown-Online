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

## The attribution, read on the machine (2026-10-24)

Two clients in a session, both in the world (`0.1.0+bca3f119`, host + one guest; the guest called the panel's
own entry `WorldGeneration.SaveAndExit` — the method the button invokes — because a real click needs a cursor the
acceptance tooling may not use):

| read | before | after the guest's call |
|---|---|---|
| guest's world | `depth 0, worldExists true` | `depth -1, worldExists false`, page `Home`, session still `active` |
| host's world | `depth 0, worldExists true` | unchanged, host still in the world, lobby unchanged |
| `save.sv` (both clients read the SAME file: they share one user profile) | 1842 bytes, mtime T0 | **1806 bytes, new mtime** |

So a MEMBER's click writes the native `save.sv` with the member's own world state and drops that member out of
the session's world while the session continues. That is decision 165's enforcement gap on a surface the
original grep-based evidence could not see, and it is native: `SaveSystem.SaveGame()` writes
`persistentDataPath\save.sv`; CUO's own archives live under `cuo/saves/w-<date>-<hex>` and were not touched.

## The ruling (user, 2026-10-24): the host owns both choices, and CUO saves are the only saves

- **Only the host may press "Continue" or "Save and exit"; on a member both buttons are hidden** — a control the
  player may not use does not belong on their screen.
- **The save must be CUO's**: "游戏原生存档对我们 cuo 没有意义" — the native save is meaningless to CUO, so the
  host's own "Save and exit" must take the CUO cut rather than write `save.sv`.

**Landed for the first half** (deployed `0.1.0+32acadd7`): `LayerPanelChoicePolicy` (pure: a guest in an active
session hides them) and `LayerAdvanceCoordinator.UpdatePanelChoices`, which finds the two buttons by what they
CALL (`WorldGeneration.ContinueRun` / `SaveAndExit`) and hides them once per panel activation, logging the count.
A member therefore cannot reach `SaveAndExit` at all.

**Still owed (the second half)**: the HOST's own click must take a CUO cut instead of `SaveSystem.SaveGame()`.
That needs a save port on the patch bridge (`IWorldSaveControl` is not reachable from a patch today) and the
capability catalogue's row for the new patch, so it is the next slice; until it lands the host's click still
writes the native save, which is the remaining defect this ticket keeps open for.


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
