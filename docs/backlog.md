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
- 2026-08-23 exploration added candidate open work below: original CrystalTeleport/owner-local presentation gaps, KrokMP-inspired co-op features, and architecture/quality debt. See `docs/exploration-2026-08-23.md` for the evidence record.

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

### Exploration 2026-08-23 — original game-mechanic gaps

- **CrystalTeleport matrix coverage** — MEDIUM/LOW. `CrystalTeleport` is an original crystal effect not listed in the entity feature matrix and has no dedicated CUO handling. The resulting body state likely self-heals via the 20 Hz body stream; one-shot presentation is unverified. Next step: add a matrix row (covered-by-body-stream + local-only presentation, or a dedicated event). See §1.2.
- **Owner-local body auto-event presentation audit** — LOW/UNVERIFIED. Vomiter, SelfHarmer, PantSound, MoodChangeSounds and `usingSleepingBag` are not part of the clone presentation contract; verify whether clone-side components are suppressed before deciding whether replay is needed. See §1.3.

### Exploration 2026-08-23 — KrokMP-inspired co-op features

- **Trader Recruit (revive a dead player at a trader)** — HIGH. KrokMP allows recruiting a trader, after reputation/health gates, to respawn a dead player and give random trader items. CUO already has a strong host-authoritative trade domain but `TraderActionKind` has no Recruit action and no revive flow. Proposed: extend the trade domain with a host-authoritative Recruit action + minimal revive. See §2.1.
- **Revive/respawn rules** — HIGH. KrokMP has `Permadeath`, `ReviveOnNextLevel`, `ReviveFromTrader`, `RespawnKeepInventory`, `RespawnKeepSkills` and save/level-transition integration. CUO currently treats death as run-ending. Proposed: a host-authoritative revive lifecycle, separate from new-run reset. See §2.2.
- **Radiation line / straggler pressure** — HIGH. KrokMP starts the radiation line when enough players have reached the layer bottom and stragglers remain, synchronizes the line's world state, and applies body pressure to players caught above it. The world-state half is now closed (see the closed RadiationLine status above); the remaining work is host-side straggler detection / pressure rules. See §2.3.
- **Minimal host rules system** — MEDIUM/HIGH (design-level). KrokMP's broad rules struct should not be copied wholesale; start with a small independent host-rules service for high-value flags (PVP, auto-continue, late join, save inventory, revive-related). See §2.4.
- **Text chat** — MEDIUM/HIGH. CUO currently has in-world Talker bubbles only; a simple chat message + UI is the first clear communication feature. Voice stays later. See §2.5.
- **PVP** — MEDIUM/HIGH but complex. No player-to-player damage domain today; defer until PvE, rules, and accept-first arbitration are stable. See §2.6.
- **Other lower-priority KrokMP candidates** — voice, admin/kick/ban/vote, co-op keybinds, push/piggyback, status icons, richer player list. See §2.7.

### Exploration 2026-08-23 — architecture & quality debt

- **Partial-aware architecture gate** — HIGH. `tools/check-architecture.ps1` counts per file, so partial classes can hide a logical class far above 600 lines (examples: `ModService` ~1590, `GameAdapter` ~1364, `ItemService` ~928). Proposed: aggregate line counts/state bools by complete top-level type and require real responsibility splits. See §3.1.
- **NetMsg direction registry fail-closed** — HIGH. `PacketReceiver.IsValidDirection` is a manually maintained fail-open switch. Proposed: a single message registry carrying direction/reliability/payload, with unregistered messages dropped. See §3.2.
- **`HandlerContext` god-object** — MEDIUM. It injects many control planes and owns world-entry fan-out. Proposed: narrow per-domain handler dependencies and move world-entry fan-out to a dedicated service. See §3.3.
- **World-entry snapshot completion semantics** — MEDIUM. Late join sends multiple independent snapshots without an explicit complete-set marker. Proposed: completion marker or batched world-entry snapshot. See §3.4.
- **GameAdapter testability / concrete service dependencies** — MEDIUM. Adapter domain objects still depend on concrete `SessionService` and Unity statics. Proposed: narrow world/identity/spawn interfaces for injectable L0 simulation. See §3.5.
- **Log-level cleanup** — LOW/MEDIUM. High-frequency 1 Hz character/periodic sync paths log at Information; move to Debug/Verbose and keep one-shot/error events at their proper levels. See §3.6.

### Networking observability / optimization (new)

Measurement-first items; do not optimize before data exists.

- **State-stream bandwidth reduction** (only after the monitor shows need): candidates include fixed-point/quantized positions, per-entity update masks / delta encoding, and field-dirty batching for 20 Hz player/enemy streams and 1 Hz `CharacterDataMsg`. No change before measurement; gameplay and visual quality must not regress.
- **Snapshot size reduction** — full world-item / character-data snapshots are correctness-oriented; only optimize after the traffic monitor identifies a dominant family.

### Final acceptance (not development work)

- Trade domain #132 — dual-side runtime pass.
- World determinism / `[WorldFingerprint]` comparison.
- Block-break first-writer-wins dual-side runtime confirmation (L0 already covered).

### Contingency

- **Event-version numbers** — required before any snapshot stream switches to an unreliable channel, to prevent a stale snapshot rolling back an in-flight event.

## Future / low priority

- **Minecraft-style in-game command console** — a standalone, complete command chain (registration → parsing → permission → execution → feedback), NOT reusing the existing console and independent of the current host-command/mod-command surfaces. Low priority; recorded for future planning.
- Strict validation / anti-cheat hardening — explicitly low; defer until sync domains are stable.
- Phase 5 tooling & ecosystem: mod manager, auto-install, crash reports, host migration, dedicated server.
- KrokMP compatibility adapter — reserved; only after the native Mod API stabilizes and real migration demand exists.

## Architecture watchlist

Files at or near the 600-line gate should be split before the next feature lands in them:

`SessionService.cs` (580), `ItemApplication.cs` (576), `CharacterDataSync.cs` (563), `EnemySyncCoordinator.cs` (551), `EntitySyncService.cs` (548), `EnemyCombatDirector.cs` (547), `Plugin.cs` (534), `RunCoordinator.cs` (512), `PlayerInteractionService.cs` (511).

`WorldService.cs` and `ItemService.cs` were split into message-flow partials in the 2026-08-22 architecture cycle (each main file + new partial is now under the gate; see `docs/selfchecks/world-item-service-partial-split-selfcheck.md`).

`docs/tech-decisions.md` is also large; future landing entries should consider a domain-split index if it keeps growing.
