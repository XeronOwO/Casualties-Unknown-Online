# Acceptance — the give gesture and the receiver's placement answer close

- Tickets: `docs/backlog/done/give-item-to-another-player.md` (the push direction) and
  `docs/backlog/done/placement-outcome-never-returns.md` (the receiver's placement answer)
- Artifacts judged: the 2026-10-22 batches (`20261022-a`, `-f`, `-g`, `-h`), plus the user's 2026-10-23
  ruling for the row no hand can produce
- Date of this close: 2026-10-24
- Result: **every row of both tickets has a verdict** (machine readings plus one user ruling); the two
  tickets close together because the give gesture's residual ledger issue is exactly the sibling's subject.

## The give ticket's rows

| # | Row | Verdict and record |
|---|---|---|
| 1, 2, 5, 7, 8, 9, 10 | the drag/centre/range/policy/cue rows | read in batches `20261022-f`/`-g` — `give-item-to-another-player-20261022-f.md`, `-20261022-g.md` |
| 3, 4 | a give onto a ring slot and into a receiver's container | read in batch `20261022-h` — `give-item-to-another-player-20261022-h.md` (the receiver's own container rule refused the toolbox against its plasticbag, the slot fallback landed, the item was still there 20 s later, and the host kept its id out of the terminal set) |
| 6 | the receiver's body/container refuses | the user's 2026-10-23 ruling: the behaviour test plus the wired return chain is its reading (the 60 ms window cannot be produced by hand); record `placement-outcome-never-returns-20261023-ruling.md` |

## The placement ticket's rows

Its own status: rows 1–5 were read on the machine in batch `20261022-h`
(`placement-outcome-never-returns-20261022-h.md`) and row 6 was judged by the same user ruling. The
answer-first half (the `PlayerPlacementQuery`/`PlayerPlacementAnswer` wire pair, the host's pending
question, the receiving client's own resolver, the answered-destination recording and its refusal path)
landed in `e3cdc117` with `PlacementAnswerTests` and the sync-coverage/census rows.

## What this close does NOT claim

- No new machine run on the 2026-10-24 artifact (`0.1.0+7b02e9d3`): the give/placement rows are pointer
  drags and frame captures, and this agent shell has no interactive desktop (`SetForegroundWindow` on the
  clients' windows leaves `GetForegroundWindow` at 0). The close rests on the 2026-10-22 machine records and
  the user's ruling, with the full suite green on the current artifact as the regression guard.
- The layout/feel of the cue labels is a user-judgement item where the earlier records already say so.

## Follow-ups that stayed filed

- `docs/backlog/todo/placement-failure-report-anchor.md` — the not-applied arm's census row remains
  unbounded; the applied arm is bounded by `NetMsg.PlayerPlacementOutcome`.
