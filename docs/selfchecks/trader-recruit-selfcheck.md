# Trader Recruit — Host-Authoritative Co-op Revive — Self-Check (2026-08-23)

Delivery fact sheet for the first slice of the KrokMP-inspired revive work
(backlog: Trader Recruit, exploration §2.1/§2.2). A living player standing at
a friendly, undamaged trader can recruit a dead in-world teammate; the host
validates the trade gates + the target's authoritative snapshot and sends the
revived physiological state directly to the target. The target stays where its
dead body is, keeps its inventory, and resumes control immediately.

## Mechanism inventory (complete side-effect table)

| # | Mechanism | Vanilla behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | Dead-player state | `Body.alive` is derived from `brainHealth > 0` (Body.cs:203-207); death is run-ending in the current CUO model | the host detects a revivable target from its saved `CharacterDataMsg.Health.Alive == false` (`TraderRecruitPolicy.IsDead`) | `TraderRecruitPolicy.cs`, `CharacterDataStore` |
| 2 | Trader gates | the game's own trader methods use reputation/build gates (TryPurchase build > 200, TraderScript.cs:747-804) | recruit requires `reputation >= 75`, `hostility <= 0`, `build.health > 200`, and one recruit per trader instance | `TraderRecruitPolicy.CanRecruit`, `TradeExecutor.Read` |
| 3 | Request path | no vanilla method exists | a dedicated `TraderRecruitRequest` (NetMsg 107) guest→host; the acting side only locates the nearest trader | `TraderRecruitRequestMsg`, `TraderRecruitRequestHandler`, `TraderRecruitCoordinator.TryRequest` |
| 4 | Host execution | n/a | host re-checks requester alive/conscious, target in-world/dead, persists the revived snapshot, marks the trader used | `TraderRecruitCoordinator.HandleHostRequest` |
| 5 | Revived physiology | n/a | `TraderRecruitPolicy.PrepareRevive` returns a safe conscious baseline (`BrainHealth=75`, `Consciousness=100`, circulation/respiration defaults, clear lethal states) while keeping skills, items, limbs and position | `TraderRecruitPolicy.PrepareRevive` |
| 6 | Target application | n/a | the target's local Body is healed in place through the existing cross-player heal state mapping (`CharacterDataSync.ApplyHealState`) inside a RemoteApply scope; no inventory wipe, no position teleport | `TraderRecruitCoordinator.ApplyRevive`, `CharacterDataSync.ApplyHealState` |
| 7 | Peer visibility | n/a | the target re-reports its full character snapshot immediately after the revive (`ReportInventoryChanged`), so host saves and peer clones refresh without waiting for the next 1 Hz tick | `TraderRecruitCoordinator.ApplyRevive` |
| 8 | Duplicate use | n/a | host-side `HashSet<int>` of used trader instance ids, cleared on session end | `TraderRecruitCoordinator._usedTraders`, `Reset` |
| 9 | Protocol | no previous wire face | `ProtocolVersion` 34→35; new NetMsg 107/108, direction-locked in `PacketReceiver` and `DirectionTests` | `NetMsg.cs`, `ProtocolVersion.cs`, `PacketReceiver.cs` |

## Design

- **Not a `TraderActionKind`**: ordinary trade actions run a vanilla game
  method first on the acting side; recruit has no vanilla method. It is a
  dedicated request/result pair so the host owns the whole outcome.
- **Request is cheap**: the acting side only computes the nearest trader
  position and sends `TargetSteamId`; the host still finds its own trader by
  the same position key and re-validates every gate (not trust-the-client).
- **Revive is not a reconnect restore**: reusing `CharacterDataMsg` as a
  full restore would wipe inventory and teleport the target. Instead the host
  sends only `CharacterHealthMsg` + limbs and the target uses the existing
  `ApplyHealState` mapping — "heal the dead body in place".
- **Random trader items landed separately**: this first slice was the minimal
  revive mechanic; the later increment gives 1–3 random trader-stock bonus
  items through `TraderRecruitResult.Items` — see
  `docs/selfchecks/trader-recruit-gift-items-selfcheck.md` and
  `docs/tech-decisions.md` #62.
- **Death is still not a full respawn system here**: this slice covers a dead
  player who is still in-world and keeps its inventory/position. The broader
  `Revive/respawn rules` lifecycle (Permadeath, ReviveOnNextLevel,
  RespawnKeepInventory, RespawnKeepSkills, save/level transitions, left-world
  re-entry) landed separately — see
  `docs/selfchecks/respawn-rules-selfcheck.md` and `docs/tech-decisions.md` #60.

## Verification design

1. L0 (pure policy): `TraderRecruitPolicyTests` — trader gates (used,
   reputation, build, hostility), dead detection, and `PrepareRevive`
   preserving items/limbs/position while restoring life signs.
2. Wire (fake network): `TraderRecruitChannelTests` — the new guest→host
   request and host→target result travel through the real dispatcher and
   direction table.
3. Direction completeness: `DirectionTests` includes both new NetMsgs in the
   host→guest / guest→host lists, and `EveryNetMsg_IsExplicitlyClassified`
   passes.
4. Full suite: **1219 green**, build 0 warnings/0 errors, `dotnet format`,
   `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
   `tools/check-entity-event-dispatch.ps1` pass.
5. Runtime (final acceptance only): host + guest in a world, one dies, the
   other stands at a trader with `reputation >= 75` and clicks Recruit — the
   host logs the recruit, the target receives `TraderRecruitResult`, its
   death screen cancels (`HandleDeathScreen` sees `body.alive == true`), and
   the peer clone/save refresh on the next snapshot. No manual acceptance
   during the dev cycle; evidence is L0 + static.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Trader gate | pure host-side policy | `TraderRecruitPolicy.CanRecruit` |
| Dead target detection | host saved-snapshot `Alive` | `TraderRecruitPolicy.IsDead` |
| Revive state | safe health baseline, no inventory/position change | `TraderRecruitPolicy.PrepareRevive` |
| Request/result wire | NetMsg 107/108 + direction rows | `TradeChannel`, `PacketReceiver`, `DirectionTests` |
| Local apply | existing heal-state mapping + RemoteApply | `CharacterDataSync.ApplyHealState`, `CallContext` |
| Peer visibility | immediate character re-report | `ReportInventoryChanged` |
| Duplicate prevent | host per-trader used set | `_usedTraders` + `Reset` |
| Protocol | v35 (new wire messages) | `ProtocolVersion.cs` |
| Structure | new small owners under 600-line gate | `tools/check-architecture.ps1` passed |
