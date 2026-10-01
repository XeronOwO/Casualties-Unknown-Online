# Acceptance record — Trap destruction drops desync in item quantity between host and guest

- Ticket: `trap-destruction-drop-quantity-desync` — verdict: **stays in `review/`** (row 1 passes; rows 2–3 unproven, named below)
- Batch: `20261001-x` — tickets `block-break-first-writer-wins`, `guest-block-mutation-re-report`, `guest-partial-block-damage-re-report`, `block-damage-table-capacity-alignment`, `trap-destruction-drop-quantity-desync`, `entity-destruction-drop-guest-fresh-state-loss`, `guest-break-drops-recovery`, `unhooked-damage-block-callers`
- Commit: `725fe0f4` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+725fe0f4de9887015b1c56a19209f600fbe1ee39`
- Run: 2026-10-01, 20:51–21:35 +08:00 · Host: physical machine (Steam app id 4576510) · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts: `s2-host-trap-census.json`, `s2-guest-trap-census.json`, `s2-pad-around-host.json`, `s2-approach-*.json`, `s2-host-items-before.json`, `s2-guest-items-before.json`, `s2-host-break-support.json`, `s2-host-items-after.json`, `s2-guest-items-after.json`, `s2-host-log-after-break.txt`, `s2-guest-baseline.png`, `s2-guest-after-break.png`, `s2-alt-after-break.png`, `s2-guest-breakB.json`, `s2-host-watch.json`, `s2-fresh-round.json`

The ticket states its expectations in prose; the run writes the rows first from the symptom and the landing record, then judges them.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host destroys the support block under a jump-pad: both ends hold the SAME drop set immediately — same count, same item identities | machine | **pass** | the pad at cell (407,668) with support (407,667): the host broke the support block at 21:15:21 (attrition: a live census found 111 jump pads and the map probe chose this one). Host log: `op=39 item=1211501758403 … [BuildingDropCaptured]`, `op=40 item=1215796725699 … [BuildingDropCaptured]`, `op=38 … FlushPendingBlockBreak Committed(0+2)` — the pad's own two building drops, zero block drops (`s2-host-log-after-break.txt`). The two clients' independent censuses (each on its own process, seconds apart) list the same nine items, and the two NEW ids — 1215796725699 and 1211501758403 — are identical on both ends with matching positions (`s2-host-items-after.json`, `s2-guest-items-after.json`; the other seven ids and their positions also match). |
| 2 | The guest's view catches up immediately, not through the periodic keyframe | machine | **unproven** | the confirming census was taken ≈20 s after the break — inside the item keyframe's 5–30 s band — so this run cannot separate "delivered by the break message" from "delivered by the periodic keyframe". |
| 3 | The drops carry their fresh initial state | machine | **unproven** | the two drops of this pad read `fresh:false` on both clients, with `vx=0.001` / `av=-0.156` (an initial spin IS present) — `s2-host-items-after.json`, `s2-guest-items-after.json`. Three attempts to catch the flag inside its short window failed and none of them observed a new item at all: a same-eval read with a 40 × 100 ms poll on the breaker (`s2-guest-breakB.json`), the observer's 4 s watch, whose window had already closed before the break it waited for (`s2-host-watch.json`), and the last round's 4 s watch (`s2-fresh-round.json`, which answers `no-new-item` and lists two unrelated items — a `purse` and `droppings`). The flag was never observed as `true`. |

## Limits

- A second pad (cell 512,761) was broken later in the same session, but by then the host had performed a leave→continue recovery: the world's item set had changed (the pre-recovery drops were gone; one new item stood in the area, identical on all three clients). That sample is recorded as an observation only, and pre/post item sets are never compared here.
- The drop COMPOSITION is not this ticket's row: the run observed two `scrapmetal` drops and zero block drops for its own pad, while the ticket's original symptom report names a different set (one circuit board and two metal scraps) for the save it was reported against. Row 1 judges that the two peers agree on count and identity, which is what it states.
- One session is not a race proof; row 1 is one pad's destruction read on both clients once.
