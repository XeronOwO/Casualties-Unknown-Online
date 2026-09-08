# Sync completeness audit: event-level sync + periodic fallback

- Status: Todo
- Priority: High
- Category: Network / sync coverage / audit
- Source: User request (2026-09-07) — "扫描所有涉及同步的游戏特性，是否都做了事件级同步与定时兜底同步？例如我印象中世界中的方块没做定时兜底同步，做一下整个项目的系统性排查"
- Related: `review/world-determinism-world-fingerprint.md`, `review/global-adaptive-report-rate-flow-control.md`, `review/network-traffic-baseline.md`

## Goal

For every synced game feature, produce an evidence-based verdict on whether it has **both**:

1. **Event-level sync** — a dedicated event message at the real trigger (one operation = one message; report only after a verified commit; deep sync chain). And
2. **Periodic fallback sync** — a periodic absolute/idempotent re-send that heals a lost or swallowed event, plus world-entry/late-join backfill and reconnect recovery.

Anything lacking one of the two is either a gap (gets its own ticket) or an explicitly accepted loss-tolerant/transient case with a recorded reason. This turns the `AGENTS.md` rule "Dedicated events, not snapshots: discrete triggers travel as dedicated event messages; periodic streams are only fallback/replay" into a per-domain matrix that can be checked, not a principle that is assumed.

## Scope inventory (must be exhaustive; no "etc." rows)

- **A. Kernel domains** (`docs/architecture/domains.md` domain table): items, players, world entities (opened entities / building health / trap state / trap consumption), enemies, fluids, world/run.
- **B. Direct `NetMsg` families** (`docs/evidence/selfchecks/architecture/phase-e-legacy-inventory-selfcheck.md` §Direct NetMsg classification): session/control; world mutation/presentation (`BlockDamaged`, `WorldBlockState`, `BlockPlaced`, `BlockDamageSnapshot`, `BuildingEntityDamaged`, `BuildingEntityOpened`, `EarthquakeStart`, `KeypadCode`, `GeyserStateSnapshot`, `EntityEvent`, `EntitySpawned`, `DynamiteExplosion`, `RadiationLineState`, `WorldBloodSpawn`, `TrapLayoutSnapshot`, `FluidRegion`, `FluidInteraction`, `FluidPresentation`); character/presentation (`CharacterData`, `HostCharacterData`, `LimbStateEvent`, `CharacterSound`, `CharacterAttackAnim`, `CharacterLandingVisual`, `CharacterRagdoll`, `TutorialClawState`); enemy (`EnemySnapshot`, `EnemyAttack`); trade/chat/speech; Mod API; player-interaction requests; item id/starting inventory.
- **C. GameAdapter/Runtime sync surfaces**: every `*Sync*.cs` / `*Replay*.cs` / channel / service that publishes or applies remote state (`src/CasualtiesUnknownOnline.GameAdapter/**`, `src/CasualtiesUnknownOnline.Runtime/Session/**`).
- **D. Adaptive streams** (`src/CasualtiesUnknownOnline.Runtime/Session/AdaptiveSync/AdaptiveStreamId.cs`): confirm the rate governor only adapts cadence and never removes a fallback, and that every adaptive stream has a terminal/full-state reconciliation.

## Deliverable: the coverage matrix

One row per feature, with these columns:

| Column | Content |
|---|---|
| Feature / domain | Owner service and file |
| Direction + roles | Host→guest / guest→host / both; host, guest, third-party views |
| Event sync | Message/envelope id, trigger, `file:line`, reliable or not |
| Periodic fallback | Cadence, sender, apply semantics (absolute vs additive), `file:line`, whether the governor can stretch it |
| Backfill / recovery | World entry, late join, reconnect, missed-range recovery |
| Loss semantics | Reliable channel? Accepted loss? Convergence guarantee? |
| Verdict | OK / Event-only gap / Fallback-only gap / Transient-by-design (reason) / Unverified |

## Known candidate findings to verify first (do not assume)

