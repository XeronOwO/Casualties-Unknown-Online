# Acceptance record — The snapshot overtakes the item reports

- Ticket: `snapshot-overtakes-item-reports` — verdict: **pass, moved to `done/`**. The member's bag-child
  wear, the hand swap and the `SwapSlots` swap over occupied slots all reported their item fact BEFORE the
  immediate character snapshot, both viewers applied the fact, and **zero** `[CharSync] divergence` lines
  appear for the moved items (or for the bag whose contents moved) in any of the three windows. The
  pre-fix pair batch `20261025-b` read — two `[CharSync] divergence` lines 1–3 ms before
  `[CarriedSync] applied` — does not reproduce.
- Batch: `20261025-c` — this ticket only. The sibling the review filed
  (`review/craft-carried-fact-retirement.md`) needs a craft fixture and is not in this batch.
- Commit: `1401eb24` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+1401eb2489177de12e2d41bf8777fa52e3dca78d` (`prelude-verify-deploy.txt`; `tools/verify-deploy.ps1`
  exit 0).
- Run: 2026-10-20 22:34 → 22:37 local · Host: physical machine · Guest: sandbox `<sandbox-guest>` · Third
  client: sandbox `<sandbox-alt>`.
- Dependencies: the eleven the preflight reported present (`preflight.log`: `RESULT: OK - a full
  two-client run is possible; the alternate third client is configured`).
- Artifacts: `20261025-c/` under the directory named by `acceptance-artifacts-dir` — the prelude reads
  (`prelude.log`, `session.json`, `prelude-verify-deploy.txt`, `preflight.*`), the fixture reads
  (`fixture-*`, `row*-guest-local.json`, `row*-host-table.json`, `row1-alt-clone.json`), the gesture
  answers (`row1-guest-wear.json`, `row2-guest-switch.json`, `row3-guest-swap.json`), the per-window log
  excerpts (`ev-row1-wear-*`, `ev-row2-switch-*`, `ev-row3-swap-*`) and the judgement (`judgement.txt`).

## The fixture

The member's body started with the `emergencylight` starting supply (slot 3); the run created a `trashbag`
(slot 0), `dogfood` (slot 1), `bandage` (slot 2) and `autopump` (slot 4) through `item-provide mode=create`
and loaded the autopump into the bag through `container-fill` (`fixture-*`). The baseline reads: the bag
holds the autopump on the owner's tree, on the host's kernel table (`contained: 22377858709<-9492956821`)
and on both viewers' proxy trees (`fixture-host-table.json`, `fixture-host-clone.json`,
`fixture-alt-clone.json`).

## The rows

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The owner wears the bag child (`Body.WearWearable`): the item lands on the limb, the bag reads one child fewer on every client, and every viewer's window over at least one 1 Hz cycle carries ZERO `[CharSync] divergence` for the item or the bag | machine | **pass** | `row1-guest-wear.json`: `applied: true`, `wasContained: true`, `parentBefore: trashbag`, `parentAfter: UpTorso`; `row1-guest-local.json`: the bag's `children: []`, the autopump at slot -1; `row1-host-table.json`: `contained 0`, `terminal 0`, the autopump carried; `row1-alt-clone.json`: the autopump top-level under the clone, `orphanCount 0`; `ev-row1-wear-*`: the owner's `[SlotMoved] autopump (id 22377858709) → slot -3 (Wear) reported.` at 22:36:01.881, `[CloneRender] local body changed — immediate re-report.` at .882, the host applies the fact at .905/.907 and the third client at .918/.920, and **no divergence line on any client** (`judgement.txt`: moved-divergence 0, any-divergence 0) |
| 2 | The owner's hand swap (`Body.SwitchHands`) over two occupied slots: both items land in each other's slot, and every viewer's window over at least one 1 Hz cycle carries ZERO divergence for them | machine | **pass** | `row2-guest-switch.json`: `kind: switch`, `applied: true`; `row2-guest-local.json`: dogfood slot 0 / trashbag slot 1; `row2-host-table.json`: `contained 0`, `terminal 0`; `ev-row2-switch-*`: the owner reports both slot facts at 22:36:10.835, the host applies both at .841/.842 and the third client at .874/.875, no divergence anywhere (`judgement.txt`: 0/0) |
| 3 | The owner's `Body.SwapSlots` swap over two occupied ring slots: both items land in each other's slot, and every viewer's window over at least one 1 Hz cycle carries ZERO divergence for them | machine | **pass** | `row3-guest-swap.json`: `slot1 2 / slot2 3`, `bandage` / `emergencylight`, `applied: true`; `row3-guest-local.json`: emergencylight slot 2 / bandage slot 3; `row3-host-table.json`: `contained 0`, `terminal 0`; `ev-row3-swap-*`: the owner reports both slot facts at 22:36:18.093, the host applies both at .112/.113 and the third client at .121/.123, no divergence anywhere (`judgement.txt`: 0/0) |
| 4 | Each moved item's `[CarriedSync] applied …` line is present in the same window (so the row cannot pass by the event never arriving) | machine | **pass** | `judgement.txt`: the autopump owner-reported 1 / host-applied 2 / alt-applied 2 (the nested replacement plus the top-level move are two applications of the one fact); dogfood and trashbag 1/1/1 each; bandage and emergencylight 1/1/1 each |

## The chain the run read

The owner's postfix ran the fact first and the snapshot second: `[SlotMoved] … reported` is followed on the
owner's log within a millisecond by `[CloneRender] local body changed — immediate re-report`, and the
viewers apply the fact before the next snapshot — the host's `[CarriedSync] applied` lines land at
.841/.842 and .112/.113, the third client's at .874/.875 and .121/.123, and the periodic 1 Hz snapshots
that follow carry no divergence. For the wear the fact is applied twice (the nested replacement, then the
top-level move), both from the one event.

## Limits

- One world, one run, one repetition per gesture; the zero-divergence windows are 4 s each (four or more
  1 Hz snapshots), which is the periodic cycle of the mechanism that can emit the warning, not a
  statistical claim.
- The host-owner direction was not driven: the same patch code runs on both roles (the role only chooses
  `SendItemCarriedSync` vs `SendItemSlot`), and the batch that filed the ticket (`20261025-b`) had the
  member as the owner. A host-owner repetition stays useful.
- The craft-scope sibling the review found is not part of this row; it is
  `docs/backlog/review/craft-carried-fact-retirement.md`.
- The row driver's first summary counted the bag as a moved item and flagged the wear row for the bag's
  missing applied line — a container whose contents moved owns no applied line. `judgement.txt` re-reads
  the same windows against the moved child (keeping the bag as a divergence watch), and the rule is now
  in `docs/acceptance/workflow.md` §6.
