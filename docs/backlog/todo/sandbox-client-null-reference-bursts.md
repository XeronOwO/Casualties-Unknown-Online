# Sandboxed clients log NullReferenceException bursts and an instantiate-null ArgumentException

- Status: Todo (batch `20261002-h`: the family reproduced on both sandboxed clients and the root cause is
  named — CUO's out-of-world member keeps receiving the world-item keyframe and materializes the whole
  table into the menu scene; the fix has NOT landed, see the finding section below)
- Priority: Low
- Category: Runtime diagnostics / sandbox
- Source: observed during agent acceptance runs — noted unjudged in `docs/acceptance/lessons.md` (2026-10-01), re-captured with context in batch `20261002-c` and again with a rolling-log stack frame in batch `20261002-d` (`docs/evidence/acceptance/20261002-c-scope.md`, `docs/evidence/acceptance/20261002-d-scope.md`)
- Related: `docs/acceptance/lessons.md`, `docs/evidence/acceptance/20261002-c-scope.md`, `docs/evidence/acceptance/20261002-d-scope.md`

## Problem

Both sandboxed clients (the Steam1 guest and the Steam2 alternate) log bursts of
`[ERR] [Unity:Exception] NullReferenceException` while the host's log carries none, and the alternate
also logs `System.ArgumentException: The Object you want to instantiate is null.` One captured stack
points at the GAME's own code (`GroundBlood.Start ()`), so the throw is not CUO's — what is unknown is
the state that leads into it and whether CUO's resets or the sandbox's environment are part of it.

## Evidence (batch `20261002-c`, artifact ids in the directory named by `acceptance-artifacts-dir`)

