# Acceptance record — Block-damage tables: CUO's registry and the game's own list disagree about capacity and eviction

- Ticket: `block-damage-table-capacity-alignment` — verdict: **stays in `review/`** (rows 1–3 unproven, named below)
- Batch: `20261001-x` — tickets `block-break-first-writer-wins`, `guest-block-mutation-re-report`, `guest-partial-block-damage-re-report`, `block-damage-table-capacity-alignment`, `trap-destruction-drop-quantity-desync`, `entity-destruction-drop-guest-fresh-state-loss`, `guest-break-drops-recovery`, `unhooked-damage-block-callers`
- Commit: `725fe0f4` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+725fe0f4de9887015b1c56a19209f600fbe1ee39`
- Run: 2026-10-01, 20:51–21:35 +08:00 · Host: physical machine (Steam app id 4576510) · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A long run with more damaged cells than the cap: host, every guest and a late joiner hold the SAME damaged-cell set | machine | **unproven** | not driven: the setup needs more than 128 live damaged cells plus a late-joining client; the session closed before that fill was staged. |
| 2 | Eviction: a cell leaving the table leaves it on every side | machine | **unproven** | same setup as row 1; not driven. |
| 3 | Unhooked writers: damage written by the callers CUO does not hook reaches the shared view, or the gap is named | machine | **unproven** | not driven this session; the live half is the footstep-crush observation owned by `unhooked-damage-block-callers`, which this run also did not reach. |

## Limits

- The ticket's own verification limits already say the crack-render and support-loss halves need the dual-client pass; this run contributes no live half to any of its rows.
