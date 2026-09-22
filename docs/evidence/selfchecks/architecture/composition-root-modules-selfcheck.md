# Self-check — composition-root feature modules and the session-reset contract

Cycle: 2026-09-22, from `414d7e17`. Ticket:
[composition-root-feature-modules](../../../backlog/review/composition-root-feature-modules.md).
Review report (machine-local, not committed): `%TEMP%\cuo-review-composition-modules.md`.

## Mechanism x change x evidence

| Mechanism | Change | Evidence |
|---|---|---|
| `CuoBootstrap` registration list (586 lines, on the aggregate line cap) | Registrations moved unchanged and in order into twelve feature composition modules; the file keeps the assembly order, the DI-cycle guard and the startup-failure log (156 lines) | `CuoServiceOrderTests.TheUpdateOrder_IsThePinnedRegistrationOrder` (18 `ICuoService` names) and `...TheContentSourceOrderAndHandlerDiscovery_AreStillWired`; the adversarial review compared all 179 HEAD registrations token by token against the modules and found one deliberate difference (`typeof(CuoBootstrap).Assembly` to `typeof(NetworkingPacketPlaneComposition).Assembly`, the same assembly) |
| Session-reset stage, five spellings | `ISessionReset` (one method) declared and implemented by every Runtime session-state owner; helper resets renamed; `ResetSession` retired | `SessionLifecycleGateTests` 11/11; `TheRetiredSessionResetSpellings_HaveNotComeBack` scans 1,635 source files |
| Four subscriptions with no unbind half | `WorldEntityKernelProjection`, `ChatService` (session end), `PendingReportFallback` (local scene report), `ItemIdCoordinator` (member added) gained the unbind plus the owner wiring that reaches it | `...EverySessionLifecycleSubscription_HasItsUnbindHalf` (94 subscriptions, each paired); the container disposes the singletons through `Plugin`'s provider dispose |
| Dead surface | `SessionPeerMaintenance.ResetForSessionEnd` (no caller; `SessionService.TeardownSession` already performs both steps) and `IKernelProtocolControl.ResetForSessionEnd` (no caller) deleted; `IKernelBatchApplication`/`ItemKernelAuthority`/`WorldParamsService` renamed to `ResetSessionState` | gate fact 3; build 0 warnings / 0 errors; `SyncCoverageGateTests` quote check after the evidence quotes were updated |
| Application-layer exemption | `KernelProtocolService` keeps the method name and the paired subscription without the interface (Application may reference GameState and Protocol only); `GuestCommandReconciliation`, believed to be in the same position, is a Runtime type and implements the contract | `...TheApplicationLayerExemption_IsStillNeededAndStillComplete` (three declared files, completeness and staleness both asserted) |
| Game Adapter session wiring | Unchanged: its session binding plus three domain `BindToSession`/`Unbind` pairs | declared census of four in `...EverySessionEndSubscriber_ReactsThroughTheOneContractMethod` |

## Verification (measured)

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings, 0 errors.
- `dotnet format CasualtiesUnknownOnline.slnx`: exit 0.
- Normative gates: 139/139 (the new gate contributes 11).
- Full suite with build: 3,797 + 139 passing.
- Independent adversarial review (fresh context, working tree frozen): 0 blocker / 4 major / 7 minor /
  5 nit; every finding fixed in this cycle.

## What this does not prove

No game-internal, dual-client or physical-machine evidence. The change is architectural — module
boundaries, one reset method name, teardown wiring at disposal — and its runtime proof is the pinned
composition order plus the full suite; the reset bodies themselves are the ones HEAD already had.
Physical-machine deployment and dual-client acceptance remain the user's release-cycle actions.
