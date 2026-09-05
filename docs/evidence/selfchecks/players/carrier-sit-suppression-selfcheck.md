# Carrier-side idle-sit suppression self-check

Owner cycle: backlog `carrier-sit-while-carrying`. Decision: close the
"carrier can sit while carrying a passenger" family gap by extending the
carry-participant idle-sit suppression to the carrier half, using the runtime
carry mirror as the single source for the local carrier fact and a remote-clone
flag for peer views. No carry authority, wire protocol, or release semantics
were changed.

## 1. Problem evidence

The native idle-sit condition is `Body.HandleVisuals`
(`reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:3145-3166`): when
`idleTime > 12` and the body is stationary/standing and `movingAllowed` is
true, the game plays `ExperimentSit` / `ArmsSit`. The already-landed
`carried-player-idle-sit-suppression` ticket covered the carried rider; the
carrier half was still allowed to sit, which is physically unreasonable.

## 2. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Native idle-sit condition | `Body.cs:3162-3166` — `idleTime > 12 && movingAllowed && !exercising` |
| 2 | Local carrier fact | `IPlayerInteractionControl.TryGetCarried(LocalSteamId)` via `GameAdapterBridge.IsLocalCarrier` |
| 3 | Remote carrier-clone flag | `RemoteBodyDriver.IsCarrier`, set by `RemotePlayerRenderer` before `SessionStatePump` |
| 4 | Local carrier prefix | `BodyUpdatePatch.Prefix` holds `idleTime = 0` before original `Body.Update` |
| 5 | Local carrier postfix | `BodyUpdatePatch.Postfix` actively leaves an already-playing sit clip |
| 6 | Carrier state publication | `RunCoordinator.PublishBodyState` never sends `Sitting=true` for a carry participant |
| 7 | Remote clone replay | `SessionStatePump` suppresses sit replay for `IsCarriedRider || IsCarrier` |
| 8 | Remote clone idle/exit | `BodyUpdatePatch` proxy branch includes `RemoteBodyDriver.IsCarrier` |

## 3. Whole-family audit

| Family member | Change |
|---|---|
| Local carrier (host or guest) | `BodyUpdatePatch` zeroes the idle timer before `Body.Update` and postfix-exits an already-playing `ExperimentSit`/`ArmsSit` |
| Local carried rider | Existing `CarriedBodyDriver` proxy path and `CarriedBodyPose` suppression remain |
| Remote carrier clone (all other players' views) | `RemoteBodyDriver.IsCarrier` suppresses sit replay and the proxy idle timer/exit |
| Remote rider clone (all views) | Existing `IsCarriedRider` suppression remains |
| Third-party view (neither participant local) | Both remote roles are flagged by the same `RemotePlayerRenderer` pass, so sit replay is suppressed on every peer |
| Normal non-carried idle-sit | Unchanged: `CarriedBodyPose` still allows non-participants to publish/replay sit |
| Carry authority/protocol | Unchanged — no wire, kernel event, or relation lifecycle change |

## 4. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Local carrier never starts idle-sit | `BodyUpdatePatch.Prefix` calls `IsLocalCarrier` through `IPatchBridge` and sets `idleTime = 0` | `CarriedBodyPoseTests.Carrier_HoldsIdleTimerAtZero`, `CarrierSitContractTests.PatchBridge_HasLocalCarrierQuery` |
| Local carrier exits existing sit | `BodyUpdatePatch.Postfix` plays `Grounded` when `ShouldExitSit(true, ...)` | `CarriedBodyPoseTests.Carrier_WithSitClip_ActivelyExitsSit`, `CarrierSitContractTests.BodyUpdatePatch_HasLocalCarrierPostfix` |
| Carrier state never published as sitting | `RunCoordinator.PublishBodyState` uses both `TryGetCarried` and `TryGetCarrier` through the mirror | `CarriedBodyPoseTests.Carrier_NeverPublishesSittingEvenWhenIdleTimerExceeded` |
| Remote carrier clone never replays sit | `RemoteBodyDriver.IsCarrier` + `SessionStatePump` uses `CarriedBodyPose.ShouldReplaySit` | `CarriedBodyPoseTests.CarrierClone_DoesNotReplaySitFromStream`, `CarrierSitContractTests.RemoteBodyDriver_HasCarrierFlag` |
| Remote carrier clone cannot accumulate/linger sit | `BodyUpdatePatch` proxy branch includes `RemoteBodyDriver.IsCarrier` | pure `ShouldZeroIdleTimer` / `ShouldExitSit` tests |
| Non-carried idle-sit preserved | Pure rules return the old behavior when `isCarryParticipant=false` | `NonCarriedIdleBody_MayPublishSitting`, `NonCarriedRemote_MayReplaySitFromStream`, `NonCarriedBody_WithSitClip_IsNotForcedOutOfSit` |

## 5. Verification design and results

- **L0 pure tests**: `CarriedBodyPoseTests` covers rider, carrier, non-participant
  publish/replay/exit/idle-timer decisions.
- **Adapter contracts**: `CarrierSitContractTests` locks `IPatchBridge.IsLocalCarrier`,
  `GameAdapterBridge.IsLocalCarrier`, `RemoteBodyDriver.IsCarrier`, and
  `BodyUpdatePatch.Postfix` shapes so the wiring cannot silently disappear.
- **Full regression**: `dotnet test CasualtiesUnknownOnline.slnx` — **2250
  passed / 0 failed**.
- **Build/format/gates**: `dotnet build`, `dotnet format`,
  `check-architecture.ps1`, `check-event-replay.ps1`,
  `check-entity-event-dispatch.ps1` pass.
- **Deployment**: latest DLLs deployed to the real game directory with
  `tools/deploy.ps1`; GameAdapter/Runtime/Plugin/GameState hashes match the build
  output. The repo delivery checklist keeps manual dual-client acceptance as a
  user release action outside the development gate.

## 6. Structure review

- `BodyUpdatePatch` was moved out of `BodyPatches` into its own top-level type,
  keeping the patch file below the architecture line gate with a real
  responsibility split (per-frame proxy/visual update).
- The carrier fact is read from the existing runtime carry mirror through
  `IPatchBridge`; no new local marker or duplicate authoritative state was added.
- `RemoteBodyDriver.IsCarrier` is a per-frame presentation flag on the render
  clone, owned/cleared by `RemotePlayerRenderer`; it is not kernel state.
- No dead mechanism, no wire/protocol/event-matrix change.
