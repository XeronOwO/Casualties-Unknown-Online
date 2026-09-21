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
- **The index below is a table of pointers, not a second summary.** A row is
  `- [Title](path) — **Priority** — one clause`: the clause says what the ticket IS, never
  its history, and every fact (decision, date, count, stage progress, verdict) lives in the
  ticket. The row's SECTION is its status, so no row repeats it; `review/` means the ticket
  is code-complete and waiting for the unified acceptance pass.
- A row's priority is the ticket's own `- Priority:` field (first token), and a ticket that
  declares none — the closed records — gets no priority in its row.
  `BacklogIntegrityGateTests` keeps the rows from drifting back into summaries: a row over
  the length budget, a row whose priority disagrees with its ticket, a ticket missing from
  the index, or one listed under the wrong section fails the build.
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

- [Plugin as a host shell](todo/plugin-host-shell.md) — **Medium** — entry, UI and registration separated.
- [Patch bridge domain ports](todo/patch-bridge-domain-ports.md) — **Medium** — per-domain ports, aggregate frozen.
- [Composition root feature modules](todo/composition-root-feature-modules.md) — **Medium** — registrations and reset contract.
- [Legacy wire DTOs](todo/legacy-wire-dto-slice.md) — **Medium** — the remaining kernel-mapper move's precondition.

### Review

