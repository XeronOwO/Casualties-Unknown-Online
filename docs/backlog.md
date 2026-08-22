# CUO Backlog

Open work only. Landed delivery details are not duplicated here; they live in:

- [`docs/tech-decisions.md`](tech-decisions.md) — binding decisions / landing log
- [`docs/selfchecks/`](selfchecks/) — per-delivery fact sheets
- [`docs/item-features.md`](item-features.md) and [`docs/entity-features.md`](entity-features.md) — canonical feature sync matrices

> Condensed 2026-08-22: completed delivery detail is no longer repeated here; see the reference docs above.

## Status

- No open high-priority bugs.
- Native game-content sync coverage is complete: item and entity feature matrices currently have no `missing` rows; the last recorded `Heater`-on-`xaloris` local-presentation residual is closed as excluded by design, and the remote-clone disfigurement/eye-loss facial presentation residual is now closed (ProtocolVersion 32).
- Network health metrics (per-peer RTT history / jitter / probe loss) now surface in `[NetworkHealth]` logs; per-peer bandwidth already surfaces in `[NetworkTraffic]` logs. See `docs/selfchecks/network-health-metrics-selfcheck.md`.
- Phase 4 Mod API **ReadGameState** landed as a read-only player-character projection (`IModGameState`); no wire/protocol change. See `docs/selfchecks/mod-game-state-selfcheck.md`.
- Remaining final dual-side acceptance items below are end-of-cycle acceptance, not development work.

## Open work

### Phase 4 Mod API (MEDIUM)

- **Custom entities** — `SpawnEntity` is declared and carried through the handshake, but no entity-spawn/replication surface exists. Needs design before implementation.
- **AccessNativeApi** — permission is declared but the explicit native/game-private escape hatch is un-designed; decide policy before exposing.

### Item / entity known gaps

None open. The previous local-only item states and the enemy LookTarget presentation gap are closed:

- **GrapplingHook** `fired` / `hookLatched` / `pulling` — synced via the item component-state path; clone renderer presents the fired sprite (see `docs/item-features.md`).
- **WatchScript** timers / **AutoPump.worn** — excluded by design: owner-local body/UI effects only, and render-clone scripts are disabled.
- **Peer-view clone renderer** — the pure state-selection helper now has an L0 test face (`RemoteItemPresentationTests`).
- **LookTarget gaze/scare** — closed via the 20 Hz player entity stream: `EntityStateMsg` now carries the owner's `LookTarget`/`CorpseScript` override gaze + the eye face timers (`eyeScareTime`/`eyePanicTime`/`eyeCloseTime`), and the remote clone writes them into its proxy Body (see `docs/tech-decisions.md` #44).
- **Heater temperature field on `xaloris`** — closed as **excluded by design**: `Heater.OnWillRenderObject` writes only the local player's body temperature, which already rides the 1 Hz character stream, so no enemy-sync surface is needed (see `docs/selfchecks/heater-xaloris-local-body-effect-selfcheck.md`).
- **Remote-clone FacialExpression disfigurement/eye-loss latches** — closed via the 1 Hz `CharacterHealthMsg` + `CloneFacePresentation`: body latches (`Disfigured`/`EyeGone`/`BothEyesGone`), the owner's random disfigurement head index, and the long-run heal presentation timers are now applied to the render clone; `ProtocolVersion` 32. See `docs/selfchecks/clone-face-presentation-selfcheck.md`.

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

`WorldService.cs` (600), `ItemService.cs` (597), `SessionService.cs` (580), `ItemApplication.cs` (576), `CharacterDataSync.cs` (563), `EnemySyncCoordinator.cs` (551), `EntitySyncService.cs` (548), `EnemyCombatDirector.cs` (547), `Plugin.cs` (534), `RunCoordinator.cs` (512), `PlayerInteractionService.cs` (511).

`docs/tech-decisions.md` is also large; future landing entries should consider a domain-split index if it keeps growing.
