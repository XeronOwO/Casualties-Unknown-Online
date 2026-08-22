# Remote Clone FacialExpression Disfigurement / Eye-Loss Presentation — Self-Check

Owner cycle: autonomous backlog selection. The user instructed this session to
pick one backlog item autonomously and complete it, then write the result back
into `../backlog.md` ("由你来自主挑选一个并完成，记得在完成之后回写 backlog").
The chosen item is the limb-presentation cycle's recorded **native body
presentation residual**: the remote clone's body-level `FacialExpression`
latches (`Disfigured`, `EyeGone`, `BothEyesGone`, the owner's random
`disfiguredIndex`) remained template-driven.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | `CharacterHealthMsg` already carries the three Body latches (`Disfigured`, `EyeGone`, `BothEyesGone`) and the 1 Hz character snapshot is the clone's presentation source | `CharacterHealthMsg.cs` ProtoMember 28-30; `CharacterDataSync.cs` |
| 2 | The owner's random disfigurement head index and the long-run heal presentation timers live on the `FacialExpression` child component, not on `Body`, so Mapster cannot see them | FacialExpression.cs: `disfiguredIndex`, `disfiguredTimeFullSkin`, `eyeTimeHealed` |
| 3 | `FacialExpression.Update` runs on render clones (no patch skips it) and reads `body.disfigured` / `body.eyeGone` / `body.bothEyesGone` plus the face component fields | FacialExpression.cs; no `FacialExpressionUpdatePatch` in `BodyPatches.cs` |
| 4 | The remote clone was created and updated by `RemotePlayerRenderer`, which previously applied inventory + limb visuals but never wrote the body-level face latches | `RemotePlayerRenderer.OnCloneSnapshotUpdated` / `Update` |
| 5 | The limb-presentation cycle recorded the residual explicitly | `docs/selfchecks/limb-presentation-selfcheck.md` accepted residuals |

## 2. Design

- **Wire**: `CharacterHealthMsg` gains `DisfiguredIndex` (int),
  `DisfiguredTimeFullSkin` (float) and `EyeTimeHealed` (float)
  (ProtoMember 65-67), so a remote clone can choose the same disfigurement
  head sprite and the same healed-eye/healed-head presentation as the owner.
- **Capture**: `CloneFacePresentation.Capture(body, health)` reads the three
  `FacialExpression` component fields and writes them into the health message.
  `CharacterDataSync.CaptureCharacterData` and `CaptureLimbStateEvent` call it
  after the normal `Body → CharacterHealthMsg` Mapster map.
- **Apply**: `CloneFacePresentation.Apply(clone, health)` writes the three Body
  booleans onto the clone and the three face component fields onto its
  `FacialExpression`. `RemotePlayerRenderer` calls it on every clone creation
  and every snapshot update, alongside inventory and limb rendering.
- **No new NetMsg / no new domain**: the existing 1 Hz character stream is the
  self-healing carrier; the dedicated `LimbStateEventMsg` carries the same
  fields because it reuses `CharacterHealthMsg`.
- **Let the game's own visual code run**: the clone's `FacialExpression.Update`
  is not patched; it consumes the written latches and continues to produce the
  eye/head sprites from the game's own formulas.
- **ProtocolVersion 31→32** because the wire shape changed.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| 1 Hz character capture | face fields added to `CharacterHealthMsg` before broadcast/report | `CharacterDataSync.CaptureCharacterData` → `CloneFacePresentation.Capture` |
| Limb-state event capture | same face fields ride the dedicated limb-latch event | `CharacterDataSync.CaptureLimbStateEvent` → `CloneFacePresentation.Capture` |
| Clone create | face latches applied immediately from the existing snapshot | `RemotePlayerRenderer.Update` creator branch |
| Clone snapshot update | face latches refreshed on every owner snapshot | `RemotePlayerRenderer.OnCloneSnapshotUpdated` |
| Body latch writes | `Disfigured` / `EyeGone` / `BothEyesGone` written to the proxy Body | `CloneFacePresentation.Apply` |
| Face component writes | `disfiguredIndex` / `disfiguredTimeFullSkin` / `eyeTimeHealed` written to the clone's `FacialExpression` | `CloneFacePresentation.Apply` |
| Out-of-range robustness | index clamped to the clone's `disfiguredHead` array length | `CloneFacePresentation.Apply` |
| Wire compatibility | v31 peers cannot render the new fields → reject mixed-version session | `ProtocolVersion.cs` Current = 32 |

## 4. Verification

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx --no-build` — **1151 passed / 0 failed**.
- New tests:
  - `NetPacketTests.CharacterHealth_FaceLatchPresentation_RoundTrips`
  - `CharacterDataFileStoreTests` full-field round-trip assertions
  - `CloneFacePresentationTests` (reflective Capture/Apply surface + static state-free helper)
- Gates: architecture / event-replay / entity-event-dispatch / format run clean.
- Runtime evidence: development-period rule — L0 simulation + static evidence +
  real-game-dir deploy; **no manual acceptance** (user mandate).
- Static evidence: character snapshot pipeline call sites and the game's
  `FacialExpression` field list cited above.

## 5. What was NOT changed (and why)

- No new `NetMsg` — the 1 Hz character snapshot and the existing limb-latch
  event already carry `CharacterHealthMsg`; adding a third face channel would
  duplicate state and self-healing.
- No patch on `FacialExpression.Update` — the clone's native visual code is the
  intended consumer; the fix is writing the inputs, not replacing the game's
  face sprite logic.
- No change to the limb wound pipeline (`CloneLimbRenderer`) — disfigurement is
  body-level face presentation, so it lives in a dedicated state-free helper
  and is called from the same clone-update points.
- No anti-cheat, validation or gameplay authority change — this is
  presentation-only.
