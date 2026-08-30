# CUO Backlog

Open work only. Landed delivery details are not duplicated here; they live in:

- [`docs/tech-decisions.md`](tech-decisions.md) — binding decisions / landing log
- [`docs/selfchecks/`](selfchecks/) — per-delivery fact sheets
- [`docs/item-features.md`](item-features.md) and [`docs/entity-features.md`](entity-features.md) — canonical feature sync matrices

## Status

- **One open bug report (2026-08-27)** — see "Open bug" below. It is not to be closed by a paper-only claim.
- The 2026-08-27 remote-backpack container take and drag-escape bugs are closed (see below). Open work below is feature/decision/acceptance work only.
- Native game-content sync coverage is complete: item and entity feature matrices currently have no `missing` rows.
- **Host body orientation after piggyback Drop — CLOSED (2026-08-27).** CUO now keeps `Body.isRight` and `transform.localScale.x` in lockstep through a shared `BodyFacing` rule on every CUO-facing write (carried local body, render proxy, carrier-side clone override), and the release restore re-applies it before native simulation resumes. See `docs/selfchecks/piggyback-facing-restore-selfcheck.md` and `docs/tech-decisions.md` #121.
- **Remote backpack container take — CLOSED (2026-08-27).** The cross-player take authority is now recursive through container `Contents`, the native remote-backpack drag surface sends the same host take request, and the custom inventory tree has Take buttons at every container depth. Display-proxy drag-loop mutations and the local-body radial re-anchor are isolated while the remote view is open. See `docs/selfchecks/remote-backpack-container-take-selfcheck.md` and `docs/tech-decisions.md` #122.
- **Remote container destroy authority — CLOSED (2026-08-27).** The remote-backpack display-proxy destroys no longer reach the host as real item destroys, and a received destroy can no longer kill a carried (non-world) item. The host also validates destroy ownership before relaying. See `docs/selfchecks/remote-container-destroy-authority-selfcheck.md` and `docs/tech-decisions.md` #120.
- **Ragdoll stale-state / clone-creation race — CLOSED (2026-08-27).** The reliable `CharacterRagdoll` one-shot is now guarded against a lagging `Standing=true` 20 Hz snapshot and is queued until the owner's render clone exists. See `docs/selfchecks/ragdoll-stale-state-fix-selfcheck.md` and `docs/tech-decisions.md` #119.
- **World bleeding effects sync — CLOSED (2026-08-26).** The visible blood decals a player leaves in the world now travel as a dedicated `WorldBloodSpawn` event (NetMsg 121, ProtocolVersion 51); every peer replays the same transient ground/wall decal. Remote render clones no longer create their own duplicate decals. See `docs/selfchecks/world-blood-spawn-sync-selfcheck.md` and `docs/tech-decisions.md` #115.
- **Online UI scoped anti-passthrough + transport-mode exclusivity — CLOSED (2026-08-26).** The quick panel and right-click context menu now get scoped UGUI raycast blockers limited to their own rectangles, and the Home page shows only the selected Steam or IP-direct transport section at a time. See `docs/selfchecks/online-ui-scoped-passthrough-selfcheck.md` and `docs/tech-decisions.md` #116.
- **Remote-player inventory UI follow-up — CLOSED (2026-08-26).** Remote inventory has an "Open backpack" path that reuses the game's native radial backpack UI focused on the remote player's render clone (display-only proxies are never mutated; cross-player operations go through the host). The Custom UI remains as a text detail fallback and the recursive container collapsibles, and `[HostRules] AllowRemoteInventoryTake` controls the cross-player take operation. See `docs/selfchecks/remote-inventory-ui-followup-selfcheck.md` and `docs/tech-decisions.md` #117.
- **LifePod shuttle-door trigger sound — CLOSED (2026-08-26).** The earlier fix only added `shuttleNotice` to the host executor; the guest live-replay path still skipped the collision-only trigger sound. `TrapVisualReplay.ReplayShuttleDoor` now replays it for live relays (elapsed == 0), while late-joiner snapshots still jump to the current state without replaying old sounds. See `docs/selfchecks/native-remote-backpack-and-door-sound-selfcheck.md` and `docs/tech-decisions.md` #118.
- **Remote-backpack drag escape / duplicate bottle — CLOSED (2026-08-27).** Closing the remote backpack while a display-proxy drag was held no longer leaves the proxy to be released into the local backpack. The proxy drag is cancelled when the remote view closes and again at release time if it was not consumed by the remote-take path; local character capture also skips `RemoteCloneRender` items as a last-line authority guard. See `docs/selfchecks/remote-backpack-drag-escape-selfcheck.md` and `docs/tech-decisions.md` #123.
- **Player-interaction line-of-sight / direct-visibility validation — CLOSED (2026-08-27).** Direct interactions now share a world-backed LOS gate; no wire change. See `docs/selfchecks/player-interaction-visibility-selfcheck.md` and `docs/tech-decisions.md` #124.
- **Legacy F7/F8/F9 session hotkeys — CLOSED (2026-08-27).** The visual Online UI now owns create/join; the Network page gained a manual Ping button, and the `[Session] CreateLobbyKey / JoinLobbyKey / PingPeerKey / TargetLobbyId` surface was retired. The F6 quick-panel hotkey remains configurable. See `docs/tech-decisions.md` #125.
- **Architecture evolution Phase B — Items authority — CLOSED (2026-08-28).** The kernel now owns persistent item facts; legacy tables are projections; native-operation and capability surfaces are introduced; temporary item checkpoint exists. Phase C (protocol/save switch) is the next architecture-evolution phase. See `docs/selfchecks/phase-b-item-authority-selfcheck.md` and `docs/tech-decisions.md` #127.

