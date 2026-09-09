# Guest break drops are lost when the break report is swallowed

- Status: Todo
- Priority: Medium
- Category: Network / sync coverage / items (guest-created drops)
- Source: `review/guest-block-mutation-re-report.md` (W1) implementation — the block state now converges when a break's air write is lost, but the drops it carries do not
- Related: `todo/guest-command-loss-reconciliation.md` (the item keyframe's in-flight reconciliation gap), `todo/carried-inventory-registration-re-report.md`

## Problem (evidence)

A guest's block break travels as two messages: the air write (`BlockPlaced`, the
first-writer proof) and, one frame later, one `BlockDamaged` carrying the break
plus every block/building drop
(`src/CasualtiesUnknownOnline.GameAdapter/World/BlockBreakSync.cs:144`).
The guest registers its own drops only on the host/solo path
(`src/CasualtiesUnknownOnline.GameAdapter/World/BlockBreakSync.cs:138`
`if (_session.Role != SessionRole.Guest)`), so the host learns them exclusively
from that message:

- If the `BlockDamaged` is swallowed (the ~30 s lazy-P2P window swallows every
  send), the host never registers the drops and the peers never materialize
  them; the guest keeps a locally-created item the host's item table does not
  know about.
- If only the air write is swallowed, the drops-carrying report arrives while
  the host's block is still standing and is applied as damage only — the W1 fix
  now records and relays the resulting air transition, but the drops in that
  message are still ignored
  (`src/CasualtiesUnknownOnline.GameAdapter/World/BlockBreakSync.cs:254`
  `OnRemoteDamageBrokeBlock(sender, cell, hasDropPayload);` logs the loss).
- The item keyframe cannot heal this: the drops were never registered on the
  guest side, so the guest's local items have no kernel fact to reconcile from
  (`todo/guest-command-loss-reconciliation.md` owns the in-flight item gap).

## Goal

A swallowed break report must not lose its drops: the breaker's locally-created
items either reach the host exactly once or are rolled back on the breaker, and
the item domain's recovery (keyframe/snapshot) can converge the result.

## Design direction (decide at implementation)

1. **Guest drop table + re-report** — the breaker keeps its unacknowledged break
   drops (item id + payload) and re-reports them until the host answers,
   mirroring the W1 pending block-report table. The host accepts them only while
   the cell's break is still attributable to that sender (the existing
   first-writer arbitration).
2. **Break report re-send** — re-send the whole `BlockDamaged` break message
   (drops included) on the same fallback cycle; the host's one-shot arbitration
   record must then be re-armable idempotently.
3. **Accepted loss** — rejected: a mined item that exists on one side only is a
   hard item-domain divergence.

Either way the duplicate guard must be the drop's item id (never the cell
alone), so a re-report cannot double-register.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest breaks a block; both reports are dropped | Host registers the drops once; every peer sees them |
| 2 | Guest breaks a block; only the air write is dropped | Same (the drops ride the delivered report) |
| 3 | Guest breaks a block; only the drops report is dropped | Same via the re-report |
| 4 | Duplicate re-report | Exactly one host registration and one materialization per drop |
| 5 | The cell was broken by someone else first | The loser's drops are rolled back (existing `BlockAlreadyBroken` reject) |
| 6 | Third-party view | Same drops, same identities |
| 7 | Reconnect / layer regeneration | No stale drop re-report into the new world |

## Non-goals

- The block state itself (W1 — landed).
- General item-domain reconciliation (`todo/guest-command-loss-reconciliation.md`).
