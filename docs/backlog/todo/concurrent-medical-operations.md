# Medical operations are exclusive (one operator at a time)

- Status: Todo
- Priority: Medium-High
- Category: Gameplay / multiplayer medical sessions (concurrency)
- Source: User ruling 2026-09-18 (design alignment session): exclusive reservation is wrong for this family — KrokMP already supports several players working the same minigame at once (pulling shrapnel, treating a dislocation), and co-op medical work is the natural expectation even though it is harder to implement.
- Related: `review/remote-interaction-local-gating.md` (the gate half of the same services), `review/remote-medical-stage-2-shrapnel-multiplayer.md`, `review/remote-medical-stage-3-other-actions.md`, `docs/backlog/watchlist/architecture-watchlist.md` (the reservation bookkeeping is the entry that must be extracted)

## Problem (evidence)

Cooperation on one victim is currently impossible by construction:

- `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/OtherMedicalOperationSessionService.cs`
  is documented as "All actions are exclusive (one operator at a time)" and takes two
  shared sets by reference — `_reservedItems` and `_sharedReservedTargetLimbs`
  (`HashSet<(ulong Target, int Limb)>`) — which the medical, shrapnel and other-medical
  services share, so one operator's start blocks every other operator on the same limb
  (and on the same item).
- The watchlist already records the consequence for the code shape: the reservation
  bookkeeping is shared by reference between three services and "belongs in its own
  object" — the concurrency change is the moment to do that extraction.

## Goal

Several players can work the same victim (and the same limb) at the same time. Each
OPERATION is an independent unit of work with its own identity, its own session and its
own outcome; the effect is applied against the authoritative limb state, so two operators
who both finish cannot double-apply one unit.

## Design direction (decide at implementation)

1. The reservation changes from "lock the limb / lock the item" to a per-UNIT claim: a
   unit is (target, limb, operation kind) plus, where the native minigame has discrete
   pieces, the piece itself (a shrapnel fragment, one minigame turn). Several units on
   one limb coexist; the same unit does not.
2. Outcome rule per kind, derived from the native semantics rather than from a blanket
   lock: a UNIQUE unit (relocating one dislocation, removing one fragment) resolves once —
   the first completion wins and the others get an immediate, precise "already handled"
   answer; a REPEATABLE effect (bandage, injection) applies per completed operation.
3. The victim's body stays locally authoritative; the completing client reports its
   terminal state, and the host's atomic commit is where two completions of one unit are
   deduplicated (the kernel command already carries an operation id — verify it is usable
   as the idempotency key before relying on it).
4. A native-concurrency audit comes first: which minigames the game itself allows two
   actors to run at once, how their progress/animations are presented, and what the native
   UI does when a second actor opens the same panel. The result decides per-kind
   eligibility instead of guessing.
5. Extract the reservation bookkeeping into its own Runtime object as part of this cycle
   (the watchlist entry), so the new per-unit rule has one owner.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Two operators start on the same limb (different kinds) | Both run; both effects apply |
| 2 | Two operators start the SAME unique unit | Both may start; exactly one resolution lands; the other is answered precisely |
| 3 | Two operators on the same repeatable effect | Each completed operation applies |
| 4 | One item, two operators | The item cannot be consumed twice for one operation |
| 5 | Operator disconnects mid-operation | The unit is released; the victim's limb state stays coherent |
| 6 | Third-party view | Every peer agrees on the limb's terminal state |
| 7 | Native UI with a second operator | No stuck panel, no duplicated progress on one body |

## Non-goals

- Rewriting the medical minigames or their UI (native reuse stays the rule).
- Cross-victim scheduling or a work queue.
- Anti-cheat (a client that lies about its own progress is out of scope).
