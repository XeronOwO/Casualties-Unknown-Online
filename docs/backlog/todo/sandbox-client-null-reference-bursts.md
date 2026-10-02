# Sandboxed clients log NullReferenceException bursts and an instantiate-null ArgumentException

- Status: Todo — Rejected (batch `20261002-i`: the menu NRE storm and both 251-copy rounds are gone
  against the deployed artifact, but row 5 fails — a re-entry still leaves 9 (guest) / 14 (alternate)
  unbound generation-time locals beside the host's bound set; record
  `docs/evidence/acceptance/sandbox-client-null-reference-bursts-20261002-i.md`)
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

Fix direction (implemented in batch `20261002-i`, see the Fix section below): target the periodic item
keyframe per member, members reported `InWorld` only (mirror the `SendInSessionRepair` filter) and sweep
the item move stream (`ItemService.SendItemMove`, also a broadcast); additionally gate the apply side on
a live world (`HarmonyTraverse.HasLiveWorld`) as defense in depth.

Related finding, same root shape (recorded, not fixed): after re-entry the guest held 505 items
(`h-w3-guest-dupcheck.json`; the census probe read 504 at its own moment) against the host's 254
(`h-w3-host-dupcheck.json`; the census probe read 253) — 252 with a CUO id + 253 without, zero duplicate
ids — the keyframe materializes copies before the re-entering member's own generation has produced the
bind targets. The batch-`20261002-i` fix removed both 251-copy rounds (host targeting + the live-world
apply gate), collapsing the armada to 9 / 14 unbound locals out of the host's 266 entries; the
remaining bind miss is this ticket's open work item.

## Fix (batch `20261002-i`)

The finding's direction is implemented; the batch-`20261002-i` re-run of the batch-`20261002-h` windows
is what judges it:

