# Piggyback release facing restore — self-check (2026-08-27)

Root-causes the reported "after Drop the released host's body orientation is
stuck and cannot flip" symptom.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native facing render | `Body.SwitchDir` (`reversing/.../Body.cs:1187-1209`) flips both `Body.isRight` and `transform.localScale.x`; `HandleVisuals` auto-flip (`Body.cs:3123-3134`) only runs when the body's native Update/visuals path is active |
| 2 | Carried local body presentation | `CarriedBodyDriver` + `PlayerInteractionApply.UpdateCarriedBody` — while carried, Body Update is skipped and the driver writes the carrier's `isRight` into the local body |
| 3 | Render-proxy freeze | `BodyPatches` skips Body Update/Limb Update for carried/remote bodies |
| 4 | Release restore | `PlayerInteractionApply.ApplyCarryStateToBody` + `CarriedBodyPlacement.RestoreLocalBody` |
| 5 | Remote clone facing | `SessionStatePump.Apply` writes clone `isRight` + scale; `RemotePlayerRenderer.ApplyLocalCarrierFollow` overrides clone `isRight` for the carrier-side immediate follow |

## 2. Root cause

- CUO's carried-follow path wrote `Body.isRight = carrier.IsRight` while the
  body's native `SwitchDir`/`HandleVisuals` was skipped. It did **not** write
  the matching `transform.localScale.x`, which is the side the sprite actually
  renders from.
- On release, `RestoreLocalBody` re-enabled physics and standing but did not
  reconcile the scale. The local body could therefore keep an isRight/scale
  mismatch from the ride. The native auto-flip then toggled the logical flag
  against a stale visual, so the released body appeared fixed to one direction.
- The same oversight existed in the carrier-side clone override
  (`RemotePlayerRenderer.ApplyLocalCarrierFollow`), which wrote `isRight`
  without the scale.
- This was a shared coupling bug, not a single release-path line: every CUO
  path that writes `isRight` on a game Body must write the rendered scale too.

## 3. Changes

- New `BodyFacing` shared rule in `GameAdapter/Character/BodyFacing.cs`:
  - `FacingScale(bool isRight, float currentScaleX)` preserves the current
    horizontal magnitude and applies the correct sign.
  - `Apply(Body body)` writes the reconciled scale onto a live Body.
- `SessionStatePump.Apply` now uses the shared rule for render clones.
- `PlayerInteractionApply.UpdateCarriedBody` applies the shared rule after
  writing the carrier's `isRight`, so the carried local body's visual follows
  the same direction as its state stream.
- `RemotePlayerRenderer.ApplyLocalCarrierFollow` applies the shared rule after
  overriding the carried clone's `isRight`, so the carrier-side immediate
  follow is visually correct even before the rider's next 20 Hz tick.
- `CarriedBodyPlacement.RestoreLocalBody` applies the shared rule after the
  native stand/ragdoll restore, so any mismatch accumulated during the ride is
  repaired before local simulation resumes. The released body's
  `HandleVisuals` auto-flip can then work normally.

## 4. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Facing rule | shared `FacingScale` maps logical facing to scale sign, preserving magnitude | `BodyFacingTests.FacingScale_MirrorsLogicalFacingIntoScaleSign` (4 cases, including negative magnitudes) |
| Apply entry point | `BodyFacing.Apply(Body)` exists and takes exactly one Body | `BodyFacingTests.Apply_TakesALocalBodyAndReconcilesScale` |
| All CUO isRight writes reconciled | `SessionStatePump`, `PlayerInteractionApply.UpdateCarriedBody`, `RemotePlayerRenderer.ApplyLocalCarrierFollow` all call the shared rule | source diff; the project-wide grep for `.isRight =` has no unwrapped CUO write |
| Release restore repairs mismatches | `RestoreLocalBody` ends with `BodyFacing.Apply(body)` | source diff + static evidence (no Unity game-object L0 harness by design) |
| Carry orientation preserved during ride | `UpdateCarriedBody` now updates scale immediately with `isRight` | source diff; no behavior change to back-offset/standing/follow |

## 5. Verification

- **Before-red**: `BodyFacingTests` first ran against the pre-fix adapter and
  failed with `TypeLoadException` (the shared `BodyFacing` type did not exist),
  pinning the missing facing-reconciliation layer.
- **L0**: `dotnet test CasualtiesUnknownOnline.slnx` — **1559 passed / 0 failed**.
- **Gates**: `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
  `tools/check-entity-event-dispatch.ps1` all pass.
- **Format**: `dotnet format` run; `--verify-no-changes` only flags the
  gitignored generated `obj/.../MyPluginInfo.cs` (known pre-existing).
- **Runtime verification**: development-period rule — L0 simulation + static
  evidence, **no manual acceptance**.

## 6. Structure review

- New `BodyFacing` is a small one-top-level-type static helper; no state bools.
- No file crossed the 600-line gate: `PlayerInteractionApply` grew by one
  call line, `SessionStatePump` shrank by the inline scale block.
- No dead mechanism remains; the native `SwitchDir` remains the only place
  that mutates facing during real local simulation.
