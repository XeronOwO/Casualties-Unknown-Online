# Acceptance record — a break log line now names the shape it judged

- Ticket: `docs/backlog/done/block-break-log-names-the-wrong-shape.md` — **CLOSED**: the wording is read on
  the machine, and the ticket moves to `done/` with this record.
- Batch: `20261017-d`, run `fresh3` — two clients (host on the physical machine, member in the `Steam1`
  sandbox), the member sending a drops-carrying break whose air write lands first. The script, its log and
  the JSON answers are in the batch directory (`run.ps1`, `fresh3.log`, `fresh3-host-break-lines.txt`).
- Commit: `04970242` — Deployed artifact: `0.1.0+04970242ba678db6c84d632195f556e2a300aff5`, verified against
  this tree by `tools/verify-deploy.ps1` (exit 0). The break probe is committed and read from the tree by the
  driver, so it is not part of the deployed artifact.
- Run: 2026-10-17, 02:23:40 → 02:23:58 · Host: physical machine · Member: `Steam1` sandbox.

| # | Row | Verdict | Evidence |
|---|---|---|---|
| 1 | The headline names the shape the judgement actually reached | **pass** | The host's own window: `[INF] [BlockBreakSync] [BlockBreak] <member>'s remote damage broke the block at (0,0) — its air write had already landed here (the fresh first-writer claim); the block-state difference was recorded and the break relayed with its claim.` The judgement was `Fresh` in the same window (`… break at (0,0) accepted — 0 block drop(s) + 0 building drop(s) registered + relayed.`) |
| 2 | It no longer contradicts the applier's own line, which is what the red consisted of | **pass** | The same window, one line earlier: `[DBG] [BlockBreakSync] a relayed break at (0,0) found the cell already air — written without a second presentation.` The red window (batch `20261016-c`'s `c14`/`c16`) carried the OLD headline directly under that Debug line, saying the opposite; the two now agree |
| 3 | The old sentence is gone from the product | **pass** | No `without an air-write report` in the window. It survives only in recorded evidence of earlier runs (and in `docs/evidence/acceptance/guest-block-mutation-re-report-20261002-o.md`, a frozen record of a run that DID emit it) — those are captures, not live text |

## Limits

- **The run reached its cell by luck, and the batch says so rather than hiding it**: the script probes eight
  offsets around the member for a cell that HOLDS a block, every one answered `block=0`, and it then sent the
  forged break to the ABSOLUTE cell (0,0) — which happened to hold block 14. The reading is valid (that
  cell's block stood, the air write landed, the drops report followed, the verdict was `Fresh`), but a cell is
  found by asking the HOST for its own block state, not by probing offsets on the member. The next batch that
  needs one should do that.
- **What this window does not show**: the other two verdicts' wordings on a machine (`LostAirWrite` and
  `Repeat`). They are read from `BlockBreakArbitration`'s own records and from the red window's repeat pair,
  not re-run here — the ticket's red covers the `Fresh` and `Repeat` cases, and this run covers `Fresh` with
  the new wording.
- **The unchanged tail's own limit is named, not fixed**: "the block-state difference was recorded" is false
  when `WorldStateMessageService.ReportBlockState` meets its 65,536-cell table full, which needs that call to
  answer whether it recorded. It is a Runtime change and is carried by the ticket's closing note rather than
  silently left.
