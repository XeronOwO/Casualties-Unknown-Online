# Piggyback drop cleanup — self-check (2026-08-26)

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/selfchecks/MANIFEST.md` and
> `docs/architecture-evolution/protocol.md` before citing.

Root-causes the remaining "after Drop the character cannot move" symptom from
the carry/piggyback follow-ups.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Carry/piggyback relation | `PlayerCarryService` — host-owned `_carriedBy`/`_carrying`, broadcast `PlayerCarryStateMsg` |
| 2 | Carried local body presentation | `CarriedBodyDriver` + `PlayerInteractionApply.UpdateCarriedBody` |
| 3 | Render-proxy freeze | `BodyPatches` — `BodyUpdatedPatch`/`BodyFixedUpdatePatch`/`LimbUpdatePatch` freeze whenever a carried/remote driver is present |
| 4 | Release restore | `PlayerInteractionApply.ApplyCarryStateToBody` + `CarriedBodyPlacement.RestoreLocalBody` |
| 5 | Unity destruction semantics | `Object.Destroy` is deferred to end-of-frame, so a component can still be returned by `GetComponent` for the remainder of that frame |

## 2. Root cause

- The first restore fix re-enabled the body/limb rigidbodies and called
  `Stand(true)`/`Ragdoll()`.
- The release code then called `Object.Destroy(driver)`, which is deferred.
- If the release event was processed before the same frame's `Body.Update`,
  the old driver was still present, so `BodyUpdatePatch` entered the proxy
  branch and `FreezeRigidbodies` re-disabled every rigidbody **after**
  `RestoreLocalBody` had just enabled them.
- The next frame had no `CarriedBodyDriver`, but nothing re-enabled the
  rigidbodies again — the released character stayed frozen/floating.

## 3. Changes

- `PlayerInteractionApply.ApplyCarryStateToBody` now sets
  `driver.CarrierSteamId = 0` **before** `RestoreLocalBody` and `Destroy`.
- `CarriedBodyDriver` gains a pure active-state predicate plus `IsCarrying`
  / `IsCarryingInParent` helpers. Active == driver present **and** carrier id
  non-zero.
- `BodyPatches` (Body FixedUpdate/Update, Limb Update, Attack/landing local
  detection), `BodyNapPatch`, `BodyWorkoutPatch`, `BodyItemPatches` use the
  active predicate instead of raw component presence, so a released driver
  stops freezing/skipping immediately even while Unity still holds the object
  until end of frame.
- No wire/protocol change.

## 4. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Driver active semantics | active requires non-zero carrier | `CarriedBodyReleaseTests.ActiveDriver_WithNonZeroCarrier_IsCarried`, `ReleasedDriver_WithZeroCarrier_IsInactiveImmediately`, `MissingDriver_IsNotCarried` |
| Release path clears carrier before deferred destroy | `CarrierSteamId=0` assignment in `PlayerInteractionApply` | source diff; driver-state regression test models the release edge |
| Render-proxy freeze guards | use active predicate in Body/Limb patches | `BodyPatches`/`BodyNapPatch`/`BodyWorkoutPatch`/`BodyItemPatches` source changes |
| Full carry state machine release | host-as-rider release from a guest carrier | `PlayerInteractionServiceTests.HostRider_CanRequestReleaseFromGuestCarrier` |
| UI get-down availability | unchanged, already covered | `OnlineUiMemberProjectionTests.CarriedLocalShowsGetDownOnLocalRowAndOnCarrierRemoteRow` |

## 5. Verification

- **L0**: `dotnet test CasualtiesUnknownOnline.slnx` — **1515 passed / 0 failed**.
- **Gates**: build/architecture/event-replay/entity-dispatch checks run during
  the cycle.
- **Runtime verification**: development-period rule — L0 simulation + static
  evidence, **no manual acceptance**.
