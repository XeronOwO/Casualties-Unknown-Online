# Self-Check Evidence Manifest

Every markdown file under `docs/evidence/selfchecks/` — this manifest excepted — carries exactly one row
here. The row is a **supersession register**: it records what is known to have replaced the mechanism a
sheet delivered, so that a reader about to cite one can see at a glance what is still the active path. The
register is routed from `README.md` beside it and from `docs/evidence/verification.md` §Self-checks.

- **`historical`** — the sheet's mechanism is no longer the active path. The record itself carries a
  `> **HISTORICAL**` banner naming what replaced it, and `SelfcheckManifestGateTests` keeps the row and
  that banner in step; the row's note names where to read instead.
- **`current`** — no supersession of the sheet as a WHOLE is recorded. This is NOT a claim that it matches
  today's code: most of these records have never been re-checked against the current architecture, so read
  the sheet, its own date and the decision it names before citing it. A `current` row's note may still
  record that part of a sheet moved — twelve do, and each says which part.
- **Note** — bounded to 1,200 characters, and one clause wherever a clause carries it: what the sheet
  establishes and where its normative facts live (a decision, a ticket, a page). A dash says the sheet's
  own text is the whole record of it.

| File | Domain | Status | Note |
|---|---|---|---|
| tooling/adapter-control-surfaces-selfcheck.md | Other | current | — |
| enemies/animal-death-presentation-selfcheck.md | Enemies/World | current | — |
| world/air-write-block-damage-cleanup-selfcheck.md | World/Entities | current | — |
| world/block-damage-progressive-selfcheck.md | Other | current | — |
| world/blueprint-popup-selfcheck.md | Other | current | — |
| ui/building-destruction-presentation-selfcheck.md | UI | current | — |
| players/building-entity-health-selfcheck.md | UI | historical | the mechanism this sheet records is not the active path; the live architecture is `docs/architecture/current.md` |
| world/cactus-selfdamage-sync-selfcheck.md | Other | historical | the mechanism this sheet records is not the active path; the live architecture is `docs/architecture/current.md` |
| players/carry-interaction-selfcheck.md | Players | historical | the pre-kernel carry wire; the live carry relation is the kernel's own simulation — `docs/decisions/active.md` |
| players/character-attack-anim-and-player-context-menu-selfcheck.md | Players | current | — |
| players/character-data-persistence-selfcheck.md | Players | historical | superseded by S4.1 (decision 178): the `.bin` reconnect store it delivered is deleted, so its layout/paths/test names are not current — `docs/decisions/active.md` |
| players/character-ragdoll-toggle-sync-selfcheck.md | Players | current | — |
| players/character-sound-selfcheck.md | Players | current | — |
| players/carried-idle-sit-suppression-selfcheck.md | Players | current | carried-ride idle-sit suppression landed |
| players/carried-rider-simulation-selfcheck.md | Players | current | rider keeps the native per-frame simulation while the carry relation owns the transform; the ECG/twitch rows await the user's dual-client run |
| players/carried-rider-placement-smoothing-selfcheck.md | Players | current | carried-ride placement smoothing, vertical consistency, and local-carrier mount rework landed |
| ui/chat-selfcheck.md | UI | current | — |
| ui/online-ui-console-page-removal-selfcheck.md | UI | current | the Online UI window's duplicated console page is deleted (decision 228); the slash-opened overlay is the only command and chat surface |
| presentation/building-entity-hit-flash-sync-selfcheck.md | Other | current | melee red HitFlash replays on non-attacker views through BuildingEntityDamaged |
| presentation/clone-face-presentation-selfcheck.md | Other | current | — |
| presentation/remote-clone-face-vitals-selfcheck.md | Other | current | — |
| tooling/config-options-selfcheck.md | Other | current | — |
| tooling/acceptance-recipe-layer-selfcheck.md | Other | current | the recipe layer: driver `-Action recipe`, declared-arg recipes under `tools/acceptance/recipes/`, gates; the live run is still pending |
| tooling/acceptance-key-hold-selfcheck.md | Other | current | the held-key capability: `-Action declare` loading `driver/eval-declarations/`, `recipes/key-hold.cs`, the one-directory OS-input exception; smoked live, the remote gesture still pending |
| items/container-content-sync-selfcheck.md | Items | current | — |
| items/container-move-event-carrier-selfcheck.md | Items | current | a peer's intent executed by the owner is the owner's own fact: `CallContext.Origin.RemoteIntentApply` + `IsReplayedRemoteFact`; the three-client run is still pending |
| items/container-move-kernel-fact-selfcheck.md | Items | current | one container report = the parent's fact + every child's place, committed as the same `SyncContainerItemsCommand` on the wire and host-local paths; row A1 still awaits a three-client batch |
| items/remote-intent-drop-report-order-selfcheck.md | Items | current | no inventory re-report goes out while a drop report is pending, at both of the owner's entry points; the drop and wearable-drop rows still await a three-client run |
| items/container-move-pair-classification-selfcheck.md | Items | current | the container-move pair: the unload registers a departure with where the item came from, the load classifies through `ContainerLoadClassifier`, and the departure machine holds one entry per item; row A1g still awaits a three-client batch |
| players/cross-player-component-medicine-selfcheck.md | Players | historical | the pre-kernel cross-player wire; the live path reads the game's own item data — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| players/cross-player-component-tool-use-selfcheck.md | Players | historical | the pre-kernel cross-player wire; the live path reads the game's own item data — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| players/cross-player-drinkable-medicine-selfcheck.md | Players | historical | the pre-kernel cross-player wire; the live path reads the game's own item data — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| items/cross-player-item-use-selfcheck.md | Items | historical | the pre-kernel cross-player wire; the live path reads the game's own item data — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| players/cross-player-limb-tool-use-selfcheck.md | Players | historical | the pre-kernel cross-player wire; the live path reads the game's own item data — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| players/cross-player-medicine-use-selfcheck.md | Players | historical | the pre-kernel cross-player wire; the live path reads the game's own item data — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| players/cross-player-opiate-use-selfcheck.md | Players | historical | the pre-kernel cross-player wire; the live path reads the game's own item data — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| players/cross-player-shrapnel-and-timed-tool-use-selfcheck.md | Players | historical | the pre-kernel cross-player wire; the live path reads the game's own item data — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| players/cross-player-timed-liquid-medicine-selfcheck.md | Players | historical | the pre-kernel cross-player wire; the live path reads the game's own item data — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| players/cross-player-topical-use-selfcheck.md | Players | historical | the pre-kernel cross-player wire; the live path reads the game's own item data — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| players/cross-player-wear-use-selfcheck.md | Players | historical | the pre-kernel cross-player wire; the live path reads the game's own item data — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| enemies/crystal-enemy-tint-selfcheck.md | Enemies/World | current | — |
| enemies/crystal-mimic-selfcheck.md | Enemies/World | current | — |
| enemies/crystal-teleport-sync-selfcheck.md | Enemies/World | current | — |
| enemies/crystal-ticking-selfcheck.md | Enemies/World | current | — |
| enemies/crystal-windup-telegraph-sync-selfcheck.md | Enemies/World | current | — |
| items/custom-item-data-state-selfcheck.md | Items | current | — |
| items/building-support-drop-sync-selfcheck.md | Items | current | support-loss building drops ride the block-break message with full transient state |
| items/entity-destruction-drop-fresh-presentation-selfcheck.md | Items | current | entity-destruction trap/building drops keep full transient spawn state on replay |
| items/item-spawn-presentation-kernel-selfcheck.md | Items | current | item-spawn kernel/command/event now carry the full transient initial-drop state to every peer |
| presentation/direct-placeable-arm-swing-selfcheck.md | Other | current | — |
| items/dynamite-explosion-selfcheck.md | Other | current | — |
| items/dynamite-fuse-presentation-selfcheck.md | Other | current | — |
| enemies/enemy-combat-replay-split-selfcheck.md | Enemies/World | current | — |
| enemies/enemy-effects-selfcheck.md | Enemies/World | historical | the pre-kernel enemy wire — the live path is `docs/en/internals/enemy-sync.md` |
| items/enemy-item-hit-sync-selfcheck.md | Items | current | — |
| items/favourite-marks-local-and-saved-selfcheck.md | Items | current | the local favourite mark: the set, the character record it rides, the one writer of a proxy's star, the adoption and the deleted `ToggleFavourite` path; the seven real-machine rows are the next batch's judgement |
| enemies/enemy-stun-presentation-selfcheck.md | Enemies/World | current | — |
| enemies/enemy-targeting-selfcheck.md | Enemies/World | historical | the pre-kernel enemy wire — the live path is `docs/en/internals/enemy-sync.md` |
| fluids/fluid-presentation-selfcheck.md | Fluids | current | — |
| presentation/footstep-sound-selfcheck.md | Other | current | — |
| tooling/game-adapter-split-selfcheck.md | Other | current | — |
| tooling/gameadapter-construction-selfcheck.md | Other | historical | the mechanism this sheet records is not the active path; the live architecture is `docs/architecture/current.md` |
| presentation/grappling-hook-presentation-selfcheck.md | Other | current | — |
| items/gun-state-sync-selfcheck.md | Other | current | — |
| tooling/handler-context-narrowing-selfcheck.md | Other | current | — |
| players/heal-interaction-selfcheck.md | Other | historical | the mechanism this sheet records is not the active path; the live architecture is `docs/architecture/current.md` |
| items/heal-item-selection-selfcheck.md | Items | current | — |
| items/heater-cook-selfcheck.md | Items | current | — |
| world/heater-xaloris-local-body-effect-selfcheck.md | Other | current | — |
| presentation/held-light-direction-selfcheck.md | Other | current | — |
| protocol/high-frequency-small-drops-selfcheck.md | Other | current | — |
| session/host-ban-selfcheck.md | Other | current | — |
| session/host-close-room-safe-exit-selfcheck.md | Other | current | — |
| session/host-kick-selfcheck.md | Other | current | — |
| session/host-rules-selfcheck.md | Other | current | — |
| protocol/hot-path-latency-instrumentation-selfcheck.md | Other | current | — |
| ui/i18n-framework-selfcheck.md | Other | current | — |
| protocol/ip-direct-selfcheck.md | Other | current | — |
| items/item-cook-replay-split-selfcheck.md | Items | current | — |
| items/item-keyframe-state-selfcheck.md | Items | current | — |
| items/item-service-split-selfcheck.md | Items | current | — |
| players/limb-presentation-selfcheck.md | Players | current | — |
| session/lobby-leave-host-rules-editor-selfcheck.md | Other | current | — |
| tooling/log-level-cleanup-selfcheck.md | Other | current | — |
| presentation/looktarget-gaze-sync-selfcheck.md | Other | current | — |
| session/member-status-icons-and-session-hotkeys-selfcheck.md | Other | current | — |
| presentation/mine-press-visual-selfcheck.md | Other | current | — |
| mod-api/mod-content-registration-selfcheck.md | Mod API | historical | superseded by decision 247 (typed content registration, 2026-10-08): the opaque payload it describes and the `ToPayload`/`FromPayload` round-trip tests it cites are deleted, and the mechanism it delivered is now the typed `IModContentDefinition` path — `docs/decisions/active.md` |
| mod-api/mod-content-migration-base-selfcheck.md | Mod API | historical | superseded by decision 247 (typed content registration, 2026-10-08): the opaque payload it describes and the `ToPayload`/`FromPayload` round-trip tests it cites are deleted, and the mechanism it delivered is now the typed `IModContentDefinition` path — `docs/decisions/active.md` |
| mod-api/mod-item-content-binding-selfcheck.md | Mod API | historical | superseded by decision 247 (typed content registration, 2026-10-08): the opaque payload it describes and the `ToPayload`/`FromPayload` round-trip tests it cites are deleted, and the mechanism it delivered is now the typed `IModContentDefinition` path — `docs/decisions/active.md` |
| mod-api/mod-entity-spawn-selfcheck.md | Mod API | current | — |
| mod-api/mod-game-state-selfcheck.md | Mod API | current | — |
| mod-api/mod-native-api-selfcheck.md | Mod API | current | — |
| mod-api/mod-service-split-selfcheck.md | Mod API | current | — |
| mod-api/mod-state-saves-selfcheck.md | Mod API | current | — |
| mod-api/mod-ui-selfcheck.md | Mod API | current | — |
| mod-api/mod-building-runtime-hooks-selfcheck.md | Mod API | current | — |
| presentation/muzzle-flash-sync-selfcheck.md | Other | current | — |
| presentation/remote-clone-facing-auto-flip-selfcheck.md | Other | current | stale render-clone facing auto-flip inputs neutralized |
| presentation/remote-clone-head-mouth-sync-selfcheck.md | Other | current | owner head/mouth state replayed on remote clones; host fall-injury mouth desync |
| presentation/remote-clone-legspeed-pose-selfcheck.md | Other | current | owner leg-speed multiplier replayed as CrouchAmount weakness/slouch input; severe-sleepiness posture desync |
| presentation/nap-and-dog-shake-sync-selfcheck.md | Other | current | — |
| players/native-remote-backpack-and-door-sound-selfcheck.md | Other | historical | the mechanism this sheet records is not the active path; the live architecture is `docs/architecture/current.md` |
| protocol/netmsg-registry-selfcheck.md | Other | current | — |
| players/network-health-metrics-selfcheck.md | Other | current | — |
| protocol/network-traffic-monitor-selfcheck.md | Other | current | — |
| protocol/network-traffic-baseline-selfcheck.md | Other | current | network traffic baseline and regression gate landed |
| protocol/state-stream-bandwidth-reduction-selfcheck.md | Other | current | player-state per-recipient echo removal landed |
| protocol/global-adaptive-report-rate-stage-2-traffic-bandwidth-selfcheck.md | Other | current | per-peer/per-stream traffic estimates, bandwidth/failed-send pressure and byte-budget policy landed |
| protocol/checkpoint-string-table-selfcheck.md | Other | current | checkpoint item-definition string table compression landed |
| players/online-ui-player-awareness-selfcheck.md | Players | current | `OnlineUiOverlay.DrawNameplatesAndArrows` is superseded by S6 of the Online UI overhaul (`docs/evidence/selfchecks/ui/online-ui-world-overlay-selfcheck.md`); the marker rules it records (the head anchor, the world-presence filter, the distance text) still hold |
| ui/online-ui-polish-selfcheck.md | UI | current | — |
| ui/player-list-peer-id-disambiguation-selfcheck.md | UI | current | — |
| ui/online-ui-scoped-passthrough-selfcheck.md | UI | historical | superseded by S5 of the Online UI overhaul (`docs/backlog/done/online-ui-art-and-controls-overhaul.md`): the scoped UGUI blocker it describes (`SetOnlineUiScopedBlocks`, `OnlineUiBlockRect`, `OnlineScopedRaycastFilter`) retired with the two IMGUI panels that were its only consumers, so the mechanism, its files and its pins are deleted |
| ui/online-ui-selfcheck.md | UI | historical | the retired IMGUI Online UI — `docs/backlog/done/online-ui-art-and-controls-overhaul.md` |
| ui/online-ui-window-selfcheck.md | UI | current | — |
| ui/remote-context-menu-medical-visibility-selfcheck.md | UI | current | Medical remote-action visibility now follows the shared line-of-sight gate on context menu/Players/quick panel |
| world/openable-keypad-prefabs-selfcheck.md | Other | current | — |
| presentation/owner-local-body-auto-events-selfcheck.md | Other | current | — |
| tooling/partial-aware-gate-selfcheck.md | Other | current | — |
| tooling/check-architecture-performance-selfcheck.md | Other | historical | the PowerShell script it measured was removed once the architecture gate became C# tests — `docs/evidence/normative-gates.md` |
| tooling/guest-frame-rate-baseline-selfcheck.md | Other | current | guest frame-rate baseline telemetry and cached RemotePlayers hot-path allocation removal |
| tooling/patch-contract-overload-selfcheck.md | Other | current | — |
| architecture/phase-a-kernel-foundation-selfcheck.md | Architecture | historical | an intermediate migration phase, not the live architecture — `docs/architecture/evolution/status.md` |
| architecture/phase-b-item-authority-selfcheck.md | Architecture | historical | an intermediate migration phase, not the live architecture — `docs/architecture/evolution/phase-b-items-authority.md` |
| architecture/phase-c-protocol-core-selfcheck.md | Architecture | historical | an intermediate migration phase, not the live architecture — `docs/architecture/evolution/status.md` |
| architecture/phase-d-enemies-shadow-selfcheck.md | Architecture | current | — |
| architecture/phase-d-fluids-shadow-selfcheck.md | Architecture | current | — |
| architecture/phase-d-full-domain-migration-selfcheck.md | Architecture | current | — |
| architecture/phase-d-high-frequency-stream-unification-selfcheck.md | Architecture | current | — |
| architecture/phase-d-players-shadow-selfcheck.md | Architecture | current | — |
| architecture/phase-d-world-entities-shadow-selfcheck.md | Architecture | current | — |
| architecture/phase-d-world-run-epoch-shadow-selfcheck.md | Architecture | current | — |
| architecture/phase-e-legacy-inventory-selfcheck.md | Architecture | current | — |
| architecture/composite-command-sequential-semantics-selfcheck.md | Architecture | current | — |
| architecture/application-layer-first-slice-selfcheck.md | Architecture | current | the Application layer as a project, the declared project-direction gate (with its control run), the command admission seam (decision 210) and the kernel-replication move with its seven ports (decision 211) |
| architecture/adapter-capability-ports-selfcheck.md | Architecture | current | the Game Adapter boundary as ten capability ports with a member-free composition, the call-site census behind the set, the shape gate with its mutation controls, and the `InternalsVisibleTo` census (decision 212) |
| items/pickup-inflight-selfcheck.md | Items | historical | the mechanism this sheet records is not the active path; the live architecture is `docs/architecture/current.md` |
| players/piggyback-drop-cleanup-selfcheck.md | Players | historical | the pre-kernel carry wire; the live carry relation is the kernel's own simulation — `docs/decisions/active.md` |
| players/piggyback-facing-restore-selfcheck.md | Players | historical | the pre-kernel carry wire; the live carry relation is the kernel's own simulation — `docs/decisions/active.md` |
| players/piggyback-releasable-carry-selfcheck.md | Players | historical | the pre-kernel carry wire; the live carry relation is the kernel's own simulation — `docs/decisions/active.md` |
| players/player-interaction-followups-selfcheck.md | Players | historical | the mechanism this sheet records is not the active path; the live architecture is `docs/architecture/current.md` |
| players/player-interaction-service-split-selfcheck.md | Players | current | — |
| players/player-interaction-visibility-selfcheck.md | Players | current | — |
| players/player-inventory-take-selfcheck.md | Players | historical | the mechanism this sheet records is not the active path; the live architecture is `docs/architecture/current.md` |
| players/player-color-and-head-tags-selfcheck.md | Players | current | the head-tag half still holds; its COLOUR half is superseded by S3 of the Online UI overhaul (`docs/backlog/done/online-ui-art-and-controls-overhaul.md`): the `PlayerColorIndex` entry, `PlayerColorResolver.TryGet` and the preset-only picker it describes are deleted, and the colour is a free `[UI] PlayerColor` hex value now |
| players/player-push-selfcheck.md | Players | current | — |
| players/player-quick-panel-selfcheck.md | Players | current | Its RENDERING half is superseded by S5 of the Online UI overhaul (`docs/backlog/done/online-ui-art-and-controls-overhaul.md`); its interaction rules (target picker, eligibility, `member.get_down`) still hold |
| players/remove-legacy-view-items-remote-inventory-selfcheck.md | Players | current | — |
| world/radiation-line-state-sync-selfcheck.md | World/Entities | current | — |
| world/radiation-straggler-pressure-selfcheck.md | World/Entities | current | — |
| players/ragdoll-stale-state-fix-selfcheck.md | Other | current | — |
| players/ragdoll-render-proxy-limb-pose-selfcheck.md | Players | current | accepted after red-green replay and user verification |
| players/ragdoll-limb-pose-sync-selfcheck.md | Players | current | exact owner limb poses now ride the 20 Hz player stream |
| README.md | Other | current | — |
| items/remote-backpack-container-take-selfcheck.md | Items | current | — |
| items/remote-backpack-native-interaction-parity-selfcheck.md | Items | historical | superseded by the stage 0 design (decision 217): the CUO routing table and the host mirror-edit path it describes are deleted by the native-intent rework; its container findings stay the rework's acceptance input — `docs/decisions/active.md` |
| items/remote-inventory-native-parity-design-selfcheck.md | Items | current | stage 0 of the native-parity rework: the native gesture inventory, the intent vocabulary and the ticket adjudication; design only, no behaviour change, stages 1-4 still open |
| items/remote-inventory-native-intent-stage1-selfcheck.md | Items | current | stage 1 of the native-parity rework: the release window, the capture seam, the owner-side replay and the validating host half; the clone-edit path it replaced is deleted, and the reported rows still await the user's acceptance run |
| players/remote-backpack-held-item-transfer-selfcheck.md | Players | historical | superseded by the stage 0 design (decision 217): the Tab-transfer and held-item kinds it describes are replaced by native intents (`TransferToBody`); its reported rows feed the rework's acceptance matrix — `docs/decisions/active.md` |
| players/remote-backpack-drag-escape-selfcheck.md | Other | historical | superseded by the native remote-backpack parity cycle (Tab-switch transfer) — `docs/decisions/active.md` |
| items/remote-container-content-view-selfcheck.md | Items | historical | the mechanism this sheet records is not the active path; the live architecture is `docs/architecture/current.md` |
| items/remote-container-destroy-authority-selfcheck.md | Items | current | — |
| items/remote-world-item-same-id-dedup-selfcheck.md | Items | current | — |
| ui/remote-inventory-ui-followup-selfcheck.md | UI | historical | the mechanism this sheet records is not the active path; the live architecture is `docs/architecture/current.md` |
| players/remote-inventory-view-selfcheck.md | Other | current | — |
| players/remote-vitals-selfcheck.md | Other | current | — |
| players/remote-native-medical-view-selfcheck.md | Players | current | native WoundView reuse; custom IMGUI medical panel removed |
| players/remote-medical-fidelity-and-syringe-minigame-selfcheck.md | Players | current | native syringe minigame routes cross-player injectable doses; remote WoundView display projection plus ECG/moodle/nap redirection |
| players/remote-medical-local-only-controls-selfcheck.md | Players | current | a remote WoundView focus hides the nap control, the workout list and the HUD hand switch, and blocks both hand-switch paths |
| players/respawn-rules-selfcheck.md | Other | current | — |
| session/run-settings-range-selfcheck.md | Other | current | — |
| tooling/simtrace-diff-selfcheck.md | Other | current | — |
| presentation/speech-sound-frequency-selfcheck.md | Other | current | — |
| enemies/spider-enemy-presentation-sync-selfcheck.md | Enemies/World | current | — |
| tooling/start-gate-alert-selfcheck.md | Other | current | — |
| protocol/steam-p2p-cert-selfcheck.md | Other | current | — |
| items/trader-recruit-gift-items-selfcheck.md | Items | current | — |
| enemies/trader-recruit-selfcheck.md | Enemies/World | current | — |
| enemies/trader-swing-sync-selfcheck.md | Enemies/World | current | — |
| presentation/turret-light-sprite-selfcheck.md | Other | current | — |
| world/transient-trap-state-checkpoint-selfcheck.md | World/Entities | current | turret/geyser transient trap state no longer replayed by periodic checkpoint |
| world/tutorial-claw-selfcheck.md | World/Entities | current | — |
| world/tutorial-claw-stream-selfcheck.md | World/Entities | current | — |
| mod-api/ui-modal-input-blocker-selfcheck.md | Mod API | current | — |
| presentation/wall-slide-landing-sync-selfcheck.md | Other | current | — |
| session/warmup-backoff-selfcheck.md | Other | current | — |
| presentation/weapon-fire-recoil-selfcheck.md | Other | current | — |
| presentation/workout-animation-sync-selfcheck.md | Other | current | — |
| world/world-blood-spawn-sync-selfcheck.md | World/Entities | current | — |
| world/world-entry-completion-selfcheck.md | World/Entities | current | — |
| items/world-item-service-partial-split-selfcheck.md | Items | historical | the mechanism this sheet records is not the active path; the live architecture is `docs/architecture/current.md` |
| world/world-service-split-selfcheck.md | World/Entities | current | — |
| world/world-time-selfcheck.md | World/Entities | current | — |
| items/remote-clone-display-content-id-free-selfcheck.md | Items | current | remote clone container contents no longer carry item-domain instance ids |
| items/guest-background-ghost-item-ground-sounds-selfcheck.md | Items | current | guest non-authoritative item impact presentation (drop/step/squeak/dust) suppressed |
| search/pinyin-console-completion-selfcheck.md | UI | historical | the CUO-resident console stage it describes (decision 200) was deleted when the stage moved into the satellite pinyin mod (decision 209); the seam it introduced is unchanged — `docs/decisions/active.md` |
| search/pinyin-search-selfcheck.md | UI | historical | the CUO-resident matcher, patch and `Search.PinyinSearch` switch it describes (decision 199) were deleted when the feature moved into the satellite pinyin mod (decision 209) — `docs/decisions/active.md` |
| search/pinyin-search-standalone-mod-selfcheck.md | UI | current | the pinyin search as a satellite mod in this repository: two assemblies split along the binding, its own BepInEx switch, the Tier-2 declaration, and the deletion of CUO's own pinyin code (decision 209) |
| architecture/plugin-host-shell-selfcheck.md | Architecture | current | the plugin as a BepInEx host shell: registration moved into the adapter's own composition, the Online UI and the lobby policy became their own units, two ports replaced the direct adapter writes, and the plugin project references no game assembly (decision 213) |
| architecture/patch-bridge-domain-ports-selfcheck.md | Architecture | current | the Harmony patch bridge split by domain: the fluid domain's eight members moved into `IFluidPatchPort`, the aggregate neither declares nor composes them (the compiler enforces the migration), and a shape gate freezes every seam's census (decision 214) |
| documentation/bilingual-human-guide-selfcheck.md | Documentation | current | the human-facing layer: three layers declared by index, the two guide levels paired English + Chinese with a per-pair blob-hash record, and a pairing gate that fails when either side moves without re-confirming the pair (policy at `docs/standard/alignment.txt`) |
| documentation/legacy-tree-followups-selfcheck.md | Documentation | current | the cycle after the migration: two dead doc paths and an off-by-one grammar comment in `src/`, the English how-to breadcrumb brought to the standard spelling, and the feature-matrix tool-path gate (tool literals plus the item column list) |
| documentation/legacy-tree-migration-selfcheck.md | Documentation | current | the last migration stage: two conclusions absorbed as pages, every live inbound reference re-pointed, the old `docs/` trees deleted, the event-replay table moved into `docs/contracts/`, and the retired sibling-pairing gate's budget check left with `AgentInstructionBudgetGateTests` |
| tooling/one-top-level-type-gate-selfcheck.md | Other | current | the one-top-level-type gate sees every modifier and the two-word record keyword, and the seven files it exposed are split one type per file |
| architecture/composition-root-modules-selfcheck.md | Architecture | current | — |
| architecture/di-cycle-guard-selfcheck.md | Architecture | current | — |
| architecture/legacy-wire-dto-slice-selfcheck.md | Architecture | current | the kernel-to-wire vocabulary moved into the Application layer (ticket `docs/backlog/done/legacy-wire-dto-slice.md`) |
| architecture/projection-health-coordinator-selfcheck.md | Architecture | current | projection failure auto-recovery: a stalled projection is observed and healed instead of dropping silently |
| enemies/enemy-hit-determination-local-selfcheck.md | Enemies/World | current | — |
| items/remote-inventory-native-intent-stage2-selfcheck.md | Items | current | remote inventory native parity stage 2: the container family |
| items/remote-inventory-native-intent-stage3-selfcheck.md | Items | current | remote inventory native parity stage 3: item interaction |
| items/remote-inventory-native-intent-stage4-audit.md | Items | current | remote inventory native parity stage 4: the family audit and acceptance preparation |
| items/remote-inventory-native-parity-acceptance-checklist.md | Items | current | the real-machine acceptance checklist the remote inventory native parity rework landed with |
| mod-api/mod-building-drop-worldgen-selfcheck.md | Mod API | historical | superseded by decision 247 (typed content registration, 2026-10-08): the opaque payload it describes and the `ToPayload`/`FromPayload` round-trip tests it cites are deleted, and the mechanism it delivered is now the typed `IModContentDefinition` path — `docs/decisions/active.md` |
| mod-api/mod-content-owner-query-selfcheck.md | Mod API | current | — |
| mod-api/mod-item-advanced-behavior-selfcheck.md | Mod API | historical | superseded by decision 247 (typed content registration, 2026-10-08): the opaque payload it describes and the `ToPayload`/`FromPayload` round-trip tests it cites are deleted, and the mechanism it delivered is now the typed `IModContentDefinition` path — `docs/decisions/active.md` |
| mod-api/mod-item-fixed-drop-sources-selfcheck.md | Mod API | historical | superseded by decision 247 (typed content registration, 2026-10-08): the opaque payload it describes and the `ToPayload`/`FromPayload` round-trip tests it cites are deleted, and the mechanism it delivered is now the typed `IModContentDefinition` path — `docs/decisions/active.md` |
| mod-api/mod-item-spawn-selfcheck.md | Mod API | current | — |
| mod-api/mod-item-visual-selfcheck.md | Mod API | historical | superseded by decision 247 (typed content registration, 2026-10-08): the opaque payload it describes and the `ToPayload`/`FromPayload` round-trip tests it cites are deleted, and the mechanism it delivered is now the typed `IModContentDefinition` path — `docs/decisions/active.md` |
| mod-api/mod-item-worldgen-loot-selfcheck.md | Mod API | historical | superseded by decision 247 (typed content registration, 2026-10-08): the opaque payload it describes and the `ToPayload`/`FromPayload` round-trip tests it cites are deleted, and the mechanism it delivered is now the typed `IModContentDefinition` path — `docs/decisions/active.md` |
| mod-api/mod-liquid-content-binding-selfcheck.md | Mod API | historical | superseded by decision 247 (typed content registration, 2026-10-08): the opaque payload it describes and the `ToPayload`/`FromPayload` round-trip tests it cites are deleted, and the mechanism it delivered is now the typed `IModContentDefinition` path — `docs/decisions/active.md` |
| mod-api/mod-liquid-placement-selfcheck.md | Mod API | current | — |
| mod-api/mod-recipe-content-binding-selfcheck.md | Mod API | historical | superseded by decision 247 (typed content registration, 2026-10-08): the opaque payload it describes and the `ToPayload`/`FromPayload` round-trip tests it cites are deleted, and the mechanism it delivered is now the typed `IModContentDefinition` path — `docs/decisions/active.md` |
| mod-api/mod-runtime-data-selfcheck.md | Mod API | current | — |
| mod-api/mod-status-moodle-content-binding-selfcheck.md | Mod API | historical | superseded by decision 247 (typed content registration, 2026-10-08): the opaque payload it describes and the `ToPayload`/`FromPayload` round-trip tests it cites are deleted, and the mechanism it delivered is now the typed `IModContentDefinition` path — `docs/decisions/active.md` |
| mod-api/mod-status-moodle-row-selfcheck.md | Mod API | current | **[Narrowed 2026-10-09]** the per-limb routing behaviour it delivered is current, but the derived members it cites on the data class moved to the declaration contract (`ModStatusPresentation`, decision 251): read `ResolveMoodleId`, `ShowsPerLimbMoodles`, `FormatLimbDisplayName` and `FormatLimbDescription` as members of `IModStatusDefinition` / `IModMoodleDefinition` now |
| mod-api/mod-status-projection-selfcheck.md | Mod API | current | — |
| mod-api/mod-status-runtime-selfcheck.md | Mod API | current | — |
| mod-api/mod-status-wire-selfcheck.md | Mod API | current | — |
| mod-api/mod-structure-content-binding-selfcheck.md | Mod API | historical | superseded by decision 247 (typed content registration, 2026-10-08): the opaque payload it describes and the `ToPayload`/`FromPayload` round-trip tests it cites are deleted, and the mechanism it delivered is now the typed `IModContentDefinition` path — `docs/decisions/active.md` |
| mod-api/mod-structure-placement-selfcheck.md | Mod API | current | — |
| mod-api/mod-structure-worldgen-distribution-selfcheck.md | Mod API | current | **[Narrowed 2026-10-09]** the distribution behaviour it delivered is current, but `TryGetSpawnCount` moved off the data class to `ModStructureDistribution.TryGetSpawnCount(IModStructureDefinition, int, out int)` — the declaration owns the `SpawnCounts` fact, the framework owns the rule (decision 251) |
| mod-api/mod-tile-content-binding-selfcheck.md | Mod API | historical | superseded by decision 247 (typed content registration, 2026-10-08): the opaque payload it describes and the `ToPayload`/`FromPayload` round-trip tests it cites are deleted, and the mechanism it delivered is now the typed `IModContentDefinition` path — `docs/decisions/active.md` |
| mod-api/mod-tile-ore-worldgen-projection-selfcheck.md | Mod API | current | **[Narrowed 2026-10-09]** the ore/drop projection it delivered is current, but `CanSpawnInLayer` moved off the data class to `ModLayerSpawnRule.CanSpawnInLayer(IModTileDefinition, int)`; the two static mask builders and `AllSpawnLayers` stay on the class as the mod-facing authoring helpers (decision 251) |
| mod-api/mod-tile-placement-selfcheck.md | Mod API | current | — |
| mods/native-binding-declaration-selfcheck.md | Other | current | the declared native-binding tier, stage 1: the declaration |
| mods/native-binding-handshake-parity-selfcheck.md | Other | current | native-binding parity in the session handshake |
| players/carried-unconscious-body-simulation-selfcheck.md | Players | current | a dead or unconscious carried body keeps the vitals half of the native per-frame pass; the carry relation keeps pose, physics, ground contact and sounds (decision 227) |
| players/carrier-sit-suppression-selfcheck.md | Players | current | — |
| players/carry-rider-limb-anchor-selfcheck.md | Players | current | — |
| players/carry-rider-acceptance-readiness-selfcheck.md | Players | current | the deciding readings are default-visible for a carry participant and the pin-drift reading is new (decision 233) |
| players/carried-rider-presenter-split-selfcheck.md | Players | current | the carry-presentation half (local-carrier mount, per-frame pin, carry-role marks, drift reading) moved out of `RemotePlayerRenderer` (587 to 338 lines) into `CarriedRiderPresenter` (309); the reading pins were re-pointed, a mark-ordering pin was added, and behaviour is preserved by the unchanged per-frame call order |
| players/interaction-gate-authority-selfcheck.md | Players | current | each client judges its own side; twelve host-side judging sites retired |
| players/medical-operation-concurrency-selfcheck.md | Players | current | medical operations settle per unit (ticket `docs/backlog/review/concurrent-medical-operations.md`) |
| players/remote-medical-operation-session-realtime-injection-selfcheck.md | Players | current | — |
| players/remote-medical-panel-acceptance-projection-selfcheck.md | Players | historical | superseded: `RemoteMedicalDisplayProjection` and the acceptance rows it carried were replaced by the unified remote display projection — `docs/evidence/selfchecks/presentation/unified-remote-display-projection-selfcheck.md` |
| players/remote-medical-panel-selfcheck.md | Players | historical | the rejected CUO IMGUI medical panel; superseded by the native WoundView remote focus — no parallel CUO medical panel — `docs/backlog/review/remote-player-medical-panel.md` |
| players/remote-medical-stage-2-shrapnel-session-selfcheck.md | Players | current | — |
| players/remote-medical-stage-3-other-actions-selfcheck.md | Players | historical | stage 3's exclusive-limb lease was superseded 2026-09-19 by per-unit settlement (`docs/evidence/selfchecks/players/medical-operation-concurrency-selfcheck.md`), and the sheet's own protocol row records `ProtocolVersion.Current = 14` against the tree's 41 |
| players/remote-medical-treatment-operations-selfcheck.md | Players | current | — |
| presentation/host-eating-sound-not-heard-on-guest-selfcheck.md | Other | current | the ingest clips and the meal-end burp reach every other side as the dedicated one-shot character sound (decision 225) |
| presentation/unhooked-item-and-body-sound-families-selfcheck.md | Other | current | the medical, world-drink, gesture and coroutine one-shot sounds reach every other side through the same dedicated character-sound event; the 2D prompts stay local by decision |
| presentation/suppressed-native-call-sounds-stay-unheard-selfcheck.md | Other | current | the remote limb treatment plays the clip its blocked native limb action would have played and the bandage minigame's own step is captured; a world-item impact reaches every member as the authority's own drop/step/dust (ItemImpact, protocol 43) |
| presentation/treatment-gore-presentation-carried-selfcheck.md | Other | current | the amputation and shrapnel minigame steps carry their `gore`/`gore{N}` clip to every peer and the suture's blocked clip is a treatment-table row; the patient's client never calls the native `Dismember`, so nothing double-plays |
| presentation/unified-remote-display-projection-selfcheck.md | Other | current | — |
| protocol/global-adaptive-report-rate-stage-3-cumulative-streams-selfcheck.md | Other | current | — |
| protocol/global-adaptive-report-rate-stage-4-high-frequency-domains-selfcheck.md | Other | current | — |
| protocol/ip-direct-name-validation-selfcheck.md | Other | current | — |
| saves/world-backup-management-selfcheck.md | Other | current | the Online UI's Worlds page and the player-chosen restore (decision 198) |
| tooling/adapter-capability-catalog-selfcheck.md | Other | current | the adapter capability catalog and probe aggregation; the installers' target table is the one hand-written surface |
| tooling/config-profile-templates-selfcheck.md | Other | current | — |
| tooling/content-id-selfcheck.md | Other | current | — |
| tooling/game-update-contract-toolchain-selfcheck.md | Other | current | — |
| tooling/mod-api-contract-governance-selfcheck.md | Other | current | — |
| ui/command-console-selfcheck.md | UI | current | the in-game command console; the modal Online UI console page it once sat beside was deleted 2026-09-26 (decision 228), so the slash-opened overlay is the only command and chat surface |
| ui/cuo-launcher-idle-fade-selfcheck.md | UI | current | the launcher button's idle fade (ticket `docs/backlog/done/cuo-launcher-button-obscures-the-view.md`) |
| ui/dead-player-context-menu-title-selfcheck.md | UI | current | — |
| ui/location-ping-selfcheck.md | UI | current | the ping control, its wire path and its fade still hold; its IMGUI rendering row (`LocationPingOverlay.Draw`) is superseded by S6 of the Online UI overhaul (`ui/online-ui-world-overlay-selfcheck.md`) — the class is deleted and a ping is a marker label on CUO's canvas now |
| ui/name-tag-ui-polish-selfcheck.md | UI | current | the marker rules it records (the head anchor, the world-presence filter, the colour tag) still hold; its drawing path (`OnlineUiOverlay.DrawNameplatesAndArrows`) is superseded by S6 of the Online UI overhaul (`ui/online-ui-world-overlay-selfcheck.md`) — the nameplate is a label on CUO's canvas now |
| ui/tab-backpack-instant-close-selfcheck.md | UI | current | — |
| world/guest-hears-only-some-block-break-sounds-selfcheck.md | World/Entities | current | a break is presented on every side through the game's own damage roll, carried by the air write's `PlayerBreak` claim (decision 224) |
| world/quake-direct-write-selfcheck.md | World/Entities | current | the game's direct `Time.timeScale` write is adopted by the host and the adopted speed is re-stated silently, so the speed HUD follows (decision 226) |
| world/runtime-entity-identity-selfcheck.md | World/Entities | current | — |
| world/world-acceleration-survives-movement-selfcheck.md | World/Entities | current | — |
| tooling/catalogue-and-manifest-census-selfcheck.md | Other | current | the catalogue's declared keys now equal the keys the product source reads (73 orphan keys deleted), and the self-check manifest is the complete per-file index it claims: both censuses are gates (decision 229) |
| world/unhooked-damage-block-callers-selfcheck.md | World/Entities | current | the block-damage hook is anchored on the overload every native roll enters (the footstep crush and the spider burrow included) and the report carries the cell; the anchor is pinned by `DamageBlockHookCoverageGateTests` |
| ui/online-ui-panel-blending-selfcheck.md | UI | current | every themed frame is drawn alpha-blended, so the palette's alphas are live: the modal window, the quick panel, the context menu and the console overlay follow the launcher's already-blended frame (ticket `docs/backlog/done/online-ui-panels-request-alpha-blend-false.md`) |
| ui/online-ui-native-facts-selfcheck.md | UI | current | S1 of the Online UI overhaul: the read-only probe of the game's own UI (font asset, settings-row style, chrome census, `PlayerCamera.uiScale`) plus the uGUI host under the game's canvas; the four values await one game run (`docs/backlog/done/online-ui-art-and-controls-overhaul.md`) |
| ui/online-ui-native-surface-selfcheck.md | UI | current | S2a of the Online UI overhaul: the LIVE surface under the game's canvas and the launcher moved onto the game's own button-row prefab, with the idle fade applied to it and the click returning as an intent; the visual result awaits one game run (`docs/backlog/done/online-ui-art-and-controls-overhaul.md`) |
| ui/online-ui-window-family-selfcheck.md | UI | current | S2b of the Online UI overhaul: the window shell, the tab row and all six pages moved off IMGUI onto the game's own settings rows through a Runtime display list and an intent channel; the look, the layout and the input await one game run (`docs/backlog/done/online-ui-art-and-controls-overhaul.md`) |
| ui/online-ui-free-color-selfcheck.md | UI | current | S3 of the Online UI overhaul: the player colour became a free hex value with a palette of blocks, a pure Runtime codec and a new local preference that profiles carry; how a tinted block reads and how the field takes typing await one game run (`docs/backlog/done/online-ui-art-and-controls-overhaul.md`) |
| ui/online-ui-layout-and-input-detail-selfcheck.md | UI | current | the acceptance pass of 2026-09-27: the shell's bands and the frame's size, content-measured control widths and one compact height, wrapped-label heights, the geometry inside the game's rows, the dropdown popup layer, the pointer surfaces, the frame's border, the launcher's idle floor and the colour field's live hex; the shape is pinned, while the pixels and the clicks await one game run (`docs/backlog/review/online-ui-layout-and-input-detail-pass.md`) |
| ui/online-ui-input-blocking-retirement-selfcheck.md | UI | current | S4 of the Online UI overhaul: the guard leaves CUO's own surface alone (its blocker used to cover the launcher and the window) and both world input paths ask one pointer census that knows the launcher's rectangle; the console overlay, the quick panel, the context menu and the world-space overlays are decided, and whether the click really lands awaits one game run (`docs/backlog/done/online-ui-art-and-controls-overhaul.md`). Its S5-affected rows (the two panels "still IMGUI", `OnlineUiBlockRectTests` "stays", the scoped blockers "kept") are superseded by S5 — see the manifest row below |
| ui/online-ui-panels-selfcheck.md | UI | current | S5 of the Online UI overhaul: the quick panel and the in-world player context menu are controls of CUO's own uGUI surface (one panel view, the shared row machinery, one namespaced action table), the scoped-rectangle blocking retires with them, the census loses its geometry, and the surface's canvas finally covers the game's; how the panels read and whether the click lands await one game run (`docs/backlog/done/online-ui-art-and-controls-overhaul.md`) |
| ui/online-ui-world-overlay-selfcheck.md | UI | current | S6 of the Online UI overhaul: the network readout, the nameplates, the off-screen arrows and the location pings are labels of the game's own font on CUO's canvas (one view, a pooled mark+label pair per marker, the Runtime's geometry asked in the canvas's own units, the arrow glyph from `OffScreenArrowText`), the IMGUI overlay and `LocationPingOverlay` are deleted, and the overlay takes no input; how the markers read and where they land await one game run |
| items/local-item-into-remote-display-selfcheck.md | Items | current | a LOCAL item released onto another player's display proxy is cancelled at the release seam before the native body runs (the container branch used to load it into the proxy, and the clone rebuild then destroyed it); the target reads are the native body's own, the seam member is pinned by the gate census and the L0 port contract, and batch `20261006-e` read the runtime rows on the deployed `0.1.0+6e07b388…` |
| items/display-body-query-seam-selfcheck.md | Items | current | every `Body` query the remote-release redirect answers is answered by SKIPPING the native body, because those bodies index the LOCAL body's state behind the guard the redirect answered (`Body.GetItem` threw `Transform child out of bounds` and lost matrix row 6's swap half, batch `20261006-f`); pinned by `RemoteDragQuerySeamGateTests`, and the runtime re-drive is the next batch's row |
| items/retired-display-proxy-adopt-selfcheck.md | Items | current | the materialization adopt scan claimed a clone display proxy the renderer had just retired (its `includeInactive: false` ancestor lookup stops answering the moment the proxy is deactivated), stamped the second child's id on it one frame before its deferred destroy, and that destroy was then reported into the kernel as `terminal`; the tie-break is now the pure `AdoptTargetRule` plus one `IsDisplayProxy` predicate across four classifiers and six domain paths, pinned by `AdoptTargetGateTests` and `AdoptTargetRuleTests`, and batch `20261006-h`'s re-drive is the next batch's row |
| items/layer-change-warning-storm-selfcheck.md | Items | current | batch `20261005-b`'s log storm is attributed to the per-frame `[ItemPhysics] settle` line — 4,445 of the surviving log's 7,368 lines, the same SHAPE from a later session rather than a re-measurement of the storm, whose log is gone — while the warning the batch juxtaposes with the growth rides the 5-second snapshot keyframe and is a few dozen lines; both are now bounded by `LogRepetitionGuard`/`ItemDistanceLog` and pinned by `LogVolumeGateTests` (mutation-checked, census floors) and 16 guard cases, and the member's exit from the world is filed separately because its cause is not attributed |
| items/remote-clone-warning-storm-selfcheck.md | Items | current | the same family's THIRD producer, filed by batch `20261007-a`: the renderer's clone ensure retries once per member per frame and a member out of the world never resolves the failure it reports, which grew one client's log by 7.25 MB/min (58,148 identical lines / 8.796 MB in ~90 s, still climbing at close). Both failure lines now ask a `LogRepetitionGuard` keyed on (failure, member) and sit inside that ask; the run is drained where it ends — the clone built again, the member's leave, or the session, the last through the guard's new `Subjects` walk, which is the only path that could tell the storm's own size. Pinned by `LogVolumeGateTests` (two new facts, the containment matchers for the ask and the drain, five census floors; 25 cases) and `LogRepetitionGuardTests` (19 cases, 3 new), four mutations red, and the runtime re-drive is the next batch's row |
| enemies/enemy-generation-pairing-late-join-selfcheck.md | Enemies/World | current | batch `20261006-g`'s third client — a member that entered the world about two minutes into the run — failed the all-or-nothing generation pairing on every 60 s cycle for the whole session (`69 host vs 69 guest`, `mapping=False`) while the member present at world entry read `mapping=True` in the same cycles: the call site ordered the host's facts by `SpawnPosition` and then COMPARED `Position`, the live pose the 20 Hz stream refreshes, against the member's frozen copies, so the pairing held only in the instant after generation. The key now lives in `EnemySpawnArbitration.TryPairGeneratedCopies`, which takes the facts and derives it (the call site no longer supplies a position list), returns each pair with the caller's own copy index, reports a refusal as both counts plus the first mismatching index and its distance, and reports the worst key distance of a pass beside `mapping=True`; `TryPair`/`Order` are deleted, the runtime-spawn channel's deliberately different (live-pose) key is now documented in its own class, two mutations restore the pre-fix behaviour with 4 and 2 cases red and the file byte-identical (SHA-256), and the runtime rows await a three-client batch |
| world/layer-complete-choice-for-members-selfcheck.md | World/Layer | current | The user's 2026-10-07 report that a dead host must not strand the group at the end of a layer, attributed statically — `docs/decisions/active.md` |
| mod-api/mod-crafting-quality-labels-selfcheck.md | Mod platform | current | A mod item can carry crafting qualities through `ModItemDefinition.Qualities`, written into the game's own `ItemInfo.qualities` — `docs/backlog/done/mod-crafting-quality-labels.md` |
| mod-api/mod-defined-wire-packets-selfcheck.md | Mod platform | current | a mod now defines its own packets: IModContext.Packets declares an id the mod owns (ModMessageMsg.PacketId beside the mod id on the existing frame, empty = the anonymous tunnel form), who may send it, and which copies run its Validate/Apply/Observe chain, while the host relays a members report to every other member, the reporter excluded, and the framework owns every refusal; pinned by ModPacketsTests/ModPacketsFailureTests/ModPacketsRailsTests (42 cases across three classes: the delivery matrix, the relay, the failure story, the rails, two mutations red) and two in-tree consumers, and the two-client rows wait for the next acceptance batch |
| mod-api/mod-value-model-review.md | Mod platform | current | independent adversarial review of the typed value model cycle (decision 248), run against the frozen tree in a fresh context: no blocker, two major (the decoder let an empty map field name reach the model and throw through a `Try` method; the Chinese how-to page was half-migrated while the alignment record already called the pair confirmed), three minor (the model handed out its mutable containers, a stale `see cref` on the renamed interface, four numbers that did not reproduce) and four nits, all dispositioned in the same commit; the report also records what it tried and could not falsify (round-trips of every kind, boundary sizes, an 8-deep value accepted and a 40-deep one refused before the descent, a 2 GiB length refused before allocation) and what needs the game (a live session's message and status delivery) |
| mod-api/mod-value-model-selfcheck.md | Mod platform | current | Stage A: `ModValue`/`ModValueKind` are the typed data model, with no null kind and one explicit binary leaf — decision 248, `docs/backlog/review/mod-api-no-opaque-envelopes.md` |
| mod-api/mod-payload-null-collections-selfcheck.md | Mod platform | current | **[Narrowed 2026-10-08]** The decode half now reaches only the contracts CUO itself carries (`ModStatusUpdate` and the two status projections); a content definition is a typed object and is never serialized (decision 247), so the census discovers the travelling contracts and the mod-built declarations separately - one payload member through all three shapes, plus a null write for each mod-built member. **[Narrowed 2026-10-09]** The travelling contracts carry a `ModValue` now, so the decode half and its census are retired (see `mod-api/mod-value-model-selfcheck.md`); what this record's member half states still holds. **[Narrowed 2026-10-09, decision 251]** The member half's two named helpers left the data classes — `ModStatusDefinition.ResolveMoodleId` and `ModStructureDefinition.TryGetSpawnCount` are members of `ModStatusPresentation` / `ModStructureDistribution` now — and the rule itself gained its read-seam half, `ModDeclarationCollections`. |
| mod-api/mod-content-typed-registration-review.md | Mod API | current | independent adversarial review of the typed content registration cycle (decision 247), run against the frozen tree: no blocker and no major, three minor findings (two stale self-check anchors, the new interface's stability level left to omission, the provider cast refusal covered for one of nine providers) and six nits, all fixed in the same commit; the report records what it could not falsify (the mod-mutates-after-registering hazard is documented, not pinned) and what needs the game (the acceptance recipe runs at acceptance time) |
| mod-api/mod-content-kind-coverage-selfcheck.md | Mod platform | current | The content-kind vocabulary names exactly the kinds a CUO provider binds, and a registration nothing binds is answered at load time — decision 245, `docs/backlog/done/mod-content-kind-with-no-provider.md` |
| players/cross-player-native-injection-semantics-selfcheck.md | Players | current | Part A: the injection chain drops its id/coefficient table and the host admits from the game's own registries through `ILimbUseSemantics` — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| players/cross-player-native-topical-semantics-selfcheck.md | Players | current | The topical chain drops its id/ml table: admission reads `ItemInfo.usableOnLimb` plus `LiquidType.healthUsable` through `ILimbUseSemantics` — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| players/cross-player-native-drink-semantics-selfcheck.md | Players | current | The drink chain: the item's own `LiquidItemInfo.usable` flag is the gate, and the transcribed `RemoteDrinkMedicineCatalog` is deleted — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| players/cross-player-native-wear-semantics-selfcheck.md | Players | current | The wear chain: the game's own item data answers whether an item is wearable, which limb, and which slot, through `IWearSemantics` — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| players/cross-player-solid-food-semantics-selfcheck.md | Players | current | The consume family's last table: the item's own use action answers instead of `RemoteConsumeCatalog`, with the host committing the post-eat condition onto the owner — `docs/backlog/done/mod-cross-player-solid-food-semantics.md` |
| players/cross-player-native-limb-tool-semantics-selfcheck.md | Players | current | The limb-tool chain reads the game's own data (`ItemInfo.usableOnLimb` plus the item's `useLimbAction`) instead of the deleted `RemoteLimbToolCatalog` — `docs/backlog/review/mod-cross-player-native-semantics.md` |
| players/limb-tool-named-limb-refusal-selfcheck.md | Players | current | The limb-tool family's own protection gaps (the independent review of the entry-mapping record cycle, findings M1 and m2): the affected side now asks `NativeLimbTarget.ResolveNamed` and never the automatic "most injured limb" rule, so a request whose named limb this body cannot serve — dismembered since the operator's pick, or an index past its layout — is REFUSED with a log naming the item and the index instead of landing on a limb nobody picked, while the injection and topical chains (and the world drag's topical measurement) keep the automatic rule their own cases pin. The world-drag isolation pin is widened from `CrossPlayerDragUse.cs` alone to `LocalUseItemEligibility.IsUseItem`/`FamilyOf`, where the gate actually lives, with a member-scoping contract case; no wire, no content and no protocol change |
| players/topical-entry-and-named-limb-selfcheck.md | Players | current | The topical chain's entry and limb (the user's 2026-10-08 ruling applied to it, decision 246): a topical application is the MEDICAL view's action — its native call site is the same wound-view release the limb tool uses (`PlayerCamera.ApplyWoundItem`, which runs the item's own `useLimbAction(this.selectedLimb, item)`) — so `LocalUseItemEligibility.IsWorldDragFamily` keeps the family off the world drag (the release falls through to the native drop, which is what the inventory use means for a carrier whose `usable` is false), the host refuses a topical request that names no limb (`TopicalUseBranch`, split out of `PlayerItemUseService` at the 600-line gate), and the affected side applies to the limb the request named or refuses (`NativeTopicalApply` asking `NativeLimbTarget.ResolveNamed`). The world drag's topical measurement and `ResolveMeasureLimb` are deleted; `TopicalChainGateTests` pins both ends; the injection chain deliberately keeps the automatic rule, where -1 is a legal auto-select |
| mod-api/mod-value-model-stage-b-review.md | Mod platform | current | Adversarial review of stage B of the value model (decision 249), dispositioned in the same commit — `docs/decisions/active.md` |
| mod-api/mod-value-model-stage-b-selfcheck.md | Mod platform | current | Stage B of the no-opaque-envelope ruling: `IModData`/`IModState` carry a `ModValue` and the byte-era payload surface is emptied — decision 249, `docs/backlog/review/mod-api-no-opaque-envelopes.md` |
| mod-api/mod-api-typed-seams-selfcheck.md | Mod platform | current | The untyped invoke path is deleted rather than retyped: an operation is reached through its own typed projection — decision 250, `docs/decisions/active.md` |
| mod-api/mod-api-typed-seams-review.md | Mod platform | current | Adversarial review of the typed-seams change (decision 250), dispositioned in the same commit — `docs/decisions/active.md` |
| mod-api/mod-content-declaration-contract-selfcheck.md | Mod platform | current | Stage A: a content declaration's kind is an interface (`IModItemDefinition` and its eight siblings extend `IModContentDefinition`) — decision 251, `docs/backlog/review/mod-content-attribute-declarations.md` |
| mod-api/mod-content-declaration-binding-selfcheck.md | Mod platform | current | Stage B: the bare `[ModContent]` attribute and the scan that discovers it, with the kind and the owner unguessed — decision 252, `docs/backlog/review/mod-content-attribute-declarations.md` |
| mod-api/mod-content-declaration-binding-review.md | Mod platform | current | Adversarial review of the attribute-declaration scan (decision 252), dispositioned in the same commit — `docs/backlog/review/mod-content-attribute-declarations.md` |
| mod-api/mod-content-nested-member-contracts-selfcheck.md | Mod platform | current | Fourteen kind contracts (`IModItemTool` and its siblings) declare exactly the members their data classes carried, collection members included — decision 253, `docs/backlog/review/mod-content-nested-member-contracts.md` |
| mod-api/mod-content-fingerprint-selfcheck.md | Mod platform | current | One canonical text over the address of every entry a mod registered, and its digest — decision 254, `docs/backlog/review/mod-content-fingerprint.md` |
| mod-api/mod-content-fingerprint-review.md | Mod platform | current | Adversarial review of the content fingerprint (decision 254), dispositioned in the same commit — `docs/backlog/review/mod-content-fingerprint.md` |
| mod-api/mod-content-nested-member-contracts-review.md | Mod platform | current | Adversarial review of the nested member contracts (decision 253), dispositioned in the same commit — `docs/backlog/review/mod-content-nested-member-contracts.md` |
| mod-api/mod-content-declaration-contract-review.md | Mod platform | current | Adversarial review of the declaration-contract change (decision 251), dispositioned in the same commit — `docs/backlog/review/mod-content-attribute-declarations.md` |
| items/item-identity-across-transforms-design.md | Items | current | Research on what an item's instance id means after a transform, written before the ruling and kept as the evidence it rests on — `docs/backlog/review/item-identity-across-transforms.md` |
| items/applied-item-location-selfcheck.md | Items | current | Stage 1 (model half): a fifth `ItemLocationKind` (`Applied`) with its limb index, the two kernel commands and the archive rule — `docs/backlog/review/item-identity-across-transforms.md` |
| items/applied-item-round-trip-selfcheck.md | Items | current | Stage 2 of the ruled option 1, and the cycle that makes the rule true in a running game: every APPLY and REMOVAL path commits one relocation — the cross-player apply from the affected side's own outcome, the local apply from `PlayerCamera.ApplyWoundItem`, the cross-player removal as a MOVE of the applied row to the operator, and the local removal from the component's own `TakeOff`, whose object wears the applied id because it is stamped in `Item.Awake`. Two wire commands (`ItemApplyLimb`/`ItemRemoveLimb` with their payload types, gateway eligibility and re-report window), `FindAppliedLimbItem` as the one `(owner, limb, definition)` binding every gesture resolves through, the read-only `RestoredAppliedItemAudit` that names a two-carrier divergence in both directions, and the DELETION of the synthesized id namespace with its counter, its resume half and its documentation. 25 new cases; the native patch bodies still need a game process, the local take-off's one round-trip race is named, and the mod-facing page and by-id read landed as stage 3 (`items/applied-item-mod-surface-selfcheck.md`). |
| items/applied-item-round-trip-review.md | Items | current | The independent adversarial review of stage 2 (FULL tier, frozen uncommitted tree at HEAD `3cf42be3`). It reproduced the gates (615/615), the behaviour total, the 25-case count by attribute diff and every quoted line count, and found ONE reasoning error that mattered plus one missing piece of the stage: the patch doc claimed a Harmony prefix returning `false` skips postfixes (it does not — the repo's own `WorldTimeSync` says so), so both postfixes now act on `__runOriginal`; and the applied row's `condition` was written once at the apply and never followed the decaying component, which the design's §6.1 assigns to the 1 Hz character capture — `AppliedItemConditionProjection` is now that seam. Plus two stale comments, an inline wire-member list and one over-broad wording about the decay path. Every finding is listed with its disposition, and the record states what the review could not falsify and what it could not check at all. |
| items/applied-item-location-review.md | Items | current | The three independent adversarial reviews of that stage-1 change, against the frozen trees `984ffa18`, `151fd56f` and `056865b3` (the last became commit `467f20f9`). **No blocker in any pass.** Pass 1 (FULL) found the diagnostic fact's missing limb and the pickup/drop hole; pass 2 (FULL) found that `DecideSpawn` accepted an applied location from the wire — a row that only another body's owner could ever end, so nothing could correct it — and that three already-checked checklist boxes carried figures the first fix round had not back-filled; pass 3 (NARROWED) found the checklist's build-box line still carrying the previous cycle's evidence clause and three further stale figures. Every finding is listed with its disposition, and the record states what the reviews could not check (`dotnet format`, the 4955 baseline, anything needing the game). |
| items/applied-item-mod-surface-selfcheck.md | Items | current | Stage 3 of the ruled option 1, the MOD-FACING half: `IModGameState.TryGetItem` answers "what is this item and where is it now" from the kernel row, `IModItemState`/`ModItemLocation` carry the answer, and the lifecycle rule ("an instance id names one item for its whole life and is never re-issued") is written into `docs/en/reference/mod-api.md` and its Chinese twin. The read's SOURCE is the kernel, which is why it answers on the host, on the applier and on a third member — no wire member was added. Four decisions settled here: the answer mirrors the kernel's one-shape location and deliberately omits the stale slot, an ENDED id answers `true` with `Terminal` rather than `false`, the new surface is `Experimental`, and the family mapping is a switch rather than an ordinal cast. 8 cases; no two-client run has read one of these, and stage 2's unproven `LimbItemTransform` is unchanged. |
| items/applied-item-mod-surface-review.md | Items | current | The independent adversarial review of stage 3 (FULL tier, frozen uncommitted tree at HEAD `a36db820`). It reproduced the behaviour total, the 8 cases, the baseline's byte-identity with the gate's emitted candidate, the three aligned page pairs, the MANIFEST row and every quoted line count, and found ONE major documentation error plus five minors and five nits: the lifecycle rule claimed every other limb tool ends the source row, while the ice pack's own branch (`Item.cs:1621-1639`) only spends `item.condition` and the item survives — contradicted by the change's own design record; a baseline delta of "17 added + 1 changed" that is really `18 0`; `ParentItemId` documented as contained-only although a world container's contents carry one too; a `false` that is also what a guest before the join checkpoint gets for every id; five stale ticket paths the rename left behind; and four wording/detail fixes (the registration claim, a claim with no case behind it, an inferred claim now recorded as a limit, the MANIFEST describing stage 3 as pending). Every finding is listed with its disposition, and the record states what the review could not falsify and what it could not check at all. |
| mod-api/mod-item-authored-actions-selfcheck.md | Mod platform | current | `ItemInfo.useAction`/`useLimbAction` are authorable in code through `IModItemActions`, with the reachability and isolation rails — `docs/backlog/done/mod-item-authored-actions.md` |
| mod-api/mod-item-wearable-declaration-selfcheck.md | Mod platform | current | The mod item's wearable declaration (ticket `docs/backlog/review/mod-item-wearable-declaration.md`, cut from the ceiling ticket's Part 3.A): `IModItemDefinition.Wearable` was a bool with no placement behind it, while the game's own wear family resolves `desiredWearLimb` with `Body.LimbByName` and dereferences the result at four sites (`Body.cs:1493`, `:1541`, `:1558`, `:1575`) — a crash for any mod item that declared the flag. The slice is `IModItemWearable` now (limb, slot, armor, isolation, hit-durability loss, held-in-inventory, worn sorting offset); the provider installs the flag and the placement TOGETHER and reports an incomplete declaration at load without installing the flag; the guard family covers all four dereference sites through one rule and a one-member `IWearablePatchPort`; the wear prefix states its outcome to the postfix so a refused wear reports nothing; and the sibling ticket's "none of them is a crash" claim is corrected in place. |
