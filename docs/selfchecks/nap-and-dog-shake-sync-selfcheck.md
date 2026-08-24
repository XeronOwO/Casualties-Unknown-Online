# Nap variant + dog-shake intensity sync — self-check (2026-08-23)

The animation audit noted that the normal lay-down pose is synced, but
`Body.TakeANap`'s sick/alt branch (`Body.cs:2519-2531`) and the continuous
water-shake intensity (`Body.cs:2550-2571`) were not. This closes both
presentation gaps through the existing 20 Hz player entity stream.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| The game's nap selection | `Body.TakeANap` branches into `NapCoroutine` or `AltNapCoroutine` from `sicknessAmount > 30 || totalHappiness < -50 || temperature < 34.5 || temperature > 38.5` (`Body.cs:2484-2498`). Neither coroutine exposes the chosen variant as a field. |
| The game's dog-shake visual | `Body.dogShakeIntensity` is public (`Body.cs:4233`), starts at the `WaterShake` coroutine (`Body.cs:2550-2571`), and `HandleVisuals` adds it to the bone-offset shake term (`Body.cs:3209`). |
| Existing player state stream | `EntityStateMsg` already carries the 20 Hz pose facts; `RunCoordinator.PublishBodyState` is the local-body source and `SessionStatePump` applies them to render clones. |
| Clone render path | `RemoteBodyDriver` already tracks discrete pose transitions (sitting/sleeping/lying/swing); `BodyPatches.BodyUpdatePatch` runs the render-only `HandleVisuals`, which reads `dogShakeIntensity` on the proxy. |
| Wire compatibility | The player entity stream is additive-protobuf; adding a byte and a float is a versioned entity-state extension (ProtocolVersion 43 so older peers are rejected before mixed-version rendering). |

## 2. Changes

- **Runtime state** — `PlayerEntity.NapVariant` (byte) +
  `PlayerEntity.DogShakeIntensity` (float), round-tripped through
  `EntityStateMsg` (ProtoMember 14/15) via `ApplyTo` / `ToEntityStateMsg`.
- **Local capture** — `BodyNapPatch` adds two Harmony prefixes on the
  `Body.NapCoroutine` and `Body.AltNapCoroutine` iterator methods. They store
  the exact wire variant (0 = standard, 1 = alt) on a `LocalNapTracker`
  component on the local body; the snippets run when the coroutine is started,
  so the capture is the same call-identity trick already used for
  `Body.DoWorkout`.
- **Publisher** — `RunCoordinator.PublishBodyState` sends the tracker's
  variant only while `body.sleeping` is true (forced sleep without a tracker
  stays 0 = standard), and sends `body.dogShakeIntensity` every snapshot.
- **Replay** — `SessionStatePump` plays the matching body+arms lay-down clip
  pair when the sleeping edge or the nap variant changes, and writes the
  synced dog-shake intensity directly onto the render clone each frame so
  `HandleVisuals` shakes it with the owner.
- **Pure rule** — `NapPresentation` owns the variant → clip mapping so the
  visual rule has an L0 test face; the patch stays a thin adapter.
- **No new NetMsg** — both facts ride the existing 20 Hz `PlayerState` /
  `PlayerStateReport` entity stream. `ProtocolVersion`: 42 → 43.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-restore` | 1332 passed |
| `NapAndDogShakeSyncTests` | 8 passed |
| `EntityStateRoundtripTests` additional nap/shake cases | 4 passed |
| `NetPacketTests.EntityState_NapVariantAndDogShake_RoundTrips` | 1 passed |
| `dotnet format CasualtiesUnknownOnline.slnx --no-restore` | passed |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` / `tools/check-entity-event-dispatch.ps1` | no event mechanism touched |
| Deploy | `tools/deploy.ps1` to the real game directory succeeded |
| Manual acceptance | Not required by the developer-cycle rule; L0 + static evidence, no manual acceptance. |

## 4. L0 proof

- `NapAndDogShakeSyncTests.ClipMapping_*` exercises the exact variant → clip
  pair used by `SessionStatePump`, including an unknown fallback to standard.
- `NapAndDogShakeSyncTests.NapPatchPrefixes_*` locks the prefix parameter
  shape; the generic `PatchContractTests` also auto-verifies the two new
  `[HarmonyPatch]` contracts against the game assembly.
- The local tracker and remote-driver field tests lock the small state shapes
  that carry and transition the variant.
- `EntityStateRoundtripTests` and `NetPacketTests` prove the wire fields are
  applied into the entity buffer, published back, encoded and decoded.

## 5. Structure review

- `NapPresentation` is a pure one-concern mapper (no Unity state).
- `LocalNapTracker` is a tiny local-body marker; it is never added to render
  clones.
- `BodyNapPatch` is a thin adapter with no cross-call business state.
- `RunCoordinator` remains under the 600-line gate; the added publishing lines
  stay in its existing state-shuttle responsibility.
- No new wire message, no duplicate periodic channel, no dead mechanism left
  behind.

## 6. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it ("由你来自主挑选一个并完成"), so this cycle's plan is approved
without a separate interactive approval step.
