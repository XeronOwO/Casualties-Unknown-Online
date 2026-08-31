# Remote Clone Face Vitals — Self-Check

Closes the backlog item **High sleepiness squint not visible remotely**. The
remote clone already ran the game's own `FacialExpression.Update`, but its
`Body` still had template-default vitals because `Body.Update` is skipped on
render proxies. The 1 Hz `CharacterHealthMsg` already carries the face-driving
health fields; this change writes them onto the clone before the game's sprite
selector runs.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | `FacialExpression.Update` selects the eye sprite from `Body` vitals: `consciousness < 70`, `energy < 20`, pain/sickness/radiation/shock/temperature branches, and the blink/time branches | `reversing/Assembly-CSharp/Assembly-CSharp/FacialExpression.cs:27-83` |
| 2 | A render clone's `Body.Update` is skipped (`BodyPatches.BodyUpdatePatch`), so the clone's vitals do not advance on the peer | `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyPatches.cs:35-131` |
| 3 | `FacialExpression.Update` is NOT skipped on render clones — it consumes whatever the clone `Body` fields say | `RemoteBodyFactory.cs` no expression disable; `FacialExpression.cs` |
| 4 | The owner's 1 Hz `CharacterHealthMsg` already carries the relevant fields (`Consciousness`, `Energy`, `BadSleepAmount`, `RadiationSickness`, `Shock`, `Adrenaline`, `SicknessAmount`, `Temperature`, `InternalBleeding`, `BloodPressure`, `Happiness`) | `src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/CharacterHealthMsg.cs` |
| 5 | `CloneFacePresentation.Apply` previously wrote only face latches/component fields; it never wrote the body vitals the same sprite formula reads | `src/CasualtiesUnknownOnline.GameAdapter/Character/CloneFacePresentation.cs` (pre-change) |
| 6 | The clone update call sites already exist: clone creation and every snapshot/event update call `CloneFacePresentation.Apply` | `src/CasualtiesUnknownOnline.GameAdapter/Character/RemotePlayerRenderer.cs:64,132` |

## 2. Design

- **Pure projection**: `FacePresentationVitals` (Runtime, state-free) is the
  exact face-driving subset of `CharacterHealthMsg`. It is L0-locked so the
  field set does not quietly drift from the adapter body writes.
- **Adapter write**: `CloneFacePresentation.Apply` calls
  `FacePresentationVitals.From(health)` and writes the projected values onto the
  render clone's `Body` before the game's `FacialExpression` consumes them.
- **No new wire/protocol change**: all fields already ride `CharacterHealthMsg`
  at 1 Hz; the clone receives them on creation, every snapshot update, and every
  dedicated limb/enemy event that refreshes the fact table.
- **Game visual remains authority**: no patch on `FacialExpression.Update`; the
  fix supplies the missing inputs, not a reimplementation of the sprite rule.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Sleepiness squint | `consciousness` / `energy` / `badSleepAmount` written from the owner's snapshot | `CloneFacePresentation.ApplyVitals` |
| Other face vitals | radiation, shock, adrenaline, sickness, temperature, internal bleeding, blood pressure, happiness written from the same snapshot | `CloneFacePresentation.ApplyVitals` |
| Pure field set | `FacePresentationVitals.From` maps every projected field | `FacePresentationVitals.cs`; `FacePresentationVitalsTests` |
| Adapter contract | `CloneFacePresentation` exposes a static `ApplyVitals(Body, FacePresentationVitals)` | `CloneFacePresentationTests.Apply_UsesPureFaceVitalsProjection` |
| Red→green | regression test failed on pre-fix adapter (`ApplyVitals not found`), passed after fix | recorded in this cycle |
| Clone refresh | existing `RemotePlayerRenderer` call sites apply the new vitals without additional wiring | `RemotePlayerRenderer.cs:64,132` |

## 4. Verification

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx --no-build` — **1844 passed / 0 failed**.
- `dotnet format CasualtiesUnknownOnline.slnx` — run clean.
- `tools/check-architecture.ps1` — passed (including GameState isolation and
  item/command/kernel-shape guards).
- `tools/check-event-replay.ps1` — passed (33 events).
- `tools/check-entity-event-dispatch.ps1` — passed (33 kinds x 3 tables).
- `tools/check-delivery.ps1` — passed (7 boxes checked).
- Development-period rule: L0 + static evidence, no manual dual-client
  acceptance during feature development.

## 5. What was NOT changed (and why)

- No new `NetMsg` / `ProtocolVersion` bump — the vitals already ride
  `CharacterHealthMsg`.
- No full `CharacterHealthMsg → Body` Mapster map on the clone — only the
  face-relevant subset is written; other physiological state remains outside
  the render-proxy domain.
- No patch on `FacialExpression.Update` — the game's own sprite selection is
  the intended visual authority.
- No gameplay/authority change — this is presentation-only.