1. **World blocks — the user's example.** Host→guest already has a 60 s absolute resend (`WorldEventSync.Update` → `SendBlockStateSnapshot` / `SendBlockDamageSnapshot`, `src/CasualtiesUnknownOnline.GameAdapter/World/WorldEventSync.cs:122-136`; `WorldStateMessageService.cs:299-311`), and the diff table is host-only (`_damagedBlocks`, `:35`, cap `:38`). The guest applies it absolutely (`WorldEventSync.OnRemoteBlockState`, `:455-478`). But the guest reports only at the local `SetBlock` trigger (`OnBlockSet` → `SendBlockPlacedReport`, `:212-215`) and keeps no local diff table: there is no guest→host periodic re-report. So a guest mutation whose report is swallowed (lazy P2P session window, reconnect) stays unknown to the host table, the 60 s host snapshot does not contain that cell and therefore cannot heal it, and the divergence persists silently until a reconnect regenerates the guest's world (losing the local mutation and its drops) or a later host write for that cell overwrites the guest's result. The audit must confirm the loss window, the `BlockPlaced`/`BlockDamaged` interaction when only one of the two arrives, and the reconnect path, then decide: guest diff re-report, host request/repair, or an explicitly accepted loss.
2. **Host→guest families that do have a fallback** (verify cadence, coverage, idempotence): items (10 Hz move + 5 s full table), fluids (10 Hz diff + 1 Hz full + kernel region checkpoint), trader (5 s), world time (5 s), geyser (world entry + 60 s), block state/damage + kernel checkpoint (60 s), radiation line (5 Hz while active), player/enemy/tutorial-claw (20 Hz streams).
3. **Explicit transient-by-design rows** (verify the intent is recorded, not accidental): world blood (`WorldBloodSpawnMsg.cs:13` — "no periodic snapshot is used", 120 s lifetime), one-shot sounds/pings/muzzle-flash/landing visuals, location ping.
4. **Guest→host event-only surfaces**: guest item commands (`KernelEnvelope` spawn/pickup/drop/destroy/use/container), block reports, container transfers, craft batches, medical/shrapnel reports (some ride cumulative adaptive streams with a terminal reconciliation — verify each). For every one: what heals a lost command, and does the next host→guest absolute snapshot converge or silently overwrite the guest's local result?
5. **Directional asymmetry pattern**: most fallbacks are host→guest absolute overwrites. The audit must state the recovery story in both directions per domain and whether a host→guest fallback can silently revert an unhealed guest-side change.
6. **Cross-checks**: `review/world-determinism-world-fingerprint.md` (world fingerprint as an independent divergence detector), `review/global-adaptive-report-rate-*` (cadence adaptation must not delete fallbacks), `review/network-traffic-baseline.md` (cost of the fallbacks).

## Deliverables

- `docs/evidence/sync-coverage-matrix.md` — the full matrix with `file:line` evidence, a verdict per row, and the method used to verify it (code path, test, or runtime log).
- A gap list; every gap becomes an independent `todo/` ticket in the same cycle, each with its own acceptance matrix. No gap may be left unrecorded, and user-visible ones (for example guest block divergence) should be promoted immediately after Stage 3.
- Doc corrections wherever the matrix contradicts `docs/architecture/domains.md`, `docs/features/*.md`, or the phase-E classification.
- A guard against rot: either a normative gate/test that every registered sync domain declares its event+fallback policy, or an explicit delivery-checklist item (decide in Stage 4; a real gate is preferred).

## Staged plan

- **S1 — Freeze the inventory**: enumerate every row from A–D. No row may be dropped without a written reason. Output: the empty matrix with owners.
- **S2 — Evidence collection**: per row, record the event trigger/message, fallback cadence/sender/apply semantics, backfill/reconnect path, reliability, and `file:line`; use runtime logs where code alone cannot prove the cadence or the swallow window.
- **S3 — Verdicts + gap classification**: classify every row; for each "Event-only"/"Fallback-only" row, decide fix vs accepted loss (user decision for user-visible behavior).
- **S4 — Gap tickets + guard + doc updates**: create the independent tickets, land the guard, fix the docs.

Fixing the gaps is outside this ticket's scope, but the audit itself must not change sync behavior; the Stage 4 guard is its only code artifact.

## Acceptance

- Every synced feature from A–D has exactly one row with a verdict; zero "Unverified" rows.
- The world-block case has a concrete verdict backed by evidence (the user's reported suspicion is answered, confirmed or refuted).
- Every gap has an independent ticket; every accepted loss has a recorded reason/decision.
- The matrix is code-verifiable (`file:line`), and the guard prevents a new sync feature from shipping without an event+fallback policy.
- No behavior change is claimed by the audit itself.

## Non-goals

- Sync architecture rewrite, rate/bandwidth tuning (adaptive-rate tickets), anti-cheat, prediction/interpolation.
- Fixing every gap in this ticket; the deliverable is the evidence-backed matrix plus the gap tickets.
