# Building-entity red HitFlash sync — self-check

The native `Body.Attack` melee branch spawns a red `HitFlash` on the attacker's
side when it hits a `BuildingEntity` (Body.cs:1948-1951). The existing
`BuildingEntityDamaged` star relay already replicated the entity health change
and the entity's own `hitSound`, but it carried no hit-flash presentation
signal, so every non-attacker view missed the red flash. This cycle closes that
presentation gap without re-running damage or adding a snapshot path.

## Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native red flash source | `Body.Attack` raycast branch calls `WorldGeneration.world.CreateHitFlash(sprite, entity.position, entity.rotation, Color.red, entity.transform)` after `buildingEntity.health -=` (Body.cs:1948-1951). |
| 2 | Native flash factory | `WorldGeneration.CreateHitFlash` instantiates `Special/HitFlash`, parents it to the entity when provided, and sets the sprite + `HitFlash.clr` (WorldGeneration.cs:292-302). |
| 3 | Existing damage relay | `BodyPatches.BodyAttackPatch` reports a health diff through `BuildingEntityDamaged`; the receiver applies the health and replays the entity `hitSound` (BodyPatches.cs, WorldBuildingEntitySync.OnRemoteBuildingEntityDamaged). |
| 4 | Wrong/incomplete remote view | The relay knew nothing about the red flash; a remote/third-party view saw the entity take the hit (and heard the sound) without the hit presentation. |

## Change

- **Protocol**: `BuildingEntityDamagedMsg` gains `PlayHitFlash` (protobuf member
  4, default false). `ProtocolVersion.Current` is bumped 7 → 8 because this is
  a behavioral wire extension and mixed-version sessions are rejected by the
  handshake.
- **Source side**: `BodyPatches.BodyAttackPatch` passes `playHitFlash: true`
  when a melee attack damaged a building entity. Explosion damage, silent
  cactus self-damage and item-vs-enemy damage keep the default false; those
  sources never spawned the red `HitFlash` locally.
- **Relay**: `WorldStateMessageService` / `WorldService` / handler carry the
  flag through the existing star channel unchanged.
- **Receiver side**: `WorldBuildingEntitySync.OnRemoteBuildingEntityDamaged`
  calls the new `ReplayHitFlash` helper after applying attack damage. The
  helper uses the same `WorldGeneration.CreateHitFlash` entry with the local
  deterministic copy's sprite/position/rotation and `Color.red`, so the
  non-attacker sees the same native one-shot. It is presentation-only: it does
  not mutate entity health, authority, drops, or any CUO state.
- **Coverage**: Host → guest, guest → host and third-party views all use the
  same remote-apply path, so all non-attacker directions receive the flag.

## Tests / verification

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build --no-restore` | 2291 passed / 0 failed |
| `WorldEventRelayTests.BuildingDamaged_HitFlashFlag_RidesThroughRelay` | flag survives guest → host → other-guest star relay |
| `BuildingEntityHitFlashReplayTests` | reflection locks the replay helper and the relay signature |
| `tools/check-architecture.ps1` / event/entity gates | passed (architecture, 33-event replay, 33 kinds x 3 entity dispatch) |
| `tools/check-delivery.ps1` | passed |
| Deployed artifact identity | `tools/deploy.ps1` deployed to the real game directory; SHA-256 and timestamps of Plugin/GameAdapter/Runtime/Protocol DLLs match the build output |
| Manual dual-client acceptance | not used in the developer cycle; real visual verification remains for the user's unified acceptance pass |

## Structure review

- No new message id or direction row: the existing `BuildingEntityDamaged`
  channel already owns building-entity damage + presentation sound, and the
  red flash is the same one-shot presentation family.
- `ReplayHitFlash` is a small single-concern helper in `WorldBuildingEntitySync`;
  it starts no new state, no new service, no new cross-call business state.
- The walker/explosion/cactus item call sites are unchanged except for leaving
  the new flag at its default, so no unrelated presentation is invented.
