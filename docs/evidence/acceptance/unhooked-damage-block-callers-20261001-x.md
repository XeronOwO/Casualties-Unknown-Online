# Acceptance record — Host block damage reports: two native `DamageBlock` callers are not hooked

- Ticket: `unhooked-damage-block-callers` — verdict: **stays in `review/`** (rows 1–6 unproven, named below)
- Batch: `20261001-x` — tickets `block-break-first-writer-wins`, `guest-block-mutation-re-report`, `guest-partial-block-damage-re-report`, `block-damage-table-capacity-alignment`, `trap-destruction-drop-quantity-desync`, `entity-destruction-drop-guest-fresh-state-loss`, `guest-break-drops-recovery`, `unhooked-damage-block-callers`
- Commit: `725fe0f4` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+725fe0f4de9887015b1c56a19209f600fbe1ee39`
- Run: 2026-10-01, 20:51–21:35 +08:00 · Host: physical machine (Steam app id 4576510) · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host walks over a `health <= 1` block (footstep crush): the guest hears the same break and receives the damage | machine (+ audible residual) | **unproven** | not driven: the session closed before the crush setup (a low-health cell under a walking body) was staged. The audible half is a residual for the user in any case. |
| 2 | Host's spider burrows through a wall | machine | **open** | no reachable path drives a spider's burrow against a wall on this machine (batch scope, named open). |
| 3 | Guest's own footstep crush, host listens | machine (+ audible residual) | **unproven** | same setup as row 1, not driven. |
| 4 | A remote apply produces no report and no echo | machine | **unproven** | not driven this session; the batch's race rounds did exercise remote applies, but no log query was taken against this row's shape before the run closed. |
| 5 | Third peer, same cadence | machine | **unproven** | not driven. |
| 6 | Report volume: the crush path reports once per crushed cell, the burrow path stays on its cooldown | machine | **unproven** | not driven. |

## Limits

- No row of this ticket was judged in this run; the record exists so the batch's coverage is named, not implied.
