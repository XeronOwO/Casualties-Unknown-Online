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

- [Snapshot size reduction](todo/snapshot-size-reduction.md) — measurement-first.


### In progress

_None._ (Folder exists for the workflow.)

### Review

- [Remote backpack native interaction parity](review/remote-backpack-native-interaction-parity.md) — native remote-backpack pour/drop/container/Tab-transfer gestures now route to host-authoritative operations instead of mutating display proxies; selfcheck linked.
- [Guest background window plays ghost item friction/ground sounds](review/guest-background-ghost-item-ground-sounds.md) — non-authoritative guest item impact presentation (drop/step/squeak/dust) suppressed; selfcheck linked.
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
- [Network traffic baseline and regression gate](review/network-traffic-baseline.md) — per-payload P50/P95 frame stats, live per-peer bytes, checkpoint chunk/size/restore baseline; selfcheck linked.
- [State-stream bandwidth reduction](review/state-stream-bandwidth-reduction.md) — per-recipient player-state stream no longer echoes a guest's own entry; selfcheck linked.
- [Entity destruction drops fresh presentation](review/entity-destruction-drop-guest-fresh-state-loss.md) — trap/building-death drops now replay with the full transient fresh/velocity state from `EntityEventMsg.Drops`; selfcheck linked.
- [Auto turret trap fires unexpectedly after reload](review/turret-stray-fire-after-reload.md) — stale periodic checkpoint replay of transient turret/geyser trap states removed; live relay unchanged; selfcheck linked.
- [Full-qualified name cleanup](review/full-qualified-name-cleanup.md) — prefer using directives/aliases; behavior-preserving refactor only.
- [Composite command sequential semantics](review/composite-command-sequential-semantics.md) — inner commands decide/reduce in declaration order on one working copy; atomic rollback and duplicate composite OperationId covered; selfcheck linked.
- [Projection failure auto-recovery](review/projection-failure-auto-recovery.md) — per-domain dirty/rebuild loop: items/fluids/world-entities, degraded after repeated failures; selfcheck linked.
- [ModService ↔ GameAdapter DI cycle](review/mod-service-gameadapter-di-cycle.md) — startup hang fixed by injecting ModStatusStore instead of ModService into the adapter; regression contract test added.
- [DI cycle guard / cycle-path diagnostics](review/di-cycle-guard.md) — composition-root ValidateOnBuild + factory re-entrancy guard; cycle chains logged to BepInEx and latest.log; selfcheck linked.
- [Remove legacy "View items" remote-inventory detail path](review/remove-legacy-view-items-remote-inventory-detail.md) — custom inline inventory expansion and right-click fallback removed; native remote backpack remains the only remote-inventory surface.
- [Remote player medical/health panel](review/remote-player-medical-panel.md) — CUO read-only medical panel from already-synced full health/limb data; Players page/quick panel/right-click entry; selfcheck linked.
- [Sync player pain vocalizations and B-key bark](review/sync-player-pain-vocalizations-and-bark.md) — PantSound pain/yawn/growl/B-bark now ride the existing CharacterSoundMsg event; continuous pant remains local; reverse direction covered by star relay.
- [Suppress native idle-sit while carried](review/carried-player-idle-sit-suppression.md) — carried characters no longer publish/replay/linger in the native sit pose; shared pure CarriedBodyPose rule applied across rider/carrier/peer views.
- [Carry/piggyback riding movement teleport and rider/carrier position mismatch](review/carry-piggyback-rider-position-smoothing.md) — rider now follows the smoothed carrier render clone; shared ride-pose path and body-root stream anchor prevent participant/third-party drift; selfcheck linked.
- [Carry/piggyback vertical placement asymmetry](review/carry-piggyback-vertical-placement-asymmetry.md) — carried riders publish body root instead of the non-standing torso anchor; shared ride-pose path also mirrors crouch state; selfcheck linked.
- [Guest container contents periodically appear as world drops on the host view](review/guest-container-contents-ghost-drops-on-host.md) — remote clone nested display proxies no longer carry item instance ids; domain lookup cannot address them; selfcheck linked.

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