## Open bug (2026-08-27)

### Host closing the lobby exits the game and can destroy the run save

- **Reported**: 2026-08-27.
- **Observed**: when the host closes the multiplayer lobby, the game exits instead of returning to a safe menu/lobby state. A brief network hiccup or a host wanting to recreate the room therefore ends the whole process.
- **Impact**: destructive UX for the host; combined with the save authority model this can effectively invalidate/lose the run's save/state. The host should be able to leave/close the session without force-quitting the game, and the save should be preserved or explicitly managed.
- **Related surfaces**: session/lobby teardown, host lifecycle (`LobbyLeft` / session end), Steam lobby ownership, save authority/run state, `GameAdapter` / `Plugin` shutdown path.

## Open work

### Items / crafting

- **KrokMP crafting loses container contents — OPEN (2026-08-30).** In
  KrokMP, crafting a recipe from an item that already contains contents (for
  example a flashlight with a battery used to craft a headlamp, or a liquid
  container used to craft another container) drops the contained items. Two
  directions are recorded for a future fix:
  1. **Exclude contained items from crafting** — simpler and closest to
     player intuition; the recipe refuses to consume an item with contents
     until the player empties it.
  2. **Inherit contents after crafting** — preserves contents across the
     recipe, but is more complex; special cases include liquid capacity
     changes and overflow when a smaller container replaces a larger one.

### Player interaction / UI

- **PVP** — LOW (reprioritized). No player-to-player damage domain today; defer until PvE, rules, and accept-first arbitration are stable.
- **Other lower-priority KrokMP candidates** — voice, vote-kick, and remaining player-list polish.

### Configuration

- **Custom configuration template system** — provide a reusable template system for full configuration profiles (log level, language, display/nameplate/color preferences, IP-direct/network settings). The default/built-in config should also be editable and savable as a template.

### Networking observability / optimization

Measurement-first items; do not optimize before data exists.

- **State-stream bandwidth reduction** — candidates include fixed-point/quantized positions, per-entity update masks / delta encoding, field-dirty batching for 20 Hz player/enemy streams and 1 Hz `CharacterDataMsg`. No change before measurement.
- **Snapshot size reduction** — full world-item / character-data snapshots are correctness-oriented; only optimize after the traffic monitor identifies a dominant family.

### Final acceptance (not development work)

- Trade domain #132 — dual-side runtime pass.
- World determinism / `[WorldFingerprint]` comparison.
- Block-break first-writer-wins dual-side runtime confirmation (L0 already covered).

### Contingency

- **Event-version numbers** — required before any snapshot stream switches to an unreliable channel, to prevent a stale snapshot rolling back an in-flight event.

## Open decisions (no code change yet)

- **World-time adjustability / sleep acceleration policy** — currently both host and guests can request `Fast` / `SuperFast`, and the host applies all-unconscious sleep acceleration. Design is open for debate: disallow manual time acceleration, or adopt Minecraft-style "only when all players sleep" cooperative acceleration.

## Future / low priority
- **Generic Prediction Runtime — Phase E (deferred from Phase D 4.3)** — cross-player interactions are host-validated without client prediction (tech-decisions.md #157). A unified prediction/rollback runtime for local movement, pickup, and drag transients belongs to Phase E; existing `PickupOrigins`, pending-pickup queue, `DropPendingState`, and `NativeOperationCoordinator` are residue candidates.


- **Minecraft-style in-game command console** — a standalone command chain (registration → parsing → permission → execution → feedback), independent of current host-command/mod-command surfaces. The bottom-right text-chat UI is disabled in favor of this eventual surface.
- Strict validation / anti-cheat hardening — explicitly low; defer until sync domains are stable.
- Phase 5 tooling & ecosystem: mod manager, auto-install, crash reports, network diagnostics, compatibility database, dedicated server (only if public community hosting becomes relevant; host migration is not planned).
- KrokMP compatibility adapter — reserved; only after the native Mod API stabilizes and real migration demand exists.

## Architecture watchlist

Files at or near the 600-line gate should be split before the next feature lands in them:

`SessionService.cs` (580), `ItemApplication.cs` (599), `CharacterDataSync.cs` (563), `EntitySyncService.cs` (548), `EnemyCombatDirector.cs` (547), `Plugin.cs` (522), `RunCoordinator.cs` (512).

`docs/tech-decisions.md` is also large; future landing entries should consider a domain-split index if it keeps growing.
