# Checkpoint chunks are not validated against the current run epoch

- Status: Todo
- Priority: Low-Medium
- Category: Network / sync coverage / protocol validation
- Source: Sync coverage audit 2026-09-09, independent adversarial review finding (side note of the stale-epoch stream fix)
- Related: `review/sync-event-and-periodic-fallback-coverage-audit.md`, `docs/backlog/todo/session-control-convergence.md`

## Problem (evidence)

`KernelProtocolService.HandleCheckpoint` assembles chunks and restores without checking the
envelope header or chunk run epoch against the current run:

- `src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelProtocolService.cs` — the
  `EnvelopeKind.Checkpoint` branch calls `HandleCheckpoint(frame.Checkpoint)`, which accumulates
  `_checkpointChunks` and restores a complete set; there is no `RunEpoch` comparison.
- `src/CasualtiesUnknownOnline.Runtime/Session/Items/WireCheckpointAssembler.cs:114` only
  checks that the chunks in one set agree with each other
  (`if (chunk.RunEpoch != first.RunEpoch || chunk.GlobalRevision != first.GlobalRevision)`); it
  does not check that the set belongs to the current run.
- After the stale-epoch stream fix, `ItemKernelAuthority.Restore` adopts the restored
  checkpoint's epoch, so a late chunk set from a previous run could move the guest's epoch
  backwards and then reject the live run's streams.
- The handshake carries no run epoch (`SessionPeerMaintenance.CreateHandshakeMsg`), so the guest
  has no expected-epoch source before its first checkpoint.

## Goal

A checkpoint chunk set from a previous run is rejected (logged, not restored), while a
legitimate first/new-run checkpoint from the current host is still accepted.

## Design direction (decide at implementation)

1. **Handshake-carried expected epoch** — the host includes its run epoch in
   `HandshakeAck`/`WorldJoin`; the guest rejects checkpoint chunks whose epoch differs from the
   negotiated value (and updates it on an explicit host run reset).
2. **Session-level last-accepted-host epoch** — remember the host's epoch from the first
   accepted checkpoint of the connection and reject any later set that differs; the first
   checkpoint is accepted by definition.
3. **Explicit reset message** — a dedicated "run changed" control message that re-arms the
   expected epoch.

Option 1 is the most explicit and gives the guest an epoch source before the first checkpoint;
it is a protocol addition and needs the same version-gating discipline as other messages.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Late chunk set from the previous run arrives | Rejected with a log; current state untouched |
| 2 | Legitimate first checkpoint (new guest, host epoch > guest epoch) | Accepted; the guest adopts the epoch |
| 3 | Reconnect while in world | Checkpoint accepted; no epoch regression |
| 4 | Mixed-epoch chunk set | Rejected (existing assembler check) |
| 5 | Host run reset mid-connection | Guest re-arms on the explicit reset; no stale restore |
| 6 | Partial chunk set | Buffered, not restored (existing behavior) |

## Non-goals

- Save-file epoch migration.
- Replacing the checkpoint/journal mechanism.
