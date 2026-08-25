# CUO Backlog

Open work only. Landed delivery details are not duplicated here; they live in:

- [`docs/tech-decisions.md`](tech-decisions.md) — binding decisions / landing log
- [`docs/selfchecks/`](selfchecks/) — per-delivery fact sheets
- [`docs/item-features.md`](item-features.md) and [`docs/entity-features.md`](entity-features.md) — canonical feature sync matrices

> Condensed 2026-08-22: completed delivery detail is no longer repeated here; see the reference docs above.

## Status

- No open high-priority bugs.
- Native game-content sync coverage is complete: item and entity feature matrices currently have no `missing` rows; the last recorded `Heater`-on-`xaloris` local-presentation residual is closed as excluded by design, and the remote-clone disfigurement/eye-loss facial presentation residual is now closed (ProtocolVersion 32).
- **CustomItemBehaviour.data — liquidcentrifuge cooldown** closed: the 60 s use-gating cooldown now travels as a synthetic `cooldown` component field on the existing item-state paths; no protocol change. See `docs/selfchecks/custom-item-data-state-selfcheck.md`.
- **Dynamite lit-fuse presentation** closed: the 5 s pre-explosion child sprite + fuse audio on remote clones now rides a synthetic `fuse` component field on the existing item-state paths; no protocol change. See `docs/selfchecks/dynamite-fuse-presentation-selfcheck.md`.
- Network health metrics (per-peer RTT history / jitter / probe loss) now surface in `[NetworkHealth]` logs; per-peer bandwidth already surfaces in `[NetworkTraffic]` logs. See `docs/selfchecks/network-health-metrics-selfcheck.md`.
- Phase 4 Mod API **ReadGameState** landed as a read-only player-character projection (`IModGameState`); no wire/protocol change. See `docs/selfchecks/mod-game-state-selfcheck.md`.
- Phase 4 Mod API **entity spawn** landed as a permission-gated `IModEntitySpawn` surface reusing the runtime `EntitySpawned` channel; no wire/protocol change. See `docs/selfchecks/mod-entity-spawn-selfcheck.md`.
- Phase 4 Mod API **AccessNativeApi** landed as a curated read-only native operation registry (`IModNativeApi` + `local.player.state`); no wire/protocol change. See `docs/selfchecks/mod-native-api-selfcheck.md`.
- Remaining final dual-side acceptance items below are end-of-cycle acceptance, not development work.
- **RadiationLine world-state sync** closed: the host-authoritative `active` / `timeGone` state now travels as `RadiationLineState` (NetMsg 106, ProtocolVersion 33); guests still run the local per-frame line presentation/body effects between resends and re-align to the host's absolute state. World entry/reconnect fan-out includes the stored snapshot; guest local activation is suppressed. See `docs/selfchecks/radiation-line-state-sync-selfcheck.md` and `docs/tech-decisions.md` #55.
- **CrystalTeleport matrix coverage** closed: `CrystalTeleportTriggered` (EntityEventKind 33, ProtocolVersion 34) replays the 2D `observerlaugh` + `FlashBrief` on every side; the body teleport itself rides the existing 20 Hz player stream. Repeatable, no late-joiner replay. See `docs/selfchecks/crystal-teleport-sync-selfcheck.md` and `docs/tech-decisions.md` #56.
- **Owner-local body auto-event presentation audit** closed: `RemoteBodyFactory` now disables `Vomiter`/`SelfHarmer`/`PantSound`/`MoodChangeSounds`/`SleepingBagUse` on render clones — these `Update` methods are not covered by the `Body.Update`/`Limb.Update` render-proxy skips, and two of them read the local player's body. Owner-local by design. See `docs/selfchecks/owner-local-body-auto-events-selfcheck.md` and `docs/tech-decisions.md` #57.
- **RadiationLine straggler pressure** closed: the host now activates the line in co-op when at least one living player has reached the layer bottom and another living player remains above it; the existing `RadiationLineState` (NetMsg 106) world-state sync carries the activation to every side. No protocol change. See `docs/selfchecks/radiation-straggler-pressure-selfcheck.md` and `docs/tech-decisions.md` #58.
- **Log-level cleanup** closed: high-frequency periodic sync logs (1 Hz character snapshot/relay, fluid region stream send/apply, 5 s trader fallback snapshot) now log at Debug; one-shot/error events stay at Information/Warn/Error. See `docs/selfchecks/log-level-cleanup-selfcheck.md`.
- **IP direct connection (non-Steam transport)** closed: a second TCP transport/identity path lets players host/join directly by IP:port without Steam P2P (LAN, port-forward, VPN). The host is logical peer id 1, guests send a random transport hello, and the existing session/handshake/star-relay stack is reused unchanged. Custom display names are carried on handshake/player-join and rendered by the Online UI; a small top-left network HUD shows live RTT plus delayed session-status text. IP-direct and Steam sessions are separate modes and are not interconnected. No new NetMsg / protocol bump (additive protobuf fields only). See `docs/selfchecks/ip-direct-selfcheck.md` and `docs/tech-decisions.md` #82.
- **Trader Recruit (revive at a trader)** closed as the first co-op revive slice: a living player at a friendly, undamaged trader can revive a dead in-world teammate. Dedicated `TraderRecruitRequest`/`TraderRecruitResult` (NetMsg 107/108, ProtocolVersion 35), host-authoritative trade gates + one-use-per-trader, in-place health revive (no inventory wipe/position teleport). The random trader-stock bonus items slice (1–3 items, ProtocolVersion 37) is closed too; see `docs/selfchecks/trader-recruit-gift-items-selfcheck.md`. See `docs/selfchecks/trader-recruit-selfcheck.md` and `docs/tech-decisions.md` #59/#62.
- **Revive/respawn rules** closed: Permadeath, trader-revive permission, next-level auto-respawn, keep-inventory/keep-skills, save-persistence, and revived re-entry for players who already left the world now ride a host-authoritative `RespawnOptions`/`RespawnPolicy`/`RespawnCoordinator` rule set; no protocol change (ProtocolVersion 35). See `docs/selfchecks/respawn-rules-selfcheck.md` and `docs/tech-decisions.md` #60.
- **Text chat** closed: a simple host-relayed chat line (`ChatMsg`, NetMsg 109, ProtocolVersion 36) with a bounded Runtime recent-buffer and a bottom-right IMGUI panel; the host validates sender identity/text and relays to the other members. See `docs/selfchecks/chat-selfcheck.md` and `docs/tech-decisions.md` #61. **UI disabled 2026-08-23**: the bottom-right IMGUI chat panel is removed from the overlay (the input-focus/Tab/WASD conflict) pending a Minecraft-style command console; the Runtime chat channel/service and wire message remain for the future redo.
- **HandlerContext per-domain narrowing** closed: every packet handler now receives only the narrow capability interface it declares; `HandlerContext` remains the single dispatcher composition root but is no longer exposed to business handler code. No protocol change. See `docs/selfchecks/handler-context-narrowing-selfcheck.md` and `docs/tech-decisions.md` #73.
- **Minimal host rules system (first slice)** closed: a small stateless `HostRulesService`/`IHostRules` composes new host-only flags (`PvP`, auto-continue, late join) with the existing respawn rules, and `AllowLateJoin` is wired as a real handshake gate. No protocol/wire change; PVP and auto-continue remain reserved flags. See `docs/selfchecks/host-rules-selfcheck.md` and `docs/tech-decisions.md` #74.
- **Host ban** closed as the second admin slice: host-only persistent SteamID ban list (`HostBanService` + `HostBanFileStore`), dedicated `Banned` message (NetMsg 112, ProtocolVersion 40), handshake rejection before roster creation, and an Online UI `Ban` button next to `Kick`. `Unban` is exposed through the same service. See `docs/selfchecks/host-ban-selfcheck.md` and `docs/tech-decisions.md` #77.
- **Online UI window** closed: the old top-left IMGUI status/lobby/member dump is replaced by a tabbed CUO Online window (Home / Lobby / Players / Network / Admin) with a top-right launcher; member interaction eligibility moved into a tested Runtime projection. No protocol change. See `docs/selfchecks/online-ui-window-selfcheck.md` and `docs/tech-decisions.md` #78.
- **I18n framework** closed: a key-based `ILocalizationService` + `LocalizationCatalog` provides English/Simplified Chinese support through a hot-reloadable `[UI] Language` BepInEx config entry; all CUO Online UI strings are migrated to it. No protocol change. See `docs/selfchecks/i18n-framework-selfcheck.md` and `docs/tech-decisions.md` #79.
- **Online window modal input blocker** closed: while the CUO Online window is open, `OnlineMenuInputGuard` disables the game's custom `AdaptiveButton` input and adds transparent UGUI raycast blockers to active screen-space canvases, so clicks on the window's non-control areas no longer reach the menu/world behind it. No protocol change. See `docs/selfchecks/ui-modal-input-blocker-selfcheck.md` and `docs/tech-decisions.md` #80.
- **Lobby leave/close + host rules editor** closed: the Online UI now has a Leave Lobby / Close Room button, and the Admin page lets a host toggle the host/respawn rules through the same BepInEx config entries (no protocol change). See `docs/selfchecks/lobby-leave-host-rules-editor-selfcheck.md` and `docs/tech-decisions.md` #81.
- 2026-08-23 exploration added candidate open work below: KrokMP-inspired co-op features and architecture/quality debt. See `docs/exploration-2026-08-23.md` for the evidence record.

