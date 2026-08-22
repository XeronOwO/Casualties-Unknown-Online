# Enemy Stun Presentation — Self-Check Table

Delivery-cycle fact sheet for the per-enemy stun presentation wiring. The enemy stream already
carried a `Stunned` presentation flag in `EnemyStateMsg`; this cycle made the host actually capture
it and the guest actually consume it. Verification is L0/contract/gates, not manual dual-open
acceptance.

## Mechanism × change × evidence

| # | Mechanism | Change | Evidence (file:line / test) |
|---|-----------|--------|------------------------------|
| 1 | SpiderHandler stun state | Host captures `SpiderHandler.stunTime > 0` as the enemy's `Stunned` presentation flag | `SpiderHandler.cs:40/313`; `EnemyStunPresentation.IsStunned`; `EnemySyncCoordinator.Capture` → `EnemyEntity.Stunned` |
| 2 | CrystalEnemy stuck state | Host captures the private `CrystalEnemy.stuck` latch as the same flag | `CrystalEnemy.cs:240`; `EnemyStunPresentation.CrystalEnemyStunAccess.IsStuck` (Traverse, exact `bool`); `GameFieldContractTests` row |
| 3 | Existing wire flag | The already-defined `EnemyStateMsg.FlagStunned` / `EnemyEntity.ToEnemyStateMsg` / `ApplyTo` path now carries real data | `EnemyStateMsg.cs:16-17/46`; `EnemyEntity.cs:51-59`; `EnemyStateRoundtripTests` |
| 4 | Guest render copy | The guest mirrors the received boolean onto `RemoteEnemyDriver.Stunned`; a transition is logged once | `EnemySyncCoordinator.Apply`; `EnemyStunPresentation.Apply`; `RemoteEnemyDriver.cs` |
| 5 | AI-state boundary | The guest never writes `SpiderHandler.stunTime` or `CrystalEnemy.stuck` — only the presentation boolean travels | `EnemyStunPresentation.Apply` only touches `RemoteEnemyDriver.Stunned` |
| 6 | Reflective surface | New contract tests lock the capture/apply surface and the driver property | `EnemyStunPresentationTests` (2 facts) |

## Explicitly out of scope (recorded, not silent)

- Guest-originated thrown-item/enemy damage is now resolved (2026-08-22): see
  `docs/enemy-item-hit-sync-selfcheck.md`. The stun presentation flag remains the guest-side
  rendering surface; the item hit itself travels through the existing `BuildingEntityDamaged`
  relay (health/drop semantics) plus the enemy state stream's `Stunned` presentation flag.
- There is no dedicated native "stun pose" renderer; the flag is the presentation surface and is
  held on the frozen copy's driver for future presentation consumers.

## Verification design

- **L0 roundtrip**: existing `EnemyStateRoundtripTests` already prove the `FlagStunned` bit
  roundtrips and clears.
- **Reflective contracts**: `EnemyStunPresentationTests` locks `IsStunned(BuildingEntity) → bool`
  and `Apply(BuildingEntity, bool) → bool`, and locks `RemoteEnemyDriver.Stunned` as a read/write
  bool property.
- **Game field contract**: `CrystalEnemy.stuck` is declared as an exact `bool` field so a rename or
  retype fails the test run before the game launches.
- **Gates**: `dotnet build` 0 warnings/errors; `dotnet test` **1095/1095 green**;
  `dotnet format` clean on tracked/untracked source (the only verify-no-changes report is the
  gitignored generated `obj/.../MyPluginInfo.cs`); `check-architecture`, `check-event-replay`
  and `check-entity-event-dispatch` all pass.

## Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it, then write the result back into `docs/backlog.md`
("由你来自主挑选一个并完成，记得在完成之后回写 backlog"). That instruction is
the plan approval for this cycle; no further interactive approval is required.

## Verification results (2026-08-22)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1095 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean on source (verify-no-changes flags only the gitignored generated `obj/.../MyPluginInfo.cs`) |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | deployed to the real game dir only |