- Alternate (Steam2), during its re-entry into the world: a burst of `NullReferenceException` lines,
  one carrying the stack `NullReferenceException: Object reference not set to an instance of an
  object` / `GroundBlood.Start () (at <ab887c07f51841c98ea015c9c29ae1af>:0)`, interleaved with the
  world's generation stream (`[GenStream] restore …`), plus ten `System.ArgumentException: The Object
  you want to instantiate is null.` lines — artifact `c-sandbox-instantiate-alt.log` (read from the
  full-log tail).
- Guest (Steam1), later while out of the world: ~50 `NullReferenceException` lines inside ~5 ms,
  interleaved with `[WRN] [RemotePlayerRenderer] Remote body: no Body component in "Experiment"
  clone.` — artifacts `c-sandbox-instantiate-guest.log`, `c-guest-exception-window.log`.
- Guest (Steam1), during its world RE-ENTRY in batch `20261002-d`: a burst of `NullReferenceException`
  that the CUO ROLLING log carries with a stack FRAME —
  `(wrapper dynamic-method) Item.DMD<Item::Update>(Item)` — plus
  `System.ArgumentException: The Object you want to instantiate is null.` lines; the host logged
  neither. Artifacts `d-guest-nre-burst.log`, `d-guest-exception-window.log` (read at the re-entry
  mark). This is the first guest-side anchor: it names `Item.Update` as the throwing frame, and a world
  re-entry as the window that produced it.
- The host's log carries no matching line in the same windows.
- Batch `20261002-e` (2026-10-02, `docs/evidence/acceptance/20261002-e-scope.md`): a whole three-client
  session logged ZERO `NullReferenceException` lines and zero `Item.DMD<Item::Update>` frames on every
  client, while each sandboxed client logged 176 `System.ArgumentException: The Object you want to
  instantiate is null.` lines at world entry. Those 176 are NOT this family: their stacks name CUO's own
  contained materialization failure (`(wrapper dynamic-method) Utils.DMD<Utils::Create>` under
  `RuntimeEntityFactory.TryCreate`, `[Enemy] cannot create trader …`), and they now have their own
  ticket, `done/enemy-runtime-spawn-classification.md`. The two shapes must be read apart.
- No user-visible failure was observed in the batch; every acceptance row was judged on its own
  evidence.

## Batch `20261002-h` finding (2026-10-02) — the burst is CUO's

The staged windows reproduced the family on BOTH sandboxed clients (guest in window 1, alt in window 2):
251 `[BrokenItemUpdate] … (world-null) in 'PreGen' …` each, then a live `Item.DMD<Item::Update>` NRE
stream that ran until the client re-entered the world (831,352 frame lines in the guest's window; the
guest's rolling log 0.86 MB → 144.30 MB; the host logged neither). Full chain and artifact ids:
`docs/evidence/acceptance/sandbox-client-null-reference-bursts-20261002-h.md`.

Root cause (evidence: `h-materializing-rounds.txt` + `h-w2-alt-materializing.log` — the menu round's 251
`[ItemSpawn] materializing …` lines at 11:39:09.743–10.081, then the 251 diagnostic reports at
11:39:10.130–.179; `h-w2-alt-snapshot-received.log` — `World-item snapshot received (251 items)` every
~10 s; code — `ItemPositionAuthority.Update` → `ItemSnapshotService.SendPeriodicItemSnapshot`):

1. A member that leaves the world keeps its session; the host's `SceneStateHandler` ends only the ENTITY
   sync for it.
2. The host's periodic world-item keyframe is a session broadcast whose only gate is
   `Role == Host && SessionActive` — no `InWorld` targeting, unlike every sibling absolute table
   (`WorldEntryFanout.SendInSessionRepair` filters `member.InWorld`).
3. The out-of-world member applies the rows with no world scene; `RemoteItemSceneOps` finds no id hit and
   no generation-time bind target, so it materializes the whole table into the menu scene. Each client
   performed 505 materializations in three rounds (3 at entry, 251 in the menu, 251 at the re-entry
   before the world was ready — `h-materializing-rounds.txt`); the 251 reports come from the diagnostic's
   per-object dedupe (`ItemWorldSync._brokenUpdateReports`, a `HashSet<Item>`).
4. Those items' `Item.Update` dereferences `WorldGeneration.world == null` every frame → the burst; the
   deduped diagnostic reports each object once. On re-entry the scene load destroys the menu copies and
   the NRE stream stops; the keyframe's re-entry round is the related finding below. The instantiate-null
   `ArgumentException` needs an item with `condition <= 0` AND `Stats.destroyAtZeroCondition` (the break
   branch's `Resources.Load("ItemBreakParticle")`); neither window held one, so that half did not
   reproduce.

Fix direction (next cycle, not implemented here): target the periodic item keyframe per member, members
reported `InWorld` only (mirror the `SendInSessionRepair` filter) and sweep the item move stream
(`ItemService.SendItemMove`, also a broadcast); additionally gate the apply side on a live world
(`HarmonyTraverse.HasLiveWorld`) as defense in depth.

Related finding, same root shape (recorded, not fixed): after re-entry the guest held 505 items
(`h-w3-guest-dupcheck.json`; the census probe read 504 at its own moment) against the host's 254
(`h-w3-host-dupcheck.json`; the census probe read 253) — 252 with a CUO id + 253 without, zero duplicate
ids — the keyframe materializes copies before the re-entering member's own generation has produced the
bind targets. Cover it with the same "receiver baseline ready" gate or split it into its own ticket.

## Limits (updated by batch `20261002-h`)

- One session, two windows; the keyframe cadence is adaptive, so a storm's start aligns to the next
  keyframe after the leave and another session may see a different delay.
- The instantiate-null half did not reproduce in either window (no `breaksNow` item); batches
  `20261002-c`/`-d` remain its only direct evidence. The `GroundBlood.Start` native frame was not staged.
- The c/d "no `[BrokenItemUpdate]`" readings are SHAPE-FILTERED extracts, not unfiltered window absences:
  batch `20261002-h` found the line by the hundred with an unfiltered read.
- The guest's post-re-entry 505-item state is read once; the duplicate finding's mechanics are a
  next-cycle measurement.

## Next step

- Implement the fix through the normal cycle: a red test pinning "no world-item rows are sent to a member
  that reports InMenu" (host targeting) and, if in scope, "no materialization without a live world"
  (guest gate); then the fix, gates, deploy and a re-run of the re-entry window against the new artifact.
  The recipe is the batch-`20261002-h` runbook and record; read `[BrokenItemUpdate]` FIRST and re-read an
  absence once.
- Keep the diagnostic-first reading rule: a staged window is read for `[BrokenItemUpdate] … (reason) …`
  before the shape is re-derived from the bare `Item.DMD<Item::Update>` frame.
- A burst whose stack names `Utils.Create` / `RuntimeEntityFactory` belongs to
  `done/enemy-runtime-spawn-classification.md`, not here.
