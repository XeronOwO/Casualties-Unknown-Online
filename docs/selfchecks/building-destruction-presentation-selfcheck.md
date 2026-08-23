# Building-destruction presentation replay (remote side)

Date: 2026-08-18
ProtocolVersion: unchanged (no wire change — pure local presentation)

## Problem

A building entity destroyed by a remote player's attack or open path died on
every side, but the non-attacker side's `BuildingEntityUpdatePatch` destroyed
the entity before `BuildingEntity.Update` could run its death branch. The
remote side therefore missed the destruction visuals/sound that the attacker
saw: `BuildingBreakParticle`, `DustBig`, and the `footstep/Rock/11` rock
sound. This was a recorded presentation gap in `../backlog.md` ("remote
building-destruction particles").

## Change

`BuildingEntityUpdatePatch.Prefix` now calls a new
`ReplayDestructionVisuals(BuildingEntity)` helper before destroying the remote
entity. The helper replays exactly the non-drop part of the native death branch
(`BuildingEntity.cs:58-73`):

- `BuildingBreakParticle` instantiated at the entity's transform, with the
  particle shape's `texture`/`sprite` set from the entity's `SpriteRenderer`
  sprite, then `Play()`.
- `DustBig` instantiated at the entity position.
- `Sound.Play("footstep/Rock/11", ...)` at the entity position.

It deliberately does NOT set `pressed`/timers or start any simulation: this is
presentation only. Drops stay attacker-side (the world-item domain already
materializes them). The animal-specific death presentation is also replayed on
the remote side for live remote deaths — see the later
`animal-death-presentation-selfcheck.md`; the attacker-side experience reward
and drop rolls remain attacker-side.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| `BuildingEntity.Update` death branch (BuildingEntity.cs:56-73) | Remote side replays the non-drop visuals/sound instead of skipping them | Decompiled `BuildingEntity.cs:58-73`; helper copies the same resources/sound calls |
| `RemoteEntityDeath` destroy suppression (`BuildingEntityUpdatePatch.Prefix`) | Still destroys the entity and returns false, so no duplicate drop roll | `BuildingEntityPatches.cs`; `BuildingDestroyReplayPatchTests` locks the prefix shape |
| New remote presentation helper | `ReplayDestructionVisuals` sets particle shape + plays dust/rock sound | `BuildingEntityPatches.cs`; `BuildingDestructionReplayPatchTests` locks the helper surface |
| Drops / AnimalDeath | Drops + experience reward unchanged (attacker-side only); remote side now replays the creature-specific death presentation for live remote deaths | `BuildingEntity.cs:75-121`; `RemoteEntityDeath`; `AnimalDeathReplay.cs`; `docs/selfchecks/animal-death-presentation-selfcheck.md` |

## Why this is safe

- The replay runs in the same `RemoteEntityDeath` branch that previously only
  destroyed the entity, so there is no new duplicate drop roll and no new
  world/entity/item state.
- The created objects (`BuildingBreakParticle`, `DustBig`) are pure effects —
  no `BuildingEntity`, `Item`, or other sync-domain component.
- The resources/sound calls are identical to the game's own death branch, so
  the remote side's observable outcome matches the attacker side.

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- L0 reflection: `BuildingDestructionReplayPatchTests` locks
  `BuildingEntityUpdatePatch.Prefix (BuildingEntity) → bool` and the
  `ReplayDestructionVisuals(BuildingEntity)` helper surface.
- Patch contract: `PatchContractTests`/`PatchInventory` already cover the
  `BuildingEntity.Update` Harmony target and prefix shape.
- Static evidence: `BuildingEntity.cs:58-73` (decompiled), the new helper in
  `BuildingEntityPatches.cs`, `RemoteEntityDeath` marker.
- Development-period rule: L0/static evidence + real-game-dir deploy;
  **no manual acceptance** (user 2026-08-16 mandate).