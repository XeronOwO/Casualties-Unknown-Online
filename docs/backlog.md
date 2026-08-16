# CUO Backlog

Deferred and future work, grouped by domain. Priority (2026-08-16 user decision): finish the
remaining BASE-GAME content coverage first (high); the Phase 4 Mod API remainder is MEDIUM and
resumes after the native game is fully covered — native work only reserves extension seams.
Longer-horizon items come from the Claude Code → DeepSeek Harness migration and the 2026-08-16
audit of the `~/.dsh/memories/projects/...` and `~/.claude/projects/.../memory` stores.

## Current bugs (highest priority)

None open. The seven 2026-08-14 validation-feedback items are all closed (session todo list,
marked done): six fixed with L0 regression tests across b4b324b..1ceec3e, and the unconscious
drop-then-pickup view offset determined game-native (CUO never writes the item transform).

## Native game content coverage (next, highest priority)

Finish every remaining base-game domain before returning to the Mod API remainder. Proposed
order (the bullets live in the sections below — no duplicate copies):
1. World consistency: World generation / determinism, Known game-native issues, and
   Building-entity damage persistence for late joiners.
2. World time flow.
3. Item / entity domain open items (excluding entries explicitly marked accepted).
4. Character / presentation / combat open items, including Online UI and sound sync.
5. Persistence + Config.
6. Tooling / testing debt.
Extension seams for the medium-priority Mod API surfaces are reserved as each native domain
lands, never bolted on afterwards.

## Phase 4 Mod API remaining (MEDIUM — after native game-content coverage)

- RESOLVED (2026-08-16, ProtocolVersion 10): host commands, full permission model,
  dependency ordering, SemVer versions — see `docs/mod-api.md`.
- Mod support stays MEDIUM priority (user 2026-08-16): land the native coverage above first,
  reserve extension space while doing native work, then return to the remaining surfaces:
  content registration, custom entities, UI, mod-state saves.

## Lobby domain

- RESOLVED (2026-08-15, lobby-domain refactor): the lobby identity is now a real state machine.
  `SteamService.JoinLobby`/`CreateLobby` both leave the current lobby first and fire `LobbyLeft`;
  `SessionService` tears the old session down, drops the role with the lobby, and rebinds as
  Guest/Host on the entered/created lobby before re-handshaking. The original repro (guest hosts
  its own lobby, then joins the host via Steam friends) is runtime-verified end-to-end: role
  switched to Guest, handshake confirmed, and the guest followed the host's run with a matching
  world fingerprint and `WorldReady` on time. Lobby switches while a world is running/generating
  are refused by `LobbySwitchGuard` (menu-only policy; the solo-in-world -> host conversion stays).
- RESOLVED (b4b324b): F8 lobby re-create residue — `SteamService.CreateLobby` now leaves the old
  lobby (`LobbyLifecycle`/`SteamMatchmaking.LeaveLobby`) before creating a new one.

## Runtime / transport

- #118 Steam P2P cert error (transient self-heal on idle — recorded, not investigated).
  Diagnostic order from memory: check the local proxy first (Clash on localhost:7890 has
  triggered `4003 Bad cert` / `5008 rendezvous timeout` probabilistically), then the Steam
  client state, then the Steamworks.NET wrapper.
- Offline-member P2P noise (memory 2026-08-14): a log sweep showed ~40
  `k_EResultConnectFailed` per minute for a member that was already removed. Candidate fix:
  stop the entity state stream for removed members.

## World generation / determinism

- Post-generation random-consumer divergence: AUDITED (see `docs/worldgen-determinism-audit.md`)
  — block 35 (`GenerateOres`) runs inside the isolated `WorldGenerateTerrain` coroutine
  (WorldGeneration.cs:1534-1547, 2734/2939/3067, 3718); no unpatched synchronized-state
  consumer was found. Remaining per-side `Start` randomness is either covered by the
  item/enemy/fluid/entity-event domains or accepted as visual/timing (grabber tendril phase,
  spike light phase, stalactite drop timing, sky/rain visuals). Runtime confirmation: compare
  `[GenStream]` + `[WorldFingerprint]` host/guest logs at the next dual-side pass.

## Known game-native issues (not CUO)

