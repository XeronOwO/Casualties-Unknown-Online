# Revive / Respawn Rules — Host-Authoritative Co-op Lifecycle — Self-Check (2026-08-23)

Delivery fact sheet for the broader KrokMP-inspired revive/respawn rules
(backlog: Revive/respawn rules, exploration §2.2). On top of the existing
trader-recruit revive slice, CUO now has an auto-respawn rule set:
Permadeath, trader-revive permission, next-level auto-respawn, keep-inventory
and keep-skills. Dead players are respawned from the host's authoritative
character snapshot when the host finishes the next world-layer generation;
menu-side dead players are invited back with a targeted `WorldJoin`.

## Mechanism inventory (complete side-effect table)

| # | Mechanism | Vanilla behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | Death state | `Body.alive` derived from `brainHealth > 0` (Body.cs:203-207); death is run-ending in the stock CUO model | the host sees death through its saved `CharacterDataMsg.Health.Alive == false`; `RespawnPolicy.IsDead` is the pure gate | `RespawnPolicy.cs`, `CharacterDataStore` |
| 2 | Next-level generation edge | `WorldGeneration.RegenerateWorld` → `InstantiateWorld(true)` → `GenerateWorld` (WorldGeneration.cs:1042-1066) | a new `RespawnCoordinator` observes the same generation-finished falling edge used by `GeneratedItemAuthority` and runs one frame later | `RespawnCoordinator.Update`, `HarmonyTraverse.IsGenerating` |
| 3 | Host rules | n/a (no rule surface) | BepInEx `[Respawn]` config entries back `RespawnOptions`: `Permadeath`, `ReviveFromTrader`, `ReviveOnNextLevel`, `KeepInventory`, `KeepSkills` | `RespawnOptions.cs`, `Plugin.cs`, `CuoBootstrap.cs` |
| 4 | Trader permission | trader recruit was always allowed by its own trade gates | `TraderRecruitCoordinator` now rejects requests when `Permadeath` or `ReviveFromTrader` is disabled | `TraderRecruitCoordinator.HandleHostRequest`, `RespawnPolicy.CanUseTraderRecruit` |
| 5 | Auto-respawn shape | n/a | `RespawnPolicy.PrepareRespawn` builds a full post-respawn snapshot: physiological baseline from `TraderRecruitPolicy.PrepareRevive`, empty inventory when keep=false, zeroed skills when keep=false, and `Position=null` so the respawn lands at the current world's spawn point | `RespawnPolicy.PrepareRespawn` |
| 6 | Guest delivery | n/a | the host saves the respawn snapshot and sends the existing full `CharacterData` restore to the target (same two-frame wipe/restore path as reconnect); the guest applies it even while already in the world | `RespawnCoordinator.TryRevive`, `CharacterDataSync.QueueRespawnRestore`, `CharacterDataStore.SendSavedCharacter` |
| 7 | Host local delivery | n/a | the host queues the same full restore on its own body through `CharacterDataSync.QueueRespawnRestore` — no inventing a parallel host-only apply path | `RespawnCoordinator.TryRevive`, `CharacterDataSync.QueueRespawnRestore` |
| 8 | Left-world revival | n/a | a dead handshaken member whose `InWorld == false` receives the saved respawn now and a targeted `WorldJoinTo` so it can re-enter the current world; `SceneStateHandler` re-sends the saved character on its InWorld edge | `RespawnCoordinator.TryRevive`, `WorldService.SendWorldJoinTo`, `SceneStateHandler` |
| 9 | Save/level integration | n/a | the respawn snapshot is persisted into `CharacterDataStore` before any delivery, so a disconnect during the delivery still restores the revived state; a new run's `ClearSavedCharacters` keeps stale death saves from leaking into the next run | `RespawnCoordinator.TryRevive`, `CharacterDataStore.ClearSavedCharacters` |
| 10 | Protocol | no new wire message | the next-level respawn reuses the existing `CharacterData` direction/restore path; `ProtocolVersion` stays 35 | `CharacterDataHandler`, `DirectionTests`, `NetMsg.cs` |

## Design

- **Not a heal-in-place slice**: the trader-recruit flow remains the
  "revive at a trader without touching inventory/position" path. The next-level
  respawn is a full respawn because the keep flags need a real wipe/reset path;
  both share the same physiological baseline via `TraderRecruitPolicy`.
- **Host-authoritative**: the host owns the decision and the saved snapshot.
  A guest never decides its own respawn; the host sends either the full restore
  to the guest or the targeted `WorldJoin` if the guest had already left the
  world.
- **Full restore for local body**: `CharacterDataSync.QueueRespawnRestore`
  uses the existing two-frame wipe/restore machinery, so the host and guests
  behave identically and the keep flags are honored on every side.
- **No protocol bump**: respawn rides `NetMsg.CharacterData` (already
  bidirectional) and the existing `WorldJoin` message. Only the new
  `WorldService.SendWorldJoinTo` targeted send is added; no new packet class or
  handler.
- **Scope boundary**: layer-transition coordination outside the host's own
  generation flow is not added in this slice; the respawn fires on the host's
  generation-finished edge, which is the existing host-authoritative next-layer
  point in CUO. The config defaults (trader revive + next-level revive on,
  keep inventory/skills on) preserve the co-op-friendly behavior.

## Verification design

1. L0 (pure policy): `RespawnPolicyTests` — 10 cases covering Permadeath /
   trader / next-level gates, dead detection, keep-inventory, keep-skills, and
   the null-position respawn shape.
2. Existing trader-recruit tests remain green; `TraderRecruitPolicyTests` (7)
   still locks the trader gates and heal-in-place revive.
3. Direction completeness: no new NetMsg, so `EveryNetMsg_IsExplicitlyClassified`
   continues to pass without table changes.
4. Full suite: **1229 green**, build 0 warnings/0 errors, `dotnet format`
   (excluding generated `obj`), `tools/check-architecture.ps1`,
   `tools/check-event-replay.ps1`, `tools/check-entity-event-dispatch.ps1`
   pass.
5. Runtime (final acceptance only): host + guest in a world, a guest dies and
   stays dead while the host advances the next layer — the host logs the
   respawn, the guest receives the full `CharacterData` restore, its death
   screen cancels, and a menu-side dead guest is re-invited with `WorldJoin`.
   No manual acceptance during the dev cycle; evidence is L0 + static.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Rule surface | small host-side `RespawnOptions` with BepInEx hot-reload | `RespawnOptions.cs`, `Plugin.cs` |
| Trader gate | trader revive disabled by Permadeath / flag | `RespawnPolicy.CanUseTraderRecruit`, `TraderRecruitCoordinator` |
| Next-level decision | host generation-finished edge + rule gate | `RespawnCoordinator.Update`, `RespawnPolicy.CanAutoReviveOnNextLevel` |
| Respawn state | full snapshot with keep flags and null position | `RespawnPolicy.PrepareRespawn` |
| Guest apply | existing full `CharacterData` restore path | `CharacterDataSync` + `CharacterDataStore.SendSavedCharacter` |
| Host apply | same local restore queue, no dedicated path | `CharacterDataSync.QueueRespawnRestore` |
| Left-world revive | targeted `WorldJoinTo` + saved restore | `WorldService.SendWorldJoinTo`, `SceneStateHandler` |
| Persistence | saved before delivery, new-run clear | `CharacterDataStore` |
| Protocol | no bump, no new NetMsg | `NetMsg.cs`, `ProtocolVersion.cs`, `DirectionTests` |
| Structure | all new files small single-responsibility | `tools/check-architecture.ps1` passed |
