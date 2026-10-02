# The host classifies world-generated enemies as runtime spawns

- Status: Todo — Rejected (batch `20261002-f` row 1: the in-session repair re-pairs already-bound copies against host anchors and fails once an animal moved — `generation spawn pairing failed` + `mapping=False` on 4/4 repair cycles)
- Priority: Medium
- Category: Entity sync / enemy runtime-spawn classification
- Source: agent acceptance batch `20261002-e` (2026-10-02) — both sandboxed clients logged 176 contained
  materialization failures at world entry while the host logged none; the batch's rolling logs show the
  classification damage behind that warning (recorded in the scope page's post-hoc reading,
  `docs/evidence/acceptance/20261002-e-scope.md`)
- Related: `review/runtime-entity-spawn-backfill.md`, `review/enemy-snapshot-binding-recovery.md`,
  `todo/sandbox-client-null-reference-bursts.md`

## Problem

Both sandboxed clients logged 176 contained materialization failures at world entry (the host logged none):

```text
[WRN] [Enemy] cannot create trader at (-280.0,-61.0) — Utils.Create threw (missing prefab or template).
System.ArgumentException: The Object you want to instantiate is null.
  at UnityEngine.Object.Instantiate (…)
  at (wrapper dynamic-method) Utils.DMD<Utils::Create>(string,UnityEngine.Vector2,single)
  at CasualtiesUnknownOnline.GameAdapter.World.RuntimeEntityFactory.TryCreate (…)
```

The `{Id}` is the KIND WORD `trader`, not a Resources prefab name: the game instantiates its traders from
`Resources.Load("trader" + Random.Range(1, 4))` (`WorldGeneration.cs`, the lifepod branch) while
`BuildingEntity.id` carries `trader`. The warning is the visible tip: the host's "runtime spawn"
classification was a timing proxy ("this enemy appeared after my first capture"), and the host's first
capture runs on its first frame with the session active — before world generation has distributed the
enemies — so the proxy classified the whole generation set as runtime spawns.

## Impact (the batch `20261002-e` rolling logs, all three clients; recorded in the scope page's post-hoc reading)

- The host bound **80 runtime spawns**: 69 `shadecrawler` (a generation-distributed enemy) + 11 `trader` —
  every one of them an enemy both sides' world generation had produced.
- The guest's generated pairing failed on **every one of the 16 snapshots**:
  `[Enemy] generation spawn pairing failed (0 host vs 80 guest generated enemies)` and
  `[Enemy] snapshot applied: 0 generated bound, 80 runtime spawns, mapping=False`. `0 generated bound` means
  no generated enemy was ever bound: the guest's own 80 copies stayed frozen and undriven for the session.
- The guest then materialized the 69 `shadecrawler` facts — a SECOND copy of an enemy it already had
  (69 duplicates) — and failed the 11 `trader` facts, one contained failure per fact per snapshot
  (11 × 16 = 176 per client).
- The traders themselves were not lost: the trade domain applies by position and neither sandbox log
  carries a single `[Trade] apply: trader not found` line, so the failed materialization was pure waste on
  top of the duplicates.
- The host's log shows zero `[EntitySpawn] reporting` lines in the session: not one of the 80 ever entered
  the runtime-creation channel. They are generation output, misclassified.

## Root cause

The two sides used two different definitions of "runtime spawn":

- Guest (`EnemySyncCoordinator.OnAnimalInstantiated`): the animal's `BuildingEntity.Start` ran with the
  session active and world generation finished — the same guard the entity-creation channel uses to decide
  whether an instantiation is a runtime creation at all.
- Host (`EnsureMapping`): the animal appeared after the host's first capture.

The host's baseline is established on its first capture, which is unrelated to the generation boundary (in
batch `20261002-e` it ran before any enemy existed). Two definitions with different scopes cannot agree;
here they disagreed about all 80 enemies.

## Rejected alternatives

- **Pair the runtime fact with the peer's generated copy by kind and position.** That is the markerless
  positional absorption `review/runtime-entity-markerless-bind-absorption.md` deleted: it binds a record to
  a copy that never carried its identity.
- **Classify by the `RuntimeEntityCreation` marker instead of the event.** A tutorial prop and an entity
  with an empty `BuildingEntity.id` never enter the creation channel, so host and guest would classify
  those differently again — the exact failure shape this change removes.

