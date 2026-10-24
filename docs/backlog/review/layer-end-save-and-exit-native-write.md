# The end-of-layer panel's "Save and exit" still writes the native save

- Status: Review — every row read on the machine (2026-10-24): the save half takes a CUO cut and leaves `save.sv`
  untouched (record `layer-end-save-and-exit-reading-20261024.md`), the host's panel follows the living share (records
  `layer-panel-visibility-20261024-g.md`, `radiation-rate-and-panel-20261024-h.md`), the dead host's panel still appears
  (batch g), and a member's own panel is gone entirely (batch h, after the member-panel fix `0.1.0+d0b38521`).
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

## The host-dead case, and the rule that answers it (user, 2026-10-24)

With the panel's two choices now host-only, the host's own death would leave nobody able to press them. The user's
ruling replaces the idea of a command with a **host rule**: the buttons are shown on the HOST's screen when the
proportion of LIVING players who have finished the current layer reaches that rule's threshold, **independently of
whether the host's own body is alive** — and conversely, a host who has finished the layer sees nothing while the
proportion is below the threshold. Members never see them (already shipped).

What that needs, in order: (1) the host rule in the census, its `/gamerule` row and the Admin page (a threshold
the host sets, like the other rules); (2) a member → host report of "my body is at the layer's bottom", which is
the same fact the panel's own condition reads; (3) the host's aggregation over its LIVING member list and the
rule's threshold; (4) the host-side panel control — it must be able to SHOW the panel (and hide it) itself, since
a dead host has no local body to trigger it, which is the reason this is a panel-control patch and not the
native check. Nothing of (1)–(4) is implemented yet; this section is the ruling the next cycle builds from.

The user also restated a standing rule while answering: a normal gameplay flow is never shipped as a console
command (`AGENTS.md`, Engineering Discipline). That is now recorded there, so a "the player can type /save" answer
does not come back.



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

## Batch 20261024-h: the member's panel is gone, the host's still follows the share (deployed `0.1.0+8b5baee6`)

Three consecutive reads with both living bodies at the floor: the host's panel **active** with its two choices
visible, the member's panel **inactive**. Then the member back up top (1/2): the host's panel **inactive**. So the
choice is the host's alone, and the panel is the host's gate — record
`docs/evidence/acceptance/radiation-rate-and-panel-20261024-h.md`.

## The panel's visibility rows, read on the machine (2026-10-24, deployed `0.1.0+d4994cca`)

Batch `20261024-g` (host + one member, two clients; `halfHeight = 512`, so the native floor line is `-508.9`), with the
bodies moved by `body-place` and every reading taken from the clients' own probes:

| state | host body | member body | panel `activeSelf` |
|---|---|---|---|
| share met — both living bodies at the floor (2/2) | -509.385 | -511.438 | **true** |
| below the threshold — the member back up (1/2) | -509.385 | 380.351 | **false**, eight consecutive reads |

So the panel appears because the GROUP finished the layer and disappears again while a living member is still up top:
exactly the two states the ruling names. Record `docs/evidence/acceptance/layer-panel-visibility-20261024-g.md`.

**The two reads before it are why the rows pass**, and both were defects of this implementation:

- the write from the host shell's `Update` lost the race with `WorldGeneration.Update` (the panel stayed hidden with
  2/2 at the floor, read on `0.1.0+cea99ab9`-era). Fixed by moving the pass to Unity's late frame
  (`ILayerPanelVisibilityPump`, `0.1.0+76dff211`).
- the native latch (`if (!savePanel.activeSelf && … && PlayerCamera.main.body.y < -halfHeight + 3.1f) SetActive(true)`)
  re-opened whatever a later phase closed, so the panel flipped every frame and stayed visible when the member went
  back up (read on `0.1.0+76dff211`). Fixed by closing INSIDE the game's own write frame
  (`WorldGenerationPanelClosePatch` + `ILayerPanelPatchPort`, `0.1.0+d4994cca`); the open half stays in the late frame,
  because the latch can never open the panel for the case the rule exists for — a host whose own body never reached the
  floor while the rest of the living group did.

## The save half, read on the machine (2026-10-24, deployed `0.1.0+2f674b80`)

A session with the host and one member in the world, then the host pressed the panel's own entry
(`WorldGeneration.SaveAndExit`, called the way the button calls it — a real click needs a cursor the acceptance
tooling may not use):

| read | before | after |
|---|---|---|
| `save.sv` (the native file) | 1842 bytes, mtime T0 | **1842 bytes, same mtime T0 — not written** |
| the CUO archive directory | `w-…-2af5` (the run's own start cut) | a **new commit into `w-…-2af5`**, reason `menu-return` |
| the host's world | in the world, generation finished | left for the main menu |

The log carries the whole chain:
`[LayerChoice] save-and-exit records the deferred menu return` → `Cut MenuReturn armed for world w-…-2af5` →
`Cut MenuReturn committed for world w-…-2af5: mid-run` → `Leaving the world to the main menu.`

**A first version of this hook was wrong and the machine said so**: it armed the cut and called `ToMainMenu` in the
same call, so the pump's seam found no world and refused (`no live world is present, so the game's own blob stays as
it is`). The shipped version records the DEFERRED menu return the game's own leave already uses
(`RunMenuReturnCoordinator.Request`, origin `PlayerLeave`), and `SaveCutSeam` takes the cut at the frame-end seam
and only then leaves — the row above is the fixed behaviour.

**Still unread, and named rather than claimed**: the panel's visibility rows (a member sees no buttons; the host's
panel appears by the living share, including a dead host). Staging them needs bodies at the layer floor, which needs
real movement input the acceptance tooling may not drive; the rules behind them are pinned by eight unit cases
(`LayerPanelChoicePolicyTests`, `LayerPanelVisibilityTests`), and the runtime rows wait for a batch with movement.
