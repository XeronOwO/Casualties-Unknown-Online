# Remote Clone Facing Auto-Flip — Guest Remote Pose/Head-Orientation Desync Self-Check

Delivery-cycle fact sheet for the re-opened
`docs/backlog/todo/guest-remote-pose-head-orientation-desync.md` item
(moved to `docs/backlog/review/` after this cycle).

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | `Body.HandleVisuals` auto-flips a character when the look target crosses to the opposite side and either `moveDir` is nonzero or `attackCooldown` is positive | `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:3131-3135` |
| 2 | A render clone does not run the original `Body.Update`, so `attackCooldown` never decays on the proxy | `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyUpdatePatch.cs` (proxy branch); `reversing/.../Body.cs:3375` |
| 3 | `RemoteBodyFactory` clones the live template and only reset `crouchAmount` / `inWater` / `currentClimbable`; a stale template/inherited `attackCooldown` could survive into the clone | `src/CasualtiesUnknownOnline.GameAdapter/Character/RemoteBodyFactory.cs` (pre-change) |
| 4 | The proxy visual path already has a per-frame `NeutralizePoseInputs` for stale pose modifiers, but it did not include the facing auto-flip inputs | `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyUpdatePatch.cs:229-248` (pre-change) |
| 5 | `SessionStatePump` writes the owner's synced `isRight`/`LookPos`; a stray auto-flip in the same frame overrides that synced facing | `src/CasualtiesUnknownOnline.GameAdapter/Character/SessionStatePump.cs` |
| 6 | `FacialExpression.Update` reads `Body.eatTime` for the mouth/head sprite; the same frozen-clone decay gap makes an inherited `eatTime` stick | `reversing/.../FacialExpression.cs:96-100`; `reversing/.../Body.cs:3376` |

## 2. Root cause

The refined mouse-crossing reproduction is a stale-simulation-input leak on
the render clone: `HandleVisuals` is the game's own visual-facing authority
and it flips `isRight`/localScale when the mouse (look target) crosses the
180° ray. On a normal body, `attackCooldown` decays in `Body.Update`; on a
frozen remote clone, that update is skipped. If the clone inherited a positive
`attackCooldown` from the template (or a previous local-body state), the
auto-flip condition remains true forever, so the proxy can turn away from the
owner's actual synced facing and produce the reported head-angle/180° jump.

## 3. Implementation

- `BodyUpdatePatch.NeutralizePoseInputs` now also zeroes:
  - `body.attackCooldown`
  - `body.moveDir`
  - `body.eatTime`
  This makes the auto-flip input and the stale mouth/face input no-ops on every
  proxy/carry-participant visual pass, before `HandleVisuals` /
  `FacialExpression.Update` can consume them.
- `RemoteBodyFactory.CreateRemoteBody` also clears `attackCooldown`, `moveDir`
  and `eatTime` at clone creation, removing the one-frame window before the
  first visual pass.
- No wire/protocol change; no local-player simulated-authority change. The only
  local path affected is the already-frozen carried-body visual path, which is
  the same no-op visual family the carry presentation already uses; a carried
  player cannot perform normal attacks while frozen, so clearing its stale
  cooldown/eat-time does not alter active simulated gameplay.

## 4. Regression

- Red: new `RemoteCloneFacingNeutralizationTests.NeutralizePoseInputs_ClearsStaleAttackCooldown`
  invoked `NeutralizePoseInputs` on an uninitialized `Body` with
  `attackCooldown = 5f` and `eatTime = 1f`; before the fix it failed
  (`attackCooldown` stayed 5).
- Green: after the fix the same test passes (`attackCooldown` and `eatTime`
  become 0).

## 5. Verification (development-period, no manual dual-client acceptance)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2251 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | pass |
| `check-delivery.ps1` | pass (7 boxes checked) |
| Deploy identity | `tools/deploy.ps1` deployed to the real game directory; `CasualtiesUnknownOnline.GameAdapter.dll` SHA256 matches build output |
| Wire/protocol | no change |

## 6. What was NOT changed

- No new player-stream pose fields; the owner's existing 20 Hz
  `isRight`/`LookPos` remains the synced facing/head authority.
- No patch that overrides `HandleVisuals`'s native facing behavior for local
  players; only the frozen proxy/carry visual path neutralizes stale inputs.
- The water-current/clipping variant is not independently closed by this
  self-check; the primary refined mouse-crossing reproduction is covered by the
  regression contract above.
