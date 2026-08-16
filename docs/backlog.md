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
1. Item / entity domain open items (excluding entries explicitly marked accepted).
2. Character / presentation / combat open items, including Online UI and sound sync.
3. Persistence + Config.
4. Tooling / testing debt.
World consistency and world time flow are code-complete; their only residual is the final
dual-side acceptance pass (world fingerprint comparison), folded into the final acceptance
round under the development-period no-manual-acceptance rule.
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
- Offline-member P2P noise: RESOLVED (2026-08-16, no protocol bump). Log
  correlation first cleared the candidate hypothesis: the entity state stream
  already stops for a removed member (`EntitySyncService.OnMemberRemoved`,
  EntitySyncService.cs:371-389). The real sender was the host's
  `SendPeerWarmup` pump — a fixed 1 s ping to every un-handshaken lobby peer,
  which keeps failing while the peer's Steam P2P session is broken/restarting
  (host `2026-08-10-24.log.gz`: member removed 15:52:24, then 1/s
  `ConnectFailed` bursts at 15:52:45-54 and 15:53:11-15; the sandbox guest
  `2026-08-10-33.log.gz` re-entered the same lobby un-handshaken at
  15:52:39-42). The pump now uses `PacketSender.TrySend` (the transport
  verdict that used to be discarded) and a pure `PeerWarmupBackoff` machine:
  failures double the retry delay 1 s → 2 s → … → 10 s cap, one success
  resets the peer, and a lobby change clears all history. Healthy peers keep
  the exact 1 s cadence; the guest-side handshake retry is intentionally
  unchanged. See `docs/warmup-backoff-selfcheck.md`; 894 tests green.

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

- RESOLVED (2026-08-16, ProtocolVersion 13): host-authoritative world time. Guest speed
  hotkeys/movement-resets become `WorldTimeRequest` (NetMsg 90) reports — never local
  timeScale writes; the host applies the pure `WorldTimePolicy` (movement forces Normal and
  clears the request; all-unconscious sleep acceleration runs 25×, or 3.5× when any sleeping
  player is brain-dying) and broadcasts `WorldTime` (NetMsg 91). The vanilla per-side
  unconscious fast-forward is suppressed by a CallContext scope; direct timeScale writers are
  re-adopted/corrected, with world-entry fan-out + 5 s resend. Slowmo/Paused stay local-only
  presentation. See `docs/world-time-selfcheck.md`; 801 tests green.

## Item / entity domain

- #89 use-event sync: RESOLVED (ffeefc2 + 0be0d19) — the `ItemCarriedSync` full-fact event
  (use/slot/pickup, host broadcast → clone re-render → component-state refresh) already removes
  the 1 Hz use latency for carried items; world-item use rides #194's correction broadcast. The
  lighter `ItemComponentSyncMsg` + `RenderItemIdentity` variant named in the original design was
  superseded — the full-fact broadcast is correct (matching renders are kept, only component
  state refreshes), and a component-only message would be a pure wire-size optimization.
- RESOLVED (2026-08-16, no protocol bump): #87 loading-screen wait info + layer-title popup —
  the multiplayer wait text is now a translucent bottom-right panel over the live frozen world
  (the game's own loading-info slot is bottom-right; `level1` LoadingImage/Text RectTransform
  path id 1195 anchors at (1,0)), and `PlayerCamera.DoAlert` popups are deferred through the
  start-gate alert window (`PlayerCameraDoAlertPatch` + `StartGateAlertQueue`): the host latch
  covers generation end, where the layer title fires one frame BEFORE the world-entry edge arms
  the gate (`WorldGeneration.cs:3637`/3640-3659), and the queue replays in order once the run is
  playing. Session end / world exit clear the queue. See `docs/start-gate-alert-selfcheck.md`;
  903 tests green (L0 reflection + patch contract + static asset/source evidence,
  no manual acceptance).
