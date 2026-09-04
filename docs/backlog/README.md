# CUO Backlog

DevOps-style issue/requirement backlog. Every item has its own ticket file under one
status folder; moving a ticket to another folder is the status transition.

## Workflow

```text
todo/  →  in-progress/  →  review/  →  done/
                             ↓
                          future/       (deferred / low priority / future work)
                          resolved/     (decision recorded, no code action)
                          watchlist/    (observability / architecture watch items)
```

- **One ticket = one file.**
- Copy this README's status table as the index; the ticket files are the source of truth.
- Status is the parent folder, not a field in the file (the file repeats it for readability).
- Code-complete items are moved to `review/` immediately; review is the waiting state
  for the single unified user acceptance pass after all high-priority backlog items
  are complete. Do not stop for per-ticket acceptance.
- Only the final unified-acceptance transition moves tickets from `review/` to `done/`;
  until then, `done/` holds previously accepted/closed delivery tickets.

## Status folders

| Folder | Meaning |
|---|---|
| `todo/` | Open work, not started |
| `in-progress/` | Active development in progress |
| `review/` | Code/verification done; waiting for the final unified acceptance pass after high-priority backlog items are complete |
| `done/` | Landed / closed |
| `future/` | Deferred, low priority, or future architecture work |
| `resolved/` | Decisions resolved without further code action |
| `watchlist/` | Maintainability / architecture watch items |

## Ticket index

### Todo

- [State-stream bandwidth reduction](todo/state-stream-bandwidth-reduction.md) — measurement-first.
- [Snapshot size reduction](todo/snapshot-size-reduction.md) — measurement-first.
- [Network traffic baseline and regression gate](todo/network-traffic-baseline.md) — concrete bytes/frame-size/checkpoint metrics before optimization (Loomi review 2026-09-04).
- [DI cycle guard / cycle-path diagnostics](todo/di-cycle-guard.md) — prevent composition-root circular dependencies or log the exact service chain when a cycle occurs.
- [Remove legacy "View items" remote-inventory detail path](todo/remove-legacy-view-items-remote-inventory-detail.md) — confirm and remove the pre-native-backpack right-click "View items" fallback if the current native backpack/right-click menu covers it.
- [Remote backpack native interaction parity](todo/remote-backpack-native-interaction-parity.md) — opening another player's backpack currently supports only take; normal pour/drop/container/Tab-transfer backpack operations are missing.


### In progress

_None._ (Folder exists for the workflow.)

### Review

- [Interactive in-game command console](review/in-game-command-console-interactive.md) — slash-opened focused input, completion/history/hints, fading text area, real selector-backed `/heal` command, IME-aware custom input, JSON host-rule command.
- [Command registration Attribute/reflection refactor](review/command-registration-attribute-refactor.md) — Attribute/reflection console registry + local mod console command API; selfcheck linked.
- [Command tree, resource-location completion, and selector filters](review/command-tree-resource-location-selector.md) — tree/argument-position completion, namespaced candidate catalog, bracketed selector filters; selfcheck linked.
- [Mod data sync model](review/mod-data-sync-model.md) — runtime scope seam landed: local-only / shared / host-authoritative mod data; no generic snapshot protocol.
- [Trade domain dual-side runtime pass](review/trade-domain-dual-side-runtime.md) — #59/#93.
- [World determinism / WorldFingerprint comparison](review/world-determinism-world-fingerprint.md).
- [Block-break first-writer-wins dual-side runtime confirmation](review/block-break-first-writer-wins.md).
- [check-architecture.ps1 performance](review/check-architecture-performance.md) — full gate ~32.4s → ~2.15s; selfcheck linked.
- [Middle-click location marker](review/middle-click-location-marker.md) — dedicated one-shot location ping: middle click circle, quick second click exclamation, star relay, 5s fade; selfcheck linked.
- [CUCoreLib migration support](review/cucorelib-migration-support.md) — external KrokMP-based evaluation complete; typed content/status/moodle/runtime seams landed; remaining rows are future/non-goal; selfchecks linked.
- [Protocol frame envelope validation](review/protocol-frame-validation.md) — unified frame validator before kernel handlers; malformed/forged/oversized frames dropped, presentation payloads remain non-fatal.
- [Full-qualified name cleanup](review/full-qualified-name-cleanup.md) — prefer using directives/aliases; behavior-preserving refactor only.
- [Composite command sequential semantics](review/composite-command-sequential-semantics.md) — inner commands decide/reduce in declaration order on one working copy; atomic rollback and duplicate composite OperationId covered; selfcheck linked.
- [Projection failure auto-recovery](review/projection-failure-auto-recovery.md) — per-domain dirty/rebuild loop: items/fluids/world-entities, degraded after repeated failures; selfcheck linked.
- [ModService ↔ GameAdapter DI cycle](review/mod-service-gameadapter-di-cycle.md) — startup hang fixed by injecting ModStatusStore instead of ModService into the adapter; regression contract test added.

