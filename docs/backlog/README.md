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

- [Save system: layer-end and mid-run saves](todo/save-system-mid-run-and-layer-end.md) — **High**: layer-end + mid-run saves; new JSON-based, directory-level archive format; mid-run consistency (consistent cut, no over/under-generation); native `save.sv` is layer-boundary only and the kernel checkpoint store is currently unwired; multi-stage, split into stage tickets at implementation start.
- [Guest partial block damage has no re-report](todo/guest-partial-block-damage-re-report.md) — **Medium**: audit gap W2 (split from the W1 ticket when its terminal block state landed); the live relay is delta-based and the authoritative registry + absolute snapshot are host → guest only, so a swallowed partial-damage report is never corrected; the re-report must be absolute with a merge rule that keeps both players' damage.
- [Guest break drops are lost when the break report is swallowed](todo/guest-break-drops-recovery.md) — **Medium**: found while landing W1; a swallowed `BlockDamaged` break report loses its block/building drops (the guest registers its own drops host/solo-side only), and the item keyframe has no fact to reconcile them from.
- [Trap-layout snapshot has no in-session recovery](todo/trap-layout-snapshot-recovery.md) — **Medium**: audit gap W6 (reclassified by the independent review); `TrapLayoutSnapshot` is world-entry/InWorld-edge only and the 60 s cycle does not include it, so a swallowed layout waits for the next edge.
- [Enemy snapshot and attack have no recovery path](todo/enemy-snapshot-and-attack-recovery.md) — **Medium**: audit gap N1; `EnemySnapshot` is world-entry/reconnect only and the 20 Hz stream cannot rebind (no prefab id); a dropped `EnemyAttack` is never re-issued.
- [Guest command loss: local pickup/drop result is not reconciled](todo/guest-command-loss-reconciliation.md) — **Medium**: audit gap I5 + I1 caveat (I5 reclassified by the independent review); a swallowed guest pickup/drop command leaves the local result un-reconciled by the item keyframe, and the keyframe is skipped entirely when the host table is empty.
- [Recipe unlock has no fallback or backfill](todo/recipe-unlock-fallback.md) — **Medium**: audit gap I6; `RecipeUnlock` is one-shot with no world-entry/checkpoint member, so a late joiner never learns a prior unlock (the `CraftReport` item facts are healed by the item keyframe/character snapshot).
- [Carried-inventory registration has no re-report](todo/carried-inventory-registration-re-report.md) — **Medium**: audit gap I8; a swallowed `CarriedInventory` leaves the host's arbitration transfer table empty for those ids (the watermark is monotonic and self-heals; this registration is not).
- [Session control convergence: lost readiness/control messages have no re-report](todo/session-control-convergence.md) — **Medium**: audit gaps R3+R4; a lost `HandshakeAckAck` leaves the host's member unconfirmed forever, `SceneState`/`PlayerJoin` have no re-report, and `WorldSnapshotComplete` is inert (no consumer).
- [Checkpoint chunks are not validated against the current run epoch](todo/checkpoint-run-epoch-validation.md) — **Low-Medium**: audit finding K2; `HandleCheckpoint` restores any complete chunk set and the assembler only checks intra-set consistency, so a late previous-run set could move the epoch backwards (surfaced by the stale-epoch stream fix).
- [Mod command requests dropped by the rate limiter leave pending callbacks unresolved](todo/mod-command-request-timeout.md) — **Low**: audit finding N8; the documented rate-limit drop is accepted for `ModMessage`, but a dropped `ModCommandRequest` leaves the caller's callback pending until session end (no timeout/failure frame).
- [Sync cadence review: fallback stretch limits and first-resend latency](todo/sync-cadence-review.md) — **Medium**: audit cadence findings; item keyframe 5 s→30 s, trader 5 s→30 s, fluid full viewport 1 s→10 s, and the single 60 s block/damage/keypad/geyser first resend need measured decisions.
- [Systemic save and backup management](todo/systemic-save-backup-management.md) — **Medium (user-promoted from future)**: manual/scheduled backup, retention, restore/import, native game-layer backup, migration; reuses the save-system package format, does not define a second one.
- [CasualtiesUnknownOnline.Pinyin: pinyin search for CUO](todo/pinyin-search-mod.md) — **Medium**: port standalone pinyin search into a CUO mod; crafting UI pinyin search plus command completion by id/name/pinyin; extra interface extraction needed for command-side pinyin support; pinyin search is a configurable toggle defaulting to on for Simplified Chinese players.
- [Runtime entity creation can be absorbed by a markerless same-prefab copy](todo/runtime-entity-markerless-bind-absorption.md) — **Low-Medium**: found by the round-4 re-review of E3; the exact-key bind falls back to a MARKERLESS same-prefab copy inside 1 m (enemy-domain backfill or generated entity) and then stamps it, so a runtime creation landing next to an unrelated markerless copy exists only on the creator. Fix direction: give the enemy backfill copy the creation key (N1) and delete the positional pass.
- [Dead runtime-entity relay API: `BroadcastEntitySpawned` has no caller](todo/runtime-entity-dead-api-cleanup.md) — **Low**: found by the round-4 re-review of E3; the source-excluding relay method and its forwarding chain have no call site (the live relay includes the source so its echo acknowledges the report). Delete it or wire it to the caller that genuinely needs source-exclusion.

