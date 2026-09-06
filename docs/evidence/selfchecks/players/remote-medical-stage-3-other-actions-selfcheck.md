# Remote medical Stage 3: remaining native medical minigames/actions self-check

Owner cycle: backlog `review/remote-medical-stage-3-other-actions.md`.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Generic Stage 3 host session reuses the medical operation envelope and shared item/limb reservations | `OtherMedicalOperationSessionService`, `OtherMedicalOperationSession`, `MedicalOperationSessionService` forwarding |
| 2 | Bandage/dressing minigame wraps are reported per `DoBandageAction` and host applies scaled profile/condition commit | `RemoteBandageMinigameCatalog`, `OtherMedicalOperationApplier.ApplyBandageWrap`, `RemoteOtherMedicalOperationHandler.ReportBandageWrap` |
| 3 | Splint/tourniquet removal is an exclusive one-shot session; removed item is awarded to the operator | `OtherMedicalRemovalApplier`, `AwardedItem` on `MedicalOperationEndCommittedMsg`, `RemoteOtherMedicalOperationHandler.TryStartRemoteRemoval` |
| 4 | Dislocation fix is exclusive-limb-leased; hits report to host and success commits `Dislocated=false` | `OtherMedicalOperationSessionService` reservation, `OtherMedicalOperationApplier.ApplyDislocationHit/CompleteDislocation`, `DislocationCheckForHitPatch` |
| 5 | AED stage battery drains and shock are host-committed; defibrillation clears fibrillation on the torso | `OtherMedicalOperationApplier.ApplyAedStage/ApplyDefibrillation`, `AedUpdatePatch` |
| 6 | Manual defib shock charge and active-time battery drain are host-committed | `OtherMedicalOperationApplier.ApplyManualShock/DrainManualDefibTime`, `ManualDefibShockPatch` |
| 7 | Amputation cut progress is incremental; partial damage persists on cancel and 100% commits dismember only on host | `OtherMedicalOperationApplier.ApplyAmputationCut/CompleteAmputation`, `AmputationPhysicsUpdatePatch` |
| 8 | Native WoundView specials (tourniquet/splint removal, dislocation) and limb drag items route through the session instead of being blocked/direct-applied | `RemoteMedicalPatches`, `RemoteMedicalOperationHandler.TryHandleLimbUse/TryStartRemoteWoundSpecial` |
| 9 | Third-party/target/operator State/End apply and AwardedItem local restore | `MedicalOperationApply`, `RemoteMedicalCoordinator.ApplyMedicalState` |
| 10 | Protocol bump to 14 with new kinds/action payloads | `MedicalOperationKind`, `MedicalOperationUpdateAction`, `ProtocolVersion.Current = 14` |

## 2. Verification

- **L0 tests**: `MedicalOperationOtherActionsSessionTests` (11 tests) covers guest→host bandage, guest→guest relay bandage, host→guest AED/manual/amputation/dislocation, splint/tourniquet removal both directions, exclusive dislocation lease, third-party state/terminal, bandage cancel partial semantics, amputation partial/final.
- **Full suite**: 2381 main tests + 16 normative gate tests green.
- **Build/format**: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings/0 errors; `dotnet format` clean.
- **Architecture gate**: all source files under the 600-line threshold; one top-level type per file.
- **Adversarial self-check**: independent subagent reviewed host authority, lifecycle, item/battery, native minigame integration, protocol and matrix; fixes landed for pre-ack cancel/terminal, authoritative-item marker set, patch state reset, NaN/negative validation, zero-item snapshot removal and state sequence.
- **Deployment**: latest build deployed to the real game directory; SHA-256 of Runtime/GameAdapter/Plugin/Protocol/Abstractions/GameState DLLs match build output.
- **Development-period rule**: L0 + static evidence; real dual-client visuals remain for the final unified user acceptance pass.
