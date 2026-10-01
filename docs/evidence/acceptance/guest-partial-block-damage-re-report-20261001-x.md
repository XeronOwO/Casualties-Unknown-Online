# Acceptance record — Guest partial block damage has no re-report

- Ticket: `guest-partial-block-damage-re-report` — verdict: **stays in `review/`** (row 7 unproven; rows 1–4, 5 and 6 open, named below)
- Batch: `20261001-x` — tickets `block-break-first-writer-wins`, `guest-block-mutation-re-report`, `guest-partial-block-damage-re-report`, `block-damage-table-capacity-alignment`, `trap-destruction-drop-quantity-desync`, `entity-destruction-drop-guest-fresh-state-loss`, `guest-break-drops-recovery`, `unhooked-damage-block-callers`
- Commit: `725fe0f4` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+725fe0f4de9887015b1c56a19209f600fbe1ee39`
- Run: 2026-10-01, 20:51–21:35 +08:00 · Host: physical machine (Steam app id 4576510) · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 7 | Game-side `blockDamages` cap (128): the boundary surfaces as a named answer/refusal, never a silent overflow | machine | **unproven** | not driven: filling the game's 128-entry list past its cap needs a 128-cell damaged-set setup this session did not run before it closed. |
| 1, 2, 3, 4, 6 | swallowed damage report / two guests one dropped report / duplicate absolute re-report / break-before-re-report / outstanding-damage re-entry | machine | **open** | swallowed-report family: no message-swallow injection on this machine (batch scope, capability boundary). Named open. |
| 5 | The block is restored to full HP (layer regeneration / baseline): no stale damage re-applied | machine | **open** | the row's scenario needs a PENDING damage re-report (a swallowed report) before the baseline move; without the swallow injection the state it judges cannot be produced. |

## Limits

- No row of this ticket was judged in this run; the record exists so the batch's coverage is named, not implied.