## Open work

### Phase 4 Mod API

No open development work in this area: the previously open `AccessNativeApi` is
now a live, permission-gated, Game Adapter-curated native operation registry
(the first slice is read-only local player state). See `docs/mod-api.md` §4i
and `docs/tech-decisions.md` #50.

Landed in this area (not duplicated here): mod entity spawn — `IModEntitySpawn`
(`context.EntitySpawn`) lets a state-bearing mod spawn a native `BuildingEntity`
prefab at runtime; replication reuses the existing `EntitySpawned` channel, no
protocol change. See `docs/mod-api.md` §4h and `docs/tech-decisions.md` #49.

### Item / entity known gaps

None open. The previous local-only item states and the enemy LookTarget presentation gap are closed:

- **AnimalDeath presentation on remote kills** — closed: `BuildingEntityUpdatePatch` now replays the creature-specific death effects (spider `gore`/`BloodExplosion`, crystal-enemy death sound/`CrystalDistort`, trader `gore`) for live remote deaths before destroying the entity; the attacker-side experience reward stays attacker-side. Late-joiner health snapshots do not replay creature-specific effects. No protocol change. See `docs/selfchecks/animal-death-presentation-selfcheck.md`.
- **GrapplingHook** `fired` / `hookLatched` / `pulling` — synced via the item component-state path; clone renderer presents the fired sprite (see `docs/item-features.md`).
- **WatchScript** timers / **AutoPump.worn** — excluded by design: owner-local body/UI effects only, and render-clone scripts are disabled.
- **Peer-view clone renderer** — the pure state-selection helper now has an L0 test face (`RemoteItemPresentationTests`).
- **Liquidcentrifuge cooldown** (`CustomItemBehaviour.data[0]`) — closed: the persistent 60 s use-gating cooldown now travels as a synthetic `cooldown` component field on the existing item-state paths; a one-frame reapply marker keeps it correct after `CustomItemBehaviour.Start` initializes the array on a fresh prefab. No protocol change. See `docs/selfchecks/custom-item-data-state-selfcheck.md`.
- **Dynamite lit-fuse presentation** (`CustomItemBehaviour.data[0]` bool) — closed: the 5 s clone child sprite + one-shot fuse audio now ride a synthetic `fuse` component field on the existing item-state paths; the detonation event is unchanged. No protocol change. See `docs/selfchecks/dynamite-fuse-presentation-selfcheck.md`.
- **Gun firing/racking state reports** — closed: the persistent `GunScript` transitions (fire/rack/safety/load/unload and Update-driven auto-rack steps) now report through the existing item-use fact path via `GunStateSync`; no protocol change, the 1 Hz character snapshot remains the fallback. See `docs/selfchecks/gun-state-sync-selfcheck.md`.
- **LookTarget gaze/scare** — closed via the 20 Hz player entity stream: `EntityStateMsg` now carries the owner's `LookTarget`/`CorpseScript` override gaze + the eye face timers (`eyeScareTime`/`eyePanicTime`/`eyeCloseTime`), and the remote clone writes them into its proxy Body (see `docs/tech-decisions.md` #44).
- **Heater temperature field on `xaloris`** — closed as **excluded by design**: `Heater.OnWillRenderObject` writes only the local player's body temperature, which already rides the 1 Hz character stream, so no enemy-sync surface is needed (see `docs/selfchecks/heater-xaloris-local-body-effect-selfcheck.md`).
- **Remote-clone FacialExpression disfigurement/eye-loss latches** — closed via the 1 Hz `CharacterHealthMsg` + `CloneFacePresentation`: body latches (`Disfigured`/`EyeGone`/`BothEyesGone`), the owner's random disfigurement head index, and the long-run heal presentation timers are now applied to the render clone; `ProtocolVersion` 32. See `docs/selfchecks/clone-face-presentation-selfcheck.md`.

