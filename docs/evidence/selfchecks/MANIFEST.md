# Self-Check Evidence Manifest

All files under `docs/evidence/selfchecks/` are historical/per-delivery evidence records. This manifest marks the current-vs-historical boundary from the 2026-08-30 content audit. "historical" means the wire path/mechanism described is no longer the active path; the user-visible feature may still exist through a newer kernel/protocol path.

| File | Domain | Status | Note |
|---|---|---|---|
| tooling/adapter-control-surfaces-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| enemies/animal-death-presentation-selfcheck.md | Enemies/World | current | candidate current evidence; verify before citing |
| world/air-write-block-damage-cleanup-selfcheck.md | World/Entities | current | candidate current evidence; verify before citing |
| world/block-damage-progressive-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| world/blueprint-popup-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| ui/building-destruction-presentation-selfcheck.md | UI | current | candidate current evidence; verify before citing |
| players/building-entity-health-selfcheck.md | UI | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| world/cactus-selfdamage-sync-selfcheck.md | Other | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/carry-interaction-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/character-attack-anim-and-player-context-menu-selfcheck.md | Players | current | candidate current evidence; verify before citing |
| players/character-data-persistence-selfcheck.md | Players | historical | superseded by S4.1 (decision 178): the `.bin` reconnect store it delivered is deleted, so its layout/paths/test names are not current |
| players/character-ragdoll-toggle-sync-selfcheck.md | Players | current | candidate current evidence; verify before citing |
| players/character-sound-selfcheck.md | Players | current | candidate current evidence; verify before citing |
| players/carried-idle-sit-suppression-selfcheck.md | Players | current | carried-ride idle-sit suppression landed |
| players/carried-rider-simulation-selfcheck.md | Players | current | rider keeps the native per-frame simulation while the carry relation owns the transform; the ECG/twitch rows await the user's dual-client run |
| players/carried-rider-placement-smoothing-selfcheck.md | Players | current | carried-ride placement smoothing, vertical consistency, and local-carrier mount rework landed |
| ui/chat-selfcheck.md | UI | current | candidate current evidence; verify before citing |
| ui/online-ui-console-page-removal-selfcheck.md | UI | current | the Online UI window's duplicated console page is deleted (decision 228); the slash-opened overlay is the only command and chat surface |
| presentation/building-entity-hit-flash-sync-selfcheck.md | Other | current | melee red HitFlash replays on non-attacker views through BuildingEntityDamaged |
| presentation/clone-face-presentation-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| presentation/remote-clone-face-vitals-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| tooling/config-options-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| tooling/acceptance-recipe-layer-selfcheck.md | Other | current | the recipe layer: driver `-Action recipe`, declared-arg recipes under `tools/acceptance/recipes/`, gates; the live run is still pending |
| tooling/acceptance-key-hold-selfcheck.md | Other | current | the held-key capability: `-Action declare` loading `driver/eval-declarations/`, `recipes/key-hold.cs`, the one-directory OS-input exception; smoked live, the remote gesture still pending |
| items/container-content-sync-selfcheck.md | Items | current | candidate current evidence; verify before citing |
| items/container-move-event-carrier-selfcheck.md | Items | current | a peer's intent executed by the owner is the owner's own fact: `CallContext.Origin.RemoteIntentApply` + `IsReplayedRemoteFact`; the three-client run is still pending |
| items/container-move-kernel-fact-selfcheck.md | Items | current | one container report = the parent's fact + every child's place, committed as the same `SyncContainerItemsCommand` on the wire and host-local paths; row A1 still awaits a three-client batch |
| items/remote-intent-drop-report-order-selfcheck.md | Items | current | no inventory re-report goes out while a drop report is pending, at both of the owner's entry points; the drop and wearable-drop rows still await a three-client run |
| items/container-move-pair-classification-selfcheck.md | Items | current | the container-move pair: the unload registers a departure with where the item came from, the load classifies through `ContainerLoadClassifier`, and the departure machine holds one entry per item; row A1g still awaits a three-client batch |
| players/cross-player-component-medicine-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/cross-player-component-tool-use-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/cross-player-drinkable-medicine-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| items/cross-player-item-use-selfcheck.md | Items | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/cross-player-limb-tool-use-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/cross-player-medicine-use-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/cross-player-opiate-use-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/cross-player-shrapnel-and-timed-tool-use-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/cross-player-timed-liquid-medicine-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/cross-player-topical-use-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/cross-player-wear-use-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| enemies/crystal-enemy-tint-selfcheck.md | Enemies/World | current | candidate current evidence; verify before citing |
| enemies/crystal-mimic-selfcheck.md | Enemies/World | current | candidate current evidence; verify before citing |
| enemies/crystal-teleport-sync-selfcheck.md | Enemies/World | current | candidate current evidence; verify before citing |
| enemies/crystal-ticking-selfcheck.md | Enemies/World | current | candidate current evidence; verify before citing |
| enemies/crystal-windup-telegraph-sync-selfcheck.md | Enemies/World | current | candidate current evidence; verify before citing |
| items/custom-item-data-state-selfcheck.md | Items | current | candidate current evidence; verify before citing |
| items/building-support-drop-sync-selfcheck.md | Items | current | support-loss building drops ride the block-break message with full transient state |
| items/entity-destruction-drop-fresh-presentation-selfcheck.md | Items | current | entity-destruction trap/building drops keep full transient spawn state on replay |
| items/item-spawn-presentation-kernel-selfcheck.md | Items | current | item-spawn kernel/command/event now carry the full transient initial-drop state to every peer |
| presentation/direct-placeable-arm-swing-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| items/dynamite-explosion-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| items/dynamite-fuse-presentation-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| enemies/enemy-combat-replay-split-selfcheck.md | Enemies/World | current | candidate current evidence; verify before citing |
| enemies/enemy-effects-selfcheck.md | Enemies/World | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| items/enemy-item-hit-sync-selfcheck.md | Items | current | candidate current evidence; verify before citing |
| enemies/enemy-stun-presentation-selfcheck.md | Enemies/World | current | candidate current evidence; verify before citing |
| enemies/enemy-targeting-selfcheck.md | Enemies/World | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| fluids/fluid-presentation-selfcheck.md | Fluids | current | candidate current evidence; verify before citing |
| presentation/footstep-sound-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| tooling/game-adapter-split-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| tooling/gameadapter-construction-selfcheck.md | Other | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| presentation/grappling-hook-presentation-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| items/gun-state-sync-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| tooling/handler-context-narrowing-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| players/heal-interaction-selfcheck.md | Other | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| items/heal-item-selection-selfcheck.md | Items | current | candidate current evidence; verify before citing |
| items/heater-cook-selfcheck.md | Items | current | candidate current evidence; verify before citing |
| world/heater-xaloris-local-body-effect-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| presentation/held-light-direction-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| protocol/high-frequency-small-drops-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| session/host-ban-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| session/host-close-room-safe-exit-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| session/host-kick-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| session/host-rules-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| protocol/hot-path-latency-instrumentation-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| ui/i18n-framework-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| protocol/ip-direct-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| items/item-cook-replay-split-selfcheck.md | Items | current | candidate current evidence; verify before citing |
| items/item-keyframe-state-selfcheck.md | Items | current | candidate current evidence; verify before citing |
| items/item-service-split-selfcheck.md | Items | current | candidate current evidence; verify before citing |
| players/limb-presentation-selfcheck.md | Players | current | candidate current evidence; verify before citing |
| session/lobby-leave-host-rules-editor-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| tooling/log-level-cleanup-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| presentation/looktarget-gaze-sync-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| session/member-status-icons-and-session-hotkeys-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| presentation/mine-press-visual-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| mod-api/mod-content-registration-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-content-migration-base-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-item-content-binding-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-entity-spawn-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-game-state-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-native-api-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-service-split-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-state-saves-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-ui-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-building-runtime-hooks-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| presentation/muzzle-flash-sync-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| presentation/remote-clone-facing-auto-flip-selfcheck.md | Other | current | stale render-clone facing auto-flip inputs neutralized |
| presentation/remote-clone-head-mouth-sync-selfcheck.md | Other | current | owner head/mouth state replayed on remote clones; host fall-injury mouth desync |
| presentation/remote-clone-legspeed-pose-selfcheck.md | Other | current | owner leg-speed multiplier replayed as CrouchAmount weakness/slouch input; severe-sleepiness posture desync |
| presentation/nap-and-dog-shake-sync-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| players/native-remote-backpack-and-door-sound-selfcheck.md | Other | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| protocol/netmsg-registry-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| players/network-health-metrics-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| protocol/network-traffic-monitor-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| protocol/network-traffic-baseline-selfcheck.md | Other | current | network traffic baseline and regression gate landed |
| protocol/state-stream-bandwidth-reduction-selfcheck.md | Other | current | player-state per-recipient echo removal landed |
| protocol/global-adaptive-report-rate-stage-2-traffic-bandwidth-selfcheck.md | Other | current | per-peer/per-stream traffic estimates, bandwidth/failed-send pressure and byte-budget policy landed |
| protocol/checkpoint-string-table-selfcheck.md | Other | current | checkpoint item-definition string table compression landed |
| players/online-ui-player-awareness-selfcheck.md | Players | current | candidate current evidence; verify before citing. Its rendering row (`OnlineUiOverlay.DrawNameplatesAndArrows`) is superseded by S6 of the Online UI overhaul (`ui/online-ui-world-overlay-selfcheck.md`): the nameplate is a label on CUO's canvas now, while the marker rules it records (the head anchor, the world-presence filter, the distance text) still hold |
| ui/online-ui-polish-selfcheck.md | UI | current | candidate current evidence; verify before citing |
| ui/player-list-peer-id-disambiguation-selfcheck.md | UI | current | candidate current evidence; verify before citing |
| ui/online-ui-scoped-passthrough-selfcheck.md | UI | historical | superseded by S5 of the Online UI overhaul (`review/online-ui-art-and-controls-overhaul.md`): the scoped UGUI blocker it describes (`SetOnlineUiScopedBlocks`, `OnlineUiBlockRect`, `OnlineScopedRaycastFilter`) retired with the two IMGUI panels that were its only consumers, so the mechanism, its files and its pins are deleted |
| ui/online-ui-selfcheck.md | UI | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| ui/online-ui-window-selfcheck.md | UI | current | candidate current evidence; verify before citing |
| ui/remote-context-menu-medical-visibility-selfcheck.md | UI | current | Medical remote-action visibility now follows the shared line-of-sight gate on context menu/Players/quick panel |
| world/openable-keypad-prefabs-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| presentation/owner-local-body-auto-events-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| tooling/partial-aware-gate-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| tooling/check-architecture-performance-selfcheck.md | Other | historical | the PowerShell script was removed after the architecture gate was ported to C# tests |
| tooling/guest-frame-rate-baseline-selfcheck.md | Other | current | guest frame-rate baseline telemetry and cached RemotePlayers hot-path allocation removal |
| tooling/patch-contract-overload-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| architecture/phase-a-kernel-foundation-selfcheck.md | Architecture | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| architecture/phase-b-item-authority-selfcheck.md | Architecture | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| architecture/phase-c-protocol-core-selfcheck.md | Architecture | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| architecture/phase-d-enemies-shadow-selfcheck.md | Architecture | current | candidate current evidence; verify before citing |
| architecture/phase-d-fluids-shadow-selfcheck.md | Architecture | current | candidate current evidence; verify before citing |
| architecture/phase-d-full-domain-migration-selfcheck.md | Architecture | current | candidate current evidence; verify before citing |
| architecture/phase-d-high-frequency-stream-unification-selfcheck.md | Architecture | current | candidate current evidence; verify before citing |
| architecture/phase-d-players-shadow-selfcheck.md | Architecture | current | candidate current evidence; verify before citing |
| architecture/phase-d-world-entities-shadow-selfcheck.md | Architecture | current | candidate current evidence; verify before citing |
| architecture/phase-d-world-run-epoch-shadow-selfcheck.md | Architecture | current | candidate current evidence; verify before citing |
| architecture/phase-e-legacy-inventory-selfcheck.md | Architecture | current | candidate current evidence; verify before citing |
| architecture/composite-command-sequential-semantics-selfcheck.md | Architecture | current | candidate current evidence; verify before citing |
| architecture/application-layer-first-slice-selfcheck.md | Architecture | current | the Application layer as a project, the declared project-direction gate (with its control run), the command admission seam (decision 210) and the kernel-replication move with its seven ports (decision 211) |
| architecture/adapter-capability-ports-selfcheck.md | Architecture | current | the Game Adapter boundary as ten capability ports with a member-free composition, the call-site census behind the set, the shape gate with its mutation controls, and the `InternalsVisibleTo` census (decision 212) |
| items/pickup-inflight-selfcheck.md | Items | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/piggyback-drop-cleanup-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/piggyback-facing-restore-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/piggyback-releasable-carry-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/player-interaction-followups-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/player-interaction-service-split-selfcheck.md | Players | current | candidate current evidence; verify before citing |
| players/player-interaction-visibility-selfcheck.md | Players | current | candidate current evidence; verify before citing |
| players/player-inventory-take-selfcheck.md | Players | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/player-color-and-head-tags-selfcheck.md | Players | current | the head-tag half still holds; its COLOUR half is superseded by S3 of the Online UI overhaul (`review/online-ui-art-and-controls-overhaul.md`): the `PlayerColorIndex` entry, `PlayerColorResolver.TryGet` and the preset-only picker it describes are deleted, and the colour is a free `[UI] PlayerColor` hex value now |
| players/player-push-selfcheck.md | Players | current | candidate current evidence; verify before citing |
| players/player-quick-panel-selfcheck.md | Players | current | candidate current evidence; verify before citing. Its RENDERING half is superseded by S5 of the Online UI overhaul: the panel is a control of CUO's own uGUI surface now and the IMGUI member card it drew is deleted; its interaction rules (target picker, eligibility, `member.get_down`) still hold |
| players/remove-legacy-view-items-remote-inventory-selfcheck.md | Players | current | candidate current evidence; verify before citing |
| world/radiation-line-state-sync-selfcheck.md | World/Entities | current | candidate current evidence; verify before citing |
| world/radiation-straggler-pressure-selfcheck.md | World/Entities | current | candidate current evidence; verify before citing |
| players/ragdoll-stale-state-fix-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| players/ragdoll-render-proxy-limb-pose-selfcheck.md | Players | current | accepted after red-green replay and user verification |
| players/ragdoll-limb-pose-sync-selfcheck.md | Players | current | exact owner limb poses now ride the 20 Hz player stream |
| README.md | Other | current | candidate current evidence; verify before citing |
| items/remote-backpack-container-take-selfcheck.md | Items | current | candidate current evidence; verify before citing |
| items/remote-backpack-native-interaction-parity-selfcheck.md | Items | historical | superseded by the stage 0 design (decision 217): the CUO routing table and the host mirror-edit path it describes are deleted by the native-intent rework; its container findings stay the rework's acceptance input |
| items/remote-inventory-native-parity-design-selfcheck.md | Items | current | stage 0 of the native-parity rework: the native gesture inventory, the intent vocabulary and the ticket adjudication; design only, no behaviour change, stages 1-4 still open |
| items/remote-inventory-native-intent-stage1-selfcheck.md | Items | current | stage 1 of the native-parity rework: the release window, the capture seam, the owner-side replay and the validating host half; the clone-edit path it replaced is deleted, and the reported rows still await the user's acceptance run |
| players/remote-backpack-held-item-transfer-selfcheck.md | Players | historical | superseded by the stage 0 design (decision 217): the Tab-transfer and held-item kinds it describes are replaced by native intents (`TransferToBody`); its reported rows feed the rework's acceptance matrix |
| players/remote-backpack-drag-escape-selfcheck.md | Other | historical | superseded by native remote-backpack parity cycle (Tab-switch transfer) |
| items/remote-container-content-view-selfcheck.md | Items | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| items/remote-container-destroy-authority-selfcheck.md | Items | current | candidate current evidence; verify before citing |
| items/remote-world-item-same-id-dedup-selfcheck.md | Items | current | candidate current evidence; verify before citing |
| ui/remote-inventory-ui-followup-selfcheck.md | UI | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| players/remote-inventory-view-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| players/remote-vitals-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| players/remote-native-medical-view-selfcheck.md | Players | current | native WoundView reuse; custom IMGUI medical panel removed |
| players/remote-medical-fidelity-and-syringe-minigame-selfcheck.md | Players | current | native syringe minigame routes cross-player injectable doses; remote WoundView display projection plus ECG/moodle/nap redirection |
| players/remote-medical-local-only-controls-selfcheck.md | Players | current | a remote WoundView focus hides the nap control, the workout list and the HUD hand switch, and blocks both hand-switch paths |
| players/respawn-rules-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| session/run-settings-range-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| tooling/simtrace-diff-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| presentation/speech-sound-frequency-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| enemies/spider-enemy-presentation-sync-selfcheck.md | Enemies/World | current | candidate current evidence; verify before citing |
| tooling/start-gate-alert-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| protocol/steam-p2p-cert-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| items/trader-recruit-gift-items-selfcheck.md | Items | current | candidate current evidence; verify before citing |
| enemies/trader-recruit-selfcheck.md | Enemies/World | current | candidate current evidence; verify before citing |
| enemies/trader-swing-sync-selfcheck.md | Enemies/World | current | candidate current evidence; verify before citing |
| presentation/turret-light-sprite-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| world/transient-trap-state-checkpoint-selfcheck.md | World/Entities | current | turret/geyser transient trap state no longer replayed by periodic checkpoint |
| world/tutorial-claw-selfcheck.md | World/Entities | current | candidate current evidence; verify before citing |
| world/tutorial-claw-stream-selfcheck.md | World/Entities | current | candidate current evidence; verify before citing |
| mod-api/ui-modal-input-blocker-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| presentation/wall-slide-landing-sync-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| session/warmup-backoff-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| presentation/weapon-fire-recoil-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| presentation/workout-animation-sync-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| world/world-blood-spawn-sync-selfcheck.md | World/Entities | current | candidate current evidence; verify before citing |
| world/world-entry-completion-selfcheck.md | World/Entities | current | candidate current evidence; verify before citing |
| items/world-item-service-partial-split-selfcheck.md | Items | historical | superseded/old-wire; do not cite as current evidence without checking protocol.md |
| world/world-service-split-selfcheck.md | World/Entities | current | candidate current evidence; verify before citing |
| world/world-time-selfcheck.md | World/Entities | current | candidate current evidence; verify before citing |
| items/remote-clone-display-content-id-free-selfcheck.md | Items | current | remote clone container contents no longer carry item-domain instance ids |
| items/guest-background-ghost-item-ground-sounds-selfcheck.md | Items | current | guest non-authoritative item impact presentation (drop/step/squeak/dust) suppressed |
| search/pinyin-console-completion-selfcheck.md | UI | historical | the CUO-resident console stage it describes (decision 200) was deleted when the stage moved into the satellite pinyin mod (decision 209); the seam it introduced is unchanged |
| search/pinyin-search-selfcheck.md | UI | historical | the CUO-resident matcher, patch and `Search.PinyinSearch` switch it describes (decision 199) were deleted when the feature moved into the satellite pinyin mod (decision 209) |
| search/pinyin-search-standalone-mod-selfcheck.md | UI | current | the pinyin search as a satellite mod in this repository: two assemblies split along the binding, its own BepInEx switch, the Tier-2 declaration, and the deletion of CUO's own pinyin code (decision 209) |
| architecture/plugin-host-shell-selfcheck.md | Architecture | current | the plugin as a BepInEx host shell: registration moved into the adapter's own composition, the Online UI and the lobby policy became their own units, two ports replaced the direct adapter writes, and the plugin project references no game assembly (decision 213) |
| architecture/patch-bridge-domain-ports-selfcheck.md | Architecture | current | the Harmony patch bridge split by domain: the fluid domain's eight members moved into `IFluidPatchPort`, the aggregate neither declares nor composes them (the compiler enforces the migration), and a shape gate freezes every seam's census (decision 214) |
| documentation/bilingual-human-guide-selfcheck.md | Documentation | current | the human-facing layer: three layers declared by index, the two guide levels paired English + Chinese with a per-pair blob-hash record, and a pairing gate that fails when either side moves without re-confirming the pair (policy at `docs/i18n/README.md`) |
| documentation/legacy-tree-followups-selfcheck.md | Documentation | current | the cycle after the migration: two dead doc paths and an off-by-one grammar comment in `src/`, the English how-to breadcrumb brought to the standard spelling, and the feature-matrix tool-path gate (tool literals plus the item column list) |
| documentation/legacy-tree-migration-selfcheck.md | Documentation | current | the last migration stage: two conclusions absorbed as pages, every live inbound reference re-pointed, the old `docs/` trees deleted, the event-replay table moved into `docs/contracts/`, and the retired sibling-pairing gate's budget check left with `AgentInstructionBudgetGateTests` |
| tooling/one-top-level-type-gate-selfcheck.md | Other | current | the one-top-level-type gate sees every modifier and the two-word record keyword, and the seven files it exposed are split one type per file |
| architecture/composition-root-modules-selfcheck.md | Architecture | current | candidate current evidence; verify before citing |
| architecture/di-cycle-guard-selfcheck.md | Architecture | current | candidate current evidence; verify before citing |
| architecture/legacy-wire-dto-slice-selfcheck.md | Architecture | current | the kernel-to-wire vocabulary moved into the Application layer (ticket `review/legacy-wire-dto-slice.md`) |
| architecture/projection-health-coordinator-selfcheck.md | Architecture | current | projection failure auto-recovery: a stalled projection is observed and healed instead of dropping silently |
| enemies/enemy-hit-determination-local-selfcheck.md | Enemies/World | current | candidate current evidence; verify before citing |
| items/remote-inventory-native-intent-stage2-selfcheck.md | Items | current | remote inventory native parity stage 2: the container family |
| items/remote-inventory-native-intent-stage3-selfcheck.md | Items | current | remote inventory native parity stage 3: item interaction |
| items/remote-inventory-native-intent-stage4-audit.md | Items | current | remote inventory native parity stage 4: the family audit and acceptance preparation |
| items/remote-inventory-native-parity-acceptance-checklist.md | Items | current | the real-machine acceptance checklist the remote inventory native parity rework landed with |
| mod-api/mod-building-drop-worldgen-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-content-owner-query-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-item-advanced-behavior-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-item-fixed-drop-sources-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-item-spawn-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-item-visual-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-item-worldgen-loot-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-liquid-content-binding-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-liquid-placement-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-recipe-content-binding-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-runtime-data-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-status-moodle-content-binding-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-status-moodle-row-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-status-projection-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-status-runtime-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-status-wire-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-structure-content-binding-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-structure-placement-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-structure-worldgen-distribution-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-tile-content-binding-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-tile-ore-worldgen-projection-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mod-api/mod-tile-placement-selfcheck.md | Mod API | current | candidate current evidence; verify before citing |
| mods/native-binding-declaration-selfcheck.md | Other | current | the declared native-binding tier, stage 1: the declaration |
| mods/native-binding-handshake-parity-selfcheck.md | Other | current | native-binding parity in the session handshake |
| players/carried-unconscious-body-simulation-selfcheck.md | Players | current | a dead or unconscious carried body keeps the vitals half of the native per-frame pass; the carry relation keeps pose, physics, ground contact and sounds (decision 227) |
| players/carrier-sit-suppression-selfcheck.md | Players | current | candidate current evidence; verify before citing |
| players/carry-rider-limb-anchor-selfcheck.md | Players | current | candidate current evidence; verify before citing |
| players/carry-rider-acceptance-readiness-selfcheck.md | Players | current | the deciding readings are default-visible for a carry participant and the pin-drift reading is new (decision 233) |
| players/carried-rider-presenter-split-selfcheck.md | Players | current | the carry-presentation half (local-carrier mount, per-frame pin, carry-role marks, drift reading) moved out of `RemotePlayerRenderer` (587 to 338 lines) into `CarriedRiderPresenter` (309); the reading pins were re-pointed, a mark-ordering pin was added, and behaviour is preserved by the unchanged per-frame call order |
| players/interaction-gate-authority-selfcheck.md | Players | current | each client judges its own side; twelve host-side judging sites retired |
| players/medical-operation-concurrency-selfcheck.md | Players | current | medical operations settle per unit (ticket `review/concurrent-medical-operations.md`) |
| players/remote-medical-operation-session-realtime-injection-selfcheck.md | Players | current | candidate current evidence; verify before citing |
| players/remote-medical-panel-acceptance-projection-selfcheck.md | Players | historical | superseded: `RemoteMedicalDisplayProjection` and the acceptance rows it carried were replaced by the unified remote display projection (its own ticket link points at the retired `docs/backlog/todo/` path) |
| players/remote-medical-panel-selfcheck.md | Players | historical | the rejected CUO IMGUI medical panel; superseded by the native WoundView remote focus - no parallel CUO medical panel (its own ticket link points at the retired `docs/backlog/todo/` path) |
| players/remote-medical-stage-2-shrapnel-session-selfcheck.md | Players | current | candidate current evidence; verify before citing |
| players/remote-medical-stage-3-other-actions-selfcheck.md | Players | historical | stage 3's exclusive-limb lease was superseded 2026-09-19 by per-unit settlement (`players/medical-operation-concurrency-selfcheck.md`), and the sheet's own protocol row records `ProtocolVersion.Current = 14` against the tree's 41 |
| players/remote-medical-treatment-operations-selfcheck.md | Players | current | candidate current evidence; verify before citing |
| presentation/host-eating-sound-not-heard-on-guest-selfcheck.md | Other | current | the ingest clips and the meal-end burp reach every other side as the dedicated one-shot character sound (decision 225) |
| presentation/unhooked-item-and-body-sound-families-selfcheck.md | Other | current | the medical, world-drink, gesture and coroutine one-shot sounds reach every other side through the same dedicated character-sound event; the 2D prompts stay local by decision |
| presentation/suppressed-native-call-sounds-stay-unheard-selfcheck.md | Other | current | the remote limb treatment plays the clip its blocked native limb action would have played and the bandage minigame's own step is captured; a world-item impact reaches every member as the authority's own drop/step/dust (ItemImpact, protocol 43) |
| presentation/treatment-gore-presentation-carried-selfcheck.md | Other | current | the amputation and shrapnel minigame steps carry their `gore`/`gore{N}` clip to every peer and the suture's blocked clip is a treatment-table row; the patient's client never calls the native `Dismember`, so nothing double-plays |
| presentation/unified-remote-display-projection-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| protocol/global-adaptive-report-rate-stage-3-cumulative-streams-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| protocol/global-adaptive-report-rate-stage-4-high-frequency-domains-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| protocol/ip-direct-name-validation-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| saves/world-backup-management-selfcheck.md | Other | current | the Online UI's Worlds page and the player-chosen restore (decision 198) |
| tooling/adapter-capability-catalog-selfcheck.md | Other | current | the adapter capability catalog and probe aggregation; the installers' target table is the one hand-written surface |
| tooling/config-profile-templates-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| tooling/content-id-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| tooling/game-update-contract-toolchain-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| tooling/mod-api-contract-governance-selfcheck.md | Other | current | candidate current evidence; verify before citing |
| ui/command-console-selfcheck.md | UI | current | the in-game command console; the modal Online UI console page it once sat beside was deleted 2026-09-26 (decision 228), so the slash-opened overlay is the only command and chat surface |
| ui/cuo-launcher-idle-fade-selfcheck.md | UI | current | the launcher button's idle fade (ticket `done/cuo-launcher-button-obscures-the-view.md`) |
| ui/dead-player-context-menu-title-selfcheck.md | UI | current | candidate current evidence; verify before citing |
| ui/location-ping-selfcheck.md | UI | current | the ping control, its wire path and its fade still hold; its IMGUI rendering row (`LocationPingOverlay.Draw`) is superseded by S6 of the Online UI overhaul (`ui/online-ui-world-overlay-selfcheck.md`) — the class is deleted and a ping is a marker label on CUO's canvas now |
| ui/name-tag-ui-polish-selfcheck.md | UI | current | the marker rules it records (the head anchor, the world-presence filter, the colour tag) still hold; its drawing path (`OnlineUiOverlay.DrawNameplatesAndArrows`) is superseded by S6 of the Online UI overhaul (`ui/online-ui-world-overlay-selfcheck.md`) — the nameplate is a label on CUO's canvas now |
| ui/tab-backpack-instant-close-selfcheck.md | UI | current | candidate current evidence; verify before citing |
| world/guest-hears-only-some-block-break-sounds-selfcheck.md | World/Entities | current | a break is presented on every side through the game's own damage roll, carried by the air write's `PlayerBreak` claim (decision 224) |
| world/quake-direct-write-selfcheck.md | World/Entities | current | the game's direct `Time.timeScale` write is adopted by the host and the adopted speed is re-stated silently, so the speed HUD follows (decision 226) |
| world/runtime-entity-identity-selfcheck.md | World/Entities | current | candidate current evidence; verify before citing |
| world/world-acceleration-survives-movement-selfcheck.md | World/Entities | current | candidate current evidence; verify before citing |
| tooling/catalogue-and-manifest-census-selfcheck.md | Other | current | the catalogue's declared keys now equal the keys the product source reads (73 orphan keys deleted), and the self-check manifest is the complete per-file index it claims: both censuses are gates (decision 229) |
| world/unhooked-damage-block-callers-selfcheck.md | World/Entities | current | the block-damage hook is anchored on the overload every native roll enters (the footstep crush and the spider burrow included) and the report carries the cell; the anchor is pinned by `DamageBlockHookCoverageGateTests` |
| ui/online-ui-panel-blending-selfcheck.md | UI | current | every themed frame is drawn alpha-blended, so the palette's alphas are live: the modal window, the quick panel, the context menu and the console overlay follow the launcher's already-blended frame (ticket `done/online-ui-panels-request-alpha-blend-false.md`) |
| ui/online-ui-native-facts-selfcheck.md | UI | current | S1 of the Online UI overhaul: the read-only probe of the game's own UI (font asset, settings-row style, chrome census, `PlayerCamera.uiScale`) plus the uGUI host under the game's canvas; the four values await one game run (`review/online-ui-art-and-controls-overhaul.md`) |
| ui/online-ui-native-surface-selfcheck.md | UI | current | S2a of the Online UI overhaul: the LIVE surface under the game's canvas and the launcher moved onto the game's own button-row prefab, with the idle fade applied to it and the click returning as an intent; the visual result awaits one game run (`review/online-ui-art-and-controls-overhaul.md`) |
| ui/online-ui-window-family-selfcheck.md | UI | current | S2b of the Online UI overhaul: the window shell, the tab row and all six pages moved off IMGUI onto the game's own settings rows through a Runtime display list and an intent channel; the look, the layout and the input await one game run (`review/online-ui-art-and-controls-overhaul.md`) |
| ui/online-ui-free-color-selfcheck.md | UI | current | S3 of the Online UI overhaul: the player colour became a free hex value with a palette of blocks, a pure Runtime codec and a new local preference that profiles carry; how a tinted block reads and how the field takes typing await one game run (`review/online-ui-art-and-controls-overhaul.md`) |
| ui/online-ui-layout-and-input-detail-selfcheck.md | UI | current | the acceptance pass of 2026-09-27: the shell's bands and the frame's size, content-measured control widths and one compact height, wrapped-label heights, the geometry inside the game's rows, the dropdown popup layer, the pointer surfaces, the frame's border, the launcher's idle floor and the colour field's live hex; the shape is pinned, while the pixels and the clicks await one game run (`todo/online-ui-layout-and-input-detail-pass.md`) |
| ui/online-ui-input-blocking-retirement-selfcheck.md | UI | current | S4 of the Online UI overhaul: the guard leaves CUO's own surface alone (its blocker used to cover the launcher and the window) and both world input paths ask one pointer census that knows the launcher's rectangle; the console overlay, the quick panel, the context menu and the world-space overlays are decided, and whether the click really lands awaits one game run (`review/online-ui-art-and-controls-overhaul.md`). Its S5-affected rows (the two panels "still IMGUI", `OnlineUiBlockRectTests` "stays", the scoped blockers "kept") are superseded by S5 — see the manifest row below |
| ui/online-ui-panels-selfcheck.md | UI | current | S5 of the Online UI overhaul: the quick panel and the in-world player context menu are controls of CUO's own uGUI surface (one panel view, the shared row machinery, one namespaced action table), the scoped-rectangle blocking retires with them, the census loses its geometry, and the surface's canvas finally covers the game's; how the panels read and whether the click lands await one game run (`review/online-ui-art-and-controls-overhaul.md`) |
| ui/online-ui-world-overlay-selfcheck.md | UI | current | S6 of the Online UI overhaul: the network readout, the nameplates, the off-screen arrows and the location pings are labels of the game's own font on CUO's canvas (one view, a pooled mark+label pair per marker, the Runtime's geometry asked in the canvas's own units, the arrow glyph from `OffScreenArrowText`), the IMGUI overlay and `LocationPingOverlay` are deleted, and the overlay takes no input; how the markers read and where they land await one game run |
| items/local-item-into-remote-display-selfcheck.md | Items | current | a LOCAL item released onto another player's display proxy is cancelled at the release seam before the native body runs (the container branch used to load it into the proxy, and the clone rebuild then destroyed it); the target reads are the native body's own, the seam member is pinned by the gate census and the L0 port contract, and batch `20261006-e` read the runtime rows on the deployed `0.1.0+6e07b388…` |
| items/display-body-query-seam-selfcheck.md | Items | current | every `Body` query the remote-release redirect answers is answered by SKIPPING the native body, because those bodies index the LOCAL body's state behind the guard the redirect answered (`Body.GetItem` threw `Transform child out of bounds` and lost matrix row 6's swap half, batch `20261006-f`); pinned by `RemoteDragQuerySeamGateTests`, and the runtime re-drive is the next batch's row |