- RESOLVED (076da7a, held-light-direction cycle): #119 held light direction on remote clones —
  `CustomItemBehaviour.Update` aims flashlight/emergencylight/rangefinder at the LOCAL mouse
  (CustomItemBehaviour.cs:439/512/526); a clone body now re-aims those three hand-slot items at the
  peer's synced `LookPos` via `HeldItemDirectionPatch` (Postfix, `RemoteBodyDriver` + first-snapshot
  gate), the only three local-mouse item-orientation call sites in the game. See
  `docs/held-light-direction-selfcheck.md`.
- #193 clone shooting direction / recoil (deferred from the 2026-08-13 fix-round plan): the
  held-light direction family is now covered by #119's fix, but weapon direction/recoil itself
  still has no log evidence — the next step is a hotrepl runtime check comparing the 20 Hz
  state-stream `LookPos` against what the clone renders, then decide whether weapon
  direction/recoil needs its own sync bit.
- #195 blueprint popup: cosmetic, game-local UI (the unlock fact itself is synced via
  `RecipeUnlock`); revisit with the online-UI pass.
- RestoreItem slot-conflict handling (#192 follow-up): ACCEPTED — the semantics are now
  explicit in `ItemStateCodec.RestoreItem`: an occupied target slot with a container loads the
  restored item into it (mirroring SaveSystem.cs:325), and an occupied slot with no container
  intentionally no-ops, leaving the item at the player position (the cross-run leak fix's
  accepted fallback).
- #120 nested container content movement (deferred): items moved inside a carried container
  still ride the 1 Hz snapshot (visible impact is zero until opening another player's
  inventory exists; a `[CharSync]` warning is already logged). When direct player interaction
  lands, design nested-content events instead of extending the snapshot.
- In-flight pickup reject friction: RESOLVED (2026-08-16, no protocol bump) — a pickup
  report that beats its spawn/drop registration now waits in `PendingPickupQueue` for a
  bounded 500 ms hold instead of being refused immediately. A registration that confirms
  the item settles the first queued claim through the normal accept-with-correction
  transfer (later queued claims lose with `ItemReject`), a registration that makes the
  claim a container content resolves it silently, and `PendingPickupPump` sends exactly
  one late `UnknownItem` reject when the hold expires. Obvious first-writer conflicts
  (the item already transferred to another guest) still reject immediately. The fake
  network's re-entrant flush was fixed along the way (handlers may not deliver a later-due
  frame into the middle of the current handler — the production poll-batch shape).
  See `docs/pickup-inflight-selfcheck.md`; 818 tests green.
- Picking up a generation-time item leaves the peer's own copy behind (low frequency,
  accepted): the id is assigned on drop/container-exit, not on the pickup path — revisit only
  if the duplicate becomes observable.
- #122 GameAdapter assembly (re-evaluated 2026-08-14): the pre-migration "collapse ~25 hand-wired
  fields to 1" is NOT a mechanical DI collapse — the hand-wired `new`s are state-belongs-to-its-owner
  (the domain objects own their state; they are not DI services), and the domain logic already sank
  out of the old AdapterDomain into ItemWorldSync/CharacterDataSync/etc. The coordinator stays a
  thin forwarder. Left only as a possible readability grouping of the ~40 constructor `new`s by
  domain — no mechanical factory, per the "no mechanical refactor" rule.
- RESOLVED (2026-08-16, ProtocolVersion 15): Heater cooker meat→steak conversion —
  one host-authoritative `ItemCook` event (NetMsg 92) carries the full cooked-steak
  capture; `HeaterCookPatch` lets the native `Heater.OnCollisionEnter2D` run on the
  host/solo side, verifies the created steak by the game's exact condition×0.3 +
  spawn-position fingerprint, claims the source destroy and stamps the steak before
  `Item.Start` so the generic hooks never decompose the conversion. Guests never cook
  locally (item-layer isolation + explicit prefix gate) and replay the conversion +
  Scald sound in one `RemoteApply` scope. See `docs/heater-cook-selfcheck.md`;
  863 tests green (L0 simulation + replay fossil + patch contract, no manual acceptance).