### Sync two-layer audit (2026-08-23)

A read-only audit checked every major game domain against the two-layer rule
(dedicated event for discrete triggers + periodic/entry snapshot as fallback).
Item/entity matrices have zero `missing` rows. Recorded observations:

- **Player death has no dedicated event** — `alive=false`/`conscious=false`
  currently ride the 20 Hz `PlayerState` stream and the final 1 Hz
  `CharacterData` snapshot. If death is treated as a discrete terminal trigger,
  a dedicated death/health event would be the strict two-layer shape.
- **Attack-swing visual still rides the periodic stream** — the exact swing
  audio is a dedicated `CharacterSound` event, but the visible `ArmsSwing`
  replay is driven by `SwingSeq`/`IsAttacking` in the 20 Hz entity stream. This
  is a recorded deviation (§29); either accept it as a presentation exception
  or add a dedicated swing event later. The separate one-shot `attackAnim`
  prefab gap is listed under the animation audit below.
- **Speech/chat are event-only, no periodic fallback** — intentional for
  transient lines; late joiners should not receive old bubbles/chat.
- **Tutorial claw / radiation line are stream-only domains** — continuous
  presentation/state, not discrete user triggers; per-side course state is
  already a design boundary.
- **Mod messages/commands, admin kick/ban, and UI are event-only** —
  request/response or one-shot session control, no snapshot needed.