### In progress

### Review
- [Guest world-block mutations have no periodic re-report](review/guest-block-mutation-re-report.md) — **High**: audit gap W1 landed; the guest keeps a bounded pending block-report table (`PendingBlockReportTable`), re-reports it every 60 s through the shared Runtime fallback pump (`WorldReportFallbackPump` + `PendingReportFallback`, which also drive the E3 entity reports) until the host answers, and drops each entry on the host's relay echo or correction (the world-entry marker deliberately does not clear it; a new world/layer apply does); the host now answers every report (accepted relays include the reporter; a refused report gets the host's cell as a targeted correction) and records + relays the air transition when a remote damage report breaks the host's block; 10 new tests (two reds recorded), matrix row W1 moved to OK, an independent adversarial review closed with its two major findings fixed; two follow-up tickets opened (W2 partial damage, break-drop loss); awaiting the final unified acceptance pass.
- [Runtime-created BuildingEntity spawns have no backfill or re-report](review/runtime-entity-spawn-backfill.md) — **Medium-High**: audit gap E3 landed; the host keeps a bounded accepted-creation table keyed by the creation-instance identity (prefab id + creation cell + creator SteamId + monotonic sequence) and re-broadcasts it absolutely in the world-entry group and on the 60 s cycle (`RuntimeEntitySnapshot`), the guest re-reports unacknowledged creations through the shared fallback pump, an accepted animal is acknowledged by the snapshot's key list only, an unmaterializable report is still accepted and relayed (accept-first), and a shared materializer replaced the `Resources.Load` pre-checks so MOD buildings and MOD animals materialize; the round-3 review's four MAJOR + one MINOR findings are fixed with red→green regression tests; 2 665 + 32 tests, format clean, matrix row E3 moved to OK; awaiting the final unified acceptance pass.
- [Sync completeness audit: event-level sync + periodic fallback](review/sync-event-and-periodic-fallback-coverage-audit.md) — **High**: 64-row evidence matrix (A–D) with 790 verified evidence entries (505 collector + 285 inline anchors, `docs/evidence/sync-coverage-evidence.json`) and zero `Unverified` rows; 47 OK / 8 Event-only gap / 0 Fallback-only gap / 9 Transient-by-design; the user's world-block seed finding confirmed (W1, closed 2026-09-09 by `review/guest-block-mutation-re-report.md`) and split into 8 gap tickets + 3 finding/cadence tickets + 2 follow-ups found while landing W1; gap E3 closed by `review/runtime-entity-spawn-backfill.md` after its round-3 findings were fixed; `SyncCoverageGateTests` rot guard (full-path-only refs, index→row mention, 64-row floor, verdict-summary consistency, gap-ticket existence, inline-ref anchoring and inline-quote verification, evidence quote verification, negative-contract self-tests); active-doc corrections and one bundled protocol fix (stale-epoch state streams dropped + restored-checkpoint epoch adoption, both red→green); three independent adversarial review rounds closed with their blockers fixed and re-verified; awaiting the final unified acceptance pass.
- [Namespaced ID system](review/id-system-namespaced-ids.md) — **Medium**: `ContentId` (`namespace:path`) vocabulary in Abstractions, `cu` built-in namespace, `[CuoMod(Namespace=...)]` declaration validated at discovery with per-candidate identity claims, canonical mod content ids, and a content-driven resource-location catalog; 2593 + 17 tests, format clean, deployed-hash verification, two independent adversarial review rounds closed; awaiting the final unified acceptance pass.
- [Command completion for the ID system: id/name search](review/command-id-name-completion.md) — **Medium**: bundled with the namespaced ID system; `ResourceLocation` completion by canonical id, bare path, or localized display name, always inserting the canonical `cu:id`; awaiting the final unified acceptance pass.
- [Remote backpack item projection acceptance issues](review/remote-backpack-item-projection-acceptance-issues.md) — **High**: dedicated remote-backpack Tab transfer (`TransferToRequester`), held-remote-item self-use (`UseOnSelf`), drain-object pour/edge-drop routing, non-destructive same-owner container apply, main-hand slot routing, and unified source-value durability projection landed; 2529 + 17 tests, full gates, deployed-hash verification complete; awaiting final unified acceptance pass.
- [Global unified projection framework](review/global-projection-framework.md) — **High**: macro/global projection system for all domains; typed contract with seven registered rebuildable domains (items, fluids, world-entities, run, players-carry, remote-character-presentation, mod-status); enemy/player continuous and remote-presentation read-source boundaries audited; full build, 2516 + 17 tests, three independent adversarial reviews and deployed-hash verification complete; awaiting final unified acceptance pass.
- [Unified remote display projection rework](review/unified-remote-display-projection-rework.md) — **High**: three per-field projection helpers absorbed into one unified remote display projection seam; item source-value path consolidated; old helpers deleted; full build, 2506 + 17 tests, independent adversarial review and deployed-hash verification complete; awaiting final unified acceptance pass.
- [Remote medical panel acceptance issues (mood cadence / breathing icon / ECG)](review/remote-medical-panel-acceptance-issues.md) — **High**: display-body projection now runs the native opiate ramp, projects breathing/respiratory readout, advances the display ECG, and the ECG redirect is a Postfix; full suite and deployed-hash verification complete; awaiting final unified acceptance pass.
- [Global adaptive report-rate / sync-frequency flow control — roadmap](review/global-adaptive-report-rate-flow-control.md) — **Medium (user-promoted from future)**: umbrella roadmap; Stages 1-4 all code-complete and in review; awaiting final unified acceptance pass.
- [Global adaptive report-rate flow control — Stage 4: remaining high-frequency domain streams](review/global-adaptive-report-rate-stage-4-high-frequency-domains.md) — **Medium**: interval-based adaptive cadence for item move/snapshot, fluid diff/full, and trader fallback; item/fluid/trader senders query the shared rate service; session reset coverage; full build, 2481 + 16 gates, two independent adversarial self-checks, deployed-hash verification complete; awaiting final unified acceptance pass.
- [Global adaptive report-rate flow control — Stage 3: cumulative stream coalescing](review/global-adaptive-report-rate-stage-3-cumulative-streams.md) — **Medium**: reliable coalescing for medical injection frame-level deltas, per-piece shrapnel ordinary position coalescing, per-stream `BaseHz`, dedicated medical traffic classification, shared operation-id allocator; full build, 2462 + 16 gates, three independent adversarial self-checks, deployed-hash verification complete; awaiting final unified acceptance pass.
- [Global adaptive report-rate flow control — Stage 1: stream taxonomy + health-driven overwrite governor](review/global-adaptive-report-rate-stage-1-global-governor.md) — **Medium**: explicit reliable/unreliable/cumulative delivery modes, pressure classifier + rate policy, shared `AdaptiveStreamRateService`, and Player/Enemy/Tutorial overwrite streams integrated; full build, 2417 + 16 gates, independent adversarial self-check, deployed-hash verification complete; awaiting final unified acceptance pass.
- [Global adaptive report-rate flow control — Stage 2: per-peer/per-stream traffic & bandwidth estimates](review/global-adaptive-report-rate-stage-2-traffic-bandwidth.md) — **Medium**: per-peer × per-stream traffic measurement, bandwidth/failed-send pressure inputs, per-stream byte-budget policy tuning; full build, 2451 + 16 gates, three independent adversarial self-checks, deployed-hash verification complete; awaiting final unified acceptance pass.
- [Remote medical parity — Stage 1: medical operation session + real-time injection](review/remote-medical-stage-1-injection-session.md) — **High**: generic MedicalOperationSession protocol, host reservations/terminal semantics, incremental syringe/IV deltas, one-shot injectable path removed, protocol bumped to 12; red→green, full suite, independent adversarial self-check and deployed-hash verification complete; awaiting the final unified acceptance pass.
- [Remote medical parity — Stage 2: multiplayer shrapnel removal](review/remote-medical-stage-2-shrapnel-multiplayer.md) — **High**: shared shrapnel session with atomic per-piece ownership, concurrent operators, read-only observer piece positions, per-operator tweezers item sync, force-ungrab, progress/third-party propagation and failure/cancel semantics; full suite, independent adversarial self-check and deployed-hash verification complete; awaiting the final unified acceptance pass.
- [Remote medical parity — Stage 3: remaining native medical minigames/actions](review/remote-medical-stage-3-other-actions.md) — **High**: bandage/dressing minigame, splint/tourniquet removal, dislocation fix, AED, manual defibrillation, amputation and remaining WoundView actions; full suite, independent adversarial self-check and deployed-hash verification complete; awaiting the final unified acceptance pass.
- [Remote medical native minigame/action parity — roadmap](review/remote-medical-native-minigame-parity.md) — **High**: umbrella ticket; all three native-parity stages are in review; CPR remains future.
- [Convert non-.editorconfig normative requirements into unit-testable gates](review/normative-style-unit-test-gates.md) — Roslyn `dotnet test` gate for unnecessary fully qualified names landed; normative-rule inventory added in `docs/evidence/normative-gates.md`.
- [Sync player pain vocalizations and B-key bark](review/sync-player-pain-vocalizations-and-bark.md) — PantSound pain/yawn/growl/B-bark plus the lockpick-failure `gore2` pain sound ride the existing CharacterSoundMsg event; red→green, full suite, independent adversarial self-check and deployed-hash verification all complete.
- [Host metal-scrap block placement sound not heard on guest](review/host-metal-scrap-block-place-sound-not-synced-to-guest.md) — direct placeable placement sounds (`scrapmetal` / `ropeplace`) now ride the existing `CharacterSoundMsg` one-shot event path with a clip whitelist and conscious-body scope; full suite, independent adversarial self-check and deployed-hash verification complete; awaiting the final unified acceptance pass.
- [Remote backpack native interaction parity](review/remote-backpack-native-interaction-parity.md) — **Critical / super-priority**: top-level root container sync, open-container background drop, guarded owner-side container apply, and detach-before-destroy transfer removal landed; latest DLLs deployed and artifact-verified; awaiting the final unified acceptance pass.
- [Tab opens backpack then closes immediately](review/tab-backpack-open-close-immediately.md) — remote-backpack Close no longer writes the native radial state when no remote focus exists; local Tab now stays open; regression test linked.
- [Entity destruction drops lose fresh-drop presentation/initial motion on the guest view](review/entity-destruction-drop-guest-fresh-state-loss.md) — kernel item-spawn path now preserves full transient initial drop state (velocity/rotation/fresh/angular) to all peers; covers ordinary building/entity deaths in both directions and third-party views; selfcheck linked.
- [Interactive in-game command console](review/in-game-command-console-interactive.md) — redo landed: compact translucent bottom overlay, live Minecraft-style suggestions on `/`, full no-fade history while open, closed-panel fading notifications, aligned input; ESC interception resolved via one-frame modal suppression (see [command console ESC fully intercepted](review/command-console-esc-not-intercepted.md)); selfcheck linked.
- [Command console ESC fully intercepted](review/command-console-esc-not-intercepted.md) — one-frame modal suppression after any CUO ESC-closing surface close (console, Online UI window, quick panel) plus a non-modal pause guard while the quick panel is open swallows the closing ESC before the game's native pause input; regression tests + deployed artifact hash verified.
- [Guest frame rate lower than host with frame drops](review/guest-frame-rate-lower-than-host.md) — guest frame-rate baseline telemetry landed; per-frame RemotePlayers enumeration and guest item-follow key snapshot allocations removed; selfcheck linked.
- [Host entity hit red flash not visible on guest](review/host-entity-hit-red-flash-not-visible-on-guest.md) — melee red HitFlash now rides the existing BuildingEntityDamaged relay as a presentation-only flag; non-attacker/third-party views replay the native flash; selfcheck linked.

