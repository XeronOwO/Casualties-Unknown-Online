# Block HP Progressive Sync — Self-Check (2026-08-16)

Delivery fact sheet for the block-HP (block damage crack/HP) sync closeout:
`BlockDamageSnapshot` (NetMsg 89, ProtocolVersion 12) + the `metalMoreDamage`
live-relay correction.

## Mechanism inventory (complete side-effect table)

| # | Mechanism | Vanilla behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | Local block damage | `WorldGeneration.DamageBlock(Vector2, …)` accumulates `BlockDamage.damage`, updates the crack sprite, and breaks + rolls drops at `health` (WorldGeneration.cs:713-849); `bonusMetal && blockInfo.metallic` multiplies the damage by 10 inside the `Vector2Int` overload (:715) | the existing postfix reports the raw damage; it now also carries `bonusMetal` so the peer applies the same multiplier; the host additionally records the post-write `BlockDamage.damage` in the new registry | WorldGenerationDamageBlockPatch.cs; BlockBreakSync.cs |
| 2 | Live damage relay | `BlockDamagedMsg` applies the reported damage on the peer | the message gains `MetalBonus` (ProtoMember 4); every apply path passes it into `DamageBlock` instead of hard-coding `false` (the laser vs metallic block divergence — Item.cs:4645 sets `metalMoreDamage`, WorldGeneration.cs:715 applies ×10); a damage-only report against an already-air cell is ignored instead of creating a transient air `BlockDamage` + hit sound/particles (air health is 0 — WorldGeneration.cs:315-322) | BlockDamagedMsg.cs; WorldService.cs; BlockBreakSync.cs |
| 3 | Local break | damage ≥ health → `SetBlock(0)` + loot roll + `blockDamages.Remove` (:836-842) | the pending-break report now carries the bonus flag too; the registry entry is removed so a broken block never ships as partial damage | BlockBreakSync.cs; BlockBreakPendingState.cs |
| 4 | Late joiner / reconnect | a regenerated world has no `BlockDamage` entries — every partially-mined block is back at full HP (and therefore breaks later, desynchronizing the damage chain) | new `BlockDamageSnapshot` (NetMsg 89) backfills the host's current partial damage on world entry / reconnect / 60 s resend; application sets `BlockDamage.damage` absolutely and refreshes the crack sprite | BlockDamageRegistry.cs; HandlerContext.cs; BlockBreakSync.cs |
| 5 | Earthquake / environment air writes | `WorldGeneration.Update` writes `SetBlock(0)` directly without `BlockDamage` (:895) | any applied air write clears the registry entry for that cell | WorldEventSync.cs |
| 6 | Remote air write | a guest break's `BlockPlaced` applies `SetBlock(0)` on the host under `RemoteApply` | the host branch clears the registry entry for the cell | WorldEventSync.cs |
| 7 | World generation / lifecycle | `ClearBlockDamages` tears the list down with the world | registry resets in `ResetDamagedBlocks` alongside block-state / trap / opened / building-health tables | WorldService.cs |
| 8 | Protocol | — | NetMsg 89 host→guest; `BlockDamagedMsg.MetalBonus`; ProtocolVersion 11→12 | NetMsg.cs; ProtocolVersion.cs |

## Design

- `BlockDamageRegistry` (Runtime/World, host-authoritative): block-cell-keyed
  latest accumulated damage, cap 256 (the game's own `blockDamages` list caps
  at 128 — WorldGeneration.cs:732-737), empty table sends nothing, explicit
  remove on break/air-write.
- `BlockDamageEntryMsg` / `BlockDamageSnapshotMsg`: integer block cells + the
  accumulated `float` damage. Integer zero coordinates are the protobuf
  zero-omission boundary case and round-trip to 0.
- Snapshot fan-out joins the existing world-state group:
  `HandlerContext.SendWorldStateToMember` + the 60 s resend loop.
- Guest application is an ABSOLUTE set (never an additive delta): find or
  create `BlockDamage`, write `damage`, call `UpdateSprite`. Entries for air
  cells or damage ≥ block health are skipped (the break rides the block-state
  snapshot, never a partial-damage entry).
- Live relay correctness: `MetalBonus` rides the existing `BlockDamagedMsg`
  (damage stays raw — the receiver's own `DamageBlock` applies the same
  `bonusMetal` multiplier to the same generated block type). A damage-only
  report against an already-air cell returns before `DamageBlock` (air
  `BlockDamage` artifacts are never created); a break relay's drops still
  materialize on the already-air guest cell.

## Verification design

1. L0 (pure): registry latest-wins / remove / reset / empty-no-send / guest
   no-op / cap (`BlockDamageRegistryTests`); wire round-trips for
   `BlockDamageSnapshot` at cell (0,0) and `BlockDamaged.MetalBonus = true`;
   direction-table row; world-entry and reconnect snapshot groups extended
   from six to seven snapshots.
2. Static: `GameFieldContractTests` continues to lock every reflected member;
   `PatchContractTests` re-verifies the updated `DamageBlock` postfix against
   the real game assembly (the new `bonusMetal` patch parameter is a
   name-matched game parameter).
3. Runtime (later, final acceptance only): host partially mines a block with
   a laser against a metallic tile, a guest joins mid-session and sees the
   same crack stage, then both mine it and the block breaks once with one
   set of drops. Logs: `[Block-damage snapshot applied]` on the guest,
   matching `[WorldFingerprint]` on both sides.
4. Assertion-validity proof: removing the registry report or the snapshot
   group send turns the new tests red; restored to green.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| DamageBlock postfix | carry `bonusMetal` | WorldGenerationDamageBlockPatch.cs; PatchContractTests |
| Live damage wire | `BlockDamagedMsg.MetalBonus` | BlockDamagedMsg.cs; NetPacketTests |
| Live apply | pass `MetalBonus` into `DamageBlock` on host and guest; ignore damage-only reports on already-air cells | BlockBreakSync.cs |
| Break pending state | store + flush the bonus flag | BlockBreakPendingState.cs; tests |
| Snapshot source | registry latest-wins + remove + reset + cap | BlockDamageRegistry.cs |
| Host record | post-write `BlockDamage.damage` on local + remote damage paths | BlockBreakSync.cs |
| Air-write clear | local SetBlock(0) + applied remote air write | WorldEventSync.cs |
| World entry + reconnect | `SendWorldStateToMember` includes NetMsg 89 | HandlerContext.cs |
| 60 s self-heal | resend loop includes NetMsg 89 | WorldEventSync.cs |
| Guest apply | absolute damage set + sprite refresh + air/health skips | BlockBreakSync.cs |
| Direction | NetMsg 89 h2g row | PacketReceiver.cs; DirectionTests.cs |
| Wire | cell (0,0) damage 0 / MetalBonus true round-trips | NetPacketTests.cs |
| Version gate | ProtocolVersion 12 | ProtocolVersion.cs |
| Structure | touched classes stay under the 600-line gate | tools/check-architecture.ps1 |