### Future

- [PVP](future/pvp.md) — low priority, deferred until PvE/rules stable.
- [KrokMP lower-priority candidates](future/krokmp-candidates.md) — voice, vote-kick; player-list polish has landed.
- [EnemyCombatOrderPolicy kernel-process follow-up](future/enemy-combat-order-policy-kernel.md).
- [Generic Prediction Runtime](future/generic-prediction-runtime.md).
- [Strict validation / anti-cheat hardening](future/strict-validation-anti-cheat.md).
- [Phase 5 tooling & ecosystem](future/phase5-tooling-ecosystem.md).
- [KrokMP compatibility adapter](future/krokmp-compatibility-adapter.md).
- [Systemic save and backup management](future/systemic-save-backup-management.md) — inspectable ZIP+JSON archives, scheduled/manual backups, restore/load, native game-layer backup.
- [Command authorization gateway](future/command-authorization-gateway.md) — central actor/AuthorityKind enforcement in front of the kernel (Loomi review 2026-09-04).
- [Runtime DI feature registration and lifecycle contract](future/runtime-di-feature-registration-lifecycle.md) — feature-scoped composition modules + verified reset/unbind/graph/update-order (Loomi review 2026-09-04).
- [Kernel replication namespace relocation](future/kernel-replication-namespace-relocation.md) — move item-scoped kernel protocol/save services to a neutral namespace (Loomi review 2026-09-04).

### Resolved

- [IP-direct duplicate names allowed](resolved/ip-direct-duplicate-names.md).
- [Runtime log errors (2026-08-30)](resolved/runtime-log-errors-2026-08-30.md) — TypeLoadException is HotRepl, not CUO; the OnlineUiOverlay ArgumentException is not in the captured logs.

### Done

- [In-game command console](done/in-game-command-console.md) — modal Online UI console with slash commands + chat input; selfcheck linked.
- [Player-list polish](done/player-list-polish.md) — duplicate-name peer-id disambiguation; selfcheck linked.

- [Guest-mined block leaves ghost fragments on host](done/guest-mined-block-ghost-fragments-on-host.md) — direct air writes now clear stale game BlockDamage; selfcheck linked.
- [Duplicate unsynced item drops (guest tree / world-spawned)](done/guest-tree-extra-unsynced-drops.md) — same-id materialization dedup; selfcheck linked.
- [Guest-mined item static-physics desync](done/guest-mined-item-static-physics-desync.md) — same-id materialization dedup; selfcheck linked.
- [Remote ragdoll state not visible](done/ragdoll-state-not-visible-to-remote.md) — X ragdoll now visible on remote; user accepted.
- [Ragdoll limb pose not synced remotely](done/ragdoll-limb-pose-not-synced.md) — real limb poses ride the player stream; selfcheck linked.
- [Name tag font, head position, and off-screen edge padding](done/name-tag-font-position-edge-padding.md) — markers now head-anchored with larger fonts and UI-safe edge padding.
- [High sleepiness squint not visible remotely](done/high-sleepiness-squint-not-visible-remotely.md) — remote face now receives face-driving vitals from the 1 Hz snapshot.
- [Host close-room safe exit](done/host-close-room-safe-exit.md).
- [Custom configuration template system](done/config-profile-templates.md).
- [IP-direct display-name validation](done/ip-direct-name-validation.md).
- [World-time manual acceleration policy](done/world-time-manual-acceleration.md).
- [Player-selectable colors and color-only head tags](done/player-color-head-tags.md).
- [Item snapshot event-version gating](done/item-snapshot-event-version-gating.md).
- [Architecture split pass](done/architecture-split-pass.md).
- [Typed deterministic kernel migration](done/typed-kernel-migration.md).
- [Native game-content sync coverage](done/native-game-content-sync-coverage.md).

### Watchlist

- [Architecture watchlist](watchlist/architecture-watchlist.md).
