# Runtime-created BuildingEntity spawns have no backfill or re-report

- Status: Todo
- Priority: Medium (High if the airdrop/keypad family is confirmed user-visible)
- Category: Network / sync coverage / world entities
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row E3)
- Related: `todo/enemy-snapshot-and-attack-recovery.md` (the enemy/animal half), `todo/sync-cadence-review.md`

## Problem (evidence)

`EntitySpawned` (NetMsg 68) is the only runtime entity-creation channel and it is
one-shot in both directions:

- Report: `src/CasualtiesUnknownOnline.GameAdapter/World/EntitySpawnSync.cs:349`
  (`_world.SendEntitySpawned(new EntitySpawnedMsg`); host relay at `:229`.
- Wire: `src/CasualtiesUnknownOnline.Runtime/Session/World/EntityEventChannel.cs:202`
  (host broadcast) / `:206` (`_sender.Send(_session.HostSteamId, NetMsg.EntitySpawned, msg);`).
- No periodic re-report and no creation snapshot: the world-entry fan-out
  (`src/CasualtiesUnknownOnline.Runtime/Session/World/WorldEntryFanout.cs:37-44`)
  has no entity-creation member; the kernel `WorldEntityState` holds only
  consumption / health / opened / trap-state facts
  (`src/CasualtiesUnknownOnline.GameState/Domains/WorldEntities/WorldEntityState.cs:11`).
- The 60 s cycle re-sends only block state, block damage, the kernel checkpoint
  and keypad codes
  (`src/CasualtiesUnknownOnline.GameAdapter/World/WorldEventSync.cs:122-135`);
  the keypad/geyser re-sends are position-keyed onto already-existing local
  entities (`src/CasualtiesUnknownOnline.GameAdapter/World/GeyserStateSync.cs:140`),
  so they heal a carried value but cannot materialize a missing entity.
- `EntitySpawnSync` itself assumes the geyser-state cycle covers a lost report
  (`src/CasualtiesUnknownOnline.GameAdapter/World/EntitySpawnSync.cs:299`), which
  only holds for the liquid type, not for the entity.
- A swallowed report therefore leaves a permanently one-sided entity until the
  next layer regeneration / reconnect.

## Goal

A runtime-created `BuildingEntity` (keypad, airdrop crate, mod-spawned structure,
crystal, etc.) materializes exactly once on every peer, survives a swallowed
report and a late join / reconnect, and never duplicates.

## Design direction (decide at implementation)

1. **Creation snapshot in the world-entry group** — the host keeps a table of
   runtime-created entities (id + prefab + position + creation-time payload) and
   sends it absolutely in the ordered world-entry group, plus a periodic
   re-report (piggyback on the existing 60 s cycle).
2. **Re-report on demand** — a peer that is missing an entity asks the host /
   creator for the creation record.
3. **Accepted loss** — rejected: a missing airdrop crate or mod structure is a
   hard gameplay divergence.

Either way the apply must stay idempotent (`EntitySpawnSync.FindExisting`'s
~1 m dedup already provides the primitive) and must not resurrect an entity the
world has since destroyed.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest creates a runtime entity; its report is dropped | Host creates its copy; third-party guests see it |
| 2 | Host creates a runtime entity; the relay is dropped | Guest converges without reconnect |
| 3 | Late joiner enters the world | All runtime entities present exactly once |
| 4 | Reconnect while in world | Same; no duplicates |
| 5 | Creation-time payload (keypad code / geyser liquid type / crystal tint) | Preserved on every path |
| 6 | Entity destroyed before the re-report | Not resurrected |
| 7 | Layer regeneration | Runtime entities from the previous layer are not re-materialized |
| 8 | Duplicate delivery of the same creation record | Exactly one local entity (existing ~1 m dedup) |

## Non-goals

- Enemy / animal runtime spawns (owned by `todo/enemy-snapshot-and-attack-recovery.md`).
- Item-domain spawns (already covered by the item kernel + keyframe).
- Replacing `EntitySpawned` for the live path; the ticket is about recovery.
