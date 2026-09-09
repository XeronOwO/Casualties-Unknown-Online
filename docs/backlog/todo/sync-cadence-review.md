# Sync cadence review: fallback stretch limits and first-resend latency

- Status: Todo
- Priority: Medium
- Category: Network / sync coverage / cadence tuning
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` cadence findings; user request 2026-09-09 — "如果有觉得同步时长不合理的，也可以提出来")
- Related: `review/global-adaptive-report-rate-stage-1-global-governor.md`, `review/global-adaptive-report-rate-stage-4-high-frequency-domains.md`, `review/network-traffic-baseline.md`, `todo/guest-block-mutation-re-report.md`

## Findings (evidence)

The adaptive governor only changes cadence (verified in row R5), but several
stretch caps and resend latencies look longer than the divergence they are meant
to heal. Each item below needs a measured A/B before a decision; if the current
value is accepted, record the acceptance in the matrix row.

1. **World-item keyframe: 5 s base → 30 s max.**
   `src/CasualtiesUnknownOnline.Runtime/Session/AdaptiveSync/AdaptiveStreamCatalog.cs:77-87`
   (`BaseIntervalMs: 5000`, `MaxIntervalMs: 30_000`); the policy stretches it under
   pressure (`AdaptiveRatePolicy.cs:78`). The keyframe is the only absolute heal
   for top-level item state (condition / liquids / components) and for
   `ItemReconcile`'s removal of phantom items. 30 s of divergence is
   user-visible (a drained/filled bottle, a broken tool). Proposal: cap at
   ~10 s or exempt the keyframe from stretching.
2. **Trader-state fallback: 5 s base → 30 s max.**
   `AdaptiveStreamCatalog.cs:108-118`; the send is reliable
   (`TradeStateSync.cs:93`). The stale `NetMsg` comment ("5 s fallback",
   "unreliable") was corrected in the audit cycle (`NetMsg.cs:61` now states the
   reliable adaptive cadence). Trader interactions broadcast immediately, so the
   fallback only heals a swallowed event; 30 s is probably acceptable but should
   be measured against a purchase/stock divergence scenario. Proposal: cap at
   ~15 s.
3. **Fluid full-viewport reconciliation: 1 s base → 10 s max.**
   `AdaptiveStreamCatalog.cs:97-107`. The 10 Hz diff stream covers changes; the
   full viewport is the fallback. 10 s is probably fine, but the ticket should
   record the measured worst-case stale cell after an anchor move.
4. **World block / damage / keypad / geyser resend: a single hardcoded 60 s.**
   `src/CasualtiesUnknownOnline.GameAdapter/World/WorldEventSync.cs:122-135`.
   The documented lazy-P2P swallow window is up to ~30 s after world entry
   (`WorldEventSync.cs:106`), so a mutation in that window can stay invisible for
   up to 60 s. Proposal: send one resend shortly after the InWorld edge (e.g.
   5–10 s) and keep 60 s steady-state; this does not change the bandwidth
   baseline materially (one extra small snapshot per join).
5. **`WorldSnapshotComplete` / late-join readiness** — tracked in
   `todo/session-control-convergence.md`.
6. **Enemy snapshot / attack** — tracked in
   `todo/enemy-snapshot-and-attack-recovery.md`.

## Goal

Every stretch cap and resend latency in the matrix is either measured and
accepted (recorded in the matrix row) or tightened with an A/B measurement, with
no regression against `docs/evidence/test-parallelization.md`-style measured
evidence and the network traffic baseline.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Pressure-simulated peer | Governor still only stretches cadence, never drops a fallback |
| 2 | Item keyframe under max pressure | Worst-case top-level divergence measured; cap decided and recorded |
| 3 | Trader fallback under max pressure | Worst-case stock divergence measured; cap decided and recorded |
| 4 | Fluid full viewport under max pressure | Worst-case stale viewport cell measured; cap decided and recorded |
| 5 | Host mutation during the P2P swallow window | Guest converges within the chosen first-resend latency |
| 6 | Bandwidth baseline | No regression beyond the recorded baseline |

## Non-goals

- Rewriting the adaptive governor.
- Adding new streams or messages (those belong to the gap tickets above).
