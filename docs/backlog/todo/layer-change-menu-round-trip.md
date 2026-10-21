# A layer change pulls every member back to the menu instead of loading the group together

- Status: Todo
- Priority: Medium
- Category: World generation / layer transition / loading choreography
- Source: the user's 2026-10-21 report while watching batch `20261021-g` — after the host descended one
  layer, the host's screen showed the native loading screen while the member's screen showed the game's
  cover (the main menu), and the member rejoined only after the host's new layer had finished loading. The
  user's ruling: "这个明显不符合游玩逻辑，就应该一起加载" (this obviously does not match how the game should
  play — they should load together).
- Related: `done/layer-change-member-dropout.md` (the family's dropout half: its park experiment
  `20261007-c` is the evidence that a member CAN be kept in the world through the host's regeneration),
  `done/layer-change-member-recovery.md` (the member that lost its body to the same boundary),
  `todo/layer-descent-spawn-separation.md` and `todo/pod-descent-on-a-member-regenerates-locally.md` (the
  same boundary, other symptoms), `todo/loading-screen-progress-detail.md` (what the screen says while it
  waits), `src/CasualtiesUnknownOnline.GameAdapter/Run/ReentryCoordinator.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/LayerAdvanceCoordinator.cs`

## What is observed (batch `20261021-g`, session A)

The host ran the game's own `skiplayer` at 21:15:43.3. The host logged its own layer reset
(`[LayerReset] dropped the previous layer's 375 enemy row(s) and 248 fluid chunk(s)`, 21:15:43.935),
captured the new layer at 21:15:52.258 (`host roster is ready over 77 enemies`) and sent the entry
snapshot at 21:16:00.476. The member's own log for the same boundary reads
`guest dropped its enemy bindings: the host reset the roster for a new layer` at 21:15:44.173, then
`generation finished: 77 animal(s) exist at the settled edge` at 21:16:01.504 — the member was out of
the new world for those ~17 s. The user, watching both windows, reports the host on the native loading
screen and the member on the game's cover (main menu) through that window, with the member following
only after the host's loading finished.

## Why it happens (code)

`ReentryCoordinator`'s own contract states it: "the host's world exit (a layer change, or the host
leaving) pulls every member to the menu — the member's own `RunCoordinator` does that from the host's
scene report. Coming back is driven by ONE `WorldJoin` the host sends at its world-entry edge." So the
menu round-trip is the current design, not an accident: the host's exit takes the member out, the host
regenerates, and the host re-invites it into the new layer. The member's own new-layer generation starts
only after that entry instruction lands, which is why it trails the host by the host's generation time.

## What the user wants (ruling, 2026-10-21)

The host's descent must be followed IMMEDIATELY, in the same shape as the host clicking start in the main
menu: the moment the host descends, every client enters the loading screen **at the same time** and each
generates the new layer in parallel (latency is not a factor to model — the descent is one shared edge,
not a request/answer round-trip). Nobody goes to the menu and nobody waits for the host's generation to
finish before starting its own. The group is then released into the new layer together when every client's
new layer is ready (the start-gate family's hold, applied to the descent instead of the run start), so the
member's screen shows the loading screen the whole way through.

`done/layer-change-member-dropout.md`'s `20261007-c` park experiment already shows a member can be kept in
the world through the host's regeneration, so the mechanism exists; the design question is what replaces
the menu round-trip without re-introducing the drop/re-entry race that ticket fixed.

## What is not known yet

- Which client performs the member's new-layer generation once the round-trip is removed: the member's
  own `RegenerateWorld` is suppressed for a member's own click
  (`LayerAdvanceCoordinator.TryDelegateLocalAdvance`), and a host-driven change is a different path —
  the member's parallel generation needs its own entry that does not go through a member request.
- What the member's screen shows in the interval today (the user saw the cover; the client was restarted
  for the batch's second session before its scene-state lines could be read — the window will be read
  first in the fix cycle).
- How the descent's shared edge is carried today (the host's scene report / `WorldJoin`), and whether the
  start gate can be armed for it without disturbing the run-start gate's own run.

## Acceptance (for the batch that lands the fix)

1. The host descends a layer; within one second the member's screen shows the loading screen, never the
   main menu or the cover (a captured frame from each client, one artifact per client).
2. The member enters the new layer within the same window as the host (the two clients' entry lines
   within a bounded skew), the member's roster adopts `aligned=True`, and its carried items cross.
3. No new `WorldReady never arrived` valve line and no `Scene state: InMenu` line on the member in the
   window.
