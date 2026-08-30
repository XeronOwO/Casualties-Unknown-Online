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
- Item snapshot event-version gating is implemented: unreliable full-table
  keyframes carry a per-payload sequence plus the host kernel revision, and
  guests drop stale/out-of-order snapshots.
- Architecture split pass completed: the previously near-limit item/session/
  character coordinators now delegate real responsibilities to dedicated
  helper classes and no coordinator is close to the 600-line gate.
- Custom configuration template system landed (2026-08-31) — full BepInEx
  config profiles can be saved/applied from the Preferences page; see
  `docs/evidence/selfchecks/tooling/config-profile-templates-selfcheck.md`.
- IP-direct display-name validation landed — the local side refuses
  host/join with an empty/malformed name and the host rejects inbound peers
  with an invalid name; see
  `docs/evidence/selfchecks/protocol/ip-direct-name-validation-selfcheck.md`.
- World-time manual-acceleration policy closed: `Fast`/`SuperFast` are
  cooperative; they never accelerate a session while any in-world player is
  awake. The all-unconscious sleep policy remains the only shared-clock
  acceleration.
- Player-selectable colors and color-only head name tags landed — a local
  palette picker is shared through handshake/roster/live updates, head tags
  show only the colored player name, and off-screen markers keep name + distance.

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

### Player interaction / UI

- **PVP** — LOW (reprioritized). No player-to-player damage domain today; defer
  until PvE, rules, and accept-first arbitration are stable.
- **Other lower-priority KrokMP candidates** — voice, vote-kick, and remaining
  player-list polish.

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

## Resolved decisions

- **IP-direct duplicate names** — allowed. Identity is always the logical
  peer id / SteamID; display names are cosmetic presentation labels and no
  player-specific state is keyed by name. Uniqueness is intentionally not
  enforced.

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

Current actual line counts (2026-08-31 after split pass, updated after player presentation landing):
`ItemApplication.cs` (238), `SessionService.cs` (487), `KernelProtocolService.cs` (516),
`ItemService.cs` (520), `CharacterDataSync.cs` (529), `EntitySyncService.cs` (553),
`RunCoordinator.cs` (550), `Plugin.cs` (511), `EnemyCombatDirector.cs` (376).
No coordinator is near the 600-line gate today; the watchlist remains a
before-landing check rather than an action item.

`docs/decisions/active.md` is also large; future landing entries should consider a
domain-split index if it keeps growing.
