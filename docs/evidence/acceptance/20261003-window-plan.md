# Unified acceptance window — batch plan (2026-10-03)

The `review/` backlog is now the whole waiting set, so the next acceptance cycle is planned as one
window instead of one batch. This page is that window's plan: every ticket waiting in
`docs/backlog/review/` is assigned to exactly one batch, the batches are ordered, and the boundary of
each batch is stated. It is written before any batch runs (workflow §1/§3).

The plan is deliberately about batch boundaries, not row detail: each batch gets its own scope page
with the row-level classification (`machine` / `visual` / `feel` / `blocked`) and the exact staging
order before it starts, because a row's setup depends on the world that batch builds. What is frozen
here is the grouping and the order, so a batch's plan cannot be reverse-engineered from its result.

## 1. Scope

- **70 tickets** in `docs/backlog/review/` at this revision (the folder holds 70 `.md` files plus
  `.gitkeep`); `todo/` and `in-progress/` are empty.
- A batch is bounded by the world it needs: same world, same save, same participant count → one
  batch; a change of world, save or player count splits batches (workflow §1).
- One record per ticket (`docs/evidence/acceptance/<slug>-<batch>.md`), the ticket transition and
  the index move in the same change (workflow §7/§8), and each batch folds its lessons back
  (workflow §10).
- A ticket that states no acceptance rows has them written first and recorded; the known case is
  `trade-domain-dual-side-runtime` (§4).
- A missing dependency leaves its rows `blocked`, keeps the ticket in `review/`, and is asked about
  once per run — never replaced by a weaker check (workflow §2).

## 2. Batch plan

The 70 tickets, grouped and ordered. Ticket counts per batch: 4 + 12 + 5 + 4 + 11 + 8 + 9 + 7 + 10
= 70.

| Batch | Tickets | Shared setup | Notes |
|---|---|---|---|
| `20261003-e` | `recipe-unlock-fallback`, `trade-domain-dual-side-runtime`, `guest-report-fallback-first-resend`, `sync-cadence-review` | one world, host + guest; inbound blackout; 60 s cycle observation | First: the craft and trade drive recipes are already proven live, and the two fallback/cadence tickets share the same blackout + cycle read |
| `20261003-f` | `guest-partial-block-damage-re-report`, `partial-damage-delta-report-overlap`, `guest-break-drops-recovery`, `guest-command-loss-reconciliation`, `entity-destruction-drop-guest-fresh-state-loss`, `runtime-entity-markerless-bind-absorption`, `trap-layout-entry-snapshot-staleness`, `trap-layout-snapshot-recovery`, `turret-stray-fire-after-reload`, `trap-action-divergence-hardening`, `unhooked-damage-block-callers`, `session-control-convergence` | one world, host + guest; block-set / break / drop / entity fixtures; blackout windows | The largest family: the block/drop/entity recovery set, all reachable in an ordinary run |
| `20261003-g` | `guest-remote-pose-head-orientation-desync`, `host-entity-hit-red-flash-not-visible-on-guest`, `guest-background-ghost-item-ground-sounds`, `remote-interaction-local-gating`, `enemy-hit-determination-local` | host + guest + alternate client; third-party view | Add the alternate sandbox: every row here is judged partly from the non-participant view |
| `20261003-h` | `sync-player-pain-vocalizations-and-bark`, `suppressed-native-call-sounds-stay-unheard`, `unhooked-item-and-body-sound-families`, `treatment-gore-presentation-carried` | host + guest; forced states (limb pain, death), Debug log level, window capture | Audio evidence comes from the Debug channel and the forced-state recipes already recorded in the lessons |
| `20261003-i` | `remote-medical-stage-1-injection-session`, `remote-medical-stage-2-shrapnel-multiplayer`, `remote-medical-stage-3-other-actions`, `remote-medical-native-minigame-parity`, `remote-medical-panel-acceptance-issues`, `remote-medical-panel-hide-local-only-actions`, `remote-player-medical-panel`, `remote-medical-treatment-operations`, `remote-fentanyl-injection-and-medical-panel-desync`, `remote-context-menu-medical-visible-when-target-not-visible`, `concurrent-medical-operations` | host + guest (+ alternate for the concurrency rows); wounded-body fixture, native medical panel captures | The whole medical family shares the same fixtures; `concurrent-medical-operations` may need the third client for its third-party rows |
| `20261003-j` | `save-mid-run-consistent-cut`, `save-system-mid-run-and-layer-end`, `save-multiplayer-restore-and-backups`, `save-restore-account-surface`, `save-guest-restore-claim-and-legacy-store-retirement`, `save-layer-end-save-and-restore`, `save-native-character-field-parity`, `save-native-run-field-parity` | save/restore world cycle: cut → Continue → new session; archive inspection | The save family is its own world cycle; the earlier save batches' staging order is the template |
| `20261003-k` | `online-ui-layout-and-input-detail-pass`, `online-ui-art-and-controls-overhaul`, `tab-backpack-open-close-immediately`, `dead-player-right-click-name-suffix`, `middle-click-location-marker`, `in-game-command-console-interactive`, `command-console-esc-not-intercepted`, `pinyin-search-mod`, `pinyin-search-standalone-mod` | mostly single client; window-level captures; in-process UI driving | Panel rows are one-client work; the console/search rows judge live UI state and the rendered rects, not source pins |
| `20261003-l` | `steam-transport-send-limit-runaway`, `guest-frame-rate-lower-than-host`, `global-adaptive-report-rate-flow-control`, `global-adaptive-report-rate-stage-1-global-governor`, `global-adaptive-report-rate-stage-2-traffic-bandwidth`, `global-adaptive-report-rate-stage-3-cumulative-streams`, `global-adaptive-report-rate-stage-4-high-frequency-domains` | dense-load generation; bandwidth/rate measurements; bounded refusal windows | The adaptive-rate family shares one measurement harness; the send-limit ticket carries a known staging difficulty (§4) |
| `20261003-m` | `bilingual-human-docs`, `plugin-host-shell`, `adapter-capability-catalog`, `global-projection-framework`, `unified-remote-display-projection-rework`, `cucorelib-migration-support`, `mod-native-binding-handshake-parity`, `remote-inventory-native-parity-rework`, `id-system-namespaced-ids`, `mod-command-request-timeout` | offline/repository evidence where a row is a document, a contract or a suite; live session only where a row needs one | The tickets whose rows are decided by the tree, not by a game session; each row still names its own evidence |

