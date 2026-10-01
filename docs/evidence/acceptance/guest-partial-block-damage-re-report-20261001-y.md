# Acceptance record — Guest partial block damage has no re-report

- Ticket: `guest-partial-block-damage-re-report` — verdict: **stays in `review/`** (row 7 unproven; rows 1–6 open)
- Batch: `20261001-y` — tickets `block-damage-table-capacity-alignment`, `guest-partial-block-damage-re-report`, `unhooked-damage-block-callers`, `trap-destruction-drop-quantity-desync`, `entity-destruction-drop-guest-fresh-state-loss`, `guest-block-mutation-re-report`
- Commit: `4b28a64d` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+4b28a64ddbdbb9068379db2f534f9ede0f472c00`
- Run: 2026-10-01, 22:12–22:26 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts: `y-fill-guest.json`, `y-census-guestfill-*.json`, `y-pump-host-log.txt`, `y-census-after-*.json`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 7 | Game-side `blockDamages` cap (128): the boundary surfaces as a named answer/refusal, never a silent overflow | machine | **unproven** | the guest's fill left 140 outstanding cells (`y-fill-guest.json`) and the 60 s pump did deliver the absolute report — host log `Partial block-damage report received from 76561199526807662 (140 cells).` at 22:24:16.618, 59.9 s after the fill returned — but NO cell was refused (`y-pump-host-log.txt`; a whole-log scan for `RefuseCap|RefuseRange|RefuseAir` and for the refusal text finds zero lines). The host already held those cells from the live deltas, so every merge was a no-op; the row's scenario (a report the host cannot take) was not produced. |
| 1, 2, 3, 4, 6 | swallowed report / two guests one dropped report / duplicate absolute re-report / break-before-re-report / outstanding-damage re-entry | machine | **open** | swallowed-report family: no message-swallow injection on this machine (the batch scope's capability boundary, unchanged). |
| 5 | The block is restored to full HP (layer regeneration / baseline): no stale damage re-applied | machine | **open** | its scenario needs a pending re-report before the baseline move, which needs the swallow. |

## Limits

- The pump half of the mechanism is now observed live: one 60 s cycle carried the whole 140-cell
  outstanding set in ONE message, which is the "one operation stays one message" claim the ticket makes.
- A refusal needs a cell the host's full table cannot take while the live delta has not already delivered
  it; producing that state needs either the swallow injection or a table filled through a path the host
  never sees — neither is available on this machine today.