## Landed (2026-10-02)

- **One classification rule, applied at one event.** `OnAnimalInstantiated` — the patch bridge that fires
  at an animal's `Start` — now records a runtime-created animal in `_runtimeAnimals` for BOTH roles through
  the same guard (`session active && !IsGenerating()`). The host reads that set when it builds the backfill
  facts, so a host fact can only exist for an animal whose `Start` ran outside generation; the guest's
  pairing candidates come from the same set. `_runtimeEnemyIds`, the bind-time memo of the old timing
  proxy, is deleted, and `Bind` no longer takes a runtime flag. The rule itself is the pure
  `EnemyRuntimeSpawnArbitration.IsRuntimeSpawn` (session active and generation finished), pinned by
  `EnemyRuntimeSpawnArbitrationTests.IsRuntimeSpawn_OnlyDuringPlayInASession`.
- **The host capture half is extracted.** `EnemyStateCapture` owns the per-frame native-to-DTO read and the
  bind-time spawn anchors — the extraction the architecture watchlist named as next for this class, done
  first because the watchlist lists the coordinator near the 600-line gate.
- **Wire unchanged.** The classification is host-side; the fact predicate
  (`RuntimeSpawned && PrefabId.Length > 0`) and every field it reads are untouched, and no protocol version
  is involved.

## Rejection — batch `20261002-f` (2026-10-02)

The batch's three-client session (record:
`docs/evidence/acceptance/enemy-runtime-spawn-classification-20261002-f.md`) confirmed the fix's
classification half and failed row 1's `zero generation spawn pairing failed` clause:

- The entry edge passes: the host bound all 85 generation animals as `host bound generation animal`
  (72 `shadecrawler` + 13 `trader`) with zero runtime spawns, and both guests logged
  `85 generated bound, 0 runtime spawns, mapping=True` with no pairing failure.
- Every in-session repair snapshot (every 60 s) then fails on BOTH guests:
  `generation spawn pairing failed (85 host vs 85 guest generated enemies)` +
  `snapshot applied: 0 generated bound, 1 runtime spawns, mapping=False` — 4/4 cycles in the run.
- Cause: `OnEnemySnapshotReceived` re-pairs the generated baseline through
  `EnemySpawnArbitration.TryPair` — the host's bind-time anchors against the guest's CURRENT positions,
  index-by-index and all-or-nothing inside `PairTolerance` 0.5 — so once the drive has moved a bound
  copy the whole set fails and `_mappingEstablished` is cleared. Already-bound copies already carry
  host ids; the repair must not re-pair them.
- Rows 2-5 passed in the same run (evidence pointers in the record).

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Three clients in one generated world, world entry | The host logs no runtime spawn for a generation enemy; each guest logs `mapping=True` with its generated set bound; zero `generation spawn pairing failed` |
| 2 | Enemy census on every client | Each peer holds one copy per host id (no duplicate materialization), and the per-prefab counts match the host's |
| 3 | Trader census before/after the entry backfill | Same trader count and positions on the host and both peers; zero `cannot create trader` |
| 4 | A genuine runtime creation (an animal instantiated outside generation) | Recorded as a runtime spawn, shipped, and bound/materialized exactly once on each peer |
| 5 | Regression half | Build, full suite, format and the normative gates pass on the deployed commit |

## Limits

- The pre-fix evidence is batch `20261002-e`'s rolling logs, recorded in the scope page's post-hoc reading
  (`docs/evidence/acceptance/20261002-e-scope.md`); the fix's own evidence must come from the batch that
  judges the matrix above.
- The adapter shell (Unity scene, `transform`/`GetComponent`) is not exercisable in the test host, so the
  rule's truth table is pinned by `EnemyRuntimeSpawnArbitrationTests` while the WIRING (which event records,
  which set the capture reads) rests on code review plus the dual-client acceptance rows — the same boundary
  the rest of the enemy domain records.
- `_runtimeAnimals` is the session's record of runtime-created animals: the host prunes destroyed entries on
  each capture, the guest drops an explicitly removed enemy, and `Unbind` clears the set. It is not a
  durable table and never leaves the session.
- A generation entity whose `Start` ran during generation now stays in the generated baseline even when the
  host captures it late; a peer that lacks that entity is reported by the existing pairing warning instead
  of receiving a materialized duplicate.
