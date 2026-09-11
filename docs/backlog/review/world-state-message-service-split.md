# WorldStateMessageService is at the type-size ceiling

- Status: Todo
- Priority: Low
- Category: Architecture / maintenance
- Source: recorded while landing S3.2 of `todo/save-mid-run-consistent-cut.md` (the
  return-value change of `ApplyBlockState` pushed the type from 595 to 601 aggregate lines)
- Related: `docs/architecture-debt.json` (the recorded baseline), `AGENTS.md` hard thresholds

## Problem

`CasualtiesUnknownOnline.Runtime.Session.World.WorldStateMessageService` is at 601 aggregate lines
against the 600-line ceiling. It was already at 595 before the S3.2 change, so the type needs a real
split rather than a shave: the recorded `docs/architecture-debt.json` entry is a nothing-more
baseline, not a licence to grow.

The type currently carries four responsibilities: the host's block-difference table, the radiation
line snapshot, the world-start parameter plumbing, and the GUEST's unacknowledged block-report
bookkeeping (`PendingBlockReportTable` glue: `SendBlockPlacedReport`'s record-before-send,
`ResendPendingBlockReports`, the answer-driven drop in `OnBlockPlacedReceived`, the
overflow-logging latch, `ResetPendingBlockReports`).

## Scope

1. Extract the guest report bookkeeping into its own collaborator (the send itself stays where the
   wire surface is, passed in as a callback or kept in the message service while the table and the
   latch move out) — the W1 recovery rules (`review/guest-block-mutation-re-report.md`) must keep
   their exact semantics: record before send, newest write per cell wins, drop on the host's relay
   echo or correction, keep the table across a reconnect-while-in-world, clear it on a new
   world/layer baseline.
2. Delete the `docs/architecture-debt.json` entry once the type is back under the ceiling; the
   entry must never be raised.

## Acceptance

| # | Scenario | Expected |
|---|---|---|
| 1 | Source-shape gate | `WorldStateMessageService` is under 600 aggregate lines and has no debt entry |
| 2 | W1 re-report suite | The existing pending-block-report tests pass unchanged (no semantics moved) |

## Verification limits

Source-shape and unit-level only; the in-game half stays covered by the W1 review's dual-client
pass.