- [Mod API contract governance](review/mod-api-contract-governance.md) — **Medium-High** — visibility rule, stability levels, API baseline.
- [Adapter capability catalog](review/adapter-capability-catalog.md) — **High** — capability ids, Required/Optional classes, probe reasons.
- [Game-update contract toolchain](review/game-update-contract-toolchain.md) — **High** — game-assembly snapshot, classified diff, report.
- [Systemic save and backup management](review/systemic-save-backup-management.md) — **Medium** — the backup/restore layer's roadmap.
- [World and backup management surface](review/world-and-backup-management-surface.md) — **Medium** — the world/backup picker and the player-chosen restore.
- [World-entry trap layout staleness](review/trap-layout-entry-snapshot-staleness.md) — **Low-Medium** — the send path re-derives the live table.
- [Partial-damage report vs the live delta](review/partial-damage-delta-report-overlap.md) — **Low-Medium** — damage is accounted per sender.
- [Guest pending-report fallback: flat 60 s first resend](review/guest-report-fallback-first-resend.md) — **Low-Medium** — the guest→host entry phase.
- [Sync cadence review](review/sync-cadence-review.md) — **Medium** — fallback stretch limits and first-resend latency.
- [Carried-inventory registration](review/carried-inventory-registration-re-report.md) — **Medium** — the absolute re-report window.
- [Recipe unlock has no fallback](review/recipe-unlock-fallback.md) — **Medium** — the absolute unlock set backs up the one-shot report.
- [Session control convergence](review/session-control-convergence.md) — **Medium** — the bounded scene re-report window and the re-ack loop.
- [Guest command loss is not reconciled](review/guest-command-loss-reconciliation.md) — **Medium** — the bounded per-item re-report queue.
- [World-time acceleration is gated on being asleep](review/world-time-local-initiation.md) — **Medium** — the manual key acts locally; the host arbitrates.
- [World/layer generation identity](review/world-layer-generation-identity.md) — **Medium** — the run baseline rides the cell-keyed reports.
- [Remaining generation-relative families](review/generation-identity-remaining-families.md) — **Medium** — trap layout and entity creation.
- [An item can be operated on before its creation is registered](review/item-creation-registration-first.md) — **Medium-High** — creation first, no hold window.
- [Remote interaction gates are judged by the host](review/remote-interaction-local-gating.md) — **Medium-High** — the two clients judge their own side.
- [Medical operations are exclusive (one operator at a time)](review/concurrent-medical-operations.md) — **Medium-High** — several operators, one victim.
- [Enemy snapshot binding has no recovery path](review/enemy-snapshot-binding-recovery.md) — **Medium** — the spawn anchor and the 60 s repair group.
- [Guest break drops are lost](review/guest-break-drops-recovery.md) — **Medium** — the pending drop table and the idempotent break verdict.
- [S3 — Mid-run consistent cut and world diff](review/save-mid-run-consistent-cut.md) — **High** — the cut seam and exactly-once restore.
- [Save system: layer-end and mid-run saves](review/save-system-mid-run-and-layer-end.md) — **High** — umbrella + design record.
- [S4 — Multiplayer restore and backups](review/save-multiplayer-restore-and-backups.md) — **High** — stage 4 (S4.1–S4.4).
- [S4.4 — Interval autosave and recovery](review/save-interval-autosave-and-backup-recovery.md) — **High** — retention and backup promotion.
- [Evidence matrix fat rows](review/evidence-matrix-fat-rows-split.md) — **Low-Medium** — a count plus the evidence file.
- [Unrepresentable runtime creations are rejected](review/runtime-entity-creation-rejection.md) — **High** — the creator's copy is destroyed.
- [S4.3 — Starting supplies for a new player](review/save-new-player-starting-supplies.md) — **High** — the policy keys on body identity.
- [S4.2 — The restore account surface](review/save-restore-account-surface.md) — **High** — one console line per save event.
- [S4.1 — Restore claim](review/save-guest-restore-claim-and-legacy-store-retirement.md) — **High** — three-valued claim; legacy store deleted.
- [S3.6 — Solo menu-exit trigger](review/save-solo-menu-exit-trigger.md) — **Medium** — the leave action is intercepted and replayed.
- [Trap/entity action divergence](review/trap-action-divergence-hardening.md) — **Low-Medium** — row verdicts instead of exceptions.
- [Restored entity row containment](review/restored-entity-row-containment.md) — **Low-Medium** — a throwing row costs only itself.
- [Block-damage table capacity alignment](review/block-damage-table-capacity-alignment.md) — **Medium** — CUO's second registry is deleted.
- [WorldStateMessageService split](review/world-state-message-service-split.md) — **Low** — guest block bookkeeping moved out.
- [Markerless runtime-entity bind absorption](review/runtime-entity-markerless-bind-absorption.md) — **Low-Medium** — positional bind deleted.
- [Dead runtime-entity relay API](review/runtime-entity-dead-api-cleanup.md) — **Low** — the relay chain is deleted.
- [Guest block mutations: periodic re-report](review/guest-block-mutation-re-report.md) — **High** — the pending table and 60 s pump.
- [Guest partial block damage re-report](review/guest-partial-block-damage-re-report.md) — **Medium** — the absolute re-report and the per-cell merge.
- [Runtime-created entity spawn backfill](review/runtime-entity-spawn-backfill.md) — **Medium-High** — the accepted-creation table.
- [Sync completeness audit](review/sync-event-and-periodic-fallback-coverage-audit.md) — **High** — the 64-row evidence matrix.
- [Namespaced ID system](review/id-system-namespaced-ids.md) — **Medium** — the `ContentId` vocabulary.
- [Command completion by id/name](review/command-id-name-completion.md) — **Medium** — canonical id, bare path or display name.
- [Remote backpack item projection](review/remote-backpack-item-projection-acceptance-issues.md) — **High** — Tab transfer and held-item self-use.
- [Global unified projection framework](review/global-projection-framework.md) — **High** — the rebuildable-domain contract.
- [Unified remote display projection](review/unified-remote-display-projection-rework.md) — **High** — three helpers become one seam.
- [Remote medical panel acceptance issues](review/remote-medical-panel-acceptance-issues.md) — **High** — opiate ramp, breathing and ECG.
- [Adaptive report rate — roadmap](review/global-adaptive-report-rate-flow-control.md) — **Medium** — stages 1-4 are in review.
- [Adaptive report rate — Stage 4](review/global-adaptive-report-rate-stage-4-high-frequency-domains.md) — **Medium** — item/fluid/trader cadence.
- [Adaptive report rate — Stage 3](review/global-adaptive-report-rate-stage-3-cumulative-streams.md) — **Medium** — cumulative coalescing.
- [Adaptive report rate — Stage 1](review/global-adaptive-report-rate-stage-1-global-governor.md) — **Medium** — taxonomy and the shared governor.
- [Adaptive report rate — Stage 2](review/global-adaptive-report-rate-stage-2-traffic-bandwidth.md) — **Medium** — traffic and bandwidth estimates.
- [Remote medical — Stage 1: injection](review/remote-medical-stage-1-injection-session.md) — **High** — the operation session.
- [Remote medical — Stage 2: shrapnel](review/remote-medical-stage-2-shrapnel-multiplayer.md) — **High** — shared per-piece ownership.
- [Remote medical — Stage 3: other actions](review/remote-medical-stage-3-other-actions.md) — **High** — bandage, splint, AED, amputation.
- [Remote medical parity — roadmap](review/remote-medical-native-minigame-parity.md) — **High** — umbrella; CPR stays future.
- [Normative requirements as gates](review/normative-style-unit-test-gates.md) — **High** — the Roslyn gate and rule inventory.
- [Player pain vocalizations and bark](review/sync-player-pain-vocalizations-and-bark.md) — **Medium** — they ride the character-sound event.
- [Metal-scrap placement sound on guest](review/host-metal-scrap-block-place-sound-not-synced-to-guest.md) — **Medium** — sounds ride the sound event.
- [Remote backpack native interaction parity](review/remote-backpack-native-interaction-parity.md) — **Critical** — root container sync.
- [Tab opens the backpack then closes](review/tab-backpack-open-close-immediately.md) — **Medium** — remote Close wrote the radial state.
- [Entity drop loses fresh state on the guest](review/entity-destruction-drop-guest-fresh-state-loss.md) — **Medium** — full drop state is preserved.
- [Interactive in-game command console](review/in-game-command-console-interactive.md) — **High** — the overlay with live suggestions.
- [Command console ESC interception](review/command-console-esc-not-intercepted.md) — **High** — one-frame modal suppression.
- [Guest frame rate lower than host](review/guest-frame-rate-lower-than-host.md) — **Medium** — telemetry and allocation removal.
- [Host entity hit red flash on guest](review/host-entity-hit-red-flash-not-visible-on-guest.md) — **Medium** — the flash rides the damage relay.
- [Remote player medical panel](review/remote-player-medical-panel.md) — **Medium** — native WoundView reuse.
- [Remote medical treatment operations](review/remote-medical-treatment-operations.md) — **Medium** — limb drag routes through the heal path.
- [Remote fentanyl injection desync](review/remote-fentanyl-injection-and-medical-panel-desync.md) — **Critical** — cross-player syringe doses.
- [Remote Medical and visibility](review/remote-context-menu-medical-visible-when-target-not-visible.md) — **Medium** — the shared visibility gate.
- [S2 — Layer-end save and restore](review/save-layer-end-save-and-restore.md) — **High** — the cut, Continue restore and `save.sv`.
- [S3.4b — Native character fields](review/save-native-character-field-parity.md) — **Medium-High** — three fields ride the snapshot.
- [Native run field parity](review/save-native-run-field-parity.md) — **Medium-High** — the frozen per-field record.
- [S1 — Save format and world repository](review/save-format-and-world-repository.md) — **High** — no gameplay wiring.
- [Dead-player context-menu name suffix](review/dead-player-right-click-name-suffix.md) — **Medium** — the localized dead suffix.
- [Guest remote pose/head desync](review/guest-remote-pose-head-orientation-desync.md) — **Medium** — stale clone inputs are neutralized.
- [Host sleepiness posture desync](review/host-severe-sleepiness-posture-desync.md) — **High** — the leg-speed multiplier rides the snapshot.
- [Host fall-injury mouth desync](review/host-fall-injury-mouth-expression-desync.md) — **Medium** — head/mouth state rides the snapshot.
- [Guest background ghost item sounds](review/guest-background-ghost-item-ground-sounds.md) — **Medium** — non-authoritative impacts are suppressed.
- [Command registration attribute refactor](review/command-registration-attribute-refactor.md) — **Medium** — the console registry and mod API.
- [Command tree and completion](review/command-tree-resource-location-selector.md) — **Medium** — tree, catalog and bracket filters.
- [Mod data sync model](review/mod-data-sync-model.md) — **Medium** — the mod-data scope seam.
- [Trade domain dual-side runtime](review/trade-domain-dual-side-runtime.md) — **High** — the dual-side trade pass.
- [World determinism fingerprint](review/world-determinism-world-fingerprint.md) — **High** — the determinism comparison.
- [Block-break first-writer-wins](review/block-break-first-writer-wins.md) — **High** — the dual-side confirmation.
- [Middle-click location marker](review/middle-click-location-marker.md) — **Medium** — the one-shot location ping.
- [CUCoreLib migration support](review/cucorelib-migration-support.md) — **Medium** — the typed KrokMP content seams.
- [Protocol frame validation](review/protocol-frame-validation.md) — **High** — the unified frame validator.
- [Network traffic baseline](review/network-traffic-baseline.md) — **Medium** — frame stats and byte baselines.
- [State-stream bandwidth reduction](review/state-stream-bandwidth-reduction.md) — **Medium** — the guest's own entry is not echoed.
- [Snapshot size reduction](review/snapshot-size-reduction.md) — **Low** — a string table for definition ids.
- [Turret stray fire after reload](review/turret-stray-fire-after-reload.md) — **Medium** — stale transient trap replay is removed.
- [Full-qualified name cleanup](review/full-qualified-name-cleanup.md) — **Low** — a using-directive sweep.
- [Composite command semantics](review/composite-command-sequential-semantics.md) — **Medium** — declaration order on one copy.
- [Projection failure auto-recovery](review/projection-failure-auto-recovery.md) — **High** — the dirty/rebuild loop.
- [ModService ↔ GameAdapter DI cycle](review/mod-service-gameadapter-di-cycle.md) — fixed by injecting ModStatusStore.
- [DI cycle guard / diagnostics](review/di-cycle-guard.md) — ValidateOnBuild and the re-entrancy guard.
- [Legacy View-items remote detail](review/remove-legacy-view-items-remote-inventory-detail.md) — **Low** — the inline path is removed.
- [Idle-sit suppression while carried](review/carried-player-idle-sit-suppression.md) — **Medium** — the native sit pose is suppressed.
- [Carrier sit while carrying](review/carrier-sit-while-carrying.md) — **Medium** — the carrier half of the family.
- [Carry vertical placement asymmetry](review/carry-piggyback-vertical-placement-asymmetry.md) — **Medium** — riders publish the torso anchor.
- [Carry rider position smoothing](review/carry-piggyback-rider-position-smoothing.md) — **Critical** — exact limb poses are suppressed.
- [Guest container ghost drops on host](review/guest-container-contents-ghost-drops-on-host.md) — **Medium** — clone proxies lose instance ids.
- [Trap destruction drop quantity desync](review/trap-destruction-drop-quantity-desync.md) — **Medium** — drops ride the block-damage message.
- [The backlog index duplicates its tickets](review/backlog-index-summary-duplication.md) — **Low-Medium** — the index is a pointer table.
- [Trap-layout snapshot recovery](review/trap-layout-snapshot-recovery.md) — **Medium** — the 60 s repair re-derives and re-sends the layout.
- [The host decides enemy hits on remote players](review/enemy-hit-determination-local.md) — **High** — the victim judges its own hit.
- [Run clock is not sent to a mid-run joiner](review/save-run-clock-not-sent.md) — **Low-Medium** — the clocks travel as their own message.
- [Layer time is not carried](review/save-layer-time-not-carried.md) — **Low-Medium** — the continued layer resumes its timer.
- [Checkpoint chunks vs the run epoch](review/checkpoint-run-epoch-validation.md) — **Low-Medium** — the join instruction carries the run identity.
- [Restore account arm release](review/restore-account-arm-release.md) — **Low** — every release path accounts for its own live-world half.
- [Restore live-object row loops](review/restore-live-object-loops-containment.md) — **Low** — the last three restore loops are contained.
- [Dropped mod command requests](review/mod-command-request-timeout.md) — **Low** — the request deadline and the bounded pending map.
- [Pinyin search for CUO](review/pinyin-search-mod.md) — **Medium** — crafting-UI and console completion.
- [Pinyin search as a standalone mod](review/pinyin-search-standalone-mod.md) — **Medium** — its own in-repo mod; CUO keeps the seam.
- [Native-binding mods declare it](review/mod-native-binding-declaration.md) — **Medium** — the tier model and the manifest declaration.
- [Native-binding session parity](review/mod-native-binding-handshake-parity.md) — **Medium** — the host can require parity.
- [Application layer: first slice](review/application-layer-first-slice.md) — **Medium** — command gateway and the kernel replication move.
- [Adapter capability ports](review/adapter-capability-ports.md) — **Medium** — ten ports; the aggregate declares nothing.