- LifepodPump `IndexOutOfRange` when the pump sits at the world edge: `pumpMin` can go
  negative against the fixed-size fluid grid (`WorldGeneration.cs:3803`); the crash is
  identical on both sides and in single-player — recorded so it is never chased as a sync bug.

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
- #87 loading-screen wait info (bottom-right, to be redesigned). The layer-title popup is
  built immediately after `loadingObject` hides (`WorldGeneration.cs:3637`/3640-3659), so it
  can play out invisibly during the host's start-gate wait — fold into the same redesign.
- #119 held light direction on remote clones (points at the local mouse).
- #193 clone shooting direction / recoil (deferred from the 2026-08-13 fix-round plan): no
  log evidence yet — the next step is a hotrepl runtime check comparing the 20 Hz state-stream
  `LookPos` against what the clone renders, then decide whether weapon direction/recoil needs
  its own sync bit.
- #195 blueprint popup: cosmetic, game-local UI (the unlock fact itself is synced via
  `RecipeUnlock`); revisit with the online-UI pass.
- RestoreItem slot-conflict handling (#192 follow-up): an occupied target slot with no
  container currently no-ops and leaves the item at the player position — acceptable for the
  cross-run leak fix, but the semantics should be made explicit.
- #120 nested container content movement (deferred): items moved inside a carried container
  still ride the 1 Hz snapshot (visible impact is zero until opening another player's
  inventory exists; a `[CharSync]` warning is already logged). When direct player interaction
  lands, design nested-content events instead of extending the snapshot.
- In-flight pickup reject friction (phase-1 memory): a pickup report that beats its spawn
  report is still refused as `UnknownItem` and rolled back; consider a short host-side queue
  instead of relying on the sender's retry.
- Picking up a generation-time item leaves the peer's own copy behind (low frequency,
  accepted): the id is assigned on drop/container-exit, not on the pickup path — revisit only
  if the duplicate becomes observable.
- #122 GameAdapter assembly (re-evaluated 2026-08-14): the pre-migration "collapse ~25 hand-wired
  fields to 1" is NOT a mechanical DI collapse — the hand-wired `new`s are state-belongs-to-its-owner
  (the domain objects own their state; they are not DI services), and the domain logic already sank
  out of the old AdapterDomain into ItemWorldSync/CharacterDataSync/etc. The coordinator stays a
  thin forwarder. Left only as a possible readability grouping of the ~40 constructor `new`s by
  domain — no mechanical factory, per the "no mechanical refactor" rule.
- Heater cooker meat→steak conversion (item domain).
- TutorialHandler claw double-give in the tutorial world (tutorial domain).
- Trade domain #132: implemented — simulation coverage landed (`TradeSimulationTests`,
  `TradeStockMachineTests`); the acceptance only lacks a dual-side runtime pass.
- Building-entity damage persistence for late joiners (phase-1 memory): only live
  `BuildingEntityDamaged` relays exist — a late joiner regenerates destroyed plants/crates and
  intermediate damage is never recovered. Needs a host-side damaged-entity registry + a
  world-entry snapshot (the `OpenedEntitiesSnapshot` family).
