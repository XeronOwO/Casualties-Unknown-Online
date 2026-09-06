# Remote medical operation session + real-time injection self-check

Owner cycle: backlog `review/remote-medical-stage-1-injection-session.md`.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Generic medical operation session (`Start -> Active updates -> one EndCommitted`) | `MedicalOperationSessionService`, `OperationSession`, `MedicalOperationInjectionApplier` |
| 2 | Host-owned item reservation + target-limb reservation | `MedicalOperationSessionService._reservedItems` / `_reservedTargetLimbs`, `HandleStartRequest` reject branches |
| 3 | Incremental injection deltas apply to authoritative snapshot and broadcast as non-terminal `MedicalOperationState` | `MedicalOperationInjectionApplier.TryApplyDelta`, `MedicalOperationSessionService.HandleUpdate/PublishState` |
| 4 | Single terminal result `MedicalOperationEndCommitted` | `MedicalOperationSessionService.Terminate` |
| 5 | Cancel / disconnect / timeout keep committed ml and release reservations | `HandleCancelRequest`, `OnMemberRemoved`, `ICuoService.Update` timeout |
| 6 | Operator adapter streams native `SyringeMinigame` progress and handles ack/end/cancel, including pre-ack endings | `RemoteMedicalOperationHandler` |
| 7 | Target/operator/third-party apply and display refresh | `MedicalOperationApply`, `CharacterDataSync.ApplyMedicalState`, `RemoteMedicalCoordinator.ApplyMedicalState` |
| 8 | One-shot injectable request path removed; strict protocol bump | `PlayerItemUseRequestMsg.DoseAmount` deleted, `PlayerItemUseService` refuses injectables, `ProtocolVersion.Current = 12` |

## 2. Verification

- **L0 tests**: `MedicalOperationSessionServiceTests` (7 tests) covers guest→host progressive, host→guest, same-item rejection, timeout, disconnect, cancel, one-shot refusal, plus rewritten timed-effect tests. Full `dotnet test` 2354 + 16 gate tests green.
- **Adversarial self-check**: two independent subagent reviews; first found blocker ack-before-end/close, stale-snapshot display overwrite, host-terminal minigame cleanup; fixes landed and verified in follow-up review.
- **Deployment**: `tools/deploy.ps1` to real game directory; SHA-256 of Runtime/GameAdapter/Plugin/Protocol/Abstractions/GameState DLLs match build output.
- **Development-period rule**: L0 + static evidence; real dual-client visuals remain for user acceptance.
