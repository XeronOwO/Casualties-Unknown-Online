# Wall-slide + landing presentation sync — self-check (2026-08-23)

The animation audit noted two rows open on the player body:
`Body.HandleGroundedState`'s wall-slide presentation (`Body.cs:2610-2632`,
`Body.cs:3274-3321`) and landing presentation (`Body.cs:2713-2740`) were not
replayed on remote clones. This closes both with a continuous wall-slide state
on the existing 20 Hz player entity stream and a dedicated landing-visual
event.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Wall-slide source flags | `Body.HandleGroundedState` sets private `slidingLeft` / `slidingRight` from `moveDir.x` + side raycasts (`Body.cs:2600-2601`); `HandleVisuals` reads them to play `Wall` and set `wallSide` params (`Body.cs:3274-3321`). |
| Wall-slide particle/audio | `Body.HandleGroundedState` starts `wallSlideParticle` and the private `slideSource` while falling against a wall (`Body.cs:2610-2632`). |
| Landing pose/dust | On a became-grounded edge, the native code plays the `Grounded` body clip and spawns `DustSmall`/`DustBig` by fall speed (`Body.cs:2713-2725`); the landing impact sounds already ride `CharacterSoundMsg`. |
| Existing player state stream | `EntityStateMsg` already carries 20 Hz pose facts; `RunCoordinator.PublishBodyState` is the local-body source and `SessionStatePump` applies them to render clones. |
| Existing one-shot event pattern | `CharacterSoundMsg` / `CharacterAttackAnimMsg` already use the star report → host fire → relay (source excluded) pattern; this landing visual follows the same shape. |

## 2. Changes

- **Wall-slide wire** — `PlayerEntity.SlidingLeft` / `SlidingRight` ride
  `EntityStateMsg.ExtendedFlags` bits `0x02` / `0x04`; `ProtocolVersion` 43 → 44.
- **Wall-slide capture** — `RunCoordinator.PublishBodyState` reads the private
  body flags via Harmony traverse and passes them to `IEntitySyncControl.PublishLocalState`;
  `EntitySyncService` stores them on the local entity.
- **Wall-slide replay** — `SessionStatePump` caches the flags on
  `RemoteBodyDriver`; `BodyUpdatePatch` re-asserts the private `Body.sliding*`
  fields before `HandleVisuals` and `WallSlidePresentation` mirrors the
  continuous particle/audio latch using the clone's synced grounded/velocity
  facts.
- **Landing wire** — new `CharacterLandingVisualMsg` (NetMsg 114) carries
  `OwnerSteamId`, `CloudSize` (0/1/2), the cloud anchor and horizontal
  emitter velocity; a dedicated `CharacterLandingVisualHandler` gives it the
  same bidirectional star semantics as the other one-shot character events.
- **Landing capture** — `BodyHandleGroundedStatePatch` now holds a
  `LandingState` (impact scope, previous grounded, local-body verdict); on a
  verified local became-grounded edge it computes the same cloud threshold as
  the native branch and reports even a soft landing (`CloudNone`) so peers
  replay the `Grounded` clip.
- **Landing replay** — `CharacterLandingVisualSync` plays the `Grounded` clip
  on the owner's clone and calls `Body.CreateCloudSmall/Big` with the reported
  position/velocity; a clone-creation race falls back to instantiating the dust
  prefab at the reported anchor. Replay runs inside a `RemoteApply` scope so
  capture patches cannot echo it.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1346 passed |
| `CharacterLandingVisualSyncTests` | 4 passed (roundtrip + guest/host/relay star semantics) |
| `WallSlideLandingSyncTests` | 4 passed (driver/presentation/patch surface + report shape) |
| `EntityStateRoundtripTests` sliding-flag cases | 4 passed |
| `DirectionTests` | all NetMsg classified, new landing message bidirectional |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` / `tools/check-entity-event-dispatch.ps1` | passed (no EntityEventKind touched) |
| `dotnet format` | run; source/checklist files formatted (generated `obj/MyPluginInfo.cs` is not part of the commit) |
| Deploy | `tools/deploy.ps1` to the real game directory succeeded |
| Manual acceptance | Not required by the developer-cycle rule; L0 + static evidence, no manual acceptance. |

## 4. L0 proof

- `CharacterLandingVisualSyncTests` proves the protobuf payload round-trips
  and the star relay fires the `CharacterLandingVisualReceived` event on the
  host and the other guest.
- `WallSlideLandingSyncTests` locks the `RemoteBodyDriver` flag fields, the
  `WallSlidePresentation.Apply` / `UpdateEffects` signatures, the landing
  patch's `LandingState`/postfix shape and the `CharacterLandingVisualSync.Report`
  surface.
- `EntityStateRoundtripTests` proves the sliding flags survive
  `PlayerEntity` → `EntityStateMsg` → `ApplyTo` in both directions and are
  published to the correct extended-flag bits.
- The existing `PatchContractTests` auto-verify the changed
  `Body.HandleGroundedState` patch contract against the game assembly.

## 5. Structure review

- `WallSlidePresentation` is a focused pure-side helper (cached reflection +
  display-only effects), no cross-call business state.
- `CharacterLandingVisualSync` follows the existing one-shot sync shape and
  holds no state.
- `BodyHandleGroundedStatePatch` remains a thin adapter; its `LandingState` is
  a private per-call capture container.
- `RemoteBodyDriver` gained only two presentation booleans already owned by the
  render-proxy driver.
- `RunCoordinator` stays under the 600-line gate; the new lines remain in its
  existing state-shuttle responsibility.
- No dead mechanism is left co-existing; landing sound remains on
  `CharacterSoundMsg`, landing visual is one dedicated event.

## 6. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it ("由你来自主挑选一个并完成"), so this cycle's plan is approved
without a separate interactive approval step.
