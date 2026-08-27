# CUO Backlog

Open work only. Landed delivery details are not duplicated here; they live in:

- [`docs/tech-decisions.md`](tech-decisions.md) — binding decisions / landing log
- [`docs/selfchecks/`](selfchecks/) — per-delivery fact sheets
- [`docs/item-features.md`](item-features.md) and [`docs/entity-features.md`](entity-features.md) — canonical feature sync matrices

## Status

- **One open bug report (2026-08-27)** — see "Open bugs" below. It is not to be closed by a paper-only claim.
- Native game-content sync coverage is complete: item and entity feature matrices currently have no `missing` rows.
- **Host body orientation after piggyback Drop — CLOSED (2026-08-27).** CUO now keeps `Body.isRight` and `transform.localScale.x` in lockstep through a shared `BodyFacing` rule on every CUO-facing write (carried local body, render proxy, carrier-side clone override), and the release restore re-applies it before native simulation resumes. See `docs/selfchecks/piggyback-facing-restore-selfcheck.md` and `docs/tech-decisions.md` #121.
- **Remote container destroy authority — CLOSED (2026-08-27).** The remote-backpack display-proxy destroys no longer reach the host as real item destroys, and a received destroy can no longer kill a carried (non-world) item. The host also validates destroy ownership before relaying. See `docs/selfchecks/remote-container-destroy-authority-selfcheck.md` and `docs/tech-decisions.md` #120.
- **Ragdoll stale-state / clone-creation race — CLOSED (2026-08-27).** The reliable `CharacterRagdoll` one-shot is now guarded against a lagging `Standing=true` 20 Hz snapshot and is queued until the owner's render clone exists. See `docs/selfchecks/ragdoll-stale-state-fix-selfcheck.md` and `docs/tech-decisions.md` #119.
- **World bleeding effects sync — CLOSED (2026-08-26).** The visible blood decals a player leaves in the world now travel as a dedicated `WorldBloodSpawn` event (NetMsg 121, ProtocolVersion 51); every peer replays the same transient ground/wall decal. Remote render clones no longer create their own duplicate decals. See `docs/selfchecks/world-blood-spawn-sync-selfcheck.md` and `docs/tech-decisions.md` #115.
- **Online UI scoped anti-passthrough + transport-mode exclusivity — CLOSED (2026-08-26).** The quick panel and right-click context menu now get scoped UGUI raycast blockers limited to their own rectangles, and the Home page shows only the selected Steam or IP-direct transport section at a time. See `docs/selfchecks/online-ui-scoped-passthrough-selfcheck.md` and `docs/tech-decisions.md` #116.
- **Remote-player inventory UI follow-up — CLOSED (2026-08-26).** Remote inventory now has an "Open backpack" path that reuses the game's native radial backpack UI focused on the remote player's render clone (read-only; the clone is never mutated). The Custom UI remains as a text detail fallback and the recursive container collapsibles, and `[HostRules] AllowRemoteInventoryTake` controls the cross-player take operation. See `docs/selfchecks/remote-inventory-ui-followup-selfcheck.md` and `docs/tech-decisions.md` #117.
- **LifePod shuttle-door trigger sound — CLOSED (2026-08-26).** The earlier fix only added `shuttleNotice` to the host executor; the guest live-replay path still skipped the collision-only trigger sound. `TrapVisualReplay.ReplayShuttleDoor` now replays it for live relays (elapsed == 0), while late-joiner snapshots still jump to the current state without replaying old sounds. See `docs/selfchecks/native-remote-backpack-and-door-sound-selfcheck.md` and `docs/tech-decisions.md` #118.

## Open bugs (2026-08-27)

### Remote backpack item operations unavailable inside open containers

- **Reported**: 2026-08-27.
- **Observed**: with `[HostRules] AllowRemoteInventoryTake` enabled, opening another player's backpack shows the container contents, but items inside cannot be operated (drag/take) from the remote view.
- **Investigation scope**: determine whether this is a UI-only limitation of the native read-only radial backpack view or a missing cross-player container-action path (remote take / move into / out of nested containers). Do not add a separate copy/move UI before the existing remote-take operation matrix is clear; the take/transfer path must remain consistent with the host decision surface.
- **Related surfaces**: remote backpack view, native radial backpack focus path, `OnlineUi` remote inventory panels, `AllowRemoteInventoryTake`, container/nested container rows.

## Open work

### Player interaction / UI

- **Player-interaction line-of-sight / direct-visibility validation** — after the direct player-interaction set is complete, add a shared direct-visibility gate so remote-player actions (take/carry/piggyback/heal/use/push/recruit, and the backpack view) cannot be performed through walls or other blockers. Deferred by design until the interaction features are stable; it belongs with the later strict-validation/anti-cheat hardening, not as a per-feature patch.
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

- **Minecraft-style in-game command console** — a standalone command chain (registration → parsing → permission → execution → feedback), independent of current host-command/mod-command surfaces. The bottom-right text-chat UI is disabled in favor of this eventual surface.
- Strict validation / anti-cheat hardening — explicitly low; defer until sync domains are stable.
- Phase 5 tooling & ecosystem: mod manager, auto-install, crash reports, network diagnostics, compatibility database, dedicated server (only if public community hosting becomes relevant; host migration is not planned).
- KrokMP compatibility adapter — reserved; only after the native Mod API stabilizes and real migration demand exists.

## Architecture watchlist

Files at or near the 600-line gate should be split before the next feature lands in them:

`SessionService.cs` (580), `ItemApplication.cs` (599), `CharacterDataSync.cs` (563), `EntitySyncService.cs` (548), `EnemyCombatDirector.cs` (547), `Plugin.cs` (522), `RunCoordinator.cs` (512).

`docs/tech-decisions.md` is also large; future landing entries should consider a domain-split index if it keeps growing.