### Future

- [PVP](future/pvp.md) — **Low** — deferred until PvE is stable.
- [KrokMP lower-priority candidates](future/krokmp-candidates.md) — **Low** — voice and vote-kick.
- [Remote medical CPR](future/remote-medical-cpr.md) — **Low** — custom CPR is not native.
- [EnemyCombatOrderPolicy follow-up](future/enemy-combat-order-policy-kernel.md) — **Low** — the kernel-process follow-up.
- [Generic Prediction Runtime](future/generic-prediction-runtime.md) — **Low** — the future prediction architecture.
- [Strict validation / anti-cheat](future/strict-validation-anti-cheat.md) — **Low** — hardening beyond the MVP scope.
- [Phase 5 tooling & ecosystem](future/phase5-tooling-ecosystem.md) — **Low** — the tooling/ecosystem phase.
- [KrokMP compatibility adapter](future/krokmp-compatibility-adapter.md) — **Low** — the compatibility adapter.
- [Handshake identity and refusal reasons](future/handshake-identity-and-refusal-report.md) — **Medium** — a refusal names which dimension failed.
- [Adapter-shell verification harness](future/adapter-shell-verification-harness.md) — **Low** — keeps the live-game half.

### Resolved

- [A dropped enemy attack is never re-issued](resolved/enemy-attack-delivery-recovery.md) — superseded: the victim judges its own hit.
- [IP-direct duplicate names allowed](resolved/ip-direct-duplicate-names.md) — an accepted IP-direct property.
- [check-architecture.ps1 performance](resolved/check-architecture-performance.md) — **Medium** — the script became C# gate tests.
- [Runtime log errors (2026-08-30)](resolved/runtime-log-errors-2026-08-30.md) — HotRepl, not CUO.
- [Sleep behavior policy](resolved/sleep-behavior-policy.md) — sleep stays allowed; no new gate.
- [Command authorization gateway](resolved/command-authorization-gateway.md) — absorbed into the Application-layer ticket.
- [Kernel replication namespace move](resolved/kernel-replication-namespace-relocation.md) — absorbed into the Application-layer ticket.
- [Runtime DI feature lifecycle](resolved/runtime-di-feature-registration-lifecycle.md) — rewritten as the composition-root ticket.

