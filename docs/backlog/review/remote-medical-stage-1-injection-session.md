# Remote medical parity — Stage 1: medical operation session + real-time injection

- Status: Review
- Priority: High
- Category: Remote medical / medical operation session / real-time syringe
- Parent: `remote-medical-native-minigame-parity.md`
- Source: User confirmation (2026-09-06) — real-time injection is required; breaking changes are allowed and old compatibility must not be preserved.
- Selfcheck: `docs/evidence/selfchecks/players/remote-medical-operation-session-realtime-injection-selfcheck.md`

## Objective

Build the reusable **MedicalOperationSession** protocol and migrate injectable/IV medicine from the current completion-time single-dose report to a real-time incremental injection stream.

The new session layer is the foundation for later stages (shrapnel, amputation, AED/defib, etc.), so it must be generic enough to carry per-minigame update payloads.

## Current state

- `RemoteMedicalOperationHandler` starts the native `SyringeMinigame` on the display body, accumulates delivered ml, and sends one `PlayerItemUseRequestMsg.DoseAmount` when `MinigameBase.EndMinigame` fires.
- The host applies the full delivered dose once, after the minigame already ended.
- The target does not see body changes while injection is in progress.
- There is no operation id, no resource lease, no intermediate progress, no robust partial/disconnect semantics.
- Old compatibility is intentionally not a constraint: the project is unreleased and the version check strategy will be strict after release.

## Design

### Logical operation

A logical medical operation is modelled as:

```text
Start -> Active (updates) -> End/Cancel
```

The terminal `End` is the single authoritative commit. Intermediate updates are authority-aware state deltas, not independent final commits.

### Host side

- Host keeps a `MedicalOperationRegistry`.
- Each session has:
  - `OperationId`
  - operator SteamId
  - target SteamId
  - limb index (where applicable)
  - item instance id (where applicable)
  - operation kind
  - sequence / version
  - resource reservations:
    - item reservation: only one operation may consume the same item instance at a time
    - optional limb reservation for operations that must not interleave on the same limb
  - state: `Pending`, `Active`, `Committed`, `Cancelled`, `TimedOut`
- Host validates start/update/end with accept-first arbitration: the first plausible report is accepted, obvious conflicts are corrected, and a correction never blocks the player.

### Wire messages

New dedicated messages (names are provisional):

- `MedicalOperationStartRequest` — operator → host
- `MedicalOperationStartAck` — host → operator
- `MedicalOperationUpdate` — operator → host, typed per operation kind
- `MedicalOperationState` — host → target/observers, authoritative progress
- `MedicalOperationEndCommitted` — host → relevant players, final terminal state
- `MedicalOperationCancel` / `MedicalOperationTimeout` — host or client teardown

For injection, the update payload carries the newly injected ml since the last report.

### Injection cadence

- The operator runs the native `SyringeMinigame` locally for input/feel.
- Every ~0.5 seconds, or immediately when the syringe fill changes meaningfully, the operator sends the ml delivered since the last report.
- The host applies each accepted delta to the authoritative character snapshot and broadcasts progress.
- The item is reserved at start and drained in the same increments. The final end message carries the exact terminal item/health/limb state.

### Cancel / disconnect / partial

- Already-committed ml remains committed.
- Unreported ml is not applied.
- The item drain matches only the committed ml.
- On disconnect or timeout the host releases the item and any limb reservation, then terminates the session with a terminal result that reflects committed progress only.

### Compatibility / cleanup

- The old completion-time-only syringe pathway is removed or replaced. Any now-unused `DoseAmount`-only shortcut is deleted rather than kept as dead compatibility.
- Protocol version is bumped; mixed versions are explicitly rejected by the future strict version gate.

## Acceptance matrix

### Roles / directions / views

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest injects host (guest → host) | Host-side target/body effects progress during the minigame, not only after End; host operator receives authoritative item-drain updates |
| 2 | Host injects guest (host → guest) | Guest target sees progressive body/health/limb changes while the host's native minigame is running; host operator sees its own item drain |
| 3 | Guest injects guest via host relay | Both participants and the host receive the same committed progress stream; the host applies each delta to its authoritative snapshots |
| 4 | Operator view | The acting player's local injected item condition/liquid stacks track the committed ml progressively; no one-shot full drain at End |
| 5 | Target view | The target's own body (if that player is a participant) and the remote WoundView display body both advance with each committed ml |
| 6 | Third-party observer view | A player who opens the remote medical view mid-injection sees the same committed progress as participants, without being able to mutate it |

