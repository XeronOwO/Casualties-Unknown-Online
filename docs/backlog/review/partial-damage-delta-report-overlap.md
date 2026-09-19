# The partial-damage absolute report and the live delta can overlap

**State note (2026-09-19):** this is the per-sender accounting landing record. The
gap described below is closed: partial damage is accounted PER SENDER on the host,
every arrival carries its sender's cumulative contribution, and only the
difference is applied — so a repeat, a stale frame or a duplicate report is a
no-op, two senders add up instead of taking a per-cell maximum, and a delayed
delta cannot be counted twice.

- Status: Review
- Priority: Low-Medium
- Category: Network / sync coverage / world blocks
- Source: Independent adversarial review of the W2 landing (2026-09-18) — the landing's recorded limitation described only the under-count half
- Related: `review/guest-partial-block-damage-re-report.md` (W2 — the re-report, landed), `review/sync-cadence-review.md`

## Problem (evidence)

The W2 recovery merged a guest's ABSOLUTE re-report into the host's row with a per-cell
maximum: `GameBlockDamageTable.DecideMerge` raised the host's row only when the reported value
is strictly above it, and `Merge` wrote `existing.damage = entry.Damage`. The live relay
stayed a DELTA and the receiving side accumulated it — the game's own
`WorldGeneration.DamageBlock` body does `blockDamage.damage += dmg`
(`reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs`, stable decompiled line). The
two halves therefore disagreed in BOTH directions:

- **Under-count.** Two senders whose contributions were never visible to each other and whose
  reports were both swallowed converged to the higher absolute value, not the sum.
- **Over-count.** A delta that arrived AFTER the absolute re-report covering it had already
  been merged raised the row a second time: the merge landed the reported `D`, then the delayed
  delta added another `D`, so the host's row ended above what either side ever held. `KeepHost`
  could not catch it — the duplicate arrives as a delta, not as a second absolute value.

The over-count needs a DELAYED (not dropped) delta. `PacketSender` defaults to reliable, so
the lazy-P2P swallow window drops frames outright; a retransmitted frame that lands after the
report's answer is what opens the window, and that is the same session phase W2 exists for.

## Goal

A sender's contribution to a cell is counted exactly once on every side, whatever interleaving
of live deltas and absolute re-reports the transport produces, and without changing when a
block breaks.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | One sender: delta, then the absolute re-report | Counted once |
| 2 | One sender: absolute re-report, then the delayed delta | Counted once |
| 3 | Two senders, both interleavings | The sum, never the maximum |
| 4 | Duplicate absolute re-report | Idempotent |
| 5 | A cell that has since broken | Ignored (air) — the block-state channel owns it |
| 6 | The game's 128-entry cap | The live path's own semantics: the game's list evicts its oldest entry (`WorldGeneration.DamageBlock`), which the report path now shares with the live relay; the per-sender ledger's own cap degrades to "every report is new" and is logged once per episode |
| 7 | Third-party view | The same total on every side |

## What landed (2026-09-19)

**Mechanism (design option 1, the per-sender ledger).** Damage is accounted PER SENDER, in the
game's own accumulated units (`BlockDamage.damage`, the metallic ×10 already folded in):

- The guest keeps its OWN contribution per cell — `LocalBlockDamageContributions` (cell → the
  damage this side applied, plus whether the host has answered for it). The value is fed by the
  DamageBlock patch, which now reads the row BEFORE the hit and after it and hands the adapter
  the difference (`WorldGenerationDamageBlockPatch`), so no multiplier rule is duplicated in
  CUO. The value is cumulative for the block's lifetime: an answered cell KEEPS it, because the
  next hit reports cumulative + its increment.
- The live report and the recovery report both carry that cumulative contribution
  (`BlockDamagedMsg.Contribution`; the guest → host report's rows are contributions, not cell
  totals). The host holds one entry per (sender, cell) in `RemoteBlockDamageLedger` and resolves
  every arrival to the difference it has not accounted for: a repeat, a stale frame and a
  duplicate report all resolve to no increment and are neither applied nor relayed, while a
  second sender's entry adds to the row instead of competing with the first (the sum, never the
  maximum).
- The difference is applied through the SAME path a live delta takes — the adapter's
  `world.DamageBlock(cell, increment, …)` with the bonus-metal multiplier off, because the
  increment is already in accumulated units — so a block still breaks exactly when the game
  says it does, and a break caused by a report runs the same state relay and support-loss
  marking as a relayed one. A relay to the other members carries the increment that was applied.

**Wire (protocol 32).** `BlockDamaged` now names the block CELL (X/Y) instead of a world
position — the cell is the key every consumer of the family already used, and the receiving
side no longer re-derives it — and carries the sender's `Contribution`.
`BlockDamageSnapshotMsg` gained `AnswersReport`: the host's answer to one report is sent to the
REPORTER only and clears the cells it names, while the periodic / world-entry snapshot stays
authoritative state that clears nothing. That distinction is the under-count's other half: a
broadcast answer used to clear every other member's outstanding entry for those cells, so the
second sender's contribution was never reported at all.

**Lifetime.** A block write on the cell (a break, a placement, a restored block) forgets the
cell on both sides — the guest's contribution and every ledger entry — because the block those
described is gone and a fresh block at the same cell starts from zero; without it a new block
would inherit the old block's accounting and a contribution below the stale value would resolve
to "already accounted for" and never be applied. A new world/layer baseline clears both tables
(the guest's world-params apply and the host's layer-boundary reset family), and the session
end clears them with the other pending reports. A reconnect-while-in-world deliberately keeps
them: re-reporting is the recovery.

