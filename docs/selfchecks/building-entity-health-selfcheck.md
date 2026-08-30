# Building-Entity Health Snapshot — Self-Check (2026-08-16)

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/selfchecks/MANIFEST.md` and
> `docs/architecture-evolution/protocol.md` before citing.

Delivery fact sheet for the damaged building-entity late-joiner snapshot
(ProtocolVersion 11, NetMsg 88).

## Mechanism inventory (complete side-effect table)

| # | Mechanism | Vanilla behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | Player attack damage | `Body.Attack` writes `entity.health -=` directly (Body.cs:1946) | postfix reports `OnBuildingEntityDamaged` with the before/after diff; the report now also records the post-write health in the host registry | BodyPatches.cs:171-216; WorldEventSync.cs:129-139 |
| 2 | Explosion structural damage | `CreateExplosion` writes building health (WorldGeneration.cs:3986-3995) | same report path (`ExplosionBuildingSync` before/after diff) | ExplosionBuildingSync.cs:12-45 |
| 3 | Live relay | `BuildingEntityDamaged` (NetMsg 51) applies by position; death from a relay gets `RemoteEntityDeath` so only the attacker rolls drops | unchanged; host-side apply additionally records the post-apply health | WorldEventSync.cs:147-171 |
| 4 | Open paths | instant-open / lockpick / keypad write health = 0 directly (Openable.cs:12) | local + remote open paths record health 0 in the same registry; the existing `OpenedEntitiesSnapshot` stays unchanged and idempotent with it | WorldEventSync.cs:173-215 |
| 5 | Late joiner | regenerated world has every entity at full health — destroyed plants/crates resurrect, intermediate damage lost | new `BuildingEntityHealthSnapshot` (NetMsg 88) backfills current health on world entry / reconnect / 60 s resend; application writes the host health and marks `< 0.5` as `RemoteEntityDeath` | HandlerContext.cs; WorldEventSync.cs:104-117, 233-271 |
| 6 | Drop rolls | `BuildingEntity.Update` rolls drops with local Random when health < 0.5 | unchanged; snapshot-applied deaths carry `RemoteEntityDeath`, so `BuildingEntityUpdatePatch` only destroys, never rolls | BuildingEntity.cs:50-123; BuildingEntityPatches.cs:22-34 |
| 7 | Lifecycle | building entities die with the world layer | registry resets in `ResetDamagedBlocks` alongside traps/opened/layout | WorldService.cs:460-468 |
| 8 | Protocol | — | NetMsg 88 host→guest; ProtocolVersion 10→11 | NetMsg.cs; ProtocolVersion.cs |

## Design

- `BuildingEntityHealthRegistry` (Runtime/World, host-authoritative):
  position-keyed latest health, cap 4096, empty table sends nothing.
- `BuildingEntityHealthEntryMsg` / `BuildingEntityHealthSnapshotMsg`:
  float X/Y/Health; protobuf zero-omission is semantically transparent for
  floats (health 0 round-trips).
- Snapshot fan-out joins the existing world-state group:
  `HandlerContext.SendWorldStateToMember` + the 60 s resend loop.
- Guest application mirrors the live relay (position lookup → health write →
  `RemoteEntityDeath` on death).

## Verification design

1. L0 (pure): registry latest-wins / reset / empty-no-send / guest no-op
   (`BuildingEntityHealthRegistryTests`); wire round-trip with health 0;
   direction-table row; world-entry and reconnect snapshot groups extended to
   six snapshots. 767 tests green.
2. Runtime (dual-side): host damages a crate partially and destroys a plant;
   a guest joins mid-session and sees the crate at the same health and the
   plant gone, with no duplicate drop roll. Logs:
   `[Building-entity health snapshot applied]` on the guest,
   `[WorldFingerprint]` matching on both sides.
3. Assertion-validity proof: removing the registry report or the snapshot
   group send turns the new tests red; restored to green.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Local damage record | report post-write health | WorldEventSync.cs:129-139 |
| Remote damage record | report post-apply health | WorldEventSync.cs:147-171 |
| Open record | report health 0 | WorldEventSync.cs:173-215 |
| Snapshot source | registry latest-wins + reset + cap | BuildingEntityHealthRegistry.cs |
| World entry + reconnect | `SendWorldStateToMember` includes NetMsg 88 | HandlerContext.cs |
| 60 s self-heal | resend loop includes NetMsg 88 | WorldEventSync.cs:104-117 |
| Guest apply | position lookup + health write + RemoteEntityDeath | WorldEventSync.cs:233-271 |
| Drop suppression | snapshot death never rolls twice | BuildingEntityPatches.cs:22-34 |
| Direction | NetMsg 88 h2g row | PacketReceiver.cs; DirectionTests.cs |
| Wire | health 0 round-trip | NetPacketTests.cs |
| Version gate | ProtocolVersion 11 | ProtocolVersion.cs; HandshakeHandler |
| Structure | touched classes stay under the 600-line gate | tools/check-architecture.ps1 |