- **Minor: `TrapLayoutSnapshot` has no periodic resend** — it is only in the
  world-entry snapshot group; covered by `WorldSnapshotComplete`, low risk.

### Animation / presentation sync audit (2026-08-23)

Read-only audit of the decompiled animation/visual triggers against CUO sync
paths. The item/entity matrices remain clean, but several transient animation
presentations still have no dedicated event or periodic field:

- **Player attack `attackAnim` prefab — CLOSED (2026-08-23)**.
  `Body.Attack` instantiates `ClawAnim` / `SwingAnim` / `LaserAnim`
  (`Body.cs:1913-1920`); the exact prefab + facing + attack direction now travel
  as one dedicated reliable `CharacterAttackAnimMsg` (NetMsg 113,
  ProtocolVersion 41) and every peer replays the same visual on the owner's
  render clone. `ArmsSwing` and the swing sound continue to ride their existing
  paths.
- **Direct placeable-item `ArmsSwing` — CLOSED (2026-08-23)**. Successful
  `scrapmetal` / `climbingrope` / `scaffoldingpack` placements now report through
  the existing `OnArmSwing` / 20 Hz swing stream; no protocol change.
- **Workout/exercise animations — CLOSED (2026-08-23)**. The active
  `Body.DoWorkout` type now rides `EntityStateMsg.WorkoutType`
  (ProtocolVersion 42); each peer replays the matching pushup/squat/plank
  clip set on the owner's clone. See `docs/selfchecks/workout-animation-sync-selfcheck.md`
  and `docs/tech-decisions.md` #87.
- **Alt-nap and water-shake variants — CLOSED (2026-08-23)**.
  `Body.NapCoroutine`/`AltNapCoroutine` variant and `Body.dogShakeIntensity`
  now ride `EntityStateMsg.NapVariant` / `DogShakeIntensity`
  (ProtocolVersion 43); each peer replays the matching lay-down clip pair and
  shake on the owner's clone. See `docs/selfchecks/nap-and-dog-shake-sync-selfcheck.md`
  and `docs/tech-decisions.md` #88.
- **Wall-slide / landing presentation — CLOSED (2026-08-23)**: the owner's
  wall-slide direction rides `EntityStateMsg` extended flags, and each landing
  is a dedicated `CharacterLandingVisual` event (NetMsg 114, ProtocolVersion 44)
  carrying the native Grounded clip + DustSmall/DustBig facts to the owner's
  clone. See `docs/selfchecks/wall-slide-landing-sync-selfcheck.md` and
  `docs/tech-decisions.md` #90.
- **Gun muzzle-flash particle — CLOSED (2026-08-23)**. The existing
  `CharacterSoundKind.GunFire` event now also replays the source's
  `muzzleParticle.Play()` on the owner's clone through `MuzzleFlashReplay`
  (nearest clone gun to the reported fire position). No wire/protocol change.
  See `docs/selfchecks/muzzle-flash-sync-selfcheck.md` and
  `docs/tech-decisions.md` #89.
- **Spider leg IK/crawl — CLOSED (2026-08-23)**: host `IKHandle.targetPos`
  positions now ride `EnemyStateMsg.SpiderLegTargets` (ProtocolVersion 45) and
  frozen copies mirror them; see `docs/selfchecks/spider-enemy-presentation-sync-selfcheck.md`.
