# CUO Backlog

Open work only. Landed delivery details are not duplicated here; they live in:

- [`docs/tech-decisions.md`](tech-decisions.md) — binding decisions / landing log
- [`docs/selfchecks/`](selfchecks/) — per-delivery fact sheets
- [`docs/item-features.md`](item-features.md) and [`docs/entity-features.md`](entity-features.md) — canonical feature sync matrices

> Condensed 2026-08-22: completed delivery detail is no longer repeated here; see the reference docs above.

## Status

- No open high-priority bugs.
- Native game-content sync coverage is complete: item and entity feature matrices currently have no `missing` rows.
- Remaining final dual-side acceptance items below are end-of-cycle acceptance, not development work.

## Open work

### Phase 4 Mod API (MEDIUM)

- **Custom entities** — `SpawnEntity` is declared and carried through the handshake, but no entity-spawn/replication surface exists. Needs design before implementation.
- **ReadGameState** — permission is declared but no framework surface exposes a read-only game-state projection.
- **AccessNativeApi** — permission is declared but the explicit native/game-private escape hatch is un-designed; decide policy before exposing.

### Item / entity known gaps

Already documented in [`docs/item-features.md`](item-features.md); kept here as open/decision items:

- **Dynamite fuse** — `CustomItemBehaviour.data` is `object[]` and deliberately unsynced; a guest-loaded fuse detonates locally only. This is the only known gameplay-affecting item gap; needs either a dedicated fuse-state event or an explicit accepted-local decision.
- **GrapplingHook** `fired` / `hookLatched` / `pulling` — local-only (no `[Saveable]`).
- **WatchScript** timers — local-only, low risk.
- **AutoPump.worn** — local-only, low risk.
- **Peer-view clone renderer** — the display path has no L0 test face (GameAdapter); add a test seam when next touched.

### Online UI / interaction refinements (LOW/MEDIUM)

- **Open another player's inventory/container** — content sync and clone fact tables are correct, but the renderer does not display a remote player's container contents; a remote inventory UI remains.
- **Heal item selection** — heals auto-select the first carried medical item; the wire already supports explicit item ids, so a UI selector is a future refinement.

### Networking observability / optimization (new)

Measurement-first items; do not optimize before data exists.

- **Network health metrics** — surface per-peer packet loss / jitter / bandwidth alongside the existing ping RTT in Online UI or logs. The whole-protocol traffic monitor now provides per-`NetMsg` send/receive byte counts and per-peer window logs; the remaining health-specific counts (loss/jitter) are still unmeasured.
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

`WorldService.cs` (600), `ItemService.cs` (597), `SessionService.cs` (580), `ItemApplication.cs` (576), `CharacterDataSync.cs` (563), `EnemySyncCoordinator.cs` (551), `EntitySyncService.cs` (548), `EnemyCombatDirector.cs` (547), `Plugin.cs` (534), `RunCoordinator.cs` (512), `PlayerInteractionService.cs` (511).

`docs/tech-decisions.md` is also large; future landing entries should consider a domain-split index if it keeps growing.