- Runtime random supply refresh (phase-1 memory, unresolved question): it is not verified
  whether any supplies spawn independently of world generation. If such a mechanism exists it
  needs host-authoritative spawn + broadcast (the #110 pattern); investigate before assuming.
- High-frequency small drops (shell casings etc.): observe message volume before optimizing —
  batch/rate-limit only if it actually hurts.
- Geyser replay duplicate report: the replay side's natural `Activate` reports once more (the
  game's cooldown gate drops it — harmless noise); add an event-origin marker only if the log
  noise matters.
- Openable keypad prefab mapping (entity-features table follow-up): `Openable.isKeypad` is a
  runtime resource configuration, so which prefabs carry it is still not enumerated in the
  matrix notes; fill during the next runtime component sweep.
- Accepted item/fluid boundaries from memory (non-blocking): guest item-vs-player and
  item-vs-item push are disabled by the layer isolation (the position stream + soft
  correction self-heal); geyser mid-eruption fluid writes cannot be patched out, so the 1 Hz
  absolute fluid snapshot corrects them within ≤1 s.
- Accepted entity-features exclusions (non-blocking, local-only semantics — recorded so they
  are re-evaluated, not re-discovered): SurvivorNote slow-mo/time-scale, GrapplingHook rope
  visual, Climbable/BounceShroom/GeigeFruit/Leadbush/Campfire/WaterPusher/ItemLock/
  Radioactive local body/physics/marker roles, continuous crystal body effects,
  CrystalGravity/Kinetic local physics, DrillPod WorldJoin-level reset, and hand-placed
  Gunmine/Sawblade.
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
- Accepted presentation gaps from the entity-domain memory (non-blocking; several are already
  recorded in `docs/event-replay-matrix.csv`): mine 0.8 s press visual, CrystalUnstable 5 s
  ticking, remote building-destruction particles, sound-cannon burst effect trigger-side-only,
  jump-pad light / turret tracer omissions, cactus self-damage HP local, the guest-side fluid
  water sound/push/slip gaps, and the fluid lightSprite flicker starting at the warning edge
  (the `didShoot` immediate-lock tradeoff). `LookTarget` gaze/startle and the Heater
  temperature field also stay local by design (accepted; only heater meat→steak conversion is
  an open item, listed under Item / entity). Re-open only as part of a deliberate presentation
  pass.
- Configurable state-stream frequency (currently hard-coded 20 Hz).
- NPC position/state sync — CORE LANDED (docs/enemy-sync.md): host-authoritative enemy simulation
  + 20 Hz `EnemyState` snapshot + late-joiner `EnemySnapshot` fan-out; the guest freezes its copies
  at generation finish (`RemoteEnemyDriver` + `EnemyPatches` for SpiderHandler/CrystalEnemy) and
  drives them from the batch (`EnemySyncCoordinator`). Remaining enemy interaction work:
  - RESOLVED (d4fcc6a): the guest's local attack drop flash-reverted on the next host batch —
    `EnemyHealthReconcile` (pure pending-delta machine) preserves the drop until the host applies it.
  - RESOLVED (33b49ab + b10ef93): the bite now travels as a dedicated `EnemyBite` event (report +
    relay + clone apply) instead of riding the 1 Hz snapshot — `EnemyBiteMsg` (limb + venom/adrenaline/
    happiness), `EnemyBitePatches` (DamageLimb postfix), `EnemySyncCoordinator.ReportEnemyBite`,
    `CloneFactTable.ApplyEnemyBite`.
  - SUPERSEDED (5799623): the old guest-side `RemoteEnemyDriver` cooldown ticker was removed when
    multiplayer targeting landed — frozen spider collision callbacks are now skipped entirely, and the
    host's `EnemyCombatDirector` is the single bite-apply path (it reads/writes `biteCooldown` on the
    host spider, guarded by `GameFieldContractTests`). The `rb.bodyType = Static` freeze remains a
    no-op (`BuildingEntity.Update`, BuildingEntity.cs:50-55, re-toggles `bodyType` to Dynamic when the
    chunk renders + `timeScale ≤ 5`), so the freeze still relies entirely on EnemyPatches, not the
    Static rb. Related edge (deferred): a thrown item that hits the frozen spider sets `stunTime` via
    `AnimalHit` (SpiderHandler.cs:264), which is ALSO frozen (Update:40 skipped) and would permanently
    re-gate the bite — that path rides the unsynced item-vs-enemy damage, out of scope until
    item-vs-enemy attacks are synced.
  - **RESOLVED (enemy-targeting + host-ordered attacks)**: enemies now see every in-world player —
    `EnemyCombatDirector` makes SpiderHandler target the nearest player inside `seeDistance` on the
    game's own moveTime-expiry edge and resolves `CrystalEnemy.body` to the nearest player body within
    its 64-unit close radius; no clone collider is re-enabled. Host-ordered `EnemyAttack` (83) +
    `EnemyLunge` (84) apply spider bites and crystal lunges to remote victims locally, with the
    terminal state reported back. Frozen spider collision callbacks are now skipped so one attack has
    exactly one apply path. Enemy-interaction gap closeout:
  - RESOLVED (ProtocolVersion 8): runtime cave-tick nest spawn — the 16 `cavetick` creations ride the
    generic `EntitySpawned` channel; the guest freezes each runtime animal at Start, live 20 Hz batches
    bind the unbound host ids by position (`EnemyRuntimeSpawnArbitration`), and the world-entry
    `EnemySnapshot.RuntimeSpawns` materializes the copies for a late joiner.
  - **RESOLVED (ProtocolVersion 9)**: enemy-proximity side effects travel as the dedicated
    `EnemyEffectMsg` (NetMsg 85) — ElderThornback horror tick/defeat, Xaloris septic tick and
    GrabberPlant grab each report their post-effect terminal state; the host merges bite/lunge/effect
    terminal states into the saved character immediately instead of waiting for the 1 Hz snapshot.
  - **RESOLVED**: the host-local CrystalEnemy lunge reports `EnemyLungeMsg` through the verified
    pre/post limb trace (`CrystalEnemyLungePatch` + `CrystalLungeTrace`) — no more 1 Hz fallback
    for the native host-body hit.
  - **RESOLVED (runtime component check)**: `cavetick`/`shadecrawler`/`wallbiter`/`thornbackyoung`/
    `overgrowntick`/`snowstrider` carry `SpiderHandler`; `thornbackelder` carries `SpiderHandlerTBE`
    + `ElderThornbackBehaviour`; `grabberplant`/`xaloris` are static traps. The existing freeze
    patches already cover every moving script, so no freeze-list extension is needed (evidence in
    `docs/enemy-sync.md` §1).
