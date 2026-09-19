# Guest partial block damage has no re-report

**State note (2026-09-18):** this is the W2 landing record. The gap described below is
closed: the guest keeps each reported cell's ABSOLUTE damage, the 60 s fallback re-reports
the outstanding set, and the host merges per cell and answers every reported cell
authoritatively — a zero answer clears a row this host's own cap/range rules refused.

- Status: Review
- Priority: Medium
- Category: Network / sync coverage / world blocks
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row W2); split out of `review/guest-block-mutation-re-report.md` when the W1 half landed
- Related: `review/guest-block-mutation-re-report.md` (W1 — the terminal block state, landed), `review/sync-cadence-review.md`

## Problem (evidence)

A guest's partial-damage report is one-shot, and the authoritative record plus the
absolute fallback are host → guest only:

- The live relay is `BlockDamaged` (bidirectional) but delta-based: a surviving
  block is reported immediately with the raw damage
  (`src/CasualtiesUnknownOnline.GameAdapter/World/BlockBreakSync.cs:107`
  `_world.SendBlockDamaged(new NetVector2(pos.x, pos.y), dmg, bonusMetal, null, null);`).
- The authoritative table is host-only AND is the GAME's own
  `WorldGeneration.world.blockDamages` list, read at send time — the CUO registry
  that used to hold a copy was deleted, see
  `review/block-damage-table-capacity-alignment.md`:
  `src/CasualtiesUnknownOnline.Runtime/Session/World/BlockDamageSnapshotSender.cs:36`
  (`if (_session.Role != SessionRole.Host)`), so a swallowed guest report never
  enters it.
- The absolute fallback is `BlockDamageSnapshot` (89), `PacketHandler`
  direction `HostToGuest` (`src/CasualtiesUnknownOnline.Runtime/Session/World/BlockDamageSnapshotSender.cs:53`),
  sent at world entry and on the 60 s cycle
  (`src/CasualtiesUnknownOnline.GameAdapter/World/WorldEventSync.cs:129`).
- Consequence: the host's table has no entry for that cell, so its snapshot
  omits it and cannot correct either side; the host's block keeps its higher
  remaining HP and the guest's crack sprite is local-only. The divergence
  persists until the break, whose terminal block state is now healed by W1
  (the air-write re-report also clears the crack via `OnBlockAirWrite`).

## Goal

A swallowed guest partial-damage report converges: the host's accumulated
damage for that cell reaches the guest's, and the host's snapshot then heals
every peer, without resurrecting damage on a block that has since broken or
been restored.

## Design direction (decide at implementation)

The live relay is additive (`world.DamageBlock(cell, dmg, …)`), so a re-report
cannot simply replay the delta — it would double-apply. The re-report must be
**absolute**, and the merge must not lose another player's concurrent damage:

1. **Absolute re-report + max-merge** — the guest keeps its per-cell absolute
   damage table and re-reports it (a `BlockDamageSnapshot`-shaped message in the
   guest → host direction). The host applies `max(host, reported)` per cell
   instead of an overwrite, so two players hitting the same block both count.
2. **Cumulative per-sender delta** — the guest re-reports the delta accumulated
   since its last acknowledged report, keyed by (guest, cell) on the host so a
   duplicate is idempotent (the medical cumulative-stream pattern).
