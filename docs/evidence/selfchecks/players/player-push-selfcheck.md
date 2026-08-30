# Cross-player push/shove self-check

Owner cycle: backlog "Other lower-priority KrokMP candidates" — `push` remained
future after piggyback closed. Decision: land it as a dedicated
host-authoritative player-interaction operation with the same
request/result shape as heal/carry item use; the target's own client applies the
native ragdoll/velocity, the pusher's client pays the native cost, and the
existing 20 Hz player stream is the motion fallback.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | KrokMP push behavior | `NetBody.Push` (KrokMP `NetBody.cs:1529-1587`): requires no reciprocal carry, strength `15 * clamp(1 + (STR-10)*0.1, 0.2, 3)`, stamina/temperature cost, `Ragdoll()` + `SetVelocity(vel + normal*strength)`, one-shot `landsmall1` sound |
| 2 | Server-side push gates | `ServerMain.cs:1479-1495`: conscious + standing pusher, 1 s cooldown, distance ≤ `max_player_interaction_distance * 1.2` |
| 3 | Native body APIs | `Body.Ragdoll()` (`Body.cs:1713-1730`), `Body.SetVelocity` (`Body.cs:2075-2088`) |
| 4 | Strength derivation | `Skills.STRFrom10` (`Skills.cs:19-25`) = `STR - 10`; `CharacterSkillsMsg.Strength` carries the authoritative value |
| 5 | Entity/character authority | `IEntitySyncControl` (positions/standing) + `ICharacterDataControl` (health/skills) on the host |
| 6 | Existing player state stream | 20 Hz `PlayerState`/`PlayerStateReport` already carries the target's resulting position/velocity as the fallback |

## 2. Design

- **Wire** — `PlayerPushRequestMsg` (NetMsg 118, guest → host) carries
  `TargetSteamId`; `PlayerPushResultMsg` (NetMsg 119, host → all) carries
  `PusherSteamId`, `TargetSteamId` and the committed `ForceX`/`ForceY` delta.
  `ProtocolVersion` 48 → 49.
- **Host service** — `PlayerPushService` validates: host role + session active +
  local in world, both players in-world, neither in a carry/piggyback relation,
  pusher conscious/alive/standing, distance in reach (9 × 1.2 world units), and
  a 1 s per-pusher cooldown. It then computes the normalized force from the
  authoritative entity positions and the KrokMP strength formula, records the
  cooldown, and publishes one reliable result to all members.
- **Local apply** — `PlayerPushApply` (extracted from `PlayerInteractionApply`
  to keep the apply file under the 600-line gate): if the local player is the
  target, call `Ragdoll()` then `SetVelocity(current + force)` inside a
  `RemoteApply` scope; if the local player is the pusher, subtract stamina and
  add heat; every side plays `landsmall1` at the target position. Mutated
  snapshots are re-reported immediately through `CharacterDataSync`.
- **UI** — `OnlineUiMemberProjection.CanPush` gates a new `Push` button on the
  Players page and in the in-world right-click menu. The button is hidden when
  the local player or target participates in a carry relation.
- **Scope limits** — no anti-cheat/strength validation beyond the host gates;
  no pushing through carry relations; the target's subsequent motion is the
  normal 20 Hz stream (not a dedicated position override).

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Host force computation | normalized pusher→target direction × strength | `PlayerInteractionServiceTests.Guest_PushesHost_ComputesForceAndBroadcastsResult` + `Host_PushesGuest_SendsResultToGuest` |
| Standing gate | non-standing pusher is refused | `Push_NotStandingPusher_IsRefused` |
| Distance gate | out-of-reach push is refused | `Push_OutOfReach_IsRefused` |
| Cooldown gate | second immediate push is refused | `Push_ImmediateSecondRequest_IsRefusedByCooldown` |
| Carry relation gate | push while a carry relation is active is refused | `Push_CarryRelation_IsRefused` |
| Direction registry | request is guest→host, result is host→guest | `DirectionTests` updated lists |
| UI eligibility | in-world remote is pushable when local is in world; carried local is not | `OnlineUiMemberProjectionTests` +2 |
| Local body apply | target ragdolls/velocity, pusher pays stamina/heat, sound replays | `PlayerPushApply` review + L0/static evidence (Unity seam, no manual acceptance) |

## 4. Verification

- **L0 unit**: `PlayerInteractionServiceTests` +6 push cases,
  `OnlineUiMemberProjectionTests` +2 push cases, `DirectionTests` updated.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` full suite
  green, `dotnet format`, check-architecture / check-event-replay /
  check-entity-event-dispatch all pass.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
