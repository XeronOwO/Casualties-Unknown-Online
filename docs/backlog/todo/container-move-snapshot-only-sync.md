# Container moves reach the viewer as a snapshot, not an event

- Status: Todo
- Priority: Medium
- Category: Item sync / observability (the clone fact monitor)
- Source: agent acceptance batch `20261005-c` (2026-10-05) — the monitor warned on every remote
  container move that run drove; raised to work by the user's 2026-10-06 ruling that a defect the
  repository's own history introduced is fixed or ticketed.
- Related: `review/remote-inventory-native-parity-rework` (the path that produced the warnings),
  `done/carried-inventory-registration-re-report.md`, `docs/architecture/remote-inventory-native-parity.md`

## Symptom (evidence)

Batch `20261005-c` drove remote container moves through the native intent path
(`MoveIntoContainer`, `TakeOutOfContainer`) in both directions, in a three-client session. Every one of
them made the OPERATOR's clone fact table warn, once per move plus once for the container:

```text
[WRN] [CharSync] divergence for <owner>'s trashbag (id 1147077248963): nested container contents changed
      without an event sync (the 1 Hz snapshot carried it).
[WRN] [CharSync] divergence for <owner>'s dogfood (id 1155667183555): left the inventory without an event
      sync (the 1 Hz snapshot carried it).
```

Six warnings for four moves (the batch's guest log excerpt `r1-guest-log.txt` / `r4-guest-log.txt` in the
artifact directory named by `acceptance-artifacts-dir`; the moves themselves are the record
`docs/evidence/acceptance/remote-inventory-native-parity-rework-20261005-c.md` rows 3, 4, 6 and 14).
The owner's own client logged no divergence: the state always converged, only the carrier differed.

## Ownership (checked 2026-10-05)

| Fact | Origin |
|---|---|
| The warning | `CloneFactTable`'s snapshot-divergence monitor, landed with `cddab4fa` ("snapshot divergence monitor + starting-supply fact registration") |
| The container wording | `6503abfc` ("sync nested container content moves") |
| The replay the owner performs | `RemoteIntentApplier` (this repository) |

So the monitor is ours, the move is ours, and the divergence it reports is inside our own path — the
question below is ours to answer, not an external observation.

## What is not known yet

1. Whether a container move is supposed to carry its own event. The stage-2 rule in the rework ticket
   says "the immediate authoritative character re-report stays with the discrete kinds", and a container
   move is discrete — if that re-report is the character snapshot, the monitor's Warn is mis-scoped for
   this family and should be Debug with the family's expectation recorded; if an event is missing, the
   event is the fix.
2. Whether the same warning fires for a LOCAL container move (a player moving an item in their own
   backpack) — the batch only drove remote moves.
3. Whether the operator's fact table converges without the periodic snapshot when the snapshot is
   delayed (a blackout window would answer it).

## Required work

1. **Attribute first**, with the red step: pin which message carried the change on the wire for a remote
   container move, and record whether the monitor's expectation is the right one for this family.
2. Then either **wire the event** the design expects, or **re-scope the monitor** so a Warn means a real
   divergence — a per-move Warn that is expected behaviour is noise that hides the next real one.
3. **Check the family**: every divergence wording in `CloneFactTable` gets the same question (pickup,
   slot move, use, limb state), because a monitor that is right for one carrier and wrong for another is
   worse than no monitor.
4. **Acceptance**: a real three-client run over the moved families, reading the monitor's output as a
   zero-warning row over at least one full periodic cycle.
