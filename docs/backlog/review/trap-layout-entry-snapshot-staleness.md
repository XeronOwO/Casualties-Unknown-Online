# The world-entry trap-layout fanout can carry an entity the host has removed

- Status: Review
- Priority: Low-Medium
- Category: Network / sync coverage / world entities
- Source: the W6 landing record (`review/trap-layout-snapshot-recovery.md`) — the in-session repair re-derives the table from the live scene, the entry fanout still sends it as last derived
- Related: `review/trap-layout-snapshot-recovery.md`, `review/sync-cadence-review.md`, `docs/decisions/active.md` (191)

## Problem (evidence)

`WorldEntryFanout.Send` sent `TrapLayoutRegistry`'s table to a member on its world-entry
edge (first InWorld, reconnect-while-InWorld) exactly as it was last derived. The table is
only as fresh as its last SCAN: the generation-finished edge records the layer's entities
once, the layer-boundary reset CLEARS the table (`WorldService.ResetDamagedBlocks` →
`ResetWorldLayerTables` → `ClearWorldFacts(includeKernelWorldEntities: true)` →
`EntityEventChannel.ResetTrapLayouts`), and since W6 the 60 s in-session repair re-derives it
from the live host scene (`WorldEntryFanout.SendInSessionRepair` →
`TrapLayoutScanner.RefreshLayout` → `IWorldControl.ReplaceTrapLayout` — the chain as it stood
before this landing; that method no longer exists, see the landing record). An entity the
world removed after the last scan was therefore still in the table — a self-destructed
turret, a broken crystal — and a table the boundary reset had cleared sent nothing at all,
which left the entering member with its own diverged generated traps.

A member whose entry group landed inside that window materialized the phantom: the guest's
apply is an absolute align (`TrapLayoutApplication.Apply` → `TrapLayoutAlign`, materialize
missing / destroy surplus) against the snapshot, and a freshly generated guest world has no
such entity, so the join spawned one the host did not own. The next repair corrected it (the
refreshed table made the guest destroy the surplus), so the divergence was transient
(≤ 60 s), but a phantom trap is a live hazard on the guest in the meantime.

## Goal

The member-facing layout send is always derived from the host's live scene at send time, so an
entering or reconnecting member never materializes an entity the host has removed.

## Design direction (decide at implementation)

1. **Refresh on the send path** — the fanout re-derives the host-side table right before
   it sends the layout. Runtime cannot scan the game scene, so this needs a narrow Runtime
   seam the adapter supplies at construction (an interface the adapter implements and DI
   resolves; NOT a late `AttachXxx` hook — late wiring is forbidden by the architecture
   rules). This must cover BOTH entry paths: `SceneStateHandler`'s InWorld edge and
   `HandshakeHandler`'s reconnect-while-InWorld (the fanout, not the adapter's scene
   event, is the shared point — the handshake path sends BEFORE it fires
   `RemoteSceneChanged`).
2. **Send the layout after the entry group** — keep the current ordering and have the
   adapter re-derive and re-send the layout on the same edge. Covers both paths only if
   the adapter is told about the reconnect case too, so the seam in (1) is the cleaner one.
