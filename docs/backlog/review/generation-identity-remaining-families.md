# The remaining generation-relative report families

- Status: Review
- Priority: Medium
- Category: Network / protocol / world generation (attribution of world reports)
- Source: `review/world-layer-generation-identity.md` — the family audit of the cycle that stamped the block-report family; these two families were audited and could NOT be recorded safe, so they are carried here instead of being declared covered
- Related: `review/world-layer-generation-identity.md`, `review/trap-layout-snapshot-recovery.md`, `review/runtime-entity-spawn-backfill.md`, `review/runtime-entity-creation-rejection.md`

## Problem (evidence)

The block-report family (block state, block damage, partial damage) now carries the kernel run
baseline's `(RunEpoch, LayerIndex)` on the wire and refuses a stale previous-layer report
(`review/world-layer-generation-identity.md`). Two other families were audited in that cycle and are
generation-relative in the same way — their keys are resolved against generated terrain — but they
were not stamped:

1. **Trap layout** (`TrapLayoutSnapshot` / `TrapLayoutEntryMsg`, host → guest): the entries are
   positions of generated entities and the guest MATERIALIZES them. The fan-out is generation-time
   and the 60 s repair re-derives the table from the host's live scene, but a repair landing across
   the guest's own layer change materializes the previous layer's traps into the new one — nothing on
   the message attributes it.
2. **Runtime entity creation** (`EntitySpawnedMsg`, guest → host, re-reported on the shared
   pending-report fallback — 5 s inside the guest's entry phase, the steady 60 s after it; see
   `review/guest-report-fallback-first-resend.md`): the host
   materializes a copy at the reported position. The reporter's pending table is dropped at its own
   generation boundary, which bounds the exposure to the in-flight window, but the wire carries no
   generation, so the host cannot tell a report of the world it is simulating from one of the world
   the reporter just left.

The families that WERE recorded safe stay safe for the recorded reason (the receiver resolves the
entity through its own live scene and refuses a position where nothing exists —
`EntityEventMsg`, `BuildingEntityDamagedMsg`, `BuildingEntityOpenedMsg`; presentation-only and
item-keyed messages carry no generated-terrain key).

## What landed

**1. One identity, one comparison, one refusal log.** `WorldReportGenerationGate` (Runtime, pure
bookkeeping) now owns the single "is this report about MY world?" decision: it relates an arrived
stamp to this side's kernel run baseline through the existing pure
`WorldReportGeneration.Relate` and logs every STALE verdict once with the report's kind, its sender
and BOTH generations. The previous cycle's `BlockReportChannel` was moved onto it unchanged in
behaviour, so the block, trap-layout and runtime-entity families refuse a report of another
generation in the same words instead of growing three copies of the comparison. `Unknown` (no stamp,
or no committed run baseline here) is never treated as fresh and still leaves each caller's pre-stamp
behaviour in place.

**2. The trap-layout snapshot is stamped, and refused whole.** `TrapLayoutSnapshotMsg.Generation`
(proto member 2) is set by `TrapLayoutRegistry.SendSnapshot` from the kernel run baseline, read at
send time — so both send sites (the world-entry fanout and the 60 s in-session repair) carry the
generation the layout was derived in, and a repair re-derived in the layer being simulated now can
never reach a guest as the previous layer's layout. `TrapLayoutEntryMsg` itself carries no stamp on
purpose: the snapshot is this family's only wire carrier, and the one-carrier rule the previous cycle
set applies per message, not per payload type. The receiving seam
(`EntityEventChannel.FireTrapLayoutReceived`, reached from `TrapLayoutSnapshotHandler` with the
sender and the stamp) asks `TrapLayoutRegistry.IsStale` and returns before the event fires, so
nothing of another generation's layout reaches `TrapLayoutApplication` — no materialization, no
destroy-surplus pass — and the refusal is logged with both sides of the comparison.

**3. The runtime-entity creation report is stamped on every send, and refused before anything is
created.** `EntitySpawnedMsg.Generation` (proto member 12) is attached by the channel itself at send
time — the live report, the host's relay/broadcast and the guest's fallback re-report all read the
kernel run baseline live, and the record taken for the accepted-creation table carries the same
stamp, so the table and the wire describe one world. `RuntimeEntitySnapshotMsg.Generation` (proto
member 3) is set by `RuntimeEntityRegistry.SendSnapshot` for the world-entry/60 s absolute table.
`FireEntitySpawnedReceived` refuses a STALE report WHOLE — nothing is created, recorded or relayed,
and the reporter's pending entry is deliberately left alone (it belongs to the reporter's own world)
— and the HOST answers the reporter through the existing rejection path, extended with
`RuntimeEntityRejectReason.StaleGeneration`: the answer drops the reporter's pending re-report and
asks the adapter to remove its local copy, idempotently, exactly as the prefab refusal does. A guest
never answers a stale relay (a guest receiving one refuses it silently). A stale absolute table is
refused with ONE log line for the whole table, before any entry or animal acknowledgement is applied
— per-entry logging would have produced one warning per row.

