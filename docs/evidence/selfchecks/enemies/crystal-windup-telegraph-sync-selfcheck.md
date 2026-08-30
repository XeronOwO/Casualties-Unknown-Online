# CrystalEnemy wind-up telegraph line sync — self-check (2026-08-24)

The animation audit listed the `CrystalEnemy` telegraph row open: frozen guest
copies skip `CrystalEnemy.Update`, so the host's pre-lunge warning line
(`CrystalEnemy.cs:66-90`) never appears on the guest view. This closes the
telegraph-line row in the enemy-presentation domain.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Crystal wind-up line | `CrystalEnemy.Update` writes the private `timeBeforeAttack`, draws `rend.SetPosition(0/1)`, fades `startColor` and sets `widthMultiplier` while `timeBeforeAttack < 0` (`CrystalEnemy.cs:66-90`). |
| Frozen copy behavior | `EnemyPatches` disables `CrystalEnemy.Update`/`FixedUpdate` on `RemoteEnemyDriver` copies, so the guest never draws the host's line. |
| Existing enemy stream | `EnemyStateMsg` carries the presentation subset (position/velocity/rotation/health/stunned/spider-leg targets) at 20 Hz and in the world-entry snapshot. |
| Line start | The line start is the entity transform, which is already carried by `EnemyStateMsg.Position`. |
| Line end | The native end point is a host-side physics raycast (`CrystalEnemy.cs:74-82`), so the exact world-space end must travel instead of being re-raycast on the guest. |
| Line fade/width | Native wind-up alpha = `-timeBeforeAttack * 2` and width = `timeBeforeAttack * 2` (`CrystalEnemy.cs:83-84`); the captured wind-up amount is `-timeBeforeAttack`. |
| Stuck fade | After `Lunge`, `stuck` is true and the native Update fades `endColor` from the sprite color to clear using `-timeBeforeAttack` (`CrystalEnemy.cs:97-110`); the remote driver's `Stunned` flag already carries the stuck state, so the helper applies this different beam when stuck. |

## 2. Changes

- **Wire** — `EnemyStateMsg.CrystalWindupAmount` (ProtoMember 8, float,
  seconds, 0 = idle) and `EnemyStateMsg.CrystalLineEnd` (ProtoMember 9,
  nullable `NetVector2Msg`, world-space end point).
  `ProtocolVersion` 45 → 47 because older peers cannot render the telegraph
  (the same bump carries the trader swing event below).
- **Domain** — `EnemyEntity` mirrors both fields in `ToEnemyStateMsg` /
  `EnemyStateMsg.ApplyTo`.
- **Capture** — `CrystalWindupPresentation.CaptureAmount` reads the private
  `CrystalEnemy.timeBeforeAttack` (exact float) and returns
  `-timeBeforeAttack` when positive; `CaptureLineEnd` reads the private
  `LineRenderer` end point. Non-crystal enemies and idle crystals carry 0/null.
- **Apply** — `CrystalWindupPresentation.Apply` mirrors the start point from
  the entity transform, the received end point, and reproduces the native
  wind-up fade/width math onto the frozen copy's `LineRenderer`; when the copy
  is stuck (`RemoteEnemyDriver.Stunned`) it applies the native post-lunge
  `endColor` fade instead; zero clears both line colors.
  `RemoteEnemyDriver.CrystalWindupAmount` stores the last applied amount so
  `EnemySyncCoordinator` can log the visible-toggle transition once.
- **No new NetMsg** — the line is a continuous presentation state, so it rides
  the existing 20 Hz `EnemyState` stream and the world-entry snapshot; no event
  matrix/direction-table/entity-event-dispatch changes.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1358 passed |
| `EnemyStateRoundtripTests` | roundtrips `CrystalWindupAmount`/`CrystalLineEnd`; missing fields default to 0/null |
| `CrystalWindupPresentationTests` | 4 passed (helper surface locked) |
| `GameFieldContractTests` | exact `timeBeforeAttack` (float) and `rend` (`UnityEngine.LineRenderer`) contracts resolved |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` / `tools/check-entity-event-dispatch.ps1` | passed (no entity event kind touched) |
| `dotnet format` | run |
| Deploy | `tools/deploy.ps1` to the real game directory succeeded |
| Manual acceptance | Not required by the developer-cycle rule; L0 + static evidence, no manual acceptance. |

## 4. L0 proof

- `EnemyStateRoundtripTests` proves `CrystalWindupAmount` and `CrystalLineEnd`
  survive `EnemyEntity` → `EnemyStateMsg` → `ApplyTo`, and that a missing
  message leaves 0/null (the no-line state).
- `CrystalWindupPresentationTests` locks the adapter boundary:
  `CaptureAmount(CrystalEnemy) -> float`,
  `CaptureLineEnd(CrystalEnemy) -> NetVector2?`,
  `Apply(BuildingEntity, float, NetVector2?) -> bool`, and
  `RemoteEnemyDriver.CrystalWindupAmount`.
- `GameFieldContractTests` locks the two new Traverse-accessed game members
  with their exact runtime types.
- Full suite has no behavioral regression; the existing enemy freeze
  `PatchContractTests` still verify `CrystalEnemy.Update`/`FixedUpdate` are
  skipped on remote copies.

## 5. Structure review

- `CrystalWindupPresentation` is a focused static bridge (capture/apply only,
  no cross-call state).
- `RemoteEnemyDriver` gains one scalar property (used only for the
  visible-toggle transition log), no new responsibility.
- `EnemySyncCoordinator` remains under the 600-line gate; it adds one captured
  pair and one apply call.
- `EnemyStateMsg`/`EnemyEntity` add two presentation fields, consistent with
  the existing `SpiderLegTargets` pattern.
- No dead mechanism is left behind; the existing periodic enemy stream remains
  the rendering source and the late-joiner snapshot carries the same fields.

## 6. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it ("由你来自主挑选并完成"), so this cycle's plan is approved
without a separate interactive approval step.
