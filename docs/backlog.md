# CUO Backlog

Open work only. Landed delivery details are not duplicated here; they live in:

- [`docs/tech-decisions.md`](tech-decisions.md) — binding decisions / landing log
- [`docs/selfchecks/`](selfchecks/) — per-delivery fact sheets
- [`docs/item-features.md`](item-features.md) and [`docs/entity-features.md`](entity-features.md) — canonical feature sync matrices

## Status

- No open high-priority bugs.
- Native game-content sync coverage is complete: item and entity feature matrices currently have no `missing` rows.
- **World bleeding effects sync — CLOSED (2026-08-26).** The visible blood decals a player leaves in the world now travel as a dedicated `WorldBloodSpawn` event (NetMsg 121, ProtocolVersion 51); every peer replays the same transient ground/wall decal. Remote render clones no longer create their own duplicate decals. See `docs/selfchecks/world-blood-spawn-sync-selfcheck.md` and `docs/tech-decisions.md` #115.

## Open work

### Player interaction / UI

- **Online UI anti-passthrough is only full-screen** — the main CUO Online UI already blocks mouse passthrough with a full-screen guard, but the other custom UI surfaces (quick panel, context menu) do not; they should use scoped/within-panel passthrough blocking.
- **Remote-player inventory UI should reuse the game backpack UI** — viewing another player's items currently uses a homemade inventory UI. Reuse the game's own backpack UI where possible; nested containers in another player's inventory should also be openable like the local player's. Add a host/session toggle controlling whether other players may take items from that inventory.
- **Online UI transport-mode exclusivity** — the Home page currently shows both Steam network and IP-direct sections at the same time, although they are mutually exclusive transports. Hide/collapse the inactive mode (low-risk UI-only).
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

`SessionService.cs` (580), `ItemApplication.cs` (576), `CharacterDataSync.cs` (563), `EntitySyncService.cs` (548), `EnemyCombatDirector.cs` (547), `Plugin.cs` (522), `RunCoordinator.cs` (512).

`docs/tech-decisions.md` is also large; future landing entries should consider a domain-split index if it keeps growing.