**4. Protocol 29 → 30** (`ProtocolVersion.Current`, `docs/decisions/active.md` #137,
`docs/api/mod-api.md`): this is a wire change on three messages and one rejection reason. A peer
without the stamp would report unstamped (compared as UNKNOWN — the old behaviour) and would
materialize this side's stamped reports without the check, so across a layer boundary the two sides
would disagree about which world a trap layout or a creation belongs to.

**5. Tests.** Two new suites, one per family
(`TrapLayoutGenerationIdentityTests`, `RuntimeEntityGenerationIdentityTests`): the stale refusal and
its consequences, the same-generation acceptance, the UNKNOWN (no stamp / no baseline) pre-stamp
behaviour, the guest-side refusal of a stale relay and of a stale absolute table, and the third-party
view (a stale snapshot reaches neither member). What the SEND PATH attaches is proven at frame level:
the stamp is decoded off the wire (`EverySend_CarriesTheHostsOwnGenerationStamp`,
`TheLiveReport_CarriesTheReportersOwnGenerationStamp`,
`TheFallbackReReport_CarriesTheCurrentGenerationStamp`), never handed to the caller. The host-side
refusal test drives a real pending report: the live report is swallowed on the wire, the host
descends, and the outstanding report is re-sent with an explicit stale stamp — the refusal, the
answer and the end of the pending entry are all observed.

**6. Evidence.** The audit matrix rows W6 and E3 carry the new decision text and their declared
anchor counts moved with the evidence file (W6 13 → 21, E3 60 → 71; `count` 861 → 880), and the
anchors the previous cycle owned that this change re-pointed were updated in the same pass
(`ProtocolVersion.Current = 30` — the value was re-pointed to 31 when
`review/recipe-unlock-fallback.md` bumped the protocol again — and the shared gate call in
`BlockReportChannel`).

**7. Independent adversarial review** (one round, fresh context, frozen tree). It reproduced every
number and could not falsify the mechanism: all three seams cover every production path
(`TrapLayoutReceived` has exactly one production subscriber), `.Generation` is written only by the
channel and always BEFORE a table takes the record, a stale stamp cannot be applied after a boundary
because both sides clear their tables there, and the block family's move onto the shared gate is
behaviour-identical (same argument order, same log text). Its findings were fixed in this same cycle,
before the commit:

- **The entry/repair groups sent stamped tables before the run baseline.** Both groups put
  `TrapLayoutSnapshot` (and the block tables) BEFORE `SendCheckpoint`, which is what carries
  `RunEpoch`/`LayerIndex` to the member — so a member that had not yet applied the layer-advance
  batch would compare the host's CURRENT-generation table against its own older baseline and refuse
  it, with no second chance before the 60 s repair. The kernel checkpoint now goes FIRST in
  `WorldEntryFanout.Send` and `SendInSessionRepair`, before every generation-stamped absolute table
  (block state, block damage, trap layout, runtime entities), and a test pins the order.
- **The fallback re-report test's comment over-claimed.** It described "a pending entry that survived
  a generation boundary re-stamped with the new generation"; that variant cannot exist, because the
  boundary clears the pending table. The comment now says what the test pins (the live read at send
  time) and the non-goals record why the boundary variant is unreachable.
- **The stale answer's no-collateral case was unpinned.** A second creation in the SAME cell keeps
  its pending report when the first one is refused (the answer is keyed by creator + per-creator
  sequence); the new test pins that, and the adapter-side copy removal of the answered creation is
  named for the acceptance pass.
- **`WorldGenerationMsg`'s doc comment** pinned "protocol 29" in a current-tense sentence; it now
  records that the vocabulary was introduced at 29 and extended to these families at 30.

## Verification