### Terminal / cancellation / robustness

| # | Scenario | Expected |
|---|---|---|
| 7 | Operator cancels mid-injection | Already-committed ml remains applied and item drain matches committed ml only; no uncommitted ml is applied; item and limb reservations are released; one terminal EndCommitted(Cancelled) is sent |
| 8 | Operator disconnects mid-injection | Host commits all already-reported ml, releases reservations, and terminates the session with EndCommitted(Disconnected); no further updates are accepted |
| 9 | Session times out (no update within configured idle window) | Host commits already-reported ml, releases reservations, and sends EndCommitted(TimedOut) |
| 10 | Target disconnects mid-injection | Session is torn down with committed ml preserved, reservations released, and no target-application is attempted on a missing snapshot |

### Concurrency / arbitration

| # | Scenario | Expected |
|---|---|---|
| 11 | Two operations attempt to use the same item instance simultaneously | Second Start is rejected (item reservation) while the first session is active; no item can be consumed by two sessions |
| 12 | Two operations attempt to mutate the same target + limb simultaneously | Second Start is rejected (target/limb reservation) while the first session on that target/limb is active |
| 13 | Two injectable operations on different limbs/items of the same target | Allowed; each session keeps its own reservations and progress identity |
| 14 | Operator already has an active medical operation | Second Start is rejected; one operator cannot run overlapping native minigame sessions |

### Protocol / commit semantics

| # | Scenario | Expected |
|---|---|---|
| 15 | Intermediate update arrives | Host applies only the accepted delta to the authoritative item/target snapshots; no final/terminal event is emitted for an update |
| 16 | End arrives | Exact delivered-total ml is reconciled; any remaining unreported delta is committed once; exactly one EndCommitted is emitted as the sole terminal result |
| 17 | Update repeatedly exceeds remaining item liquid | Host clamps to the available liquid; no negative item amount and no negative target health value |
| 18 | Mixed liquid medicine | Each incremental draw remains proportional across all stacks; the final committed ml equals the sum of all accepted deltas capped by the original total |
| 19 | Old completion-time single-dose request for injectable medicine | Removed / no longer reachable from the remote medical path; direct generic use of injectable items is refused so real-time session remains the only injection route |
| 20 | Mixed CUO versions | Strict handshake rejection after protocol-version bump; no old/new compatibility shim is provided |

### Observable / lifecycle

| # | Scenario | Expected |
|---|---|---|
| 21 | Session registry after End | The active session is removed and reservations are released; a new operation can start on the same item/limb |
| 22 | Host session ends / lobby closes | All in-flight sessions are cleared; no stale session can send or block later interactions |
| 23 | Operator closes remote medical view mid-injection | Cancel is sent/processed; the native minigame is torn down; committed progress remains; uncommitted input is discarded |

### Timed-only medicine note

For medicines whose only effect is a timed native `DoTimedOp` (stimulants,
procoagulant, epinephrine, etc.), intermediate updates intentionally do not start
the timed effect on the target body. The single `EndCommitted` carries the
committed timed effect once, so no overlapping/stacked native cooldowns are
created. The real-time guarantee for those items is the committed item drain and
minigame progress; the timed body effect begins at the sole authoritative
terminal.

## Red-test plan (to be executed before implementation)

The focused regression test must fail on the current code and prove that injection is not progressive, e.g.:

- Host service receives an `Update`/incremental dose event and target state does not change until `End`.
- Or a unit-level probe asserts that a mid-operation commit path exists and currently throws/returns false.
- The test must be a real runtime failure on the current implementation, not a compile failure.

## Out of scope for Stage 1

- Shrapnel multiplayer session (Stage 2).
- Bandage, dislocation, AED, manual defib, amputation (Stage 3).
- CPR (future).
