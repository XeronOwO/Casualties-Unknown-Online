# Guest command loss: local pickup/drop result is not reconciled

- Status: Review
- Priority: Medium
- Category: Network / sync coverage / items
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row I5, plus the empty-host-table caveat the audit attached to row I1)
- Related: `review/guest-block-mutation-re-report.md` (same swallowed-guest-report family), `review/session-control-convergence.md` (the bounded re-report window this one follows), `todo/carried-inventory-registration-re-report.md` (row I8, the remaining item-domain gap)

## Problem (evidence)

An item command is a REPORT of a native action that already happened on the guest
side: the item domain's report funnel (`src/CasualtiesUnknownOnline.Runtime/Session/Items/ItemMessageFlowService.cs`,
`Kind = WireCommandKind.ItemPickup` and its drop/destroy/creation siblings) sends a
`CommandEnvelope` through `KernelProtocolService.SendCommand` on the reliable
`NetMsg.KernelEnvelope` channel, and the adapter's triggers are the game's own
pickup/drop hooks (`src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyItemPatches.cs`).

Reliability only covers a lost frame while the peer is reachable. The documented
lazy-P2P swallow window drops frames the sender never learns about, and that channel
had no re-report at all:

- Nothing re-sent a report. `src/CasualtiesUnknownOnline.Runtime/Session/Items/PendingItemCreations.cs`
  is ORDERING only — its own contract reads "This is ORDERING, never a latency
  window: nothing here waits, expires or measures time" — and the drop machine holds
  a report one frame for the final throw velocity, no longer
  (`src/CasualtiesUnknownOnline.Runtime/Session/Items/DropPendingState.cs`).
- The absolute world-item keyframe converges the **host's** table, never the guest's
  local result, and it is skipped while the host's table is empty
  (`src/CasualtiesUnknownOnline.Runtime/Session/Items/ItemSnapshotService.cs`: the
  send path returns when the host holds no world item).
- The 1 Hz character snapshot does not feed the host's arbitration transfer table
  (`CharacterDataStore.SaveCharacterData` stores the snapshot), so it cannot register
  a pickup the host never heard about.
- The host answers a refused operation at once, with the refused creation's own
  reason when its tombstone holds one, and logs a protocol violation when the sender
  simply never reported the creation
  (`src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelProtocolCommandHandler.cs`).

So one swallowed frame left the two sides disagreeing about where the item is for the
rest of the run — the guest holding it while the host's table still had it in the
world, or the reverse — and nothing healed it short of a reconnect.

*(The 2026-09-09 problem text cited a 500 ms hold window and a `PendingPickupQueue.cs`
that no longer exist: the creation-before-operation invariant replaced the hold with an
immediate refusal, so the citations were re-derived from the current tree before this
round's implementation.)*

## Goal

A swallowed guest item command whose native local action already happened converges
without a reconnect, in one of the two explicit ways the ticket allowed.

## Design direction (frozen before implementation)

Direction 1 of the original three — **command re-report / ack, idempotent by
`OperationId`**: the guest keeps its unacknowledged reports and re-sends them until
the host's committed batch (or a rejection) is seen. Direction 3 (revert the guest's
local result from the next authoritative snapshot) was rejected: the native action is
the player's own and already happened, and decision 184 puts that judgment on the
client it happened to — the host must LEARN the operation, not roll it back.

## What landed (2026-09-19)

