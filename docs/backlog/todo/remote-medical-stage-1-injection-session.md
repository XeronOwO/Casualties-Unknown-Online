# Remote medical parity — Stage 1: medical operation session + real-time injection

- Status: Todo
- Priority: High
- Category: Remote medical / medical operation session / real-time syringe
- Parent: `remote-medical-native-minigame-parity.md`
- Source: User confirmation (2026-09-06) — real-time injection is required; breaking changes are allowed and old compatibility must not be preserved.

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

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest injects host | Host sees body/injection effects progress during the minigame, not only at end |
| 2 | Host injects guest | Guest sees the same progressive effect |
| 3 | Guest injects guest via host relay | Both participants and the host see committed progress |
| 4 | Third party opens remote medical view during injection | Third party sees the same in-progress state |
| 5 | Operator cancels mid-injection | Committed ml remains; no uncommitted ml is applied; item releases |
| 6 | Operator disconnects mid-injection | Host commits already-reported ml, releases reservation, terminates session |
| 7 | Two operations attempt to use the same item simultaneously | Host rejects/queues the second start while the item is reserved |
| 8 | Two operations attempt to mutate the same reserved limb/target resource | Host applies the configured per-resource policy (reject or serialize) |
| 9 | Old completion-time request path | Removed or no longer reachable for injectable medicine |
| 10 | Mixed CUO versions | Strict rejection (part of later version-gate work, but no compatibility shim in this stage) |

## Red-test plan (to be executed before implementation)

The focused regression test must fail on the current code and prove that injection is not progressive, e.g.:

- Host service receives an `Update`/incremental dose event and target state does not change until `End`.
- Or a unit-level probe asserts that a mid-operation commit path exists and currently throws/returns false.
- The test must be a real runtime failure on the current implementation, not a compile failure.

## Out of scope for Stage 1

- Shrapnel multiplayer session (Stage 2).
- Bandage, dislocation, AED, manual defib, amputation (Stage 3).
- CPR (future).