- RESOLVED (2026-08-16, no protocol bump): TutorialHandler claw double-give — the tutorial
  courses run per side, so every `objectToCreate` prop was created by BOTH host and guest and
  entered the shared item/entity domains under two ids (every player saw two copies). The claw
  creations are now marked `TutorialClawProp` inside a `TutorialClawSpawn` call-identity scope
  (`TutorialHandlerUpdatePatch` + `UtilsCreateTutorialPatch`) and the item/entity entry hooks
  leave them out of the shared domains: an item stays id-less until a player actually picks it
  up (the existing generation-item spawn-then-pickup flow takes over), a BuildingEntity stays
  per-player local, and both bind-target finders skip marked props so one player's pickup can
  never destroy another player's course object. Tutorial course state remains per-side by design;
  the claw 20 Hz flow todo stays open. See `docs/tutorial-claw-selfcheck.md`; 899 tests green
  (L0 reflection + static evidence, no manual acceptance).
- Trade domain #132: implemented — simulation coverage landed (`TradeSimulationTests`,
  `TradeStockMachineTests`); the acceptance only lacks a dual-side runtime pass.
- Building-entity damage persistence for late joiners: RESOLVED (2026-08-16,
  ProtocolVersion 11) — `BuildingEntityHealthRegistry` records the host's latest per-position
  health (local + remote damage/open paths), and `BuildingEntityHealthSnapshot` (NetMsg 88)
  backfills world entry / reconnect / 60 s resend; the guest writes the host health and marks
  deaths `RemoteEntityDeath` so no duplicate drop roll happens. See
  `docs/building-entity-health-selfcheck.md`; 767 tests green.
