# Batch 20261024-q — the rhythm verdict at two intelligence levels

- Ticket: `docs/backlog/done/remote-medical-cpr.md` (stage 4's intelligence rule)
- Artifact: `0.1.0+10807c23`, deployed and verified against `<game-dir>` before the run
- Session: host + one member; host forced into arrest; the member opened the chart through `medical-open` and pressed the
  compressions control through `cpr-press`; the member's INT set with `body-force intelligence=…`; the member's own log read
  from its sandbox tree.

## The readings

| operator INT | press interval | verdict logged |
|---|---|---|
| 15 | 1.00 s | `TooSlow` |
| 15 | 0.01 s (same frame) | `TooFast` |
| 5 | 1.90 s | `TooSlow` |
| 5 | 0.01 s (same frame) | `TooFast` |

Both misses are fed back to both operators, which is the reference's own split: its `stupid` branch suppresses exactly the
`cpr_minigame_good` line and swaps the guide text, while the miss feedback stays. The clear operator also keeps the full
guide sentence on the control and the low-intelligence one keeps the bare word (`medical.cpr_guide` vs
`medical.cpr_guide_simple`).

## The residual this batch names

A `Good` verdict cannot be produced by the tooling: one driver call is ≥1.0 s between presses (past the 0.92 s slow edge)
and same-frame presses are 0.01 s (below the 0.27 s fast edge). The affirmation's visibility below INT 12 is therefore
pinned by unit cases only (`CprRhythmFeedbackTests`), and the ticket carries it as a named residual rather than a claim.