## 3. Order and cross-batch notes

- The order front-loads what the proven recipes reach (`e`), then the largest ordinary-world family
  (`f`), the third-peer family (`g`) and the audio/medical/save families that need their own
  fixtures (`h`–`j`), then the single-client and measurement work (`k`–`l`), and finally the
  repository-evidence batch (`m`).
- A batch may hand a ticket to a later batch when its own world cannot stage a row; the record names
  the move, and the ticket stays in `review/` until its batch. The grouping is the plan; the
  per-batch scope is where a row's own setup is frozen.
- `20261003-f` and `20261003-g` could share one three-client session; they are split because the
  third client costs a launch and `f` needs none of its rows. A batch that finds it must run both
  in one session may merge them and say so in both records.
- Three tickets (`online-ui-art-and-controls-overhaul`, `online-ui-layout-and-input-detail-pass`,
  `global-adaptive-report-rate-*`) are stage families: a batch judges the rows the tickets
  actually carry today, not the roadmap — a stage the ticket declares as future stays out.

## 4. Known blockers and risks

- **`trade-domain-dual-side-runtime` carries no acceptance rows** (an 8-line stub: "Dual-side
  runtime pass for the trade domain", reference backlog #59/#93). Batch `e` writes the row table
  from the landed trade behaviour and the drive recipes before it runs, and records it with the
  result.
- **`steam-transport-send-limit-runaway` row 5** needs a peer blocked at the transport level while
  its session stays connected; the 2026-10-01 lessons record that stopping the application's drain
  is not that state. If the state cannot be produced, the row is `unproven` and the ticket stays
  in `review/` with the gap named.
- **Mod-environment rows** (`cucorelib-migration-support`, `pinyin-search-standalone-mod`,
  `mod-native-binding-handshake-parity`) depend on the mod-host fixtures; where a row needs a
  fixture the repository does not have, the row is `blocked` and the ticket stays in `review/`.
- **Reconnect rows**: the 2026-09-30/10-01 lessons record the intermittent rejoin wedge (a lobby
  rejoin that does not re-activate the session). A row that needs a late joiner uses the host's
  world-entry fan-out, not a bare rejoin, unless the run first proves the rejoin activated.
- The window is long; each batch closes on its own (record, transition, lessons, gates, commit)
  before the next starts, so a stopped window never leaves half-moved tickets.

## 5. What each batch does

Per batch, in order: preflight and `session-environment -Mode status` (workflow §2), write the
batch's scope page with its row classification and staging order, run the workflow §4 chain (build,
deploy, verify identity), run the session, write one record per ticket, transition the tickets and
their index rows, fold the lessons, and run the gates before the commit.

## 6. First batch (`20261003-e`) — what must be true before it starts

- The craft and trade drive recipes are in the tree and proven live (batch `20261003-d`); the
  blackout recipe is proven by earlier batches.
- `recipe-unlock-fallback` needs: both clients in one world; a blueprint use whose one-shot report
  is swallowed; the 60 s repair cycle; a late joiner; the crafting list read open on the receiving
  side (rows 1–5), the host without the recipe (row 6), and the item-facts half read as the
  unchanged path (row 7).
- `trade-domain-dual-side-runtime` needs its row table first (§4).
- `guest-report-fallback-first-resend` and `sync-cadence-review` reuse the same session's blackout
  windows and cycle reads; their rows are classified in the batch scope page.
