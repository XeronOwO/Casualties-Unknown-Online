# Remote Clone Leg-Speed Pose Sync — Host Severe Sleepiness Posture Desync Self-Check

Delivery-cycle fact sheet for
`docs/backlog/todo/host-severe-sleepiness-posture-desync.md`
(moved to `docs/backlog/review/` after this cycle).

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The owner's `Body.HandleVisuals` feeds `max(crouchAmount, 1 - legSpeedMult)` into the CrouchAmount animator parameter | `reversing/Body.decompiled.cs:3259` (dnSpy decompile) |
| 2 | `Body.legSpeedMult` is a computed get-only property from limb force/consciousness/stamina/hunger/temperature/weight etc | `reversing/Body.decompiled.cs:67-87` |
| 3 | A remote render proxy skips `Body.Update` and freezes limb rigidbodies, so the proxy's own `legSpeedMult` collapses toward 0 and cannot reproduce the owner's value | `BodyUpdatePatch.cs`; `RemoteBodyFactory.cs`; `BodyPatches.cs` |
| 4 | The existing proxy override set the CrouchAmount parameter from `crouchAmount` only, so severe sleepiness/weakness (low owner `legSpeedMult`) was lost | `BodyUpdatePatch.cs` pre-change |
| 5 | The 1 Hz `CharacterHealthMsg` is the self-healing carrier for remote-clone presentation data; it already carries the face vitals and head/mouth state | `CharacterDataSync.cs`; `CharacterHealthMsg.cs` |
| 6 | The owner's `legSpeedMult` is directly capturable from the live Body and fits the same 1 Hz snapshot without a new protocol channel | `CloneBodyPosePresentation.Capture` |

## 2. Root cause

The reported host severe-sleepiness posture desync is not a missing "make the
clone slouch" patch. The root cause is that the remote clone's CrouchAmount
animator input was reconstructed from only the proxy's boolean `crouchAmount`;
the owner's HandleVisuals uses the *maximum* of that amount and the
leg-speed-derived weakness input (`1 - legSpeedMult`). A severely sleepy body
has a low `legSpeedMult`, so the owner visibly slouches; the frozen clone cannot
compute that value and previously stood straight.

## 3. Implementation

- `CharacterHealthMsg.LegSpeedMult` (ProtoMember 80) carries the owner's
  computed leg-speed multiplier in the existing 1 Hz character snapshot.
- `CloneBodyPosePresentation` is the thin adapter capture/apply pair:
  - capture: `health.LegSpeedMult = Mathf.Clamp01(body.legSpeedMult)`;
  - apply: stores the value on `RemoteBodyDriver.LegSpeedMult` (default 1f,
    so a clone before its first snapshot stands with normal strength).
- `BodyPosePresentation.ProxyCrouchInput` is the pure Runtime rule that
  reproduces the game's own `max(crouchAmount, 1 - legSpeedMult)` input,
  clamping `legSpeedMult` to 0-1.
- `BodyUpdatePatch` uses `remoteDriver.LegSpeedMult` through that pure rule when
  it overrides the CrouchAmount animator parameter after the proxy's
  HandleVisuals pass.
- `ProtocolVersion.Current` 6 → 7 because the 1 Hz character wire shape changed.

## 4. Regression / tests

| Test | Coverage |
|---|---|
| `BodyPosePresentationTests` | weak leg-speed produces the slouch input; full strength leaves only the real crouch; crouching stays fully crouched; out-of-range leg-speed is clamped |
| `CharacterHealth_LegSpeedMult_RoundTrips` | the new wire field survives protobuf round-trip |
| `CloneBodyPosePresentationTests` | the adapter capture/apply surface and the `RemoteBodyDriver.LegSpeedMult` field exist |
| Full suite | 2275 passed / 0 failed |

## 5. Verification (development-period, no manual dual-client acceptance)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2275 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | passed (7 boxes checked) |

## 6. Whole-family alignment

This is the same body-pose presentation family that previously lost remote
pose facts one by one. Instead of adding a one-off "make the clone slouch"
patch, the change syncs the single computed input (`legSpeedMult`) that drives
the weakness/slouch portion of the CrouchAmount animator parameter. It covers:
- severe sleepiness / low-energy weakness posture;
- low consciousness / stamina / hunger / temperature movement-debility poses
  through the same computed `legSpeedMult`;
- every remote view that already uses the 1 Hz character snapshot.

Already-separated pose family members remain on their existing paths:
head/mouth replay, face vitals, ragdoll/limb poses, carry placement and
idle-sit suppression.

## 7. What was NOT changed (and why)

- No change to carry/movement authority, host rules, or any gameplay state.
- No new NetMsg / separate pose channel: the existing 1 Hz character snapshot
  is the carrier.
- No new per-feature Harmony patch; the existing proxy visual path now receives
  the missing owner-side input.
- The game's own HandleVisuals remains the visual authority; the override only
  supplies the correct proxy-side input.
