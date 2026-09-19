# The world-entry trap-layout fanout can carry an entity the host has removed

- Status: Todo
- Priority: Low-Medium
- Category: Network / sync coverage / world entities
- Source: the W6 landing record (`review/trap-layout-snapshot-recovery.md`) — the in-session repair re-derives the table from the live scene, the entry fanout still sends it as last derived
- Related: `review/trap-layout-snapshot-recovery.md`, `review/sync-cadence-review.md`

## Problem (evidence)

`WorldEntryFanout.Send` sends `TrapLayoutRegistry`'s table to a member on its world-entry
edge (first InWorld, reconnect-while-InWorld). Since W6 the table is re-derived from the
live host scene on the 60 s in-session repair
(`WorldEntryFanout.SendInSessionRepair` → `TrapLayoutScanner.RefreshLayout` →
`IWorldControl.ReplaceTrapLayout`), so between two repairs the table can still list an
entity the host's world has since removed — a self-destructed turret, a broken crystal.

A member whose entry group lands inside that window materializes the phantom: the guest's
apply is an absolute align (`TrapLayoutApplication.Apply` → `TrapLayoutAlign`, materialize
missing / destroy surplus) against the snapshot, and a freshly generated guest world has
no such entity, so the join spawns one the host does not own. The next repair corrects it
(the refreshed table makes the guest destroy the surplus), so the divergence is transient
(≤ 60 s), but a phantom trap is a live hazard on the guest in the meantime.

## Goal

The member-facing layout send is always derived from the host's live scene at send time,
so an entering or reconnecting member never materializes an entity the host has removed.

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
| 1 | An entity was removed (self-destruct / break) and a member enters the world before the next repair | The entry group carries the LIVE table: no phantom is materialized |
| 2 | Reconnect while InWorld inside the same window | Same; the member's existing copies are not duplicated |
| 3 | An entity is removed while every member stays in the world | Unchanged: the 60 s repair (W6) destroys the surplus |
| 4 | The host's scene scan sees nothing (world in transition) | The fail-safe keeps the last known table (never a mass destroy) |
| 5 | Third-party guest | All peers converge to the same layout |

## Non-goals

- The in-session repair itself (landed as W6, `review/trap-layout-snapshot-recovery.md`).
- Removing entities from the layout for reasons other than an observed live-scene scan.