- **Spider bite `ClawAnim` — CLOSED (2026-08-23)**: host-ordered remote bites
  now replay the one-shot claw on the host view and the victim via
  `SpiderClawReplay`; no protocol change beyond the leg field above.
- **Crystal wind-up/telegraph line — CLOSED (2026-08-24)**.
  `CrystalEnemy.Update`'s pre-lunge line (`CrystalEnemy.cs:66-90`) now rides
  `EnemyStateMsg.CrystalWindupAmount` / `CrystalLineEnd`
  (ProtocolVersion 47); frozen copies replay the host's native fade/width on
  their `LineRenderer`. See
  `docs/selfchecks/crystal-windup-telegraph-sync-selfcheck.md` and
  `docs/tech-decisions.md` #92.
- **Trader hostile `Swing()` attackAnimation — CLOSED (2026-08-24)**.
  `TraderScript.Swing`'s attackAnimation + swing sound now travel as one
  dedicated reliable `TraderSwingMsg` (NetMsg 115, ProtocolVersion 47) with
  star semantics; every other member replays the exact visual on its
  same-position trader. See
  `docs/selfchecks/trader-swing-sync-selfcheck.md` and
  `docs/tech-decisions.md` #93.
- **Coroutine/shake body states — NOT_SYNCED/OWNER_LOCAL**: `FurExplode`,
  brain-damage ragdoll shake, `specialCrying`, and underwater/waterdrip
  particle branches remain owner-side/local.

The player attack-anim, direct placeable-item, workout, nap/dog-shake,
muzzle-flash, wall-slide/landing, spider leg/claw, crystal wind-up/telegraph
and trader hostile swing rows above are now closed; the remaining line is the
owner-local coroutine/shake group, intentionally not a shared presentation
surface.

### Exploration 2026-08-23 — KrokMP-inspired co-op features