- [Remote player medical/health panel](review/remote-player-medical-panel.md) — native WoundView reuse: display-only body copy fed from the 1 Hz character snapshot; custom CUO IMGUI panel removed; selfcheck linked.
- [Remote medical treatment operations](review/remote-medical-treatment-operations.md) — native WoundView limb drag routes through the existing host-validated heal/use path with selected-limb support; selfcheck linked.
- [Remote fentanyl injection bypass and remote medical panel desync](review/remote-fentanyl-injection-and-medical-panel-desync.md) — **Critical**: native syringe minigame now routes cross-player injectable/IV doses with exact delivered ml; remote WoundView display projection, ECG/Moodle redirection and sleep-button disable landed; selfcheck linked; awaiting the final unified acceptance pass.
- [Remote context menu Medical visible when target is not visible](review/remote-context-menu-medical-visible-when-target-not-visible.md) — Medical now follows the same line-of-sight/visibility gate as the other remote actions; fixed in the shared member projection so context menu, Players page, and quick panel stay consistent; selfcheck linked.
- [Dead-player right-click context menu should show a dead status suffix in the title](review/dead-player-right-click-name-suffix.md) — context-menu title and duplicate-name target selector append the localized dead suffix from the existing `IsDead` projection; Players-list dead rendering and wire/protocol unchanged; selfcheck linked.
- [Guest remote pose / head-orientation desync on host view](review/guest-remote-pose-head-orientation-desync.md) — stale render-clone attackCooldown/moveDir auto-flip inputs neutralized; regression contract and selfcheck linked.
- [Host severe sleepiness posture not synced to guest](review/host-severe-sleepiness-posture-desync.md) — owner leg-speed multiplier now rides the 1 Hz character snapshot and is replayed as the CrouchAmount weakness/slouch input on remote clones; selfcheck linked.
- [Host fall injury mouth-expression desync](review/host-fall-injury-mouth-expression-desync.md) — owner head/mouth state now rides the 1 Hz character snapshot and is replayed on remote clones; root cause, not a cosmetic remote-face patch; selfcheck linked.
- [Guest background window plays ghost item friction/ground sounds](review/guest-background-ghost-item-ground-sounds.md) — non-authoritative guest item impact presentation (drop/step/squeak/dust) suppressed; selfcheck linked.
- [Command registration Attribute/reflection refactor](review/command-registration-attribute-refactor.md) — Attribute/reflection console registry + local mod console command API; selfcheck linked.
- [Command tree, resource-location completion, and selector filters](review/command-tree-resource-location-selector.md) — tree/argument-position completion, namespaced candidate catalog, bracketed selector filters; selfcheck linked.
- [Mod data sync model](review/mod-data-sync-model.md) — runtime scope seam landed: local-only / shared / host-authoritative mod data; no generic snapshot protocol.
- [Trade domain dual-side runtime pass](review/trade-domain-dual-side-runtime.md) — #59/#93.
- [World determinism / WorldFingerprint comparison](review/world-determinism-world-fingerprint.md).
- [Block-break first-writer-wins dual-side runtime confirmation](review/block-break-first-writer-wins.md).
- [Middle-click location marker](review/middle-click-location-marker.md) — dedicated one-shot location ping: middle click circle, quick second click exclamation, star relay, 5s fade; selfcheck linked.
- [CUCoreLib migration support](review/cucorelib-migration-support.md) — external KrokMP-based evaluation complete; typed content/status/moodle/runtime seams landed; remaining rows are future/non-goal; selfchecks linked.
- [Protocol frame envelope validation](review/protocol-frame-validation.md) — unified frame validator before kernel handlers; malformed/forged/oversized frames dropped, presentation payloads remain non-fatal.
- [Network traffic baseline and regression gate](review/network-traffic-baseline.md) — per-payload P50/P95 frame stats, live per-peer bytes, checkpoint chunk/size/restore baseline; selfcheck linked.
- [State-stream bandwidth reduction](review/state-stream-bandwidth-reduction.md) — per-recipient player-state stream no longer echoes a guest's own entry; selfcheck linked.
- [Snapshot size reduction](review/snapshot-size-reduction.md) — checkpoint item-definition string table compresses repeated definition ids; selfcheck linked.
- [Auto turret trap fires unexpectedly after reload](review/turret-stray-fire-after-reload.md) — stale periodic checkpoint replay of transient turret/geyser trap states removed; live relay unchanged; selfcheck linked.
- [Full-qualified name cleanup](review/full-qualified-name-cleanup.md) — prefer using directives/aliases; behavior-preserving refactor only.
- [Composite command sequential semantics](review/composite-command-sequential-semantics.md) — inner commands decide/reduce in declaration order on one working copy; atomic rollback and duplicate composite OperationId covered; selfcheck linked.
- [Projection failure auto-recovery](review/projection-failure-auto-recovery.md) — per-domain dirty/rebuild loop: items/fluids/world-entities, degraded after repeated failures; selfcheck linked.
- [ModService ↔ GameAdapter DI cycle](review/mod-service-gameadapter-di-cycle.md) — startup hang fixed by injecting ModStatusStore instead of ModService into the adapter; regression contract test added.
- [DI cycle guard / cycle-path diagnostics](review/di-cycle-guard.md) — composition-root ValidateOnBuild + factory re-entrancy guard; cycle chains logged to BepInEx and latest.log; selfcheck linked.
- [Remove legacy "View items" remote-inventory detail path](review/remove-legacy-view-items-remote-inventory-detail.md) — custom inline inventory expansion and right-click fallback removed; native remote backpack remains the only remote-inventory surface.
- [Suppress native idle-sit while carried](review/carried-player-idle-sit-suppression.md) — carried characters no longer publish/replay/linger in the native sit pose; shared pure CarriedBodyPose rule applied across rider/carrier/peer views.
- [Carrier can sit while carrying a player](review/carrier-sit-while-carrying.md) — carrier half of the same family closed: local carrier cannot enter/linger in native sit, mirror-backed via IPatchBridge.IsLocalCarrier; remote carrier clones suppress sit replay on every peer; selfcheck linked.
- [Carry/piggyback vertical placement asymmetry](review/carry-piggyback-vertical-placement-asymmetry.md) — carried riders publish body root instead of the non-standing torso anchor; shared ride-pose path also mirrors crouch state; selfcheck linked.
- [Carry/piggyback rider position smoothing and movement teleport](review/carry-piggyback-rider-position-smoothing.md) — **Critical**: root cause found in the exact world-space limb-pose stream; conscious/alive carried riders now suppress exact limb poses so remote clones keep HandleVisuals attached to the local-carrier mount; dead/unconscious carry and non-carried ragdolls keep exact poses; full suite 2521 + 17, independent adversarial review, deployed-hash verification complete; awaiting final unified acceptance pass.
- [Guest container contents periodically appear as world drops on the host view](review/guest-container-contents-ghost-drops-on-host.md) — remote clone nested display proxies no longer carry item instance ids; domain lookup cannot address them; selfcheck linked.
- [Trap destruction drops desync in item quantity between host and guest](review/trap-destruction-drop-quantity-desync.md) — support-loss building drops now ride the same `BlockDamagedMsg` as the break; non-breaker sides are marked remote death and receive the full initial drop set; selfcheck linked.

