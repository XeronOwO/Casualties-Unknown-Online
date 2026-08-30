# CUO Backlog

Open work only. Landed delivery details are not duplicated here; they live in:

- [`docs/decisions/active.md`](../decisions/active.md) — binding decisions / landing log
- [`docs/evidence/selfchecks/`](../evidence/selfchecks/) — per-delivery fact sheets
- [`docs/features/items.md`](../features/items.md) and
  [`docs/features/entities.md`](../features/entities.md) — canonical feature sync matrices

## Status

- **One open bug report (2026-08-27)** — see "Open bug" below. It is not to be
  closed by a paper-only claim.
- Native game-content sync coverage is complete: item and entity feature matrices
  currently have no `missing` rows.
- The typed deterministic kernel migration (Phases A–E) is complete; future
  architecture work is listed under "Future / low priority".

## Open bug (2026-08-27)

### Host closing the lobby exits the game and can destroy the run save

- **Reported**: 2026-08-27.
- **Observed**: when the host closes the multiplayer lobby, the game exits instead
  of returning to a safe menu/lobby state. A brief network hiccup or a host wanting
  to recreate the room therefore ends the whole process.
- **Impact**: destructive UX for the host; combined with the save authority model
  this can effectively invalidate/lose the run's save/state. The host should be
  able to leave/close the session without force-quitting the game, and the save
  should be preserved or explicitly managed.
- **Related surfaces**: session/lobby teardown, host lifecycle (`LobbyLeft` /
  session end), Steam lobby ownership, save authority/run state, `GameAdapter` /
  `Plugin` shutdown path.

## Open work

### Items / crafting

- **KrokMP crafting loses container contents — OPEN (2026-08-30).** In KrokMP,
  crafting a recipe from an item that already contains contents (for example a
  flashlight with a battery used to craft a headlamp, or a liquid container used
  to craft another container) drops the contained items. Two directions are
  recorded for a future fix:
  1. **Exclude contained items from crafting** — simpler and closest to player
     intuition; the recipe refuses to consume an item with contents until the
     player empties it.
  2. **Inherit contents after crafting** — preserves contents across the recipe,
     but is more complex; special cases include liquid capacity changes and
     overflow when a smaller container replaces a larger one.

### Player interaction / UI

- **PVP** — LOW (reprioritized). No player-to-player damage domain today; defer
  until PvE, rules, and accept-first arbitration are stable.
- **Other lower-priority KrokMP candidates** — voice, vote-kick, and remaining
  player-list polish.

### Configuration

- **Custom configuration template system** — provide a reusable template system for
  full configuration profiles (log level, language, display/nameplate/color
  preferences, IP-direct/network settings). The default/built-in config should also
  be editable and savable as a template.

### Networking observability / optimization

Measurement-first items; do not optimize before data exists.

- **State-stream bandwidth reduction** — candidates include fixed-point/quantized
  positions, per-entity update masks / delta encoding, field-dirty batching for
  20 Hz player/enemy streams and 1 Hz `CharacterDataMsg`. No change before
  measurement.
- **Snapshot size reduction** — full world-item / character-data snapshots are
  correctness-oriented; only optimize after the traffic monitor identifies a
  dominant family.

### Final acceptance (not development work)

- Trade domain (#59/#93) — dual-side runtime pass.
- World determinism / `[WorldFingerprint]` comparison.
- Block-break first-writer-wins dual-side runtime confirmation (L0 already covered).

### Contingency

- **Event-version numbers** — required before any snapshot stream switches to an
  unreliable channel, to prevent a stale snapshot rolling back an in-flight event.

## Open decisions (no code change yet)

- **World-time adjustability / sleep acceleration policy** — currently both host
  and guests can request `Fast` / `SuperFast`, and the host applies all-unconscious
  sleep acceleration. Design is open for debate: disallow manual time acceleration,
  or adopt Minecraft-style "only when all players sleep" cooperative acceleration.

## Future / low priority

- **EnemyCombatOrderPolicy kernel-process follow-up** — the extracted
  `EnemyCombatOrderPolicy` owns the enemy apply-path decisions, but those paths do
  not yet feed a kernel process/event. `EnemyAttackMsg` stays the host-order
  local-apply command for now. See `docs/evidence/selfchecks/architecture/phase-e-legacy-inventory-selfcheck.md`.
- **Generic Prediction Runtime — future architecture work (deferred from Phase D
  4.3)** — cross-player interactions are host-validated without client prediction
  (`docs/decisions/active.md` #157). A unified prediction/rollback runtime for local
  movement, pickup, and drag transients remains future work; existing
  `PickupOrigins`, pending-pickup queue, `DropPendingState`, and
  `NativeOperationCoordinator` remain non-kernel active-path mechanisms.
- **Minecraft-style in-game command console** — a standalone command chain
  (registration → parsing → permission → execution → feedback), independent of
  current host-command/mod-command surfaces. The bottom-right text-chat UI is
  disabled in favor of this eventual surface.
- Strict validation / anti-cheat hardening — explicitly low; defer until sync
  domains are stable.
- Phase 5 tooling & ecosystem: mod manager, auto-install, crash reports, network
  diagnostics, compatibility database, dedicated server (only if public community
  hosting becomes relevant; host migration is not planned).
- KrokMP compatibility adapter — reserved; only after the native Mod API stabilizes
  and real migration demand exists.

## Architecture watchlist

Files at or near the 600-line gate should be split before the next feature lands in
them:

Current actual line counts (2026-08-30 audit): `SessionService.cs` (507),
`ItemApplication.cs` (542), `CharacterDataSync.cs` (525), `EntitySyncService.cs` (462),
`EnemyCombatDirector.cs` (326), `Plugin.cs` (444), `RunCoordinator.cs` (495).
`ItemApplication.cs` and `SessionService.cs` remain closest to the 600-line gate;
the others are below it today.

`docs/decisions/active.md` is also large; future landing entries should consider a
domain-split index if it keeps growing.