**1. The guest's per-item queue of unacknowledged reports.**
`src/CasualtiesUnknownOnline.Runtime/Session/Items/GuestCommandReconciliation.cs` (new,
one `ICuoService`, its own time edge) keeps every unacknowledged report as the WHOLE
FRAME that was sent — so a repeat carries the same `OperationId`, the same run epoch
and the same payload — and re-sends it at a 5 s cadence for at most 12 repeats per
report (each report re-arms its own cadence, so an item's queue is not bounded by one 60 s window). It owns the four kinds whose native local action has already happened:
creation, pickup, drop, destroy (`ItemSpawn`/`ItemPickup`/`ItemDrop`/`ItemDestroy`
payloads on `KernelEnvelope`).

**2. The reports are kept PER ITEM AND IN SEND ORDER — that ordering is the
mechanism.** The independent review's MAJOR-1 falsified the first version of this
window, which kept one report per item (newest wins): the adapter's generation-time
item report sends the CREATION and the pickup that follows it back to back in one
frame (`src/CasualtiesUnknownOnline.GameAdapter/Items/PickupSync.cs`), so the pickup
evicted the creation — and a lost creation is the one report no later one can stand in
for: the host answers every operation on that item with "creation this host has never
judged", the guest's pickup is rolled back, and the item can never be used again in
that run. The same hazard sits one level down: a pickup behind a LOST drop is refused
as a conflict ("item is already carried") because the host still holds the older
location. So the item's reports are queued (cap 8, the oldest non-creation dropped at
the cap) and replayed in their original order — creating before operating, dropping
before re-picking.