- **Trader Recruit (revive a dead player at a trader)** — **CLOSED (first slice, 2026-08-23)**. The trader-recruit revive slice is landed as dedicated `TraderRecruitRequest`/`TraderRecruitResult` (NetMsg 107/108, ProtocolVersion 35) with host-authoritative trade gates and in-place health revival. **Random trader-stock bonus items are CLOSED (2026-08-23)**: a successful recruit grants the revived player 1–3 distinct items drawn from the host trader's current stock, delivered through `TraderRecruitResult.Items` (ProtocolVersion 37). The stock is treated as a catalog (not depleted); the existing one-use-per-trader guard prevents repeat farming. See `docs/selfchecks/trader-recruit-gift-items-selfcheck.md` and `docs/tech-decisions.md` #62.
- **Revive/respawn rules** — CLOSED (2026-08-23). The broader lifecycle is now a host-authoritative rule set (`RespawnOptions` + `RespawnPolicy`): Permadeath, trader-revive permission, next-level auto-respawn, keep-inventory/keep-skills, save-persistence and revived re-entry for players who already left the world. The trader-recruit first slice remains the heal-in-place path; the next-level auto-respawn uses the full character restore path so the keep flags really wipe/reset when disabled. No protocol change (ProtocolVersion 35). See `docs/selfchecks/respawn-rules-selfcheck.md` and `docs/tech-decisions.md` #60.
- **Minimal host rules system** — **CLOSED (first slice, 2026-08-23)**. A small independent host-rules service landed: `HostRulesOptions` + `HostRulesService`/`IHostRules` compose PVP, auto-continue, late-join, save-inventory and revive-related flags, and `AllowLateJoin` is wired as an actual handshake gate (new members are rejected when the host is in-world and late join is disabled). PVP remains reserved until the damage domain exists (§2.6); auto-continue is surfaced but not wired yet. No wire/protocol change. See `docs/selfchecks/host-rules-selfcheck.md` and `docs/tech-decisions.md` #74.
- **Text chat** — **CLOSED (2026-08-23)**. A simple host-relayed chat line landed as `ChatMsg` (NetMsg 109, ProtocolVersion 36) with a bounded recent-buffer (`ChatService`) and a bottom-right IMGUI panel; the host validates sender identity/text and relays to the other members. Voice stays later. See `docs/selfchecks/chat-selfcheck.md` and `docs/tech-decisions.md` #61.
- **Host kick (first admin slice)** — **CLOSED (2026-08-23)**. The host can remove a guest from the session with a dedicated `Kicked` message (NetMsg 111, ProtocolVersion 39); the target tears its session down, the host's existing member-removal path cleans up the remaining peers. As a small adjacent polish item, the Online UI member list now shows each member's own RTT. Remaining admin/ban/vote slices stay lower priority. See `docs/selfchecks/host-kick-selfcheck.md` and `docs/tech-decisions.md` #76.
- **Host ban (second admin slice)** — **CLOSED (2026-08-23)**. The host can permanently reject a guest SteamID with a dedicated `Banned` message (NetMsg 112, ProtocolVersion 40); the ban list is persisted by `HostBanService` + `HostBanFileStore`, `HandshakeHandler` rejects the SteamID before roster creation, and `Unban` is exposed through the same service. See `docs/selfchecks/host-ban-selfcheck.md` and `docs/tech-decisions.md` #77.
- **PVP** — MEDIUM/HIGH but complex. No player-to-player damage domain today; defer until PvE, rules, and accept-first arbitration are stable. See §2.6.
- **Player nameplates / off-screen indicators / player colors** — **CLOSED (2026-08-24)**. Off-screen arrows now show distance (world units = metres), and each SteamId has a stable local deterministic marker color. No wire/protocol change. See `docs/selfchecks/online-ui-player-awareness-selfcheck.md` and `docs/tech-decisions.md` #94.
- **In-world right-click player interaction menu** — **CLOSED (2026-08-23)**. Right-clicking near a remote player now opens a context menu reusing the Players-page action eligibility (Carry/Drop/Heal/Recruit/Take) plus an always-available "View items" fallback that opens the Online window Players page and expands that member. It uses authoritative entity positions instead of the remote clones' disabled colliders. No protocol change. See `docs/selfchecks/character-attack-anim-and-player-context-menu-selfcheck.md` and `docs/tech-decisions.md` #84.
- **Cross-player item use (give/feed/drink/wear/use an item on another player)** — **CLOSED (first slice + medicine + topical + opiates + component medicine + limb-tool + component-bearing tools + wear, 2026-08-25)**. Host-authoritative `PlayerItemUseRequest`/`PlayerItemUseResult` (NetMsg 116/117, ProtocolVersion 48) now covers drink/food, curated injectables, topical `ApplyToLimb`, opiates/antagonists, component medicine (`analgesicgauze` opiate component), non-liquid limb tools, component-bearing limb tools, and native wearable placement. The additive `WornItem` result field carries the worn item to the target; no protocol bump. Remaining future slices: timed/random medicine, minigame-random tools (tweezers), timed tools (medicalsuture). See `docs/tech-decisions.md` #96/#98/#100/#102/#103/#105/#106/#107.
- **Overlapping remote-player target disambiguation in in-world UI** — **CLOSED (2026-08-24)**. The right-click hit-test now returns every remote inside the radius (nearest first) and the context menu shows a target selector when several overlap. No wire/protocol change. See `docs/selfchecks/online-ui-player-awareness-selfcheck.md` and `docs/tech-decisions.md` #94.
- **Piggyback (conscious-alive ride) + carried-player release** — **CLOSED (2026-08-24)**. The existing cross-player carry relation now accepts a conscious/alive target as a piggyback ride (`PlayerCarryStartRequestMsg.Piggyback`, additive; no NetMsg/protocol bump); the carried player can also request release ("Get down"). Same `PlayerCarryStateMsg`/`CarriedBodyDriver` presentation, no second sync surface. See `docs/selfchecks/piggyback-releasable-carry-selfcheck.md` and `docs/tech-decisions.md` #97.
- **Push/shove (cross-player)** — **CLOSED (2026-08-24)**. Host-authoritative `PlayerPushRequest`/`PlayerPushResult` (NetMsg 118/119, ProtocolVersion 49): the host validates in-world/standing/cooldown/distance/carry, computes KrokMP-style strength from the pusher's Strength, and broadcasts one force delta; the target's own client applies the native ragdoll+velocity, the pusher pays the stamina/heat cost, and every side replays the `landsmall1` sound. The 20 Hz player state stream remains the motion fallback. Players page and in-world right-click menu expose `Push`. See `docs/selfchecks/player-push-selfcheck.md` and `docs/tech-decisions.md` #99.
- **Other lower-priority KrokMP candidates** — voice, vote-kick, and any remaining player-list polish. **Co-op keybinds and status icons closed (2026-08-25)**; ban is closed as the second admin slice; piggyback and push are closed above. See §2.7.

### Exploration 2026-08-23 — architecture & quality debt

