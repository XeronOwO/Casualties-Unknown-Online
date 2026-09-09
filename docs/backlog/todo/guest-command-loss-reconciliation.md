# Guest command loss: local pickup/drop result is not reconciled

- Status: Todo
- Priority: Medium
- Category: Network / sync coverage / items
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` rows I5 and I1 caveat, after the independent adversarial review reclassified I5 from OK)
- Related: `todo/guest-block-mutation-re-report.md` (same swallowed-guest-event family), `review/remote-backpack-native-interaction-parity.md`

## Problem (evidence)

The kernel command channel is reliable, but a command swallowed at transport level has no
guest-side re-report, and the host → guest absolute fallback does not always revert the
guest's local result:

- The guest's native pickup/drop action runs locally first; the command is the report
  (`src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelProtocolCommandHandler.cs:168`
  shows the accepted-first carried update path for the general case).
- The in-flight windows are time-bounded and local:
  `src/CasualtiesUnknownOnline.Runtime/Session/Items/PendingPickupQueue.cs:17` (500 ms hold),
  the reject paths at `KernelProtocolCommandHandler.cs:84` / `:130`, and
  `src/CasualtiesUnknownOnline.Runtime/Session/Items/DropPendingState.cs:69` (one-frame hold).
- The absolute world-item keyframe is the only periodic heal
  (`src/CasualtiesUnknownOnline.Runtime/Session/Items/ItemSnapshotService.cs:88`), and it
  converges the **host's** table. For a swallowed pickup/drop, the guest's local result is not
  reconciled by that keyframe: the item keyframe neither kills the guest's inventory copy nor
  re-materializes a duplicate (`ItemApplication.FindWorldItem` resolves the carried item), so
  the divergence persists until a reconnect.
- Additional caveat from row I1: the keyframe is skipped entirely when the host table is empty
  (`ItemSnapshotService.cs:90`), so a swallowed destroy is not healed once the table is empty.

## Goal

A swallowed guest item command whose native local action already happened converges without a
reconnect, in one of two explicit ways: the host learns the operation (re-report/ack), or the
guest's local result is deliberately reverted by the next authoritative snapshot with the
revert observable in the log.

## Design direction (decide at implementation)

1. **Command re-report / ack** — the guest keeps a small uncommitted-operation table and
   re-sends until the host's committed batch (or a rejection) is seen; idempotent by
   `OperationId`.
2. **Host reconciliation request** — the host's keyframe/periodic pass asks for the guest's
   uncommitted operations and applies or rejects them.
3. **Explicit revert** — extend the keyframe apply so a carried item absent from the host table
   is returned to the world (or the carried fact is reconciled) and log the revert; only if the
   revert is deterministic and user-visible behavior is accepted.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest pickup command dropped | Host and guest converge (item ends in one agreed place) without reconnect |
| 2 | Guest drop command dropped | Same |
| 3 | Destroy command dropped while the host table is non-empty | Converges |
| 4 | Destroy command dropped while the host table is empty | Converges (the empty-table skip no longer hides it) |
| 5 | Duplicate/replayed re-report | Idempotent by `OperationId`; no double pickup/drop |
| 6 | Reconnect | Same behavior as today |
| 7 | Third-party view | All peers agree on the item's location/ownership |
| 8 | In-flight race (pickup before spawn report) | The existing 500 ms hold/reject→rollback path is unchanged |

## Non-goals

- Kernel protocol redesign or a new command envelope.
- Anti-cheat / strict validation.
