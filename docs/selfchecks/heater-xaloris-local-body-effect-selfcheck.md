# Heater temperature field on Xaloris — local-body-effect exclusion self-check

Owner cycle: autonomous backlog selection; the user asked for native
game-content mechanism priority. The only remaining recorded native-content
gap was the `Heater` temperature field on the `xaloris` prefab (low priority).
Decision: **close it as excluded by design**, not as a new sync surface.

## Conclusion

`Heater.OnWillRenderObject` (Heater.cs:10-21) is a local-body effect: it reads
`PlayerCamera.main.body` (the local player) and writes only that body's
`temperature`. The same component on the frozen guest-side `xaloris` copy
continues to affect the guest's own body, which is the correct local-compute
behaviour — each player's body is simulated on their own side. The resulting
temperature is already part of the 1 Hz character stream
(`CharacterHealthMsg.Temperature`, mapped from `Body.temperature` by
`CharacterDataMapper`), so remote peers already receive/display it through the
player-state path. There is no enemy-local visual or state the remote clone
must mirror; therefore no `EnemyState` field, no new `NetMsg`, and no
ProtocolVersion change is warranted.

## Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | `Heater.OnWillRenderObject` | `Heater.cs:10-21`: every 0.5 s, if the local `PlayerCamera.main.body` is within `maxDistance`, lerps the **local body** temperature toward `desiredTemp` |
| 2 | `Heater` on `xaloris` | `docs/enemy-sync.md` runtime-verified prefab mapping: `xaloris` carries `XalorisScript` + `Heater`; the guest copy is frozen only for AI/physics (EnemyPatches skips `Update`/`FixedUpdate`), not for local-body field components |
| 3 | Local-body family precedent | `CrystalTemperature`, `RadioactiveObject`, `Climbable`, etc. are already `excluded` / `local body` in `entity-features-matrix.csv` |
| 4 | Player temperature wire path | `CharacterDataMsg.Health` carries `CharacterHealthMsg.Temperature`; `CharacterDataSync.CaptureCharacterData` maps `Body` → `CharacterHealthMsg` via Mapster, and the 1 Hz stream ships it to peers |
| 5 | No remote presentation | `Heater` has no renderer/sprite/light write; it only mutates a local body field, so a remote clone has nothing to display |

## Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Xaloris Heater temperature field | No new sync; classified `excluded` (local body effect) | `Heater.cs:10-21`; `entity-features-matrix.csv` Heater row path updated |
| Local player receives the field on both sides | Unchanged; the guest's frozen Xaloris keeps its `Heater` component active | EnemyPatches only skips AI/physics; freeze marker does not disable components |
| Remote peers see the resulting body temperature | Already covered by the 1 Hz character stream | `CharacterHealthMsg.Temperature` + `CharacterDataSync.CaptureCharacterData` |
| Cooker branch of `Heater` | Unchanged, remains `covered` by `ItemCook` (NetMsg 92) | Existing `ItemCookSimulationTests` / `HeaterCookPatchTests` |
| Backlog / docs | Heater residual removed from open known-gap list; recorded as excluded by design | `../backlog.md`, `../entity-features.md`, `../enemy-sync.md`, `../tech-decisions.md` #44 |

## Verification

- **Docs consistency**: `EntityFeaturesDocConsistencyTests` verifies the matrix
  and narrative carry the same `covered` / path value for `Heater`.
- **Static evidence**: decompiled `Heater.cs:10-21` (local body target);
  `CharacterDataMapper` flexible name matching maps `Body.temperature` into
  `CharacterHealthMsg.Temperature`; enemy freeze patches do not touch
  `OnWillRenderObject`.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