- **Partial-aware architecture gate** — **CLOSED (2026-08-23)**. `tools/check-architecture.ps1` now aggregates line counts and expression-state bools by complete top-level type across partial files, and refuses unrecorded debt or any growth beyond the recorded debt ledger (`docs/architecture-debt.json`). `-Strict` fails even on recorded debt once the mountain is flattened. The first real split landed: `WorldEventSync`'s building-entity half moved to its own `WorldBuildingEntitySync` (aggregate 643 → under 600, removed from the debt ledger). See `docs/selfchecks/partial-aware-gate-selfcheck.md` and `docs/tech-decisions.md` #65.
- **Large logical class debt flattening** — **CLOSED (2026-08-23)**. The recorded logical debt is now empty: `ModService` (1590) and `GameAdapter` (1397) were the last two, both split into real top-level responsibilities with all physical partials deleted. `ModService` became a 98-line facade + `ModLifecycle` / `ModCommandService` / `ModStateStore` / `ModContext` plus `ModCatalog` / `ModPermissionGate` / `ModSessionSnapshot`. `GameAdapter` became a 299-line facade + `GameAdapterDomains` / `GameAdapterBridge` / `GameAdapterSessionBinding` / `PlayerInteractionApply`. Earlier flattening this cycle: `PlayerInteractionService` (716), `ItemApplication` (630), `EnemySyncCoordinator` (750), `WorldService` (899), `ItemService` (928). **No recorded aggregate debt remains in `docs/architecture-debt.json`.** See `docs/selfchecks/player-interaction-service-split-selfcheck.md`, `docs/selfchecks/item-cook-replay-split-selfcheck.md`, `docs/selfchecks/enemy-combat-replay-split-selfcheck.md`, `docs/selfchecks/world-service-split-selfcheck.md`, `docs/selfchecks/item-service-split-selfcheck.md`, `docs/selfchecks/mod-service-split-selfcheck.md`, `docs/selfchecks/game-adapter-split-selfcheck.md`, `docs/tech-decisions.md` #66/#67/#68/#69/#70/#71/#72 and §3.1.
- **NetMsg direction registry fail-closed** — **CLOSED (2026-08-23)**. The old manually maintained fail-open switch is gone. Every `[PacketHandler]` now carries an explicit `NetMessageDirection`; `NetMessageRegistry` (built once from all Runtime handlers) carries direction + payload type and is read by `PacketReceiver` (unknown ids dropped), `PacketSender` (unknown sends refused) and `PacketDispatcher` (startup consistency). Reliability is deliberately not stored as a single boolean because several messages are sent both reliably and unreliably depending on the path (e.g. `ItemSnapshot` one-shot reliable vs periodic unreliable); it remains a send-call-site decision. See `docs/selfchecks/netmsg-registry-selfcheck.md` and `docs/tech-decisions.md` #63.
- **`HandlerContext` god-object** — **CLOSED (2026-08-23)**. The world-entry fan-out half already moved to `WorldEntryFanout`; this cycle completes the remaining narrowing: every packet handler now receives only the capability interface it needs (`PacketHandlerBase<TPacket, TContext>`), and `HandlerContext` remains the single internal composition root at the dispatch seam. No protocol change. See `docs/selfchecks/handler-context-narrowing-selfcheck.md` and `docs/tech-decisions.md` #73.
- **World-entry snapshot completion semantics** — **CLOSED (2026-08-23)**. The world-entry fan-out now owns an ordered snapshot group + explicit completion marker: `WorldEntryFanout` sends the full group and then `WorldSnapshotComplete` (NetMsg 110, ProtocolVersion 38); the guest raises `WorldSnapshotCompleteReceived`, so a receiver can distinguish full authoritative backfill from partial best-effort state. See `docs/selfchecks/world-entry-completion-selfcheck.md` and `docs/tech-decisions.md` #64.
- **GameAdapter testability / concrete service dependencies** — concrete Runtime-service dependency slice **CLOSED (2026-08-23)**; the Unity seam remains open. Every GameAdapter domain object now composes against `ISessionControl` / `IWorldControl` / `IItemControl` / `IEntitySyncControl` / `ICharacterDataControl` / `IPlayerInteractionControl` instead of the concrete `SessionService` / `WorldService` / `ItemService` / `EntitySyncService` / `CharacterDataStore` / `PlayerInteractionService`, with the adapter-facing missing members added to those interfaces. No wire/protocol change. Remaining: Unity statics / object-lookup / spawn-factory seam for a true L0 adapter harness (see §3.5). See `docs/selfchecks/adapter-control-surfaces-selfcheck.md` and `docs/tech-decisions.md` #75.

### Networking observability / optimization (new)

Measurement-first items; do not optimize before data exists.

