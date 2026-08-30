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
- Final user acceptance items live in `review/` until verified.
- Closed delivery tickets live in `done/` with links to the corresponding selfcheck.

## Status folders

| Folder | Meaning |
|---|---|
| `todo/` | Open work, not started |
| `in-progress/` | Active development in progress |
| `review/` | Code/verification done, needs acceptance/verification pass |
| `done/` | Landed / closed |
| `future/` | Deferred, low priority, or future architecture work |
| `resolved/` | Decisions resolved without further code action |
| `watchlist/` | Maintainability / architecture watch items |

## Ticket index

### Todo

- [Remote ragdoll state not visible](todo/ragdoll-state-not-visible-to-remote.md) — X ragdoll not shown on remote view; previous fix rejected for workflow, reopen.
- [Duplicate unsynced item drops (guest-dug tree and world-spawned items)](todo/guest-tree-extra-unsynced-drops.md) — guest action/world drops produce two unsynced frozen copies.
- [High sleepiness squint not visible remotely](todo/high-sleepiness-squint-not-visible-remotely.md) — remote eyes stay open when sleepiness is high.
- [Runtime log errors to investigate (2026-08-30)](todo/runtime-log-errors-2026-08-30.md) — log files pinned with last write times.
- [Name tag font size, head position, and off-screen edge padding](todo/name-tag-font-position-edge-padding.md) — UI/name tag issue.
- [PVP](todo/pvp.md) — low priority, deferred until PvE/rules stable.
- [KrokMP lower-priority candidates](todo/krokmp-candidates.md) — voice, vote-kick, player-list polish.
- [State-stream bandwidth reduction](todo/state-stream-bandwidth-reduction.md) — measurement-first.
- [Snapshot size reduction](todo/snapshot-size-reduction.md) — measurement-first.

### In progress

_None._ (Folder exists for the workflow.)

### Review

- [Trade domain dual-side runtime pass](review/trade-domain-dual-side-runtime.md) — #59/#93.
- [World determinism / WorldFingerprint comparison](review/world-determinism-world-fingerprint.md).
- [Block-break first-writer-wins dual-side runtime confirmation](review/block-break-first-writer-wins.md).

### Future

- [EnemyCombatOrderPolicy kernel-process follow-up](future/enemy-combat-order-policy-kernel.md).
- [Generic Prediction Runtime](future/generic-prediction-runtime.md).
- [Minecraft-style in-game command console](future/in-game-command-console.md).
- [Strict validation / anti-cheat hardening](future/strict-validation-anti-cheat.md).
- [Phase 5 tooling & ecosystem](future/phase5-tooling-ecosystem.md).
- [KrokMP compatibility adapter](future/krokmp-compatibility-adapter.md).

### Resolved

- [IP-direct duplicate names allowed](resolved/ip-direct-duplicate-names.md).

### Done

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
