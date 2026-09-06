# Remote medical fidelity and syringe minigame self-check

Owner cycle: backlog `todo/remote-fentanyl-injection-and-medical-panel-desync.md`.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native fentanyl/local injectable use starts `SyringeMinigame` | `reversing/.../Item.cs:1035-1057` (fentanyl useLimbAction), `SyringeMinigame.cs` |
| 2 | Syringe minigame delivers a per-frame millilitre rate | `SyringeMinigame.cs:88-93` (`OnUse(num2 * Time.deltaTime)`), native callback `wat.Inject(limb, mult * 100f)` |
| 3 | Cross-player injectable path previously skipped the minigame | `PlayerItemUseService.HandleUseRequest` medicine branch, `RemoteMedicineCatalog.TryCreatePlan` (pre-change fixed per-item draw) |
| 4 | Remote WoundView is a display-only clone fed by the 1 Hz character snapshot | `RemoteMedicalCoordinator.cs`, `RemoteMedicalView.cs` |
| 5 | WoundView reads `heartRate`, `bloodPressureReadout`, `totalHappiness`, `canTakeNap` | `WoundView.cs:151,194-230,258` |
| 6 | Painkiller/antidepressant/mindwipe happiness are produced by component/body updates on a live body, not Mapster | `Painkillers.cs:26-104`, `Antidepressants.cs:27-46`, `Body.cs:193-199,3569` |
| 7 | Native ECGVisualizer is hard-wired to `PlayerCamera.main.body` | `ECGVisualizer.cs:10-16,55` |
| 8 | Native MoodleManager status icons read the local body and are shown while WoundView is open | `MoodleManager.cs:10-17,32-35,855-863` |

## 2. Design

- **Partial-dose wire**: `PlayerItemUseRequestMsg.DoseAmount` (protobuf field 4)
  carries the ml actually pumped by the native minigame. `0` keeps the old
  per-item full dose behaviour, so existing callers and old-version requests
  are unchanged. `ProtocolVersion.Current` bumped to 11 because mixed-version
  clients would otherwise silently turn a partial dose back into a full dose.
- **Remote minigame routing**: `RemoteMedicalOperationHandler` now detects
  injectable/IV medicines on the WoundView drag release and starts the native
  `SyringeMinigame` with the display-body limb. The session accumulates the
  delivered ml, keeps the native syringe fill in sync by temporarily lowering
  `item.condition`, and on `MinigameBase.EndMinigame` sends a `SendUseRequest`
  with the exact ml. The active session is cancelled when the remote medical
  focus closes, and `StartMinigame` is verified by reference before the session
  is kept, so no stale/stranded session can send or block later use.
- **Display-body projection**: `RemoteMedicalCoordinator.ApplyDisplayDerived`
  explicitly projects the fields a live body would produce in `Update`: heart
  rate, blood pressure readout, opiate happiness, antidepressant happiness and
  mindwipe. Stale mindwipe components on the display clone are removed when the
  snapshot says the state is gone.
- **Native HUD redirection**: while the remote medical view is open,
  `ECGVisualizer.get_body` is redirected only for the ECG inside the active
  WoundView, and `MoodleManager.UpdateMoodles` temporarily reads the display
  body so bottom status icons represent the viewed player. The WoundView nap
  button is forced disabled after the native update.

## 3. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Protocol partial dose | `DoseAmount` roundtrips | `UseRequest_RoundTripsMinigameDoseAmount` |
| Medicine plan partial draw | explicit dose draws proportionally and caps at remaining total | `Plan_DrawsExplicitPartialDoseProportionallyAcrossStacks`, `Plan_ExplicitDoseCapsAtRemainingLiquidTotal` |
| Host applies partial dose | 50 ml morphine on host yields 45 opiate and 50 ml left in item | `Guest_InjectPartialMorphineDoseOnHost_AppliesExactMinigameMl` |
| Remote minigame lifecycle | start refuses when another minigame is active; cancel on close | `RemoteMedicalOperationHandler.TryStartRemoteSyringeUse`, `CancelActiveSyringeUse`, `RemoteMedicalView.Close` |
| Display vitals | heartRate/blood pressure/opiate/antidepressant/mindwipe projection | `RemoteMedicalCoordinator.ApplyDisplayDerived` + game field evidence |
| HUD redirection | ECG only inside WoundView, MoodleManager display-body swap, nap disabled | `RemoteMedicalPatches.RemoteMedicalEcgBodyPatch`, `RemoteMedicalMoodleBodyPatch`, `RemoteMedicalWoundViewBodyPatch` |

## 4. Verification

- **L0 unit**: `RemoteMedicineApplicationTests` +2 partial-plan cases,
  `PlayerInteractionServiceTests` +1 partial-dose host case +1 protocol
  roundtrip.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` full suite
  green, `dotnet format`, normative gates all pass.
- **Development-period rule**: L0 + static evidence; real dual-client visuals
  and minigame feel remain for user acceptance, as stated in `AGENTS.local.md`.