**Structure.** `PendingBlockDamageTable` became `LocalBlockDamageContributions` (the name no
longer described the state), `RemoteBlockDamageLedger` is new (both pure, both unit-tested),
and the report's application moved out of `INativeWorldFacts`: the port no longer carries a
merge, the host's answer reads its own rows through the existing `CaptureBlockDamages`, and the
dead `MergeBlockDamages` / `DecideMerge` / `Merge` family is deleted rather than kept beside the
ledger. `BlockBreakPendingState` and `PendingBreakDropTable` are keyed by the cell alone (the
world position they stored existed only to build the wire message).

## Verification

- **Red (recorded in-session on the pre-fix working tree, before any implementation edit):** the
  new integration suite failed with `two senders' contributions must sum on the host's row, it
  ended at 20` (expected 50), `a delayed delta must not add a second time, the row ended at 40`
  (expected 20) and `both senders must be counted exactly once, the row ended at 30` (expected 50);
  the two guard cases (delta-then-report, duplicate report) passed on the pre-fix tree, as they
  should. That pre-fix tree is an in-session intermediate state and is part of no revision, so the
  numbers cannot be reproduced from the repository — what the tree CAN be re-checked against are
  the assertions themselves (`PartialDamageAccountingTests`) and the green run below, the same
  convention the W2 landing recorded.
- **Green:** `dotnet test CasualtiesUnknownOnline.slnx` (with build) — 3471/3471 in the test
  project and normative gates 69/69 with this cycle's delivery checklist FILLED (the checklist
  gate refuses an open box by design: a run against the reset checklist reports 68/69 with
  `DeliveryChecklist_NoIncompleteRequiredBoxes` as its only failure); the focused families
  (`PartialDamage*`, `GuestBlockDamageReportRecoveryTests`, `BlockBreak*`, `BreakDrop*`) 104/104;
  `dotnet format` clean; build 0 warnings / 0 errors. Two wire round-trip cases for the new
  members (`BlockDamagedMsg.Contribution`, `BlockDamageSnapshotMsg.AnswersReport`) were added by
  the independent review's findings (minor-2) and are part of that run.

### Acceptance matrix coverage

| # | Covered by |
|---|---|
| 1 | `DeltaThenReport_CountsTheContributionOnce` — the delta lands once, and the fallback's report of the same cumulative value changes nothing |
| 2 | `DelayedDelta_AfterTheAbsoluteReport_IsNotAppliedTwice` — the retransmitted delta resolves to no increment after the report that covers it |
| 3 | `TwoSenders_ContributionsAddUpInsteadOfTakingTheMaximum` (two swallowed reports) and `TwoSenders_BothInterleavings_LandTheSameSum` (one sender per interleaving) |
| 4 | `DuplicateAbsoluteReport_IsIdempotent` and `PartialDamageLedgerTests.Resolve_OfARepeatOrAStaleFrame_HasNoIncrement` |
| 5 | `AirWrite_ForgetsTheOutstandingCell` (the contribution dies with the block) and `PartialDamageLedgerTests.Forget_DropsEverySendersEntryForThatCellOnly` |
| 6 | `PartialDamageLedgerTests.Resolve_AtTheCap_IsNotTrackableButStillYieldsTheWholeContribution` — a full LEDGER degrades to "every report is new" (logged once per episode) instead of losing the sender's damage; the GAME's own 128-entry list bound is the live path's own semantics (`DamageBlock` evicts its oldest entry, `WorldGeneration.cs:732-737`), which the report path now shares with the live relay instead of the old CUO-side refusal rule |
| 7 | `ThirdParty_ReceivesTheAppliedIncrement` (the relay carries the applied increment) and `PeriodicSnapshot_DoesNotClearAnotherSendersOutstandingContribution` (state convergence cannot drop an unreported contribution) |

## Known limitations (recorded, not hidden)

- **The adapter's engine-side half is not exercisable in the test host.** The patch's
  applied-increment read, `DamageBlock`'s accumulation and break handling, the crack-sprite
  refresh and the zero-row clearing a snapshot answer can trigger
  (`BlockBreakSync.OnBlockDamageSnapshot` → `BlockDamageCleaner.ClearForAirWrite`) rest on code
  review plus the unified dual-client acceptance pass, exactly as the W2 landing recorded. What
  the automated half proves is the whole Runtime-owned decision: what a report and a delta are
  worth, and what an answer does. The independent review named the sharpest edge of that gap:
  no harness models the GUEST-side row write of a relayed increment either (the tests assert the
  payload the third member receives, not what its game list then holds), so the dual-client pass
  should watch the third member's crack state after a hit it did not make.
- **The ledger's cap is a degradation, not a wall.** At 65 536 tracked (sender, cell) entries a
  new cell is applied without being tracked (logged once per episode), so a repeat of that
  report could count twice until the world is reset. The game's own 128-entry list bound makes
  this unreachable in practice.
- **A raw delta keeps its old semantics.** A sender that cannot account for a cell (its table is
  full) reports no contribution and the host applies its raw damage additively, as before. That
  is a bounded, logged degradation, and it is the only path where the old overlap can still
  happen — for that sender, for that cell.
- **Two local hits inside one round trip are now healed by the ledger, not by the 60 s
  snapshot** (the W2 landing's second recorded limitation). The report is resolved per sender,
  so a second hit's increment is applied even when the first answer already cleared the cell.

## Non-goals

- The W2 recovery itself and its entry lifetime (landed; the lifetime now also drops a cell on
  any block write, which is what the per-sender accounting needs).
- Optional members / relay re-sends (`review/sync-event-and-periodic-fallback-coverage-audit.md`).
- Cadence tuning (`review/sync-cadence-review.md`, landed).