- CrystalMimic runtime spawn (entity-features matrix "excluded — AI domain"): the
  CrystalEnemy it spawns does not ride the runtime enemy-spawn channel. Re-evaluate against
  `EnemySnapshot.RuntimeSpawns` now that runtime enemy-spawn binding exists.
- Online UI (create/join room, player status, nameplates + off-screen arrows).
- RESOLVED (2026-08-16): command system + permission model — `ModPermission` declaration
  + live enforcement, host-authoritative `IModCommands` (NetMsg 86/87), host mod authorizes
  per-guest via `IModCommandContext.RequesterSteamId`.
- Damage events (environment damage local — `ExplosionBodyEffect` rolls it locally and the result rides
  the 1 Hz character snapshot; player-vs-player is OUT OF SCOPE: the base game has no PvP mechanic, the
  KrokMP mod added it as an extra, and CUO prioritizes the base game loop).
- Character sound / block sound sync.
- Direct player interaction (view/take items, carry, view vitals, heal).
- Periodic keyframe self-healing (partially implemented; extend to remaining domains). Before
  any snapshot stream switches to an unreliable channel, event-version numbers are required —
  an old snapshot arriving after an in-flight event would otherwise roll the event back
  (carried-sync-monitoring memory; the snapshot streams are reliable today).

## Persistence

- Character data disk persistence (currently in-memory, lost on host exit).

## Config

- BepInEx `ConfigFile` → `IOptionsMonitor<T>` adapter (bridge `ConfigEntry.SettingChanged` to
  `OnChange`); trigger: Phase 4 Mod API or when config entries appear in bulk. Standalone JSON only
  when structured/nested/array config is needed.
- Runtime logging levels (DSH dtodo `6a7caba1`): the mod now logs a lot at Info — use the
  level hierarchy so normal play stays quiet (Info/Warning/Error only) while a local dev can
  raise Debug to diagnose, without affecting other players. Depends on the config hot-reload
  foundation (the entry above).

## Tooling / testing

- Replay corpus extension (evolution-roadmap memory): the replay format already fossilizes
  item/craft/entity-event/fluid behavior; the remaining domains are block-break and trade
  cross-domain action sets.
- Real-log → replay / SimTrace diff automation: `tools/extract-itemtrace.ps1` extracts the
  trace, but the real-log-vs-simulation diff is still a manual/CI step; automate the full
  pipeline when the next replay class lands.
- Patch-contract same-name limitation: `PatchContractTests` identifies targets by name, so a
  same-name overload pair cannot be distinguished (the `LoadSceneAsync` case). Extend the
  contract only when a game update actually hits it.
- Block-break drop-race dual-side runtime pass: L0 simulation covers the first-writer-wins +
  loser-rollback arbitration, but the 2026-08-09 "two guests break the same block at the same
  time" runtime confirmation was never recorded — fold into the next dual-side verification.
- `docs/entity-features.md` status-column refresh: the narrative table still says "missing"
  for rows that the CSV now marks `covered` (lifepod/terminal/med-station/talker/crystal
  events) — regenerate the table text from `tools/entity-features.ps1 list` so the two sources
  cannot disagree again.

## Future phases / ecosystem

- Strict validation / anti-cheat hardening: explicitly LOW priority until the core feature set
  stabilizes (user mandate, KEY-archive) — do not schedule before the sync domains are complete.
- Phase 5 tooling & ecosystem: mod manager, auto-install, crash reports, host migration,
  dedicated server (AGENTS.md phase list).
- KrokMP compatibility adapter: reserved, API-level only — trigger: the native Phase 4 API
  stabilizes AND real migration demand exists (architecture.md §5.4). Not near-term.