- Host targeting: `ISessionControl.InWorldRemoteSteamIds()` — the presence table's handshaken members
  reported InWorld — is the target set of the two STEADY-STATE world-item streams:
  `ItemSnapshotService.SendPeriodicItemSnapshot` (the periodic keyframe) and
  `ItemService.SendItemMove` (the position stream); `EnemySyncService` now delegates its identical
  private filter to the shared method. The kernel gained the targeted
  `IKernelProtocolControl.BroadcastItemStateStreamTo`; the untargeted `SendStateStream` entry point is
  deleted. `ItemService.PublishGeneratedItems` deliberately stays a handshaken-peer broadcast: it has
  to reach a member whose own generation is still running (the receiver holds it until that generation
  finishes, and the host's InWorld record for that member is still false there); its menu case is
  covered by the receiving gates below. Both streams' adaptive-rate input is the same in-world set
  (no in-world member -> the stream is not sent at all).
- Apply side (defense in depth): `ItemReconcile` applies a snapshot only while
  `HarmonyTraverse.HasLiveWorld`; `RemoteItemSceneOps.SpawnWorldItem` — the single materialization seam
  — refuses without a world scene and refuses on the guest while its own generation is still running
  (the keyframe re-delivers once live); `GeneratedItemApplication` holds the generation snapshot while
  the local generation runs AND while a run entry is still loading its world scene (the adversarial
  review's major-1 window: the host can publish before this side's world object exists), drops it only
  in the menu with no entry in flight, and clears it on session unbind.
- The re-entry duplicate round (the related finding) is covered by the same two halves: the host no
  longer sends the keyframe to a member whose host-side record is InWorld=false (the 11:41:00.837
  round), and the receiver refuses rows while its world is not live.
- Considered and deliberately left alone: the kernel committed-batch family (item spawn/drop/pickup/
  destroy) still broadcasts to every handshaken member — it carries command events, not table rows, and
  the apply-side guards above are what keep the out-of-world case harmless; `SendItemImpact` is a
  transient cosmetic replay with no item materialization and is not this family.

## Fix (batch `20261002-j`)

The residual was measured against the deployed artifact before the code changed: a re-entry's
authoritative table arrives ROW BY ROW (the world-entry repair's `ItemSpawn` commands land in
`ItemApplication.OnRemoteItemSpawned` → `RemoteItemSceneOps.SpawnWorldItem`) while this side's own
generation is still registering its last objects in `Item.Start` — `FindExistingAt` reads
`Item.allItems` and adopts only objects that carry no `ItemInstanceId` yet, so a row whose object had
not landed was materialized immediately and the late local object stayed beside it as an id-less
duplicate (the guest's window left 6; `total=291 withId=283 noId=8`).

- `RemoteItemSceneOps.SpawnWorldItem` keeps the adopt-first seam: a row binds the id-less object at
  its reported spot when there is one and is materialized otherwise — the ordinary landing.
- `ItemReconcile` (the periodic keyframe) drops id-less standalone world items the authority's table
  does not own — the rule `GeneratedItemReconcile.Apply` already runs at apply time, here on the
  keyframe's cadence — so a late local arrival converges instead of stranding beside its materialized
  row. Tutorial props are excluded (deliberately id-less until picked up).
- An earlier revision of this batch ALSO deferred such a row for a 2 s grace and retried adopting it
  (`DeferredWorldItemLanding` + `PendingWorldItemRows`, plus a sweep that skipped rows still
  waiting). The staging runs removed that revision: the guest's initial entry and the same client's
  5 fps re-entry each deferred the SAME seven rows (`k-entry-sweep-guest.log`, `k-probe-window.log`)
  and every `[ItemSpawn] deferred rows:` summary read `adopted 0 late` (the alternate's entry
  deferred the same seven; `k-entry-sweep-alt.log`). Those seven local objects sat 1.8–3.6 units
  from the authority row — outside the 1.5-unit adopt tolerance `FindExistingAt` shares — so for
  these seven rows neither the adopt retry nor the sweep's deferral guard could see them. The
  authority's copy then materialized and the sweep dropped the local object, converging to the
  host's census; the acceptance re-run below is what proves the sweep-only landing reaches the same
  end state without the 2 s hold. The queue, its pump, its sweep guard and its unit tests were
  deleted with the revision. The queue also waited for a container row's parent before landing the
  child; without it a child row lands immediately, which `BindToContainer`'s positional fallback and
  the keyframe's content alignment cover.

## Acceptance (batch `20261002-i`, agent-run)

The batch-`20261002-i` record re-ran the batch-`20261002-h` window recipe against the deployed
artifact (`0.1.0+b6702483`) and judged seven machine rows:

- Rows 1–4, 6, 7 pass: the menu dwell (window 1 ~97 s / ~10 keyframe cycles, window 2 ~40 s / ~4)
  holds zero item copies, zero `[BrokenItemUpdate]` and zero `Item.DMD<Item::Update>` lines on the
  out-of-world client; the host's keyframe goes from 2 to 1 in-world member(s) on the first cycle
  after the leave report lands; the in-world peer keeps its ~8–10 s cadence; and the generation
  publish still binds at entry (host published 266 ground items, both guests applied 266, all three
  sides at 269 items).
- Row 5 fails: the re-entry binds 257 (guest) / 252 (alternate) of the host's 266 entries by position
  and leaves 9 / 14 unbound local objects — each missed host entry is materialized beside the local
  object, so the client ends 9 / 14 objects above the host's table with zero duplicate ids.

The remaining work is that bind: the entry snapshot's positional adopt misses the generation-time
objects the receiver produces late (or whose host copy has drifted past the tolerance) and materializes
the host's entry beside them instead of adopting them.

## Limits (updated by batch `20261002-h`)

- One session, two windows; the keyframe cadence is adaptive, so a storm's start aligns to the next
  keyframe after the leave and another session may see a different delay.
- The instantiate-null half did not reproduce in either window (no `breaksNow` item); batches
  `20261002-c`/`-d` remain its only direct evidence. The `GroundBlood.Start` native frame was not staged.
- The c/d "no `[BrokenItemUpdate]`" readings are SHAPE-FILTERED extracts, not unfiltered window absences:
  batch `20261002-h` found the line by the hundred with an unfiltered read.
- The guest's post-re-entry 505-item state is read once; the duplicate finding's mechanics are a
  next-cycle measurement.
- The three receiver guards (`ItemReconcile`, `SpawnWorldItem`, `GeneratedItemApplication`) have no
  automated coverage: the GameAdapter's Unity dependency keeps them out of the test suite, so the
  batch-`20261002-i` re-run is their only runtime evidence. A refusal logs at Debug — the production
  default `Information` hides it, and the runbook enables Debug on all three clients.
- New side effect of the guest-generation refusal: a trap/building-death drop's transient presentation
  facts (`FreshItemDrop`, initial velocity/rotation/angular velocity — deliberately outside the kernel
  projection) are lost on a guest that is mid-generation when `ApplyTrapDropPresentation` is refused;
  the item fact itself converges through the committed batch/keyframe (bounded, accepted — the same
  family as the matrix's other presentation-loss notes).
- Batch `20261002-j`'s landing is the keyframe's late-local sweep: a late-landing local object the
  authority's table has no row for is dropped and the authoritative row is materialized, so a client
  whose local objects land late converges through the sweep (observed under a pinned 5 fps: seven
  rows deferred by the revision that was then removed, seven late id-less locals dropped, settled at
  the carried count).
- The late-local sweep has no automated coverage (the GameAdapter's Unity dependency keeps the scene
  half out of the suite); the batch-`20261002-j` staging runs are its runtime evidence.

## Next step

- Batch `20261002-j`'s landing is in the tree (adopt-first `SpawnWorldItem` plus the keyframe's
  late-local sweep; the deferred-queue revision was removed). What remains is the acceptance re-run —
  `.acceptance/20261002-j/runbook-20261002-j.md` (two windows; a probe on each re-entering client
  plus `dupcheck` on both sides) against the deployed artifact. Green means the re-entering client's
  `noId` count is the carried items only, `dupIds=0`, and both censuses sit at the host's.
- Keep the diagnostic-first reading rule: a staged window is read for `[BrokenItemUpdate] … (reason) …`
  before the shape is re-derived from the bare `Item.DMD<Item::Update>` frame.
- A burst whose stack names `Utils.Create` / `RuntimeEntityFactory` belongs to
  `done/enemy-runtime-spawn-classification.md`, not here.