3. **Accepted loss** — rejected: the accept-first rule requires the host to be able to
   represent what it accepts, and a phantom trap is exactly a record with no owner.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | An entity was removed (self-destruct / break) and a member enters the world before the next repair | The entry group carries the LIVE table: no phantom is materialized (an empty live scan is row 4's fail-safe instead) |
| 2 | Reconnect while InWorld inside the same window | Same; the member's existing copies are not duplicated |
| 3 | An entity is removed while every member stays in the world | Unchanged: the 60 s repair (W6) destroys the surplus |
| 4 | The host's scene scan sees nothing (world in transition) | The fail-safe keeps the last known table (never a mass destroy) |
| 5 | Third-party guest | All peers converge to the same layout |

## Non-goals

- The in-session repair itself (landed as W6, `review/trap-layout-snapshot-recovery.md`).
- Removing entities from the layout for reasons other than an observed live-scene scan.

## Landing record (2026-09-19)

**What landed.** Design direction 1: the freshness belongs to the SEND, and it has one owner
instead of two places.

- A narrow Runtime port, `ILiveTrapLayoutSource.RefreshFromLiveScene`
  (`src/CasualtiesUnknownOnline.Runtime/Session/World/ILiveTrapLayoutSource.cs`), is consumed
  by `WorldEntryFanout` as an OPTIONAL constructor argument — the same convention
  `INativeWorldFacts` uses, so a Runtime-only composition (the test host) still builds and
  sends the table as last derived instead of failing to construct the fan-out.
- BOTH group sends call it before they read the table: `Send` (the InWorld edge AND the
  reconnect-while-InWorld handshake) and `SendInSessionRepair` (the periodic wave and the
  entry-window repeat repair). The fan-out is the shared point of both entry paths, which is
  why the port is consumed there and not at the adapter's scene event: the handshake path
  sends before it fires `RemoteSceneChanged`.
- The Game Adapter's implementation moved out of the generation-edge scanner into a
  DI-registered `LiveTrapLayoutSource`
  (`src/CasualtiesUnknownOnline.GameAdapter/World/LiveTrapLayoutSource.cs`), registered by the
  plugin's composition root next to `NativeWorldFacts`. It keeps the old
  `TrapLayoutScanner.RefreshLayout` guards (host role, not while a layer is generating) and
  adds a third: a repeat inside ONE frame is not scanned again — the repair wave calls it once
  per in-world member inside one frame, and a scan is one `FindObjectsOfType` pass per
  sync-domain component type, so a WAVE costs one scan however many members it heals. The
  refresh also runs at moments W6 never scanned (the InWorld edge, the reconnect handshake and
  every entry-window repeat repair): the count per wave is unchanged, the count per session is
  not — every entry, reconnect and repeat now re-derives once.
- `TrapLayoutScanner.RefreshLayout` is deleted and `WorldEventSync` lost the trap-layout
  dependency entirely: W6's own lesson — the entry group and the cycle's subset must not be
  owned in two places — now holds for the re-derive as well.

**Acceptance matrix results.** Row 1: `WorldEntry_ReDerivesTheLayout_SoARemovedEntityIsNeverSent`
(red on the pre-fix tree, green after). Row 2:
`GuestReconnects_WhileStillInWorld_ReceivesTheLiveLayout` in `ReconnectWorldSnapshotTests`
(the reconnect send is the same `Send`, driven through the real handshake restore of
`member.InWorld`). Row 3: `InSessionRepair_ReDerivesTheLayout_TheSameWay`. Row 4:
`AnEmptyLiveScan_KeepsTheLastDerivedTable` — the port runs and the registry's empty-scan
refusal keeps the table, so the last table still rides. Row 5:
`EveryEnteringMember_ReceivesTheSameLiveTable` (both guests converge on the live table). A
guard case pins the optional-port behaviour: `WithoutThePort_TheSendCarriesTheLastDerivedTable`.

**Red.** Recorded on the tree with the seam present but not yet called (the fix's own call
site absent, so the failure is behavioural, not a missing type). The focused family is
`--filter "FullyQualifiedName~TrapLayoutEntryFreshnessTests|FullyQualifiedName~ReconnectWorldSnapshotTests"`
(9 cases): 5 failed / 4 passed, the five failures being the entry case
(`the entry send must re-derive the table first, refreshes=0`), the reconnect case, the repair
case, the third-party case and the empty-scan case (`the send must attempt the re-derive,
refreshes=0`); the four passes are the three pre-existing reconnect cases and the
optional-port guard. After the two call sites landed the family is 38/38 under
`--filter "FullyQualifiedName~TrapLayout|FullyQualifiedName~ReconnectWorldSnapshot"` (the wider
`~Reconnect` sibling filter counts 41, the extra three being other reconnect classes); the full
suite with build is 3454 passed / 0 failed plus 69/69 normative gates (the
delivery-checklist case is the one that is red until THIS cycle's boxes are checked, so an
interim run that filters it out reports 68/68 — the final, unfiltered run is 69/69).

**Verified / not verified.** Machine-checked: the port's call on both send points, the wire
delivery of the live table to the entering, reconnecting and third-party members, the
registry's refusal path through the port, and the optional-port composition. NOT exercisable in
the test host: the adapter's own guards and the per-frame de-duplication
(`Time.frameCount`, `TrapEntityScan`'s `FindObjectsOfType`, `HarmonyTraverse.IsGenerating`) —
that class is code-reviewed and belongs to the unified dual-client acceptance pass, like the
rest of the adapter's Unity-typed wiring. The same boundary leaves the entry-window repeat
repair covered by construction only (it calls the `SendInSessionRepair` the direct-call cases
exercise), and row 4 drives the Runtime seam through the fake source rather than through
`LiveTrapLayoutSource`.

**Limits (recorded, not hidden).** (1) The fail-safe is asymmetric and inherited from W6: an
EMPTY scan against a non-empty table is refused, but a NON-EMPTY and PARTIALLY visible scan is
accepted and replaces the table wholesale, and the member-side apply destroys surplus — so a
partially loaded scene could make peers drop traps. No threshold guard was added (a shrink
threshold would be an unmeasured tolerance, the same smell as a fixed latency window), and the
existing Information line `host layout re-derived from the live scene: <N> entries` is what
lets the acceptance pass compare counts across scans. The new call sites fire the refresh at
more moments than the steady wave did, which is the one way this landing widens that exposure;
it is not exercised by any test. (2) Whether the generation-finished edge's OWN scan survives
into a send is order-dependent and NOT settled here: `TrapLayoutScanner.Update()` runs before
`WorldEventSync.Update()` in the adapter's pump (`GameAdapter.cs`), and the boundary capture
inside the latter clears the table (`TrapLayoutRegistry.Reset`), so unless a `SetBlock`-driven
earlier capture wins that frame the scan's records are wiped in the same frame they were
written. `TrapLayoutRegistry.Reset` now logs the clear together with the number of entries it
dropped, and the scanner's own line carries `Time.frameCount`, so a field log that shows the
clear right after the scan and dropping exactly the entries that scan reported settles it in
one session (a clear with a different count is another generation's table). Either way this
landing does not depend on the answer: the send-time re-derive is
the writer that matters, and the generation-edge record remains the fail-safe's fallback when a
live scan comes back empty.

**Evidence.** Matrix row W6: the fallback and backfill cells re-pointed to the send-path seam,
the recorded entry-fanout residual closed, the feature cell names the two new files, anchors
21 → 27 (`docs/evidence/sync-coverage-evidence.json` 982 → 988 entries); decision 191 records
the rule; the W6 landing record names this ticket as the successor of its residual.
