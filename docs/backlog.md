# CUO Backlog

Deferred and future work, grouped by domain. The most current, highest-priority items are the
2026-08-14 validation-feedback bugs tracked in the session todo list; the rest are longer-horizon
or lower-priority items gathered during the Claude Code → DeepSeek Harness migration.

## Current bugs (highest priority)

None open. The seven 2026-08-14 validation-feedback items are all closed (session todo list,
marked done): six fixed with L0 regression tests across b4b324b..1ceec3e, and the unconscious
drop-then-pickup view offset determined game-native (CUO never writes the item transform).

## Phase 4 Mod API remaining

- Content registration, custom entities, host commands, UI, mod-state saves, dependency ordering.
- Full permission model (architecture.md §5) + semantic version comparison.

## Lobby domain

- Guest that created its own lobby then joins the host via Steam friends does not follow the
  host into a run (WorldJoin follow broken) — lobby-domain refactor.
- RESOLVED (b4b324b): F8 lobby re-create residue — `SteamService.CreateLobby` now leaves the old
  lobby (`LobbyLifecycle`/`SteamMatchmaking.LeaveLobby`) before creating a new one.

## World time flow

- Multiplayer time domain: the base game supports wait/fast-forward and sleep-acceleration, which
  do not fit multiplayer. Undecided: host-authoritative world time, how fast-forward/sleep degrade
  or disable on the guest, forced-sleep residual handling.

## Item / entity domain

- #89 use-event sync: RESOLVED (ffeefc2 + 0be0d19) — the `ItemCarriedSync` full-fact event
  (use/slot/pickup, host broadcast → clone re-render → component-state refresh) already removes
  the 1 Hz use latency for carried items; world-item use rides #194's correction broadcast. The
  lighter `ItemComponentSyncMsg` + `RenderItemIdentity` variant named in the original design was
  superseded — the full-fact broadcast is correct (matching renders are kept, only component
  state refreshes), and a component-only message would be a pure wire-size optimization.
- #87 loading-screen wait info (bottom-right, to be redesigned).
- #119 held light direction on remote clones (points at the local mouse).
- #122 GameAdapter assembly (re-evaluated 2026-08-14): the pre-migration "collapse ~25 hand-wired
  fields to 1" is NOT a mechanical DI collapse — the hand-wired `new`s are state-belongs-to-its-owner
  (the domain objects own their state; they are not DI services), and the domain logic already sank
  out of the old AdapterDomain into ItemWorldSync/CharacterDataSync/etc. The coordinator stays a
  thin forwarder. Left only as a possible readability grouping of the ~40 constructor `new`s by
  domain — no mechanical factory, per the "no mechanical refactor" rule.
- #118 Steam P2P cert error (transient self-heal on idle — recorded, not investigated).
- Heater cooker meat→steak conversion (item domain).
- TutorialHandler claw double-give in the tutorial world (tutorial domain).
- Trade domain #132: implemented — simulation coverage landed (`TradeSimulationTests`,
  `TradeStockMachineTests`); the acceptance only lacks a dual-side runtime pass.
- RESOLVED (1ceec3e): world-entry snapshot-group consistency — `GeyserStateSnapshot` and `KeypadCode`
  now re-fan-out on member world-entry via the `RemoteSceneChanged(inWorld=true)` signal (the Game
  Adapter owns the data and re-broadcasts on the signal; `HandlerContext.SendWorldStateToMember`
  keeps the 5 Runtime snapshots).

## Character / presentation / combat

- RESOLVED: attack/throw swing animation sync — `Body.Attack` (Body.cs:1887) and `Body.ThrowItem`
  (Body.cs:1665) mark the local swing (`AttackSwingState`), which rides the `IsAttacking`
  ExtendedFlags bit on the 20 Hz entity snapshot; the peer's clone replays `ArmsSwing` on the flag's
  rising edge (`SessionStatePump`). The procedural attackRot/armOffset lean and the weapon slash
  effect are not synced (weapon-specific presentation, separate from the swing clip).
- Block HP progressive sync (currently only the break instant is synced).
- Death-pose / limb / bleed / mining presentation-state sync.
- Configurable state-stream frequency (currently hard-coded 20 Hz).
- NPC position/state sync — CORE LANDED (docs/enemy-sync.md): host-authoritative enemy simulation
  + 20 Hz `EnemyState` snapshot + late-joiner `EnemySnapshot` fan-out; the guest freezes its copies
  at generation finish (`RemoteEnemyDriver` + `EnemyPatches` for SpiderHandler/CrystalEnemy) and
  drives them from the batch (`EnemySyncCoordinator`). Remaining: enemy attack side-effects (bite
  damage to players — host arbitration + event), the runtime cave-tick nest spawn (16 ticks spawn on
  the triggering side only, EntityEventKind.CaveTicksSpawned), and the cave-tick/shadecrawler/wallbiter
  prefab script mapping (Unity asset, needs a runtime component check to extend the freeze list).
- Online UI (create/join room, player status, nameplates + off-screen arrows).
- Command system + permission model (host-authoritative, host can authorize guests).
- Damage events (environment damage local — `ExplosionBodyEffect` rolls it locally and the result rides
  the 1 Hz character snapshot; player-vs-player is OUT OF SCOPE: the base game has no PvP mechanic, the
  KrokMP mod added it as an extra, and CUO prioritizes the base game loop).
- Character sound / block sound sync.
- Direct player interaction (view/take items, carry, view vitals, heal).
- Periodic keyframe self-healing (partially implemented; extend to remaining domains).

## Persistence

- Character data disk persistence (currently in-memory, lost on host exit).

## Config

- BepInEx `ConfigFile` → `IOptionsMonitor<T>` adapter (bridge `ConfigEntry.SettingChanged` to
  `OnChange`); trigger: Phase 4 Mod API or when config entries appear in bulk. Standalone JSON only
  when structured/nested/array config is needed.
- Runtime logging levels: the mod now logs a lot at Info — use the level hierarchy so normal play
  stays quiet (Info/Warning/Error only) while a local dev can raise Debug to diagnose, without
  affecting other players. Depends on the config hot-reload foundation (the entry above).
