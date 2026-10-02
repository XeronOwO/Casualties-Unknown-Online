# The runtime-spawn backfill ships world-generated traders as runtime spawns

- Status: Todo
- Priority: Low-Medium
- Category: Entity sync / enemy runtime-spawn backfill
- Source: agent acceptance batch `20261002-e` (2026-10-02) — each sandboxed client's rolling log carried
  176 `ArgumentException: The Object you want to instantiate is null` lines at world entry while the
  host's log carried none; scope page `docs/evidence/acceptance/20261002-e-scope.md`
- Related: `review/runtime-entity-spawn-backfill.md`, `review/enemy-snapshot-binding-recovery.md`,
  `todo/sandbox-client-null-reference-bursts.md`

## Problem

Both sandboxed clients log 176 contained materialization failures at world entry (the host logs none):

```text
[WRN] [Enemy] cannot create trader at (-280.0,-61.0) — Utils.Create threw (missing prefab or template).
System.ArgumentException: The Object you want to instantiate is null.
  at UnityEngine.Object.Instantiate (…)
  at (wrapper dynamic-method) Utils.DMD<Utils::Create>(string,UnityEngine.Vector2,single)
  at CasualtiesUnknownOnline.GameAdapter.World.RuntimeEntityFactory.TryCreate (…)
```

The `{Id}` in that line is the KIND WORD `trader`, not a Resources prefab name. The host's runtime-spawn
snapshot takes `PrefabId = entity.id` (`src/CasualtiesUnknownOnline.GameAdapter/Character/EnemySyncCoordinator.cs`
— "PrefabId = entity.id") for every entity its registry marks `RuntimeSpawned` with a non-empty prefab id
(`src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EnemySyncService.cs` —
".Where(e => e.RuntimeSpawned && e.PrefabId.Length > 0)"), and the world's generated traders are among
them (they appear after the generation snapshot, so the host classifies them as runtime spawns).

## Impact

- The peers' traders are NOT lost. The same positions receive the host's authoritative state by position
  key: the guest's log carries `[Trade] state received trader=(-280.0,-61.0) … items=14` for a position
  the materialization attempt failed on (1276 such lines in the one session). The attempt is redundant.
- What it costs today: 176 contained throws plus warnings per sandboxed client per world, per session —
  and this is the shape that made `todo/sandbox-client-null-reference-bursts.md` read as an instantiating
  failure of the item path. The two are different mechanisms.

## Root cause chain (this run's evidence)

1. The host classifies world-generated traders as runtime spawns and ships them with the kind word as the
   prefab id.
2. `EnemyRuntimeSpawnArbitration.MatchRuntimeSpawnsByIdentity` does not pair them with the peer's own
   copies, so they arrive as unmatched — `EnemySyncCoordinator.RuntimeSpawns.cs` calls
   `CreateRuntimeSpawn` only for `unmatchedSpawnIndices`.
3. `RuntimeEntityFactory.TryCreate("trader", …)` → `Utils.Create` → `Resources.Load("trader")` returns
   null → the contained warning plus the orphan check.

## Direction

- Stop shipping an entity whose id is not a materializable prefab as a runtime spawn (host-side
  classification), or let the arbitration pair a world-generated kind with the peer's own copy by kind
  and position. `RuntimeEntityFactory`'s warning stays as the genuine-missing-prefab signal — this is not
  a log-level question.
- Either fix starts from a census of the peer's own traders and positions before and after the backfill,
  not from the log alone.

## Limits

- One session's logs (batch `20261002-e`); the 176 count is that run's, not a contract. The host's zero is
  structural — it never backfills its own world.
- The peers were not censused for their trader population in that run: the "redundant" verdict rests on
  the trade-state lines at the same positions.
