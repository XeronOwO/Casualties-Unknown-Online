# Turret lightSprite flicker timing sync

Date: 2026-08-20
ProtocolVersion: unchanged (24 — no wire change)

## Problem

The native turret has a two-stage warning→shot experience:

1. `TurretScript.Update` discovers a warm body and beeps (`turretsee`),
   `didBeep = true`, `beepTime` starts (TurretScript.cs:37-53).
2. 0.5 s later `beepTime >= 0.5` flips `didShoot = true`, sets
   `timeSinceFired = 0`, and fires the shot visuals (TurretScript.cs:40-53).

The turret's `lightSprite` is driven by the same `didShoot` latch:

```csharp
this.lightSprite.enabled = !this.didShoot || Mathf.Sin(Time.time * 10f) > 0f;
// TurretScript.cs:29
```

So on the trigger side the lightSprite is **steady** during the warning
(`didShoot == false`) and starts **flickering** at the firing moment
(`didShoot == true`).

CUO's turret replay already locked the post-fire state at the warning:
`TurretReplayTimeline.OnWarning()` returns `didShoot = true` immediately so
the game's own `Update` can never fire a REAL shot (the `!didShoot` guard at
TurretScript.cs:40-41). That same lock made the replayed `lightSprite` start
flickering 0.5 s early — the recorded `didShoot` immediate-lock tradeoff in
`docs/backlog.md` / `docs/event-replay-matrix.csv`.

## Change

A tiny CUO-owned `TurretLightSpriteGate` MonoBehaviour is added to the
replayed turret when `TrapStateActions.ApplyTurretFired` runs:

- `TurretLightSpriteGate.Begin(turret)` captures `turret.lightSprite` and the
  `TurretReplayTimeline.ShotDelaySeconds` (0.5 s) window.
- `LateUpdate` runs **after every Update** and forces
  `lightSprite.enabled = true` while the warning window is still open,
  overriding the early flicker that the game's `Update` would otherwise draw.
- When the window expires the component destroys itself; the native
  `Update` flicker takes over at the firing moment, exactly like the trigger
  side.

The existing `DelayedFireVisuals` coroutine (which already waits
`TurretReplayTimeline.ShotDelaySeconds` and then plays the shot visuals) is
unchanged in behavior; it now uses the timeline constant instead of a literal
`0.5f`.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| `TurretScript.Update` lightSprite rule | The game's flicker rule stays untouched; the gate only overrides it during the 0.5 s warning window | `TurretScript.cs:29` |
| `TurretScript.Update` shot guard | `didShoot` is still set true at the warning — the game can never fire a real shot on a replay side | `TurretScript.cs:40-53`; `TurretReplayTimeline.OnWarning()` |
| `TrapStateActions.ApplyTurretFired` | Starts the gate alongside the existing state write + delayed visuals | `TrapStateActions.ApplyTurretFired` |
| `TurretLightSpriteGate` | New MonoBehaviour: LateUpdate forces `lightSprite.enabled = true` until `ShotDelaySeconds`, then self-destroys | `TurretLightSpriteGate.cs` |
| `TurretReplayTimeline` | Reuses `ShotDelaySeconds` as the single timing constant (no new magic value) | `TurretReplayTimeline.cs` |
| Event dispatch | No new event/kind/message — the gate rides the existing `TurretFired` replay path (`TrapEffectApplier` host apply + `TrapVisualReplay` guest replay) | `TrapEffectApplier.cs`, `TrapVisualReplay.cs` |

## Why this is safe

- The change is **pure presentation**: it only affects the `lightSprite`
  renderer's enabled state during the replay's 0.5 s warning window.
- The safety-critical `didShoot = true` lock is **not** relaxed; the game's
  real-shot guard remains intact.
- The gate is a CUO-owned component on the turret object. It dies with the
  turret if the turret is destroyed, and removes itself after the window, so
  it cannot leak into later reload cycles.
- No protocol bump: old peers and new peers exchange the same `TurretFired`
  event; the visual fix is entirely receiver-side.

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- L0 reflective contract tests:
  - `TurretLightSpriteGateTests.GateType_Exists_DerivesFromMonoBehaviour_AndHasBegin`
    — the gate type exists, derives from `UnityEngine.MonoBehaviour`, and
    `Begin` takes `TurretScript`.
  - `TurretLightSpriteGateTests.GateType_HasLateUpdate` — the override point
    exists.
  - `TurretLightSpriteGateTests.ApplyTurretFired_MethodStillExists` — the
    replay action that starts the gate is still present.
- Existing `TurretReplayTimelineTests` lock the warning/firing timeline
  constants.
- Gates: `check-architecture`, `check-event-replay` (32 events),
  `check-entity-event-dispatch` (32 kinds × 3 tables) all pass.
- Full suite: 998 tests green.
- Development-period rule: L0/static evidence; **no manual acceptance**
  (user 2026-08-16 mandate).