### Future

- [PVP](future/pvp.md) — low priority, deferred until PvE/rules stable.
- [KrokMP lower-priority candidates](future/krokmp-candidates.md) — voice, vote-kick; player-list polish has landed.
- [Remote medical CPR enhancement](future/remote-medical-cpr.md) — KrokMP custom CPR is not native; deferred to future as an enhancement.
- [EnemyCombatOrderPolicy kernel-process follow-up](future/enemy-combat-order-policy-kernel.md).
- [Generic Prediction Runtime](future/generic-prediction-runtime.md).
- [Strict validation / anti-cheat hardening](future/strict-validation-anti-cheat.md).
- [Phase 5 tooling & ecosystem](future/phase5-tooling-ecosystem.md).
- [KrokMP compatibility adapter](future/krokmp-compatibility-adapter.md).
- [Command authorization gateway](future/command-authorization-gateway.md) — central actor/AuthorityKind enforcement in front of the kernel (Loomi review 2026-09-04).
- [Runtime DI feature registration and lifecycle contract](future/runtime-di-feature-registration-lifecycle.md) — feature-scoped composition modules + verified reset/unbind/graph/update-order (Loomi review 2026-09-04).
- [Kernel replication namespace relocation](future/kernel-replication-namespace-relocation.md) — move item-scoped kernel protocol/save services to a neutral namespace (Loomi review 2026-09-04).
- [Adapter-shell sync paths have no automated verification](future/adapter-shell-verification-harness.md) — deferred by decision: the entity-creation materialization/stamping/death key, the deferred geyser queue, the enemy runtime-spawn materializer and the mod template materializer need the live Unity world, so today they are code-reviewed plus the unified dual-client pass only; revisit if adapter-shell regressions keep reaching the user's acceptance step.