| # | Scenario | Expected | Evidence |
|---|---|---|---|
| 1 | A trap-layout repair crosses the guest's layer change | Refused as stale; no previous-layer trap is materialized | `TrapLayoutGenerationIdentityTests.SnapshotFromAPreviousLayer_IsRefusedWhole_AndNothingIsMaterialized` (the snapshot arrives — 1 frame — and the guest's `TrapLayoutReceived` never fires) |
| 2 | A runtime-entity creation report crosses the host's layer change | Refused as stale; the host neither creates nor relays it, and the reporter's local copy is answered (its pending report does not re-report forever) | `RuntimeEntityGenerationIdentityTests.ReportFromAPreviousLayer_IsRefusedWhole_Answered_AndNeverRelayed` (swallowed live report, host descended, stale re-report refused; registry 0, no relay to the third member, `RuntimeEntityRejected` received, pending count 0) |
| 3 | Same generation | Unchanged: the entry materializes exactly as today | `TrapLayoutGenerationIdentityTests.SnapshotOfTheSameGeneration_IsApplied`, `RuntimeEntityGenerationIdentityTests.ReportOfTheSameGeneration_IsAcceptedRelayedAndRecorded` |
| 4 | No stamp / no baseline | Pre-stamp behaviour (UNKNOWN is never treated as fresh) | `...ASnapshotWithoutAStamp_KeepsThePreStampBehaviour`, `...ReportWithoutAStamp_KeepsThePreStampBehaviour` (an unstamped report is accepted, and no rejection is sent) |
| 5 | Third-party view | Every peer's trap/entity world agrees with the host's after a layer change | `...AStaleSnapshot_ReachesNeitherPeer_WhenBothHaveDescended` (both members receive the frame and neither materializes it) + the `G2` relay assertion of row 2 |
| 6 | The stamp is on the wire, not added by the caller | Every send path stamps from the kernel run baseline, read live | `...EverySend_CarriesTheHostsOwnGenerationStamp`, `...TheLiveReport_CarriesTheReportersOwnGenerationStamp`, `...TheFallbackReReport_CarriesTheCurrentGenerationStamp` (frames decoded from the peer's transport) |
| 7 | A guest receives a stale relay / a stale absolute table | Refused; a guest never answers | `...AStaleRelay_IsRefusedByTheGuest_AndTheGuestNeverAnswers`, `...AStaleAbsoluteTable_IsRefusedWhole_BeforeAnyEntryIsApplied` |
| 8 | The entry group's ordering | Every generation-stamped absolute table follows the run-baseline carrier, so the entry edge cannot refuse the host's own current-generation table | `TrapLayoutGenerationIdentityTests.TheEntryGroup_SendsTheRunBaselineBeforeEveryStampedTable` (the guest's frame order: `KernelEnvelope` before block state / block damage / trap layout / runtime entities) |
| 9 | A stale answer while a second creation sits in the same cell | Only the answered creation's pending report ends | `RuntimeEntityGenerationIdentityTests.AStaleAnswer_EndsOnlyItsOwnCreation_NotAnotherOneAtTheSameCell` (pending count 1 after the answer, and the answered key's sequence is the only one reported) |

Cycle measurements (this tree): focused world-entity filter 86/86, main suite 3368 green, normative
gates 56/56, `dotnet build` 0 warnings / 0 errors, `dotnet format` exit 0.

## Known coverage gaps (declared, not silently carried)

- The adapter's own half is not executable in the test host (Unity types): the trap prefab
  materialization and the destroy-surplus pass, the runtime entity factory, and the
  rejection-driven local destruction rest on code review plus the unified dual-client acceptance
  pass — the same boundary `review/world-layer-generation-identity.md` recorded.
- The stale refusals' log lines are asserted by review, not captured: the runtime test host does not
  capture `ILogger` output for these channels, so what the tests pin is the refusal and its
  consequences.
- The stale answer's ADAPTER half is not executable here: the rejection resolves the reporter's local
  copy by its full creation key and hands it to the entity death funnel (idempotent when the copy is
  already gone). The Runtime half is pinned (only the answered key's pending entry ends); the
  dual-client pass should check the one line that needs a live world — a creation made in the new
  layer at the same cell survives a late stale rejection.
- The host leads the guest: a member whose own baseline is still the previous one refuses the NEW
  generation's report or table. Both fan-out groups now send the run baseline before the stamped
  tables, so the entry edge and the 60 s repair no longer trigger that refusal by their own ordering;
  what remains is the genuine in-flight case (a report that was stamped before a descent and arrives
  after it), which converges on the reporter's own boundary reset or the next repair cycle.
- Physical deployment and dual-client acceptance remain the user's release-cycle action;
  development-period verification is simulation/static by rule.

## Non-goals

- Re-stamping the families the previous cycle already covered or recorded safe.
- A shared frame-level generation header: the transport carries no world concept, and the previous
  cycle recorded why the per-message carrier was chosen (`review/world-layer-generation-identity.md`).
- Re-validating the expired pending entry on the reporter's side: its own generation boundary drops
  the pending table, which is what bounds the exposure; this cycle only adds the wire identity and
  the receiving seam's refusal.
