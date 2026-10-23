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
  is code-complete and waiting for the agent's next acceptance batch (`docs/acceptance/`).
- A row's priority is the ticket's own `- Priority:` field (first token), and a ticket that
  declares none — the closed records — gets no priority in its row.
  `BacklogIntegrityGateTests` keeps the rows from drifting back into summaries: a row over
  the length budget, a row whose priority disagrees with its ticket, a ticket missing from
  the index, or one listed under the wrong section fails the build.
- Status is the parent folder, not a field in the file (the file repeats it for readability).
- Code-complete items are moved to `review/` immediately; review is the waiting state
  for the agent's acceptance run (`docs/acceptance/`), which accepts tickets in **batches** —
  one build, one deploy and one two-client session cover every waiting ticket they can serve,
  because a per-ticket session is the loop that costs the most. Code review and green tests
  are not acceptance. While the run's session step is still being built, a ticket whose rows
  need it stays in `review/` with the capability named: waiting is the honest state.
- A passing acceptance record moves its ticket to `done/`; a failed or unproven row moves
  it back to `todo/` with the rejection marked in its status field (`- Status: Todo — Rejected (…)`); a missing dependency keeps it in `review/`
  with the blocker named. Tickets that landed before the run existed still say "the final
  unified acceptance pass" in their own text; that pass is now this run, and it decides them.

## Status folders

| Folder | Meaning |
|---|---|
| `todo/` | Open work, not started |
| `in-progress/` | Active development in progress |
| `review/` | Code/verification done; waiting for the next agent acceptance batch (`docs/acceptance/`) |
| `done/` | Landed / closed |
| `future/` | Deferred, low priority, or future architecture work |
| `resolved/` | Decisions resolved without further code action |
| `watchlist/` | Maintainability / architecture watch items |

## Ticket index

### Todo

- [The placement failure report has no anchor](todo/placement-failure-report-anchor.md) — **Low-Medium** — the applied arm is anchored, the claim is not.
- [Command refusals render as successes](todo/console-command-refusals-render-green.md) — **Low-Medium** — a refused verb still prints green.

