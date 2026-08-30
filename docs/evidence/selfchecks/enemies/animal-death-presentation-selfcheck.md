# Animal death presentation replay (remote side)

Date: 2026-08-22
ProtocolVersion: unchanged (no wire change — pure local presentation)

## Problem

`BuildingEntity.Update`'s death branch calls `SendMessage("AnimalDeath")` on
the local creature before destroying it (BuildingEntity.cs:69-72). The remote
side suppresses that branch with `RemoteEntityDeath` so it never rolls a
second set of drops or awards the attacker-side experience, but it also never
saw the creature-specific death presentation:

- `SpiderHandler.AnimalDeath` — `gore` sound + `BloodExplosion` when
  `doDeathExplode` is true (SpiderHandler.cs:284-295); the experience reward
  in the same method must stay attacker-side.
- `CrystalEnemy.AnimalDeath` — `crystalenemydeath` sound +
  `Special/CrystalDistort` death animation (CrystalEnemy.cs:120-124).
- `TraderScript.AnimalDeath` — `gore` sound + `BloodExplosion` at the torso
  (TraderScript.cs:597-601).

This was a remaining native-content presentation gap: a peer watching a remote
player kill a spider/crystal enemy saw only the generic building-break
particles/dust/rock sound.

## Change

- **New `AnimalDeathReplay.Replay(BuildingEntity)`** — presentation-only
  replay for the three known creature families. It intentionally OMITS
  `SpiderHandler.AnimalDeath`'s experience reward (the attacker-side side
  effect), and only instantiates the gore/crystal visuals/sounds.
- **`RemoteEntityDeath.ReplayAnimalDeath`** — the live/snapshot distinction:
  the live damage/open relay marks the death with `ReplayAnimalDeath = true`;
  the world-entry / 60 s health snapshot marks it `false`. A late joiner
  therefore does not hear creature death effects for kills that happened
  before the snapshot.
- **`BuildingEntityUpdatePatch.ReplayDestructionVisuals`** — now runs
  `AnimalDeathReplay.Replay` in the native order (particle → dust → animal
  death → rock sound) before destroying the remote entity, but only when the
  marker says this is a live remote death.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| `BuildingEntity.Update` death branch | Remote side now replays the creature-specific death presentation instead of skipping it | Decompiled `BuildingEntity.cs:69-72`; `AnimalDeathReplay.Replay` |
| `SpiderHandler.AnimalDeath` | Replayed without the local experience reward (sound + `doDeathExplode` visual) | `SpiderHandler.cs:284-295`; `AnimalDeathReplay.cs` |
| `CrystalEnemy.AnimalDeath` | Replayed with the death sound + `Special/CrystalDistort` | `CrystalEnemy.cs:120-124`; `AnimalDeathReplay.cs` |
| `TraderScript.AnimalDeath` | Replayed with `gore` + `BloodExplosion` at the torso | `TraderScript.cs:597-601`; `AnimalDeathReplay.cs` |
| Late joiner | Creature-specific effects are not replayed from a world-entry health snapshot | `RemoteEntityDeath.ReplayAnimalDeath` set true only by the live damage/open relay; false by `ApplyRemoteBuildingEntityHealth` |
| Drops | Unchanged — attacker-side only, world-item domain materializes them | `BuildingEntity.cs:75-121`; `RemoteEntityDeath` |

## Why this is safe

- The replay is presentation only: no `BuildingEntity`, `Item`, world-table or
  character-state mutation is created.
- The attacker-side experience reward remains where the game puts it — the
  attacker's local body only.
- The live/snapshot flag prevents a late joiner from receiving a burst of
  death effects for entities that were already dead before they entered.
- No wire/protocol change: `RemoteEntityDeath` is a local-only marker and the
  existing destruction path already carries the death fact.

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx --no-build` — **1154 passed / 0 failed**
  (3 new `AnimalDeathReplayPatchTests` reflection rows).
- `dotnet format CasualtiesUnknownOnline.slnx` — clean.
- `check-architecture.ps1` / `check-event-replay.ps1` /
  `check-entity-event-dispatch.ps1` — all pass.
- Protocol version: unchanged.
- Development-period rule: L0/static evidence + real-game-dir deploy;
  **no manual acceptance** (user 2026-08-16 mandate).