**3. A report leaves the window on either verdict the wire already carries.**
A committed batch carrying the report's own operation id closes it
(`KernelProtocolService.HandleCommittedBatch` observes this at RECEIPT, before the
revision guard, because the host answers a repeat with the ORIGINAL batch). A refusal
naming the item closes the NEWEST report of that item's queue
(`KernelProtocolService.HandleCommandRejected`), while the older reports stay
outstanding so the host converges to the state the refusal left behind; a refusal of a
creation (the host's tombstone then answers every later report about that item) drains
the queue one answer at a time. Each report also has its own budget, and the queue is
dropped wholesale when the world baseline is replaced (a restored checkpoint or a new
session).

**4. The host needed no change at all.** `GameStateKernel.Execute` returns the original
decision for a known `OperationId`, `ItemKernelAuthority.TryExecute` fires
`BatchCommitted` on that path, and `KernelProtocolService.BroadcastCommittedBatch`
re-broadcasts that same batch — so a repeat is always answered and never commits
twice. The same property makes a wire-level duplicate (a retransmission delivered
twice) a single operation.

**5. The budget is spent only by reports that actually left** (`PacketSender.TrySend`
reports the transport's verdict, so a cancelled send cannot spend the window), and a
report the host never answers is NAMED in a warning carrying its kind, item, operation
id and repeat count instead of trickling for the rest of the session. The warning is
asserted by a test, not only described.

**6. No wire change.** Nothing in this cycle adds, removes or re-shapes a `NetMsg`, a
`WireCommandKind`, a `WireEventKind` or an `AdaptiveStreamId`; the protocol version
stays 31 and the whole mechanism is convergence over messages that already exist.

**7. Tests.** `tests/CasualtiesUnknownOnline.Tests/Items/GuestCommandReconciliationTests.cs`
(one new class, 13 cases, `[Trait("Category", "Integration")]`): the three swallowed
operations converging, the empty-host-table destroy, the swallowed creation surviving a
refused pickup (MAJOR-1), the fully swallowed creation+pickup chain replaying in order
and keeping the player's carry, the drop-behind-a-refused-pickup converging through the
older report, the spent budget naming its loss (with the warning line asserted through
`RecordingLoggerFactory`), the repeated judged command committing once, the wire-level
duplicate absorbed by the operation window, a third party's surface, and the session
end. The swallow is injected precisely: `LinkFaults.DropMessageId` drops one message id
on one link while the link still reports the send as successful — the production
swallow contract.

## Acceptance matrix (result)

| # | Scenario | Result | Evidence |
|---|---|---|---|
| 1 | Guest pickup command dropped | Converges without a reconnect: the host learns the pickup, its transfer table records the guest's carry, the third party sees the same location | `SwallowedPickup_ConvergesThroughTheReReportWindow` |
| 2 | Guest drop command dropped | Converges: the host puts the item back in its world table and clears the transfer entry | `SwallowedDrop_ConvergesThroughTheReReportWindow` |
| 3 | Destroy command dropped while the host table is non-empty | Converges: the re-report removes the item from the host's world table | `SwallowedDestroy_ConvergesThroughTheReReportWindow` |
| 4 | Destroy command dropped while the host table is empty | Converges: the keyframe is skipped for an empty table, so the re-report is the only heal — the host's transfer entry for the guest's last carried item goes away | `SwallowedDestroyOfTheLastCarriedItem_ConvergesWithAnEmptyWorldTable` |
| 5 | Duplicate/replayed re-report | Idempotent by `OperationId`: a repeat is answered from the host's operation window and commits once; a wire-level duplicate (retransmission delivered twice) is one operation and is not refused even after the item's state moved on | `RepeatOfAJudgedCommand_IsAnsweredFromTheOperationWindowAndCommitsOnce`, `DuplicateWireDelivery_IsAbsorbedByTheOperationWindow` |
| 6 | Reconnect | Same behaviour as today: the window is dropped at the session edge and the rejoin re-baselines from the host's checkpoint + world-entry item snapshot (declared limit below) | `SessionEnd_DropsTheUnacknowledgedCommands` |
| 7 | Third-party view | All peers agree on the item's location/ownership, and the healed report materializes no duplicate on the third party | `ThirdParty_SeesTheHealedLocationAndNoDuplicate` |
| 8 | In-flight race (pickup before spawn report) | The creation-before-operation ordering is unchanged, and the pair the review found is now covered: a creation whose report was swallowed stays queued and heals the item (with the refused pickup not repeating), and the fully swallowed pair replays in order and keeps the player's carry | `SwallowedCreation_IsStillQueuedAndReReportedAfterALaterPickupIsRefused`, `BothReportsSwallowed_TheChainReplaysInSendOrderAndKeepsThePlayersCarry` |
| 9 | A drop lost and followed by a pickup (added by the review) | The refusal pops the pickup only; the older drop keeps its place and its re-report lands, so the host converges to the state the refusal left behind (item back in the world) | `SwallowedDropBehindAPickup_ConvergesThroughTheOlderReport` |

## Verification

- Focused: `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~GuestCommandReconciliation"`
  — 13/13; the touched neighbours (`KernelProtocol`, `ItemArbitration`, `ItemDestroy`,
  `PendingItemCreations`, `SessionControlConvergence`) 74/74 together.
- Gates: `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests/...` — 56/56,
  the evidence contract included (every anchor quote still matches its source, the
  declared anchor counts match the file, the verdict summary matches the rows).
- Full: `dotnet test CasualtiesUnknownOnline.slnx` (with build) — recorded in the
  handoff; the implementation-complete tree measured 3397 passed / 0 failed before the
  checklist pass.
- Independent adversarial review: one FULL round plus a fix-verification round, both in
  a fresh context against the frozen tree, before the commit.

### Review findings and fixes (same commit)

- **MAJOR-1 — the newest-wins slot evicted the creation, the one report no later one
  can replace.** The adapter's generation-time item report sends the creation and the
  pickup in one frame; with one report per item the pickup evicted the creation, and a
  lost creation makes every later report about that item a refused protocol violation —
  the item was unusable for the rest of the run, which is the exact divergence this
  ticket exists to remove. Fixed by the per-item send-order queue, the creation-first
  replay and the cap's creation preference; pinned by
  `SwallowedCreation_IsStillQueuedAndReReportedAfterALaterPickupIsRefused`,
  `BothReportsSwallowed_TheChainReplaysInSendOrderAndKeepsThePlayersCarry` and
  `SwallowedDropBehindAPickup_ConvergesThroughTheOlderReport` (the operation-level half
  of the same hazard, which the review could not settle from the tree).
- **MAJOR-2 — the audit artifact contradicted the code.** Row K1's fallback cell
  claimed "No periodic re-send of individual commands exists, and none is needed"
  while this change adds one, and row I5 still declared the gap. Both rows, the verdict
  summary, the gap list, the index and the evidence file are updated in the same commit.
- **MINOR-3 — the session-end clear was an unstated limit.** Now declared: a report
  swallowed across a session end is not re-sent after the rejoin; the rejoin converges
  through the host's checkpoint and world-entry item snapshot, and a local result that
  snapshot does not carry is the same declared loss any uncommitted local action has at
  a session boundary. Behaviour unchanged (the queue must never leak into the next run).
- **NIT — the class doc contradicted its own code** ("only after the frame was actually
  handed to the transport" while `Track` runs BEFORE the send) and the same claim was
  made in `KernelProtocolService.SendCommand`. Both rewritten: the registration is
  before the send, and the stated reason is a transport that dispatches inline (the
  simulation harness does) rather than a claim about the production transport.
- **NIT — `ItemTransfer` was cited as having a fallback.** It has no sender-side
  emission in this tree at all (the validator, the wire mapper and the host's guard name
  it, no guest path sends it); the exclusion is reworded.
- **NIT — the timing arithmetic.** "well inside the host's 60 s repair cycle" was a
  boundary coincidence, not a margin: a report now says it lives about 60 s after its own
  edge, the same order of magnitude as the host's absolute resend cycle.
- **Coverage the review named as unpinned** and now pinned: an explicit repeat count
  (the host's received kernel frames), the wire-level duplicate (`LinkFaults.Duplicate`),
  the warning text of the spent window, and the drop-behind-pickup case.

## Known coverage gaps (declared, not silently carried)

- The Game Adapter / Unity half is not exercised: the native pickup/drop/destroy
  triggers, `PickupSync`'s generation-time branch and the rendered behaviour of the
  rollback path all need a live game. The suite drives the exact Runtime seams the
  adapter calls (`IItemControl.SendItemSpawned/PickedUp/Dropped/Destroyed`).
- The real lazy-P2P swallow window is simulated by a targeted drop, not by Steam's own
  session behaviour, and the real 60 s host repair cadence is not pumped (the window's
  budget is the same order of magnitude by design, not a measured margin).
- The refusal is item-scoped on the wire (it carries the item id and the reason, never
  the operation id), so a refusal drops the item's newest queued report even when it
  judged a different report about that item. The older reports stay queued, which is
  what makes that case heal instead of persisting; a future wire revision that carried
  the operation id would make the drop exact.
- The host's destroy-of-another-player's-carried-item guard IGNORES a report instead of
  refusing it, so such a report spends its budget and ends in the "not answered"
  warning. Answering it with a refusal was rejected: the guest's rejection path rolls a
  pickup back, and that rollback would land on the remote display clone the guard exists
  to reject. Reachable only for an item whose judged location is another player's carried
  item (an unknown item is refused before that guard, and a world item may be destroyed
  by any peer).
- A session that ends while reports are outstanding drops them (declared above): the
  next entry re-baselines instead of re-reporting.
- Out of scope here, with their owners: `ItemUpdateState` and `ItemContainerSync` keep
  their absolute fallbacks (matrix rows I7 and I4, whose own container-contents gap stays
  I4's); the cross-player transfer seam and its arbitration table are unchanged; the
  remaining item-domain audit gap is row I8
  (`todo/carried-inventory-registration-re-report.md`).
- Observation, pre-existing and not touched: `IKernelProtocolControl.ResetForSessionEnd()`
  has no production caller — the kernel's per-session reset is reached through
  `KernelProtocolService`'s own `SessionEnded` subscription.
- Dual-client acceptance (two real processes) is the user's release-cycle action: a
  guest's pickup/drop/craft action must converge after a swallow without a reconnect, and
  a creation report swallowed around a world entry must not leave the guest's item
  unusable.

## Non-goals

- Kernel protocol redesign or a new command envelope: the mechanism re-sends the frame
  that already exists.
- Anti-cheat / strict validation.
- The cross-player take/arbitration seam (the arbitration table and its protocol stay
  as they are).
- Row I8 (carried-inventory registration) — its own ticket.