### Done

- [Test suite parallelization](done/test-suite-parallelization.md) — **Medium** — the parallelism contract and splits.
- [In-game command console](done/in-game-command-console.md) — **Low** — the modal UI console.
- [Player-list polish](done/player-list-polish.md) — peer-id disambiguation.
- [Guest-mined block ghost fragments](done/guest-mined-block-ghost-fragments-on-host.md) — air writes clear stale damage.
- [Duplicate unsynced item drops](done/guest-tree-extra-unsynced-drops.md) — same-id materialization dedup.
- [Guest-mined item physics desync](done/guest-mined-item-static-physics-desync.md) — same-id materialization dedup.
- [Remote ragdoll state not visible](done/ragdoll-state-not-visible-to-remote.md) — the X ragdoll is visible remotely.
- [Ragdoll limb pose not synced](done/ragdoll-limb-pose-not-synced.md) — limb poses ride the player stream.
- [Name tag font and edge padding](done/name-tag-font-position-edge-padding.md) — head-anchored, UI-safe markers.
- [High sleepiness squint remotely](done/high-sleepiness-squint-not-visible-remotely.md) — vitals ride the 1 Hz snapshot.
- [Host close-room safe exit](done/host-close-room-safe-exit.md) — the host's safe exit.
- [Configuration profile templates](done/config-profile-templates.md) — the template system.
- [IP-direct display-name validation](done/ip-direct-name-validation.md) — validation for IP-direct joins.
- [World-time manual acceleration](done/world-time-manual-acceleration.md) — the acceleration policy.
- [Player colors and head tags](done/player-color-head-tags.md) — color-only head tags.
- [Item snapshot version gating](done/item-snapshot-event-version-gating.md) — event-version gating.
- [Architecture split pass](done/architecture-split-pass.md) — the architecture split.
- [Typed kernel migration](done/typed-kernel-migration.md) — the typed deterministic kernel.
- [Native content sync coverage](done/native-game-content-sync-coverage.md) — native game-content coverage.

### Watchlist

- [Architecture watchlist](watchlist/architecture-watchlist.md) — files near the 600-line gate.