- Runtime random supply refresh (phase-1 memory, unresolved question): it is not verified
  whether any supplies spawn independently of world generation. If such a mechanism exists it
  needs host-authoritative spawn + broadcast (the #110 pattern); investigate before assuming.
- High-frequency small drops (shell casings etc.): observe message volume before optimizing —
  batch/rate-limit only if it actually hurts.
- RESOLVED (c6b7d92): Geyser replay duplicate report — the report now rides `TryRumble`'s
  verified idle→rumbling transition (TrapGeyserPatch), not `Activate`, and `OnTrapTriggered`
  drops any `RemoteApply` origin; a replay can no longer produce the natural-Activate echo.
  The earlier "add an event-origin marker only if the log noise matters" option is obsolete.
- RESOLVED (2026-08-16, docs-only): Openable keypad prefab mapping — a full
  serialized-asset sweep of every MonoBehaviour under the game data folder
  (2688 components) found exactly 17 `Openable` instances, all in
  `resources.assets`, and decoded the three serialized fields at raw offsets
  32/36/40 (`instantOpen` / `isKeypad` / `lockpickAnglePrecision`).
  `isKeypad = true` only on the `dropcapsule` prefab and on the two nested
  `dropcapsule` props inside `Structures/BrickLoot`; every other Openable is
  lockpick (`containercrate` 0.5, `medcrate` 1.25, `lifepodchest` 4.0) or
  instant-open (`foodbox`). The decode sanity-checks against the native
  semantics (`Openable.cs:8-32`, `KeypadMinigame.cs:76`, `LockpingMinigame.cs`,
  locale `v1.8.3.json`). See `docs/openable-keypad-prefabs-selfcheck.md`.
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
- RESOLVED (2026-08-16, ProtocolVersion 12): Block HP progressive sync — the live
  `BlockDamaged` relay now carries `MetalBonus` (the laser-vs-metallic ×10 multiplier applied
  identically everywhere), and `BlockDamageSnapshot` (NetMsg 89) backfills the host's accumulated
  `BlockDamage.damage` to late joiners/reconnects on world entry and the 60 s resend (absolute
  set — never an additive delta). See `docs/block-damage-progressive-selfcheck.md`; 778 tests
  green.
- RESOLVED (2026-08-16, ProtocolVersion 16): death-pose / limb / bleed / mining
  presentation-state sync — the remote clone now renders the owner's full limb wound state
  from the 1 Hz character snapshot (`CloneLimbRenderer` + the pure `LimbPresentation`
  formulas: brokenBone sprite, both-direction dismember toggle, all seven wound/bleed
  shader params, and the game's >0.95 fur-blood drip threshold). Every limb latch
  (BreakBone/MendBone/Dislocate/UnDislocate/Dismember) travels as a dedicated
  `LimbStateEvent` (NetMsg 93, ProtocolVersion 16) carrying the body's FULL post-event
  limb + health state — one operation = one message, exact rebuild, so Dismember's lower/
  connected-limb mutations and the body side effects never wait for the snapshot. The
  patches report verified false→true / true→false transitions only, the host merges the
  event into the saved character immediately, and the clone fact-table divergence monitor
  now watches broken/dismembered/dislocated. Rapid mining swings ride a rolling
  `EntityStateMsg.SwingSeq` beside the held `IsAttacking` flag, so every `ArmsSwing` clip
  replays even inside one held flag window; the first snapshot only seeds the sequence and
  the old-sender flag edge stays as fallback. The death/unconscious lying rule is extracted
  as the pure `LyingPose` machine. See `docs/limb-presentation-selfcheck.md`; 932 tests
  green (L0 simulation + reflective patch surface + static evidence, no manual acceptance).
  Accepted residuals: the clone's body-level FacialExpression latches (disfigured/eye
  sprites + the owner's disfiguredIndex) stay template-driven, and the underwater/downward
  fur-blood transfer branches are owner-side simulation — both recorded in the self-check.
- Accepted presentation gaps from the entity-domain memory (non-blocking; several are already
  recorded in `docs/event-replay-matrix.csv`): mine 0.8 s press visual, CrystalUnstable 5 s
  ticking, remote building-destruction particles, sound-cannon burst effect trigger-side-only,
  jump-pad light / turret tracer omissions, cactus self-damage HP local, the guest-side fluid
  water sound/push/slip gaps, and the fluid lightSprite flicker starting at the warning edge
  (the `didShoot` immediate-lock tradeoff). `LookTarget` gaze/startle and the Heater
  temperature field also stay local by design (accepted; only heater meat→steak conversion is
  an open item, listed under Item / entity). Re-open only as part of a deliberate presentation
  pass.
- RESOLVED (2026-08-16): configurable state-stream frequency — `[Sync] StateStreamHz`
  (1-60, default 20) drives both the player entity stream (host broadcast + guest
  report) and the enemy stream through `StateStreamOptions`; the attack-swing hold
  stays six stream ticks at any cadence. See `docs/config-options-selfcheck.md`.
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
- RESOLVED (2026-08-16, ProtocolVersion 14): CrystalMimic runtime spawn — the re-evaluation
  confirmed the spawned CrystalEnemy already rides `EntitySpawned` + `EnemySyncCoordinator`
  runtime binding and `EnemySnapshot.RuntimeSpawns` for late joiners; the missing one-shot latch
  now travels as `CrystalMimicTriggered` (EntityEventKind 30) with host apply / guest replay /
  late-joiner TrapStateSnapshot consumption. The same round fixed two event-channel family bugs:
  host-triggered one-shot consumptions are recorded for late joiners, and EntityEvent/
  EntitySpawned relays no longer double-broadcast (the adapter domain is the single relay owner).
  See `docs/crystal-mimic-selfcheck.md`; enemy SetColor remains a recorded presentation gap.
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

- RESOLVED (2026-08-16, no protocol bump): character data disk persistence —
  the host's per-SteamID saves now ride a versioned protobuf file
  (`CharacterDataFileStore`, atomic temp+replace writes) under
  `BepInEx/config/CasualtiesUnknownOnline.character-data.bin`. The table loads
  once at host construction (restart/continue-run restore), persists after
  every verified mutation (1 Hz report save + enemy bite/lunge/effect merges),
  survives `SessionEnded` as a disk copy while memory clears, and a NEW run
  writes an empty tombstone before deleting it. No same-process lazy reload,
  and restores are only sent while the host is InWorld — a menu handshake can
  never stage a previous run's save for the next run. Corrupt/unknown-version
  files degrade to empty with a warning, never a startup crash. See
  `docs/character-data-persistence-selfcheck.md`; 878 tests green (L0
  simulation, no manual acceptance).

## Config

- RESOLVED (2026-08-16): BepInEx `ConfigFile` → `IOptionsMonitor<T>` adapter landed
  (`BepInExOptionsMonitor<T>`, hot-reload via `ConfigFile.SettingChanged`) — Phase 4 Mod API
  triggered the 2026-08-09 decision. Standalone JSON still only when structured/nested/array
  config is needed.
- RESOLVED (2026-08-16): runtime logging levels — `[Logging] MinimumLevel` (default
  Information) is enforced by both providers (BepInEx + rolling file) and hot-reloads without
  rebuilding the logging factory. See `docs/config-options-selfcheck.md`.

## Tooling / testing

- RESOLVED (2026-08-16): replay corpus extension — the block-break and trade domains now
  have replay worlds, grammar, assertions and fossil files. BlockBreakReplayWorld drives the
  real BlockBreakArbitration + IItemControl surfaces (`airwrite`/`break` actions;
  `block_accepted` / `block_accepted_by` / `block_received` / `block_reject` /
  `block_registered` assertions); TradeReplayWorld drives the real TradeStockMachine through
  SimTraderHost (`trade` action with meet/purchase/give/haggle/threaten/hug/move;
  `trade_received` / `trade_converged` / `trade_rejected` assertions). Five new fossil files
  cover first-writer-wins + loser rollback, unattributed-break refusal, the full interaction
  sequence, a rejected purchase and a delayed three-action sequence; ReplayTests dispatches by
  exclusive domain actions and the SimTrace contract covers every world. 846 tests green.
- RESOLVED (2026-08-16): real-log → replay / SimTrace diff automation — the new
  `tools/compare-itemtrace.ps1` resolves a replay's generated `SimTraces/<file>.trace`
  (`-Refresh` re-runs the replay theory first), reads real logs plain or `.log.gz`, and
  normalizes both sides on the same begin-event/result/events surface as
  `extract-itemtrace.ps1`. Default subsequence matching finds the expected gesture battery
  inside a whole-session log and reports the original log-line span; `-Contiguous` / `-Strict`
  / `-NoBegins` cover windowed and result-only checks. Expected begin-without-end leaks always
  fail, real-log leaks warn by default and fail with `-FailOnLeak`. Nine end-to-end PowerShell
  contract tests + a matcher-mutation assertion-effectiveness proof landed; 887 tests green.
  See `docs/simtrace-diff-selfcheck.md`.
- Patch-contract same-name limitation: `PatchContractTests` identifies targets by name, so a
  same-name overload pair cannot be distinguished (the `LoadSceneAsync` case). Extend the
  contract only when a game update actually hits it.
- Block-break drop-race dual-side runtime pass: L0 simulation covers the first-writer-wins +
  loser-rollback arbitration, but the 2026-08-09 "two guests break the same block at the same
  time" runtime confirmation was never recorded — fold into the next dual-side verification.
- RESOLVED (2026-08-16): `docs/entity-features.md` status-column refresh — every narrative
  table now mirrors the CSV's `sync`/`path` cells (lifepod, unlocks, talker, crystal family,
  environment and creature rows included), and `EntityFeaturesDocConsistencyTests` runs in
  `dotnet test` to fail any future narrative/CSV drift instead of re-discovering it in review.

## Future phases / ecosystem

- Strict validation / anti-cheat hardening: explicitly LOW priority until the core feature set
  stabilizes (user mandate, KEY-archive) — do not schedule before the sync domains are complete.
- Phase 5 tooling & ecosystem: mod manager, auto-install, crash reports, host migration,
  dedicated server (AGENTS.md phase list).
- KrokMP compatibility adapter: reserved, API-level only — trigger: the native Phase 4 API
  stabilizes AND real migration demand exists (architecture.md §5.4). Not near-term.