### Resolved

- [IP-direct duplicate names allowed](resolved/ip-direct-duplicate-names.md).
- [check-architecture.ps1 performance](resolved/check-architecture-performance.md) — resolved: PowerShell script removed after the architecture gate was ported to C# unit tests; no further work needed.
- [Runtime log errors (2026-08-30)](resolved/runtime-log-errors-2026-08-30.md) — TypeLoadException is HotRepl, not CUO; the OnlineUiOverlay ArgumentException is not in the captured logs.
- [Sleep behavior policy decision](resolved/sleep-behavior-policy.md) — resolved: normal and forced sleep stay allowed; world-time acceleration remains host-authoritative all-unconscious, no new sleep gate/protocol.

### Done

- [Test suite parallelization and runtime efficiency](done/test-suite-parallelization.md) — **Medium (closed 2026-09-09)**: the xUnit v2 suite's parallelism is explicit, safe and effective; Stage 1 landed (explicit runner contract, `GameAssembly` static-state collection + gate, per-node file sink removed, the 135-case critical-path class split into five); Stage 2 landed (the remaining long-pole classes split into behaviour/kind-shard families, the per-node setup cost profiled — the DI graph is 0.41 ms, not the cost — and `DirectionTests` cut from 152 to 6 node constructions via a read-only probe; paired A/B 36.3 s vs the Stage 1 tree's 40.1 s); Stage 3 landed (219 full-stack/game-assembly/socket classes carry `Category=Integration`, the `Category!=Integration` fast subset is 1 291 cases in ~14.5 s, `TestClassSizeGateTests` caps any class at 40 real cases/data rows including inherited/static tests, and the interleaved thread sweep keeps `maxParallelThreads: "1x"`; final suite 2 597 + 20 gates, three-run median 40.1 s and post-hardening confirmations at 37.8/40.7 s). Final unified acceptance passed.
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

