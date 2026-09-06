# Remote medical Stage 2: multiplayer shrapnel session self-check

Owner cycle: backlog `todo/remote-medical-stage-2-shrapnel-multiplayer.md`.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Host-owned shared session per target limb, multiple operators join the same operation id | `ShrapnelOperationSessionService`, `ShrapnelOperationSession`, `MedicalOperationSessionService` shrapnel forwarding |
| 2 | Per-piece ownership is atomic for a grab→release/remove drag; non-owner moves are rejected while a piece is owned | `ShrapnelOperationSessionService.HandleUpdate`, `ShrapnelPieceState.Owner` |
| 3 | Per-operator tweezers condition is drained once on join and the authoritative item-after state is sent to each operator's own client | `ShrapnelSessionStateWriter.DrainTweezers/BuildState(recipient)`, `ShrapnelOperationSessionService.PublishState` |
| 4 | Authoritative piece positions/removed state are broadcast to all handshaken members; non-operators receive a read-only native `ShrapnelMinigame` observer when the remote WoundView is open on the target | `ShrapnelOperationSessionService.PublishState`, `RemoteShrapnelOperationHandler.TryStartObserver/ApplyStateToMinigame` |
| 5 | Initial grab/release/break/removal use reliable transport; ordinary held-piece position reports are unreliable | `ShrapnelPieceUpdate.OwnershipChange`, `RemoteShrapnelMinigamePatch`, `ShrapnelOperationSessionService.SendUpdate` |
| 6 | Session end/cancel/disconnect/timeout semantics: removed pieces stay removed, operator leaves release ownership, target/operator disconnect and all-removed terminal are covered | `ShrapnelOperationSessionService.LeaveOperator/OnMemberRemoved/Update/Terminate` |
| 7 | Break-grasp failure applies native-style limb damage and releases the piece | `ShrapnelSessionStateWriter.ApplyBreakGrasp`, `BreakGraspPatch` |
| 8 | Old direct one-shot shrapnel path removed | `RemoteLimbToolCatalog` no longer exposes `tweezers`; `RemoteLimbToolProfile/Application` no longer carry `RequiresShrapnel` |
| 9 | Cross-type medical operation arbitration symmetric: shrapnel operator cannot start injection and vice versa; target-limb conflicts are shared | `MedicalOperationSessionService` active check, `ShrapnelOperationSessionService` shared `_reservedTargetLimbs` |

## 2. Verification

- **L0 tests**: `MedicalOperationShrapnelSessionTests` now covers shared join, same-piece contention, concurrent different-piece movement/removal, cancel, operator disconnect, all-removed terminal, break-grasp, per-operator item-after state, third-party wire state, and shrapnel-blocks-injection; full suite 2369 tests + 16 gate tests green.
- **Build**: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors.
- **Format**: `dotnet format CasualtiesUnknownOnline.slnx` clean.
- **Adversarial self-check**: independent subagent reviewed Stage 2 before finalization; it found the native `EndMinigame` ordering blocker (final removal lost), missing third-party position display, reliable/unreliable boundary violation, asymmetric cross-type arbitration, and stale `_lastHeld` cleanup. All were fixed in this cycle.
- **Deployment**: latest build deployed to the real game directory with artifact hash verification (see delivery record).
- **Development-period rule**: L0 + static evidence; real dual-client visuals remain for user acceptance.
