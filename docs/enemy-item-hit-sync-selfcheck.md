# Item-vs-Enemy Hit Sync — Self-Check Table

Delivery-cycle fact sheet for the item-vs-enemy damage path. The native
`SpiderHandler.OnCollisionEnter2D` item branch (SpiderHandler.cs:246-258) only
runs when the enemy is within 50 units of the **local** body — single-player
scoping that breaks when a guest throws an item far from the host's own body.
This cycle generalizes that proximity guard to the in-world player set and
relays every reportable item hit through the existing `BuildingEntityDamaged`
event, so the host-authoritative enemy health, death/drop and `RemoteEntityDeath`
semantics stay identical to every other player-vs-entity hit. Verification is
L0/contract/gates, not manual dual-open acceptance.

## Mechanism × change × evidence

| # | Mechanism | Change | Evidence (file:line / test) |
|---|-----------|--------|------------------------------|
| 1 | Native item-hit formula | Extract the exact damage/stun formulas (`speed × clamp(mass, 0, 4)`, health `× 0.66`, stun `× 1.5`) into a pure, engine-agnostic machine | `SpiderHandler.cs:249/254/256`; `EnemyItemHitArbitration.ComputeImpactWeight/ComputeHealthDamage/ComputeStunDamage` |
| 2 | Native proximity guard | Generalize `distance to local body < 50` to "any in-world player within 50" using the host's entity-stream candidate set | `SpiderHandler.cs:247`; `EnemyItemHitArbitration.AnyPlayerWithin`; `EnemyCombatDirector.BuildCandidates` |
| 3 | Host-side fallback | When the original skipped the item branch (local body far), apply the same native effects on the host authority: health, `AnimalHit` stun, sounds, item bounce | `EnemyCombatDirector.OnEnemyItemCollision` + `ApplyNativeItemBranch` |
| 4 | Remote relay | Every reportable item hit now rides the existing `BuildingEntityDamaged` message so guests get the same health/remote-death/drop semantics as a melee or explosion hit | `GameAdapter.Enemy.cs` → `_worldEventSync.OnBuildingEntityDamaged`; `WorldEventSync.BuildingEntities.cs:24-35/48-74` |
| 5 | Unreliable-state boundary | The decaying `stunTime` remains a presentation flag on the enemy state stream (`EnemyStateMsg.FlagStunned`), not a new wire event; the guest never writes the native timer | `EnemyItemHitPatch.cs`; `EnemyStunPresentation`; `EnemySyncCoordinator.Apply` |
| 6 | Patch surface | New postfix is added to `SpiderHandler.OnCollisionEnter2D` alongside the existing guest-freeze prefix | `EnemyItemHitPatch.cs`; `PatchContractTests.EnemyCombatPatchSet_IsComplete` |
| 7 | Reflection guards | The private `threatWorkaround` toggle used by the fallback is declared as an exact `bool` field contract | `GameFieldContractTests` row; `EnemyCombatDirector.ThreatWorkaroundField` |
| 8 | Pure L0 tests | Damage formulas, mass clamping, impact-speed gate and multiplayer proximity are covered without Unity | `EnemyItemHitArbitrationTests` (9 facts/theory) |

## Explicitly in scope / out of scope

- In scope: every `SpiderHandler`-family animal item hit on the host, whether
  thrown by the host near its own body (native already applied — relay only) or
  thrown by a remote guest far from the host (native skipped — fallback applies
  local effects + relay).
- Out of scope: `CrystalEnemy` item damage — the game has no native
  item-collision damage branch for it; its `AnimalHit` is a no-op
  (CrystalEnemy.cs:115-117). The stun presentation for frozen copies remains a
  snapshot presentation flag by design.

## Verification design

- **L0 pure rules**: `EnemyItemHitArbitrationTests` lock the native formulas,
  mass clamp, speed gate and the any-player-in-radius generalization.
- **Patch contract**: `EnemyCombatPatchSet_IsComplete` now requires two
  `SpiderHandler.OnCollisionEnter2D` patch classes (freeze + item-hit).
- **Field contract**: `SpiderHandler.threatWorkaround` is declared as an exact
  `bool` field so a rename/retype fails before the game launches.
- **Gates**: `dotnet build` 0 warnings/errors; `dotnet test` **1105/1105 green**;
  `dotnet format` clean; `check-architecture`, `check-event-replay` and
  `check-entity-event-dispatch` all pass.

## Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it, then write the result back into `docs/backlog.md`
("由你来自主挑选一个并完成，记得在完成之后回写 backlog"). That instruction is
the plan approval for this cycle; no further interactive approval is required.

## Verification results (2026-08-22)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1105 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| Protocol version | unchanged (no protocol bump) |
| Manual dual-open acceptance | not used (development-period L0/static-evidence rule) |