- [Drag-use aims at a fixed circle](todo/cross-player-drag-use-model-bounds.md) — **Medium** — judge the model; name the pick on overlap.
- [Cross-player use by drag shows nothing](todo/cross-player-drag-use-feedback.md) — **Medium** — highlight, label and cue sound.
- [A nested container's clone proxy leaks as a world item](todo/nested-container-clone-proxy-leaks-as-world-item.md) — **Medium** — a refused load orphans it.
- [The host rolls a second drop set for a member's death](todo/host-blast-rolls-a-second-drop-set.md) — **Medium** — two sets for one death.
- [A member's drill-pod descent regenerates a layer of its own](todo/pod-descent-on-a-member-regenerates-locally.md) — **Medium** — the panel's sibling.
- [The panel's save-and-exit writes the native save](todo/layer-end-save-and-exit-native-write.md) — **Medium** — decision 165's blind spot.
- [A player's own music, heard by the group](todo/player-music-sync-playback.md) — **Medium** — transfer the file, then play.
- [Remote medical CPR (KrokMP custom)](todo/remote-medical-cpr.md) — **Medium** — promoted; assessment first.
- [The loading screen says what it waits for](todo/loading-screen-progress-detail.md) — **Low-Medium** — reuse the game's own line.
- [Recursive crafting as a standalone mod](todo/recursive-crafting-mod.md) — **Low-Medium** — a third recipe-panel state.
- [Who skips the intro cover, and when](todo/launch-intro-cover-policy.md) — **Low** — a join-flow decision.
- [The opener can claim a key the game is rebinding](todo/enter-opener-keybind-capture.md) — **Low-Medium** — the key-bind capture is not a text field.
- [The trap layout never carries a crystal row](todo/trap-layout-crystal-rows-empty.md) — **Low-Medium** — the kind is read off the component, which has none.
- [A health-usable liquid cannot reach the wound view](todo/topical-limb-action-shape.md) — **Medium** — the action shape decides, not the flag.
### In progress

### Review


- [A dead player can watch the run](review/dead-player-spectating.md) — **Medium** — awaiting the run.
- [The document corpus audit: every page earns its place](review/document-corpus-audit.md) — **Critical** — a reader for every page, or it is not landed.
- [The id resume reads one source](review/id-resume-sources-still-unseeded.md) — **Medium** — both id carriers, in the one namespace ids are issued in.
- [Random rolls in a cross-player item use](review/cross-player-item-use-random-determinism.md) — **High** — rows 2/4/5 pass; row 3 open.
- [The mod item's wearable declaration](review/mod-item-wearable-declaration.md) — **High** — the placement the game's own wear flow reads.
- [Cross-player semantics from the game's own data](review/mod-cross-player-native-semantics.md) — **High** — five chains native; the natives need a batch.
- [The end-of-layer choice must reach every member](review/layer-complete-choice-for-members.md) — **High** — landed; the dead-host row needs a batch.
- [Steam send-limit refusal floods the log and wedges the host](review/steam-transport-send-limit-runaway.md) — **High** — gated; rows 1-4 hold, 5 open.

- [Treatment gore presentation carried](review/treatment-gore-presentation-carried.md) — **Low** — the amputation, shrapnel and suture gore the review found.
- [Sounds whose native call is suppressed](review/suppressed-native-call-sounds-stay-unheard.md) — **Low** — blocked treatment and impact sounds.
- [Local-only item and body sounds](review/unhooked-item-and-body-sound-families.md) — **Low-Medium** — the medical, drink, gesture and body clips are carried.
- [Adapter capability catalog](review/adapter-capability-catalog.md) — **High** — capability ids, Required/Optional classes, probe reasons.
- [Guest pending-report fallback: flat 60 s first resend](review/guest-report-fallback-first-resend.md) — **Low-Medium** — the guest→host entry phase.
- [Sync cadence review](review/sync-cadence-review.md) — **Medium** — fallback stretch limits and first-resend latency.
- [Recipe unlock has no fallback](review/recipe-unlock-fallback.md) — **Medium** — the absolute unlock set backs up the one-shot report.
- [Medical operations are exclusive (one operator at a time)](review/concurrent-medical-operations.md) — **Medium-High** — several operators, one victim.
- [Guest break drops are lost](review/guest-break-drops-recovery.md) — **Medium** — the pending drop table and the idempotent break verdict.
- [S3 — Mid-run consistent cut and world diff](review/save-mid-run-consistent-cut.md) — **High** — the cut seam and exactly-once restore.
- [Save system: layer-end and mid-run saves](review/save-system-mid-run-and-layer-end.md) — **High** — umbrella + design record.
- [Markerless runtime-entity bind absorption](review/runtime-entity-markerless-bind-absorption.md) — **Low-Medium** — positional bind deleted.
- [Unified remote display projection](review/unified-remote-display-projection-rework.md) — **High** — three helpers become one seam.
- [Remote medical — Stage 1: injection](review/remote-medical-stage-1-injection-session.md) — **High** — rows 1-7/9/12-19/21/23 pass; 8/10/20/22 unproven.
- [Speech in the remote panel is not synced](review/remote-medical-panel-speech-text-desync.md) — **High** — rows 1-5/7-8 pass; trade-menu row open.
- [Remote medical — Stage 2: shrapnel](review/remote-medical-stage-2-shrapnel-multiplayer.md) — **High** — shared per-piece ownership.
- [Remote medical — Stage 3: other actions](review/remote-medical-stage-3-other-actions.md) — **High** — bandage, splint, AED, amputation.
- [Remote medical parity — roadmap](review/remote-medical-native-minigame-parity.md) — **High** — umbrella; CPR stays future.
- [Player pain vocalizations and bark](review/sync-player-pain-vocalizations-and-bark.md) — **Medium** — they ride the character-sound event.
- [Tab opens the backpack then closes](review/tab-backpack-open-close-immediately.md) — **Medium** — remote Close wrote the radial state.
- [Entity drop loses fresh state on the guest](review/entity-destruction-drop-guest-fresh-state-loss.md) — **Medium** — full drop state is preserved.
- [Host entity hit red flash on guest](review/host-entity-hit-red-flash-not-visible-on-guest.md) — **Medium** — the flash rides the damage relay.
- [Remote player medical panel](review/remote-player-medical-panel.md) — **Medium** — native WoundView reuse.
- [Remote medical treatment operations](review/remote-medical-treatment-operations.md) — **Medium** — limb drag routes through the heal path.
- [Remote Medical and visibility](review/remote-context-menu-medical-visible-when-target-not-visible.md) — **Medium** — the shared visibility gate.
- [S2 — Layer-end save and restore](review/save-layer-end-save-and-restore.md) — **High** — the cut, Continue restore and `save.sv`.
- [S3.4b — Native character fields](review/save-native-character-field-parity.md) — **Medium-High** — three fields ride the snapshot.
- [Native run field parity](review/save-native-run-field-parity.md) — **Medium-High** — the frozen per-field record.
- [Dead-player context-menu name suffix](review/dead-player-right-click-name-suffix.md) — **Medium** — the localized dead suffix.
- [Guest remote pose/head desync](review/guest-remote-pose-head-orientation-desync.md) — **Medium** — stale clone inputs are neutralized.
- [Guest background ghost item sounds](review/guest-background-ghost-item-ground-sounds.md) — **Medium** — non-authoritative impacts are suppressed.
- [Trade domain dual-side runtime](review/trade-domain-dual-side-runtime.md) — **High** — the dual-side trade pass.
- [Middle-click location marker](review/middle-click-location-marker.md) — **Medium** — the one-shot location ping.
- [Turret stray fire after reload](review/turret-stray-fire-after-reload.md) — **Medium** — stale transient trap replay is removed.
- [The host decides enemy hits on remote players](review/enemy-hit-determination-local.md) — **High** — the victim judges its own hit.
- [Pinyin search for CUO](review/pinyin-search-mod.md) — **Medium** — crafting-UI and console completion.
- [Pinyin search as a standalone mod](review/pinyin-search-standalone-mod.md) — **Medium** — its own in-repo mod; CUO keeps the seam.
- [Plugin as a host shell](review/plugin-host-shell.md) — **Medium** — the adapter's own composition, a presentation host, and no game assembly.

- [Remote medical panel hides actions](review/remote-medical-panel-hide-local-only-actions.md) — **Medium** — hidden, not disabled; both switch paths blocked.
- [Two native DamageBlock callers stay unhooked](review/unhooked-damage-block-callers.md) — **Low-Medium** — crush + burrow coverage; the echo is fixed.

### Future

- [PVP](future/pvp.md) — **Low** — deferred until PvE is stable.
- [KrokMP lower-priority candidates](future/krokmp-candidates.md) — **Low** — voice and vote-kick.
- [EnemyCombatOrderPolicy follow-up](future/enemy-combat-order-policy-kernel.md) — **Low** — the kernel-process follow-up.
- [Generic Prediction Runtime](future/generic-prediction-runtime.md) — **Low** — the future prediction architecture.
- [Strict validation / anti-cheat](future/strict-validation-anti-cheat.md) — **Low** — hardening beyond the MVP scope.
- [Phase 5 tooling & ecosystem](future/phase5-tooling-ecosystem.md) — **Low** — the tooling/ecosystem phase.
- [KrokMP compatibility adapter](future/krokmp-compatibility-adapter.md) — **Low** — the compatibility adapter.
- [Handshake identity and refusal reasons](future/handshake-identity-and-refusal-report.md) — **Medium** — a refusal names which dimension failed.
- [Adapter-shell verification harness](future/adapter-shell-verification-harness.md) — **Low** — keeps the live-game half.
- [The wire surface is recorded beside the protocol version](future/wire-surface-baseline.md) — **Medium** — the schema moves without the number.

### Resolved

- [A dropped enemy attack is never re-issued](resolved/enemy-attack-delivery-recovery.md) — superseded: the victim judges its own hit.
- [IP-direct duplicate names allowed](resolved/ip-direct-duplicate-names.md) — an accepted IP-direct property.
- [check-architecture.ps1 performance](resolved/check-architecture-performance.md) — **Medium** — the script became C# gate tests.
- [Runtime log errors (2026-08-30)](resolved/runtime-log-errors-2026-08-30.md) — HotRepl, not CUO.
- [Sleep behavior policy](resolved/sleep-behavior-policy.md) — sleep stays allowed; no new gate.
- [Command authorization gateway](resolved/command-authorization-gateway.md) — absorbed into the Application-layer ticket.
- [Kernel replication namespace move](resolved/kernel-replication-namespace-relocation.md) — absorbed into the Application-layer ticket.
- [Runtime DI feature lifecycle](resolved/runtime-di-feature-registration-lifecycle.md) — rewritten as the composition-root ticket.
- [Remote backpack parity](resolved/remote-backpack-native-interaction-parity.md) — **Critical** — absorbed: its routing table is deleted.
- [Remote backpack item projection](resolved/remote-backpack-item-projection-acceptance-issues.md) — **High** — absorbed by the native-intent rework.
- [The item identity bridge](resolved/item-identity-bridge.md) — **Low** — resolved without code: the index already runs both ways.
- [Bilingual human documentation](resolved/bilingual-human-docs.md) — **Medium** — absorbed by the two-block `en/` + `zh/` standard.

### Done
- [The decision register cannot take another decision](done/decision-register-budget.md) — **Low-Medium** — done: split by number.
- [Interaction gates judged by the two clients](done/remote-interaction-local-gating.md) — **Medium-High** — accepted: actor and target judge their own side.
- [Single-file package for the core plugin](done/single-file-plugin-package.md) — **Low-Medium** — accepted: one DLL packs the core; extensions stay separate.
- [Descent ambience parity](done/layer-descent-audio-parity.md) — **Low-Medium** — accepted: first-layer by design; the member hears it.
- [Heal restores a severed limb](done/heal-restores-severed-limbs.md) — **Medium** — accepted: restore, save survival and vignette clear.
- [A refused intent reaches the actor](done/intent-refusal-never-reaches-the-actor.md) — **Medium** — accepted: the reason reaches the player.
- [No opaque payloads in the mod API](done/mod-api-no-opaque-envelopes.md) — **Critical** — accepted: no live `byte[]`; the leaf is typed.
- [Mod-defined wire packets](done/mod-defined-wire-packets.md) — **High** — accepted: example packets reach their peers.
- [Give an item to another player](done/give-item-to-another-player.md) — **Medium** — accepted 2026-10-24; every row judged.
- [Nested member types are contracts](done/mod-content-nested-member-contracts.md) — **Medium** — accepted: a mod's own tool.
- [Content declarations by attribute](done/mod-content-attribute-declarations.md) — **High** — accepted: item + recipe materialize.
- [Two peers that materialize different content are never compared](done/mod-content-fingerprint.md) — **Medium** — accepted 2026-10-24: the mismatch is named.
- [The receiver's placement never returns to the host](done/placement-outcome-never-returns.md) — **Medium** — accepted 2026-10-24; rows 1-5 + ruling.
- [Limb install from a bag](done/container-child-limb-install-keeps-a-contained-record.md) — **Low-Medium** — accepted.
- [S4 — Multiplayer restore and backups](done/save-multiplayer-restore-and-backups.md) — **High** — closed 2026-10-24: all stages and rows.
- [S4.1 — Restore claim](done/save-guest-restore-claim-and-legacy-store-retirement.md) — **High** — accepted 2026-10-24: the IP-direct claim rows.
- [S4.2 — The restore account surface](done/save-restore-account-surface.md) — **High** — accepted 2026-10-24: row 2's ambiguous claim.
- [A drink measurement can run a destructive action](done/drink-measurement-destructive-use-action.md) — **Medium-High** — accepted: classified first.
- [Four tracked docs carry code-page mojibake](done/docs-mojibake-cleanup.md) — **Low** — restored 2026-10-24; six files, no damaged bytes left.
- [Enter opens the console as plain chat](done/enter-key-opens-plain-chat.md) — **Low** — accepted 2026-10-24: E2 passes after the guard fix.
- [Native-binding session parity](done/mod-native-binding-handshake-parity.md) — **Medium** — accepted 2026-10-24: the Admin row renders.
- [Cheat permission and the op commands](done/cheat-permission-and-op-commands.md) — **Medium-High** — accepted 2026-10-24: the IP-direct row passed.
- [A typo in a rule's config value silently widens it](done/config-clamp-silently-widens-rule-levels.md) — **Medium-High** — accepted: fallback first.
- [The delivery-checklist gate reads no census](done/delivery-checklist-gate-census.md) — **Low** — accepted 2026-10-24; two censuses now guard it.
- [Live tickets cite the deleted `docs/api/` pages](done/stale-api-doc-paths-in-live-tickets.md) — **Low** — swept 2026-10-24; paths resolve.
- [The console completes only what you may run](done/console-completion-hides-what-you-cannot-run.md) — **Medium-High** — accepted in `20261024-c`; hidden word
- [/gamerule joins the op family](done/gamerule-joins-the-op-family.md) — **Medium-High** — accepted in `20261024-b`; a granted member changes rules.
- [A host rule is read and changed with `/gamerule`](done/gamerule-host-rule-command.md) — **Medium-High** — accepted in `20261023-j`; `/hostrules` gone.
- [The give gesture respects the player's own UI](done/give-gesture-own-ui-and-center-use.md) — **Medium-High** — accepted in `20261023-j`; row 7 read last.
- [A refused child of a restored container keeps its id](done/restore-refused-child-keeps-its-id.md) — **Low-Medium** — `20261023-i`; it falls to the ground.
- [Who may operate another player's backpack](done/remote-backpack-access-policy.md) — **Medium** — accepted in `20261022-d`; one three-way rule.
- [The archive's two id carriers can disagree](done/id-carriers-can-disagree-about-placement.md) — **Low** — accepted in `20261022-c`; one placement per id.
- [Members do not arrive together on a new layer](done/layer-descent-spawn-separation.md) — **Medium** — accepted in `20261021-j`; old-layer positions refused.
- [A layer change pulls members back to the menu](done/layer-change-menu-round-trip.md) — **Medium** — accepted in `20261021-i`; the group loads together.
- [Enemy sync per-row roster adoption](done/enemy-generation-pairing-late-join.md) — **High** — accepted in `20261021-g`.
- [The snapshot overtakes the item reports](done/snapshot-overtakes-item-reports.md) — **Medium** — the fact now goes first; `20261025-c` read zero divergence.
- [A craft report never retires a carried material](done/craft-carried-fact-retirement.md) — **Medium** — `20261025-d` read the removals at zero divergence.
- [A container child moved into a slot loses its container fact](done/container-content-event-gap-on-repick.md) — **Medium** — accepted in `20261025-b`.
- [A pending-drop pickup reports only a slot re-home](done/pickup-early-return-kernel-relocation.md) — **Low-Medium** — accepted in `20261025-b`.
- [A health-usable liquid in a drinkable container](done/topical-live-stack-family-order.md) — **Low-Medium** — accepted in `20261020-b`.
- [A member that lost its body to a layer change](done/layer-change-member-recovery.md) — **Medium** — accepted in `20261025-a`; the entry is re-asked.
- [Limb-tool protections weaker than recorded](done/limb-tool-family-protection-gaps.md) — **High** — accepted; the picked limb is served, the other refused.
- [An item id can be re-issued after a restore](done/item-id-counter-is-not-resumed-by-a-restore.md) — **High** — accepted; the restore resumes the counter.
- [Guest frame rate lower than host](done/guest-frame-rate-lower-than-host.md) — **Medium** — accepted; the guest's pump was not lower.
- [The off-screen arrow's label escapes the screen edge](done/offscreen-arrow-label-clipping.md) — **Low** — accepted; four edge frames, corner clamp.
- [The item data read verb](done/item-data-read-commands.md) — **Medium-High** — accepted; rows 5b and 6 on a three-client session.
- [Online UI layout and input detail pass](done/online-ui-layout-and-input-detail-pass.md) — **High** — accepted; the dropdown list and the top-edge title bar.
- [The member reports that still have no bound](done/member-report-bounds.md) — **Medium-High** — the census rows with no anchor.
- [Health on every client, reconciled to the host](done/health-simulation-and-host-authority.md) — **Medium-High** — accepted; the host model and its cost.
- [An item's identity across a transform](done/item-identity-across-transforms.md) — **High** — accepted; applied and taken-off items keep their id.
- [Heal restores severed limbs and clears hollow](done/heal-command-limb-and-hollow-restore.md) — **Medium** — the owed save/reconnect row passes.
- [A /heal reset cannot restore host vitals](done/heal-reset-does-not-restore-host-owned-vitals.md) — **High** — the applied event restores them.
- [/heal resets a player's state as a cheat command](done/heal-cheat-command-state-reset.md) — **Medium-High** — accepted; full reset, dead revived.
- [Paired controls and page scroll](done/online-ui-paired-controls-and-page-scroll.md) — **Medium-High** — accepted; the frame is the user's look.
- [Interactive in-game command console](done/in-game-command-console-interactive.md) — **High** — accepted; the overlay, the suggestions and the history.
- [Command console ESC interception](done/command-console-esc-not-intercepted.md) — **High** — accepted; the one-frame suppression holds and releases.
- [Item and entity data commands](done/item-and-entity-data-commands.md) — **Medium-High** — `/data`, `/give` and `/summon`; all ten rows accepted.
- [The full test suite takes four minutes](done/test-suite-wall-clock.md) — **Medium** — the measured two phases and the host bound behind a slow run.
- [The `/give` command](done/give-item-to-a-player-command.md) — **Medium-High** — the SNBT give and the destination ladder; all twelve rows accepted.
- [The item state language](done/snbt-state-language.md) — **High** — decision 264's SNBT parser, paths and completion; accepted by batch `20261018-q`.
- [Mod content ceiling](done/mod-content-ceiling.md) — **High** — the platform inventory: every stage cut, Part 3.C parked.
- [Source, permission and data checks](done/validation-and-source-checks.md) — **Medium-High** — accepted; every machine row read.
- [The radiation line stops above the layer floor](done/radiation-line-floor-stop.md) — **Medium** — a host switch, default on.
- [The version shows the build's commit](done/version-string-build-suffix.md) — **Low** — the log, the UI and the tools.
- [A break log line names the wrong shape](done/block-break-log-names-the-wrong-shape.md) — **Low** — it names the shape it judged.
- [The body census should name a carried item's own instance id](done/item-census-own-instance-id.md) — **Low-Medium** — the id a write target asks for.

- [Host rules reach the members, and both sides check](done/host-rules-replication.md) — **Medium-High** — accepted; the announcement and the two-sided check.
- [Online UI art and controls are placeholders](done/online-ui-art-and-controls-overhaul.md) — **High** — accepted; the blocks read and size as declared.
- [The item data write verb](done/item-data-write-commands.md) — **Medium-High** — `/data set` for the holder; a member's write is the host's rule.
- [Crafting-quality labels for mod content](done/mod-crafting-quality-labels.md) — **High** — accepted; the item surface plus a load-time reference check.
- [A null collection in a content payload must mean "none"](done/mod-payload-null-collection-tolerance.md) — **Medium** — accepted; both ends answer for it.
- [Content kinds with no provider](done/mod-content-kind-with-no-provider.md) — **Medium-High** — accepted; the vocabulary names what binds.
- [Typed seams, not object handles](done/mod-api-typed-seams.md) — **Critical** — accepted; one typed projection per operation; a framework seam left.
- [Content registration carries its type](done/mod-content-typed-registration.md) — **Critical** — accepted; a typed definition instead of an opaque payload.
- [Dropped mod command requests](done/mod-command-request-timeout.md) — **Low** — accepted; the request deadline and the bounded pending map.
- [Session control convergence](done/session-control-convergence.md) — **Medium** — accepted; the bounded scene re-report window and the re-ack loop.
- [World-entry trap layout staleness](done/trap-layout-entry-snapshot-staleness.md) — **Low-Medium** — accepted; the send path re-derives the live table.
- [Trap-layout snapshot recovery](done/trap-layout-snapshot-recovery.md) — **Medium** — accepted; the 60 s repair re-derives and re-sends the layout.
- [Namespaced ID system](done/id-system-namespaced-ids.md) — **Medium** — accepted; the `ContentId` vocabulary.
- [CUCoreLib migration support](done/cucorelib-migration-support.md) — **Medium** — accepted; the typed KrokMP content seams.
- [Adaptive report rate — roadmap](done/global-adaptive-report-rate-flow-control.md) — **Medium** — accepted; stages 1-4 landed.
- [Adaptive report rate — Stage 1](done/global-adaptive-report-rate-stage-1-global-governor.md) — **Medium** — accepted; taxonomy and the shared governor.
- [Adaptive report rate — Stage 2](done/global-adaptive-report-rate-stage-2-traffic-bandwidth.md) — **Medium** — accepted; traffic and bandwidth estimates.
- [Adaptive report rate — Stage 3](done/global-adaptive-report-rate-stage-3-cumulative-streams.md) — **Medium** — accepted; cumulative coalescing.
- [Adaptive report rate — Stage 4](done/global-adaptive-report-rate-stage-4-high-frequency-domains.md) — **Medium** — accepted; item/fluid/trader cadence.
- [Global unified projection framework](done/global-projection-framework.md) — **High** — accepted; the rebuildable-domain contract.
- [The root README as a language pair](done/readme-language-pair.md) — **Low-Medium** — accepted; English first, a Chinese twin, and the current model named.
- [A game-side throw inside a scope-entering patched frame latches CallContext off](done/call-context-scope-leak.md) — **High** — accepted; one line per leak.
- [A mod item that declares no tags throws inside the game's own frames](done/mod-item-with-no-declared-tags-throws.md) — **High** — accepted on both peers.
- [The history carries machine identifiers](done/purge-machine-identifiers-from-history.md) — **High** — rewritten and force-pushed; a check, not a gate.
- [The records carry the machine's real Steam ids](done/acceptance-records-carry-real-steam-ids.md) — **Medium** — swept; its gate refused (261).
- [Mod-authored effects](done/mod-authored-effects.md) — **High** — accepted; the drink effect lands on the drinker and both guards were driven.
- [Mod-authored item actions](done/mod-item-authored-actions.md) — **High** — accepted; the cross-player limb chain runs it on the treated client.
- [A declared behaviour with no function](done/mod-declared-behaviour-with-no-function.md) — **High** — accepted; both reporting paths and the tile refusal.
- [The item declaration reaches every field the game reads](done/mod-item-declaration-parity.md) — **High** — accepted; the gate judged on both peers.
- [Cross-player solid food from the game's own data](done/mod-cross-player-solid-food-semantics.md) — **High** — accepted; row 5 judged on the machine read.
- [Remote inventory native parity rework](done/remote-inventory-native-parity-rework.md) — **Critical** — accepted; row 7's favourite half judged.
- [A favourite mark is local and saved](done/favourite-marks-are-local-and-saved.md) — **High** — accepted; the gesture half included.
- [A removed limb component stays on the body](done/medical-removal-limb-component-not-applied.md) — **Critical** — accepted; rows 1-6.
- [A live body under the medical focus runs native components](done/display-clone-limb-component-runs-native-start.md) — **High** — accepted.
- [A state that names no kind reads as an injection](done/medical-state-names-no-kind.md) — **Critical** — accepted; row 4 re-ran green.
- [Layer changes drop members and storm the log](done/layer-change-member-dropout.md) — **Medium** — both members held when both are parked.
- [A member out of the world warns once per clone per frame](done/remote-clone-warning-storm-on-member-dropout.md) — **Medium** — bounded per subject.
- [Container moves reach the viewer as a snapshot](done/container-move-snapshot-only-sync.md) — **Medium** — classifies on the departure; control now read.
- [A same-frame second drop overwrites the pending report of the first](done/drop-pending-single-slot-overwrite.md) — **Medium** — world half passes 2/2.
- [A second drop report at the same position loses its world object](done/second-drop-report-loses-its-world-object.md) — **Medium** — no proxy is adopted.
- [Local item lands inside a remote display proxy](done/local-item-into-remote-display.md) — **Medium** — the seam refuses a display-proxy target.
- [Guest command loss is not reconciled](done/guest-command-loss-reconciliation.md) — **Medium** — the unacknowledged item reports re-report until judged.
- [An item can be operated on before its creation is registered](done/item-creation-registration-first.md) — **Medium-High** — creation first; tombstone.
- [Enemy snapshot binding has no recovery path](done/enemy-snapshot-binding-recovery.md) — **Medium** — the spawn-anchor key and the repair carrier.
- [World/layer generation identity](done/world-layer-generation-identity.md) — **Medium** — the run baseline rides the cell-keyed reports.
- [Remaining generation-relative families](done/generation-identity-remaining-families.md) — **Medium** — trap layout and entity creation.
- [Runtime-created entity spawn backfill](done/runtime-entity-spawn-backfill.md) — **Medium-High** — the accepted-creation table and the one template path.
- [Unrepresentable runtime creations are rejected](done/runtime-entity-creation-rejection.md) — **High** — the creator's copy is destroyed.
- [World determinism fingerprint](done/world-determinism-world-fingerprint.md) — **High** — the entry and post-mutation pairs agree.
- [A member's generation spanning the host's absence](done/guest-generation-segments-over-host-absence.md) — **High** — host-wait yields are not segments.
- [Guest block mutations: periodic re-report](done/guest-block-mutation-re-report.md) — **High** — the pending table and 60 s pump.
- [Re-entering member generates before the restored baseline](done/reenter-baseline-adoption.md) — **Medium-High** — the invite carries the baseline.
- [A Continue after a run opens a stale world](done/layer-mod-baseline-divergence-on-continue.md) — **High** — the Continue target moves on a run's first cut.
- [Sandboxed clients: NullReferenceException bursts](done/sandbox-client-null-reference-bursts.md) — **Low** — out-of-world item streams; re-entry converges.
- [Host classifies generation enemies as runtime spawns](done/enemy-runtime-spawn-classification.md) — **Medium** — one rule recorded at the entity's Start.
- [Block-damage table capacity alignment](done/block-damage-table-capacity-alignment.md) — **Medium** — CUO's second registry is deleted.
- [Recipe `// args:` gate and CRLF working trees](done/recipe-args-crlf-gate.md) — **Medium** — a CRLF checkout feeds `\r` into the last argument.
- [Trap destruction drop quantity desync](done/trap-destruction-drop-quantity-desync.md) — **Medium** — drops ride the block-damage message.
- [Block-break first-writer-wins](done/block-break-first-writer-wins.md) — **High** — the dual-side confirmation.
- [An earthquake's clock write ends an acceleration](done/world-acceleration-quake-direct-write.md) — **Medium** — the host adopts it, vanilla-style.
- [World-time acceleration is gated on being asleep](done/world-time-local-initiation.md) — **Medium** — local-first initiation; the reset is superseded.
- [Manual world acceleration must not end when a player moves](done/world-acceleration-survives-movement.md) — **High** — only announced speeds own the clock.
- [The backlog index duplicates its tickets](done/backlog-index-summary-duplication.md) — **Low-Medium** — the index is a pointer table.
- [Composition root feature modules](done/composition-root-feature-modules.md) — **Medium** — feature modules, one reset contract, binding gate.
- [Evidence matrix fat rows](done/evidence-matrix-fat-rows-split.md) — **Low-Medium** — a count plus the evidence file.
- [Snapshot size reduction](done/snapshot-size-reduction.md) — **Low** — a string table for definition ids.
- [DI cycle guard / diagnostics](done/di-cycle-guard.md) — ValidateOnBuild and the re-entrancy guard.
- [The one-top-level-type gate sees every modifier](done/source-shape-gate-modifier-blindness.md) — **Medium** — seven files split; samples pin the matcher.
- [A gate that keeps a run off a machine its owner is playing on](done/session-environment-gate.md) — **High** — refuses while a game process runs.
- [Systemic save and backup management](done/systemic-save-backup-management.md) — **Medium** — the backup/restore layer's roadmap.
- [S3.6 — Solo menu-exit trigger](done/save-solo-menu-exit-trigger.md) — **Medium** — the leave action is intercepted and replayed.
- [Run clock is not sent to a mid-run joiner](done/save-run-clock-not-sent.md) — **Low-Medium** — the clocks ride their own message, read at the send point.
- [S4.3 — Starting supplies for a new player](done/save-new-player-starting-supplies.md) — **High** — the body-identity policy and the entry-group hold.
- [Agent acceptance workflow — foundation](done/agent-acceptance-workflow-foundation.md) — **High** — the acceptance area, preflight and rules.
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
- [Adapter capability ports](done/adapter-capability-ports.md) — **Medium** — ten ports; the aggregate declares nothing.
- [Application layer: first slice](done/application-layer-first-slice.md) — **Medium** — command gateway and the kernel replication move.
- [Two censuses that drifted](done/catalogue-and-manifest-census-drift.md) — **Low-Medium** — orphan catalogue keys and the selfcheck index, gated.
- [Checkpoint chunks vs the run epoch](done/checkpoint-run-epoch-validation.md) — **Low-Medium** — the join instruction carries the run identity.
- [Command completion by id/name](done/command-id-name-completion.md) — **Medium** — canonical id, bare path or display name.
- [Command registration attribute refactor](done/command-registration-attribute-refactor.md) — **Medium** — the console registry and mod API.
- [Command tree and completion](done/command-tree-resource-location-selector.md) — **Medium** — tree, catalog and bracket filters.
- [Composite command semantics](done/composite-command-sequential-semantics.md) — **Medium** — declaration order on one copy.
- [Feature-matrix tooling has no path gate](done/feature-matrix-tool-path-gate.md) — **Low-Medium** — the tool literals and both column lists are gated.
- [Full-qualified name cleanup](done/full-qualified-name-cleanup.md) — **Low** — a using-directive sweep.
- [Game-update contract toolchain](done/game-update-contract-toolchain.md) — **High** — game-assembly snapshot, classified diff, report.
- [Legacy wire DTOs](done/legacy-wire-dto-slice.md) — **Medium** — the kernel <-> wire vocabulary moves into the layer; materialization stays.
- [Mod API contract governance](done/mod-api-contract-governance.md) — **Medium-High** — visibility rule, stability levels, API baseline.
- [Mod data sync model](done/mod-data-sync-model.md) — **Medium** — the mod-data scope seam.
- [Native-binding mods declare it](done/mod-native-binding-declaration.md) — **Medium** — the tier model and the manifest declaration.
- [ModService ↔ GameAdapter DI cycle](done/mod-service-gameadapter-di-cycle.md) — fixed by injecting ModStatusStore.
- [Network traffic baseline](done/network-traffic-baseline.md) — **Medium** — frame stats and byte baselines.
- [Normative requirements as gates](done/normative-style-unit-test-gates.md) — **High** — the Roslyn gate and rule inventory.
- [Patch bridge domain ports](done/patch-bridge-domain-ports.md) — **Medium** — per-domain ports; the aggregate is frozen.
- [Projection failure auto-recovery](done/projection-failure-auto-recovery.md) — **High** — the dirty/rebuild loop.
- [Protocol frame validation](done/protocol-frame-validation.md) — **High** — the unified frame validator.
- [Legacy View-items remote detail](done/remove-legacy-view-items-remote-inventory-detail.md) — **Low** — the inline path is removed.
- [Restore live-object row loops](done/restore-live-object-loops-containment.md) — **Low** — the last three restore loops are contained.
- [Restored entity row containment](done/restored-entity-row-containment.md) — **Low-Medium** — a throwing row costs only itself.
- [Dead runtime-entity relay API](done/runtime-entity-dead-api-cleanup.md) — **Low** — the relay chain is deleted.
- [S1 — Save format and world repository](done/save-format-and-world-repository.md) — **High** — no gameplay wiring.
- [State-stream bandwidth reduction](done/state-stream-bandwidth-reduction.md) — **Medium** — the guest's own entry is not echoed.
- [Sync completeness audit](done/sync-event-and-periodic-fallback-coverage-audit.md) — **High** — the 64-row evidence matrix.
- [WorldStateMessageService split](done/world-state-message-service-split.md) — **Low** — guest block bookkeeping moved out.
- [In-process session driver](done/session-driver-in-process.md) — **High** — create/join/start driven through the Online UI's own controls.
- [Host sleepiness posture desync](done/host-severe-sleepiness-posture-desync.md) — **High** — the leg-speed multiplier rides the snapshot.
- [Host fall-injury mouth desync](done/host-fall-injury-mouth-expression-desync.md) — **Medium** — head/mouth state rides the snapshot.
- [Carry vertical placement asymmetry](done/carry-piggyback-vertical-placement-asymmetry.md) — **Medium** — riders publish the torso anchor.
- [A dead or unconscious carried body stops simulating](done/carried-unconscious-body-simulation.md) — **Medium** — vitals advance behind the pinned pose.
- [Carry rider position smoothing](done/carry-piggyback-rider-position-smoothing.md) — **Critical** — the rider must stay attached on every view.
- [Carrier sit while carrying](done/carrier-sit-while-carrying.md) — **Medium** — the carrier half of the family.
- [Idle-sit suppression while carried](done/carried-player-idle-sit-suppression.md) — **Medium** — the native sit pose is suppressed.
- [Carried rider's own body stops simulating](done/carried-rider-own-body-stops-simulating.md) — **Critical** — the rider's own client keeps simulating.
- [Guest container ghost drops on host](done/guest-container-contents-ghost-drops-on-host.md) — **Medium** — clone proxies lose instance ids.
- [Carried-inventory registration](done/carried-inventory-registration-re-report.md) — **Medium** — the absolute re-report window.
- [CUO launcher button covers the view](done/cuo-launcher-button-obscures-the-view.md) — **Medium** — idle fade to semi-transparent.
- [Remove the Online UI console page](done/remove-the-online-ui-console-page.md) — **Low-Medium** — the `/` overlay is the only console.
- [Online UI panels asked for alphaBlend false](done/online-ui-panels-request-alpha-blend-false.md) — **Low-Medium** — every themed frame blends.
- [The window title must use the game's official Chinese name](done/official-game-name-in-window-title.md) — **Medium** — `未知伤亡`, not the reversed form.
- [Host eating sound on the guest](done/host-eating-sound-not-heard-on-guest.md) — **Medium** — the consume family rides the one-shot event.
- [Metal-scrap placement sound on guest](done/host-metal-scrap-block-place-sound-not-synced-to-guest.md) — **Medium** — sounds ride the sound event.
- [Guest hears only some block-break sounds](done/guest-hears-only-some-block-break-sounds.md) — **Medium-High** — the break was silent on the other side.
- [Layer time is not carried](done/save-layer-time-not-carried.md) — **Low-Medium** — the continued layer resumes its timer once the generation finishes.
- [Restore account arm release](done/restore-account-arm-release.md) — **Low** — every release path accounts for its own live-world half.
- [World and backup management surface](done/world-and-backup-management-surface.md) — **Medium** — the world/backup picker and the player-chosen restore.
- [S4.4 — Interval autosave and recovery](done/save-interval-autosave-and-backup-recovery.md) — **High** — retention and backup promotion.
- [Guest partial block damage re-report](done/guest-partial-block-damage-re-report.md) — **Medium** — the absolute re-report and the per-cell merge.
- [Partial-damage report vs the live delta](done/partial-damage-delta-report-overlap.md) — **Low-Medium** — damage is accounted per sender.
- [Trap/entity action divergence](done/trap-action-divergence-hardening.md) — **Low-Medium** — row verdicts instead of exceptions.
- [Remote medical panel acceptance issues](done/remote-medical-panel-acceptance-issues.md) — **High** — accepted in `20261023-b`: ramp, breathing, ECG, icons.
- [Remote fentanyl desync](done/remote-fentanyl-injection-and-medical-panel-desync.md) — **Critical** — accepted in `20261023-b`.
- [The remote panel's alarm follows the viewer](done/remote-medical-panel-alarm-follows-viewed-player.md) — **High** — accepted; follows the displayed body.
- [The remote panel's header shows the viewer](done/remote-medical-panel-header-shows-viewer-identity.md) — **High** — accepted in `20261024-a`.

### Watchlist

- [Architecture watchlist](watchlist/architecture-watchlist.md) — files near the 600-line gate.
