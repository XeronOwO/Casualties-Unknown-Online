# Checkpoint chunks are not validated against the current run epoch

- Status: Review
- Priority: Low-Medium
- Category: Network / sync coverage / protocol validation
- Source: Sync coverage audit 2026-09-09, independent adversarial review finding (side note of the stale-epoch stream fix)
- Related: `review/sync-event-and-periodic-fallback-coverage-audit.md`, `docs/backlog/review/session-control-convergence.md`

## What landed

The guest's checkpoint path now validates every chunk against the run identity the host
announced, and it does so BEFORE a chunk reaches the assembler:

- **The instruction carries the identity** (protocol 33 → 34): `WorldJoinMsg.RunEpoch` is
  the host's kernel run epoch at the enter-the-world instruction — the edge a new run
  legitimately begins at. It is stamped from `ItemKernelAuthority.CurrentRunEpoch` at
  every send site: `WorldService.SendWorldJoin` / `SendWorldJoinTo` (through
  `WorldStateMessageService`) and the handshake's direct join.
- **The receiver validates and buffers by identity** (`GuestCheckpointReceiver`, extracted
  from `KernelProtocolService` the same way `KernelStateStreamService` was): the guest
  records the announced identity and refuses a chunk whose run epoch is not it before
  anything is buffered; a frame whose envelope-header stamp disagrees with its payload
  stamp is refused as malformed; the pending set is keyed by its own
  `(RunEpoch, GlobalRevision, ChunkCount)` so a superseded set's leftovers are dropped as
  a unit instead of occupying a slot the live set needs; when no instruction preceded the
  set (a reconnect's entry group is sent before the join) the first restored set defines
  the identity. The identity is released with the session (`ResetForSessionEnd`).
- **The message is the only wire change**: `WorldJoinMsg` gains `[ProtoMember(2)] RunEpoch`
  and `ProtocolVersion.Current` becomes 34. No `NetMsg` / `WireCommandKind` /
  `WireEventKind` / `AdaptiveStreamId` member was added, so the vocabulary index is
  unchanged.

### The ticket's original framing was rewritten against the transport facts

The problem statement said a "late chunk set from a previous run could move the guest's
epoch backwards". Both transports in the tree are reliable AND ordered — Steam's reliable
channel, and `IpDirectTransport` is TCP ("all frames are reliable") — so a *complete*
stale set cannot overtake the live one inside a connection. What IS reachable at this
seam, and what this change fixes:

1. **No host-authored identity was compared at all** (the audit's finding): whatever set
   arrived was restored, and `GameStateStore.Restore` overwrites the local epoch with it,
   so one foreign-epoch set moves this side onto a run whose live streams it then rejects
   (`Guest_RefusesCheckpointSetFromAnotherRun` fails on the pre-fix tree).
2. **The pending buffer was keyed by chunk INDEX alone** (found while writing the tests):
   a leftover chunk of a superseded set sits at an index the live set never fills, so the
   count check can never be satisfied for any later set and the member never restores
   again — permanently, because every later set leaves the same residue
   (`Guest_StaleChunkDoesNotBlockTheLiveCheckpointSet` and
   `Guest_SupersededSetOfTheSameRunDoesNotBlockTheLiveSet` both fail on the pre-fix tree).
   This is the defect with the permanent consequence; the identity check is what makes
   the first one impossible rather than merely unlikely.

## Acceptance matrix

| # | Scenario | Expected | Verified by |
|---|---|---|---|
| 1 | A stale set from the previous run arrives after the live run is established | Refused with a log; live state and identity untouched; the live run's stream still applies | `KernelProtocolServiceTests.Guest_RefusesCheckpointSetFromAnotherRun` (red pre-fix) |
| 2 | Legitimate checkpoint with no announcement yet (fresh join / rejoin in a new session) | Accepted; the guest adopts its epoch, and the NEXT set is compared against what that one established | `KernelProtocolServiceTests.Guest_AdoptsTheIdentityFromTheFirstRestoredSet`, `Host_SendsCheckpoint_AndGuestRestoresItemState`, `Guest_AcceptsStateStreamAfterARealCheckpointRestore` |
| 3 | Reconnect while in world | Checkpoint accepted; no epoch regression | `KernelProtocolServiceTests.Disconnect_ThenReconnectWithCheckpoint_RestoresGuest` |
| 4 | A mixed-epoch set handed to the assembler | Refused | `KernelWireMapperTests.CheckpointAssemble_ChunksDisagreeingOnEpoch_Throws` (the assembler's own contract; the receiver cannot mix two sets because it buffers one identity at a time) |
| 5 | The announced run is what decides, and a host run reset mid-connection (a Continue restore changes the host's epoch) | The instruction's identity outranks the set's own stamp and this side's epoch; the previous run's set is refused, the announced one accepted | `KernelProtocolServiceTests.Guest_RefusesASetFromARunTheWorldJoinDidNotAnnounce`, `Guest_ServesTheRunTheWorldJoinAnnounced` + `Guest_RefusesCheckpointSetFromAnotherRun` |
| 6 | Partial chunk set | Buffered, nothing restored | `KernelProtocolServiceTests.Guest_SupersededSetOfTheSameRunDoesNotBlockTheLiveSet` (asserts the kernel revision is unchanged while a partial set is buffered) |
| 7 | Malformed frame: header stamp ≠ payload stamp | Refused | `KernelProtocolServiceTests.Guest_RefusesCheckpointChunkWhoseHeaderDisagreesWithItsPayload` (red pre-fix) |

The wire field itself is pinned by `NetPacketTests.WorldJoin_RunEpoch_RoundTrips`.

## Limits

- **The announcement is deliberately not written into the guest's kernel epoch.** The
  restore remains its only writer: a member that stamped the announced run onto the state
  it still holds from the previous run could apply a batch it has no baseline for. The
  consequence is unchanged by this ticket: a guest command or report sent between the
  join instruction and the first restore still carries this side's own epoch and can be
  refused by the host (the residual window the K4 row records).
- **A stale set that arrives before the first instruction of a connection is accepted by
  definition** (the reconnect entry group is sent before the join). At that instant the
  set's own stamp is the only host-authored identity available, and the ordering property
  of both transports is what keeps a straggler out of that window. The matrix rows K4/R3
  state this limit.
- **The repair group's trigger path is not what these cases pin.** The cases that stand in
  for the 60 s repair call `SendCheckpoint` directly — the same send the group makes — so
  what is covered here is the receive side; the group's own trigger (the entry-repair
  window and the periodic wave through `WorldEntryFanout.SendInSessionRepair`) is covered
  by the existing suites that drive it (`RunClockFactsTests`, `RecipeUnlockBackfillTests`,
  `EnemySnapshotRecoveryTests`).
- **Runtime-visible validation only.** The engine-side halves (the native apply, the save
  path) are untouched; nothing in this ticket changes what a member does with a restored
  checkpoint.
- Both transports being ordered means the index-keyed-buffer defect needs a superseded
  set to be present at all (a repair send racing an entry group, a re-send after a failed
  restore). It is demonstrated at the receive seam, not reproduced from a live session.
- `reversing/sync-audit/` holds dated audit snapshots and still names this ticket's old
  `todo/` path; that tree is never edited by design (its line numbers are stable), and
  every live source — backlog index, the audit ticket, decision 194, the matrix and the
  evidence JSON — points at `review/`.

## Evidence anchors

- `GuestCheckpointReceiver` — the announced-identity refusal, the per-set keying and the
  header-stamp check: 3 new anchors, which take row K4 to 18 declared anchors.
- `WorldJoinMsg.RunEpoch`, `WorldJoinHandler`'s adoption and `WorldService.SendWorldJoin`'s
  stamping: 3 new anchors, which take row R3 to 44 declared anchors. The adoption line is
  pinned by `Guest_RefusesASetFromARunTheWorldJoinDidNotAnnounce` (removing it leaves the
  set adoptable), and the first-restore adoption by
  `Guest_AdoptsTheIdentityFromTheFirstRestoredSet`.