- **State-stream bandwidth reduction** (only after the monitor shows need): candidates include fixed-point/quantized positions, per-entity update masks / delta encoding, and field-dirty batching for 20 Hz player/enemy streams and 1 Hz `CharacterDataMsg`. No change before measurement; gameplay and visual quality must not regress.
- **Snapshot size reduction** — full world-item / character-data snapshots are correctness-oriented; only optimize after the traffic monitor identifies a dominant family.

### Networking / transport candidate

- **IP direct connection (non-Steam transport)** — **CLOSED (2026-08-23)**. TCP IP-direct host/join by IP:port, custom display name, separate non-interconnected mode, and a top-left RTT/status HUD are landed. See `docs/selfchecks/ip-direct-selfcheck.md` and `docs/tech-decisions.md` #82.

### Configuration / preferences

- **Custom configuration template system** — OPEN. When the multiplayer preference/config items grow, provide a reusable template system so players can easily edit and switch between full configuration profiles (e.g. log level, language, display/nameplate/color preferences, IP-direct/network settings). The default/built-in configuration should also be editable and savable as a template, not a read-only preset. Intended to build on the existing Preferences page and BepInEx config-backed options.
- **Custom run-settings range broadening for co-op** — **CLOSED (2026-08-24)**. The host can widen native run-settings slider ranges in co-op via `[HostRules] WidenRunSettings`; scalable tuning maxes scale with lobby size and selected values still ride the existing world-start params. No protocol change. See `docs/selfchecks/run-settings-range-selfcheck.md` and `docs/tech-decisions.md` #95.
- **Dedicated standalone player-interaction UI** — OPEN (design). Player interactions currently live in the Online window Players page rows plus the in-world right-click context menu; evaluate whether a dedicated compact interaction panel (target + available actions + immediate result) is a better fit for frequent co-op actions than requiring the full Online window. Decide before implementing; no code change yet.

### Performance profiling / instrumentation

- **Hot-path latency instrumentation** — **CLOSED (2026-08-25)**. An opt-in `[Diagnostics]` config pair (default off) drives `LatencyInstrumentation`, which times the compute-heavy Game Adapter pumps (`Run`, `WorldTime`, `StartGate`, `Respawn`, `ItemPosition`, `WorldEvent`, `Fluid`, `Trader`, `Renderer`, `EnemySync`, `EnemyCombat`) and emits aggregated `[Latency]` call/total/avg/max lines on a hot-reloadable interval. No wire/protocol change. See `docs/selfchecks/hot-path-latency-instrumentation-selfcheck.md` and `docs/tech-decisions.md` #104.

### Final acceptance (not development work)

- Trade domain #132 — dual-side runtime pass.
- World determinism / `[WorldFingerprint]` comparison.
- Block-break first-writer-wins dual-side runtime confirmation (L0 already covered).

### Contingency

- **Event-version numbers** — required before any snapshot stream switches to an unreliable channel, to prevent a stale snapshot rolling back an in-flight event.

## Open decisions (no code change yet)

- **World-time adjustability / sleep acceleration policy** — currently the host and guests can both request `Fast` / `SuperFast`, and the host applies an all-unconscious sleep acceleration. Gameplay-wise this works, but the design is open for debate: either disallow manual time acceleration, or adopt a Minecraft-style "only when all players sleep" cooperative acceleration. No change made; record only for future design.

## Future / low priority

- **Minecraft-style in-game command console** — a standalone, complete command chain (registration → parsing → permission → execution → feedback), NOT reusing the existing console and independent of the current host-command/mod-command surfaces. Low priority; recorded for future planning. The current bottom-right text-chat UI is disabled in favor of this eventual command-style input surface.
- Strict validation / anti-cheat hardening — explicitly low; defer until sync domains are stable.
- Phase 5 tooling & ecosystem: mod manager, auto-install, crash reports, host migration, dedicated server.
- KrokMP compatibility adapter — reserved; only after the native Mod API stabilizes and real migration demand exists.

## Architecture watchlist

Files at or near the 600-line gate should be split before the next feature lands in them:

`SessionService.cs` (580), `ItemApplication.cs` (576), `CharacterDataSync.cs` (563), `EntitySyncService.cs` (548), `EnemyCombatDirector.cs` (547), `Plugin.cs` (522), `RunCoordinator.cs` (512).

`WorldService.cs` and `ItemService.cs` were further flattened in this cycle into real top-level classes: `WorldStateMessageService` + `WorldChannelRelay`, and `ItemMessageFlowService` + `ItemPendingPickupArbiter`; both were removed from the debt ledger.

`docs/tech-decisions.md` is also large; future landing entries should consider a domain-split index if it keeps growing.