3. **Accepted loss** — rejected: the divergence is user-visible (crack state and
   the next hit's break threshold differ per side).

Either way the apply must stay bounded by the game's 128-entry `blockDamages`
list, skip air cells and out-of-range values (the existing guards), and never
break the block (a break is the block-state channel's semantic).

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest damages a block; the report is dropped | The host's accumulated damage converges to the guest's; a later hit breaks on both sides |
| 2 | Two guests damage the same block; one report is dropped | The host keeps both contributions (no lost damage, no double count) |
| 3 | Duplicate delivery of the same absolute re-report | Idempotent — the host's damage is unchanged |
| 4 | The block breaks before the re-report lands | The re-report is ignored (air cell); no crack resurrects |
| 5 | The block is restored to full HP (layer regeneration / baseline) | No stale damage is re-applied |
| 6 | World entry / reconnect while a cell has outstanding damage | The host snapshot and the guest table agree |
| 7 | Game-side `blockDamages` cap (128) | The re-report respects the cap and logs the overflow |

## Non-goals

- The terminal break state (W1 — landed).
- Anti-cheat / strict validation of guest damage reports.
- Host-side cadence tuning (`review/sync-cadence-review.md`, landed).

## Landed (2026-09-18)

**Mechanism (design option 1).** The recovery unit is the guest's *unacknowledged partial
damage*, not a copy of the whole table: `PendingBlockDamageTable` (cell → absolute damage,
cap 65 536, upsert on a later hit) is fed by the adapter **before** the live delta report
goes out (`BlockBreakSync.OnBlockDamaged` reads the cell's accumulated
`BlockDamage.damage` and calls `IWorldControl.ReportBlockDamage`; that record-before-send
order is adapter-side — a Unity type the test host cannot construct — so it rests on code
review plus the dual-client pass, like the rest of this half), and the existing 60 s
`WorldReportFallbackPump` drives `GuestReportRecovery.ResendDamages` — ONE
`BlockDamageReport` per cycle carrying the whole outstanding set, so one operation stays one
message. The live relay stays a DELTA (`BlockDamaged` 40): replaying it would double-apply.

**Host side.** `HandleBlockDamageReport` merges the report through the native port
(`INativeWorldFacts.MergeBlockDamages` → `GameBlockDamageTable.Merge`), per cell and never
below what this host already holds (`DecideMerge`: `Raise` / `KeepHost` plus the same
`RefuseAir` / `RefuseRange` / `RefuseCap` boundary the snapshot apply uses), refreshes the
crack sprite of every raised row, then broadcasts this host's authoritative value for EVERY
reported cell through the existing `BlockDamageSnapshot` (89) — the reporter included, which
is its acknowledgement, and a third party's copy converges with it. A zero row means "this
host holds no damage here": the receiver clears its local crack through the same seam an air
write uses (`BlockDamageCleaner.ClearForAirWrite`), so a row the game's own 128-entry cap
refused converges instead of re-reporting forever.

**Entry lifetime.** An entry is dropped when the host answers for its cell (any
`BlockDamageSnapshot` that names it), when the cell goes air
(`IWorldControl.ForgetPendingBlockDamage`, called from the air-write path on both roles), or
when a new world/layer baseline is applied (`ResetPendingBlockDamageReports`, wired into the
guest's `WorldParamsService.Apply` beside the W1 reset). A reconnect-while-in-world
deliberately keeps the table: re-reporting is the recovery.

**Structure.** The 600-line gate forced a split while this landed. The file is 575 lines at
HEAD; the W1+W2 additions took it to a measured **713** in the working tree — an in-session
line count of the pre-split intermediate state, which was never committed and therefore
cannot be reproduced from any revision (the reviewer correctly flagged that the number is
unverifiable from a frozen tree; the two states that CAN be checked are 575 at HEAD and 579
after the split). The re-send/answer wire logic moved into `GuestReportRecovery` (the
recovery STATE stays in the two bookkeeping collaborators). The final file is 579 lines: its
only remaining logic is the role-guarded answer hand-off in
`FireBlockDamageSnapshotReceived`, and everything else forwards.

**Verification.**

- Red (recorded in-session): the integration test failed on the code before the recovery
  half landed — `Assert.Contains() Failure: Filter not matched in Collection: []` (the
  host's damage table stayed empty through the swallowed report and the whole fallback
  window).
- Green: `GuestBlockDamageReportRecoveryTests` (9) + `PendingBlockDamageTableTests` (6) +
  `GameBlockDamageTableTests` (6, including the new `DecideMerge` rules) +
  `GuestBlockReportRecoveryTests` (6, the W1 neighbour) = 27/27; normative gates 44/44;
  build 0 warnings / 0 errors; `dotnet format` clean.

### Acceptance matrix coverage

| # | Covered by |
|---|---|
| 1 | `SwallowedGuestPartialDamageReport_ConvergesOnTheFallbackCycle` — the host's table takes the guest's absolute value after the swallowed report. The live path itself is the adapter's (`BlockBreakSync.OnBlockDamaged`), code-reviewed plus the dual-client pass |
| 2 | `HostsAnswer_ClearsThePendingReport` + the fake port's never-lower contract: the host keeps every contribution it holds and the answer carries the merged value (see the recorded limitation for the double-loss window) |
| 3 | `DecideMerge_NeverLowersTheHostsOwnDamageAndKeepsTheSameBoundary`'s `KeepHost` rows plus the fake port's never-lower branch pin idempotency (a duplicate report cannot change a row); `HostsAnswer_ClearsThePendingReport`'s second window adds that a cleared entry is not re-sent |
| 4 | `AirWrite_ForgetsThePendingCell` (once the cell is air its damage belongs to the block-state channel) |
| 5 | `WorldReset_DropsThePendingReports` (a new world/layer baseline clears the table; nothing of the old world is merged) |
| 6 | `ThirdParty_ReceivesTheHostsAuthoritativeValue` (the broadcast reaches every member) and `NoNativeReader_LeavesTheReportOutstandingInsteadOfInventingAnAnswer` (no table means no invented answer) |
| 7 | `DecideMerge`'s `RefuseCap` row pins the rule, and `RefusedReport_IsAnsweredWithZeroAndClearsThePendingReport` pins the answer shape (an explicit zero) and the clear. A real 128-entry list driven to an actual refusal needs the live game and belongs to the dual-client pass |

**Known limitations (recorded, not hidden).**

- The merge is a per-cell MAXIMUM against a live DELTA the receiver accumulates, so the two
  halves can disagree in BOTH directions. **Under-count**: two senders whose contributions
  were never visible to each other and whose reports were both swallowed converge to the
  higher value rather than the sum. **Over-count** (found by the independent adversarial
  review, not by this landing): a delta that arrives AFTER the absolute re-report covering it
  has been merged raises the row a second time, so the host's row can end above what either
  side ever held — `KeepHost` cannot see it, because the duplicate arrives as a delta rather
  than as a second absolute value. Both need per-sender accounting (a (sender, cell) ledger
  whose cell total is re-derived, or sequenced deltas) and are tracked in
  `todo/partial-damage-delta-report-overlap.md`; the recovery itself stays bounded and
  self-healing, which is why W2 closes as `OK` with this recorded.
- Two local hits at the SAME cell inside one round trip share the W1 shape: the first answer
  clears the cell's entry, so a swallowed second report falls back to the host's 60 s
  snapshot.
- The adapter's engine-side path is not exercisable in the test host: the merge's world reads
  and crack-sprite refresh, the live `DamageBlock` hook and the guest's absolute read, AND the
  zero-row clearing a snapshot answer can now trigger (`BlockBreakSync.OnBlockDamageSnapshot`
  → `BlockDamageCleaner.ClearForAirWrite`: reflection over `BlockDamage.spr` plus
  `Object.Destroy` — destructive local state removal that until this change only the air-write
  path performed). All of it rests on the pure `DecideMerge` rules plus the unified
  dual-client acceptance pass.
