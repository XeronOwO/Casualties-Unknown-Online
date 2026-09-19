# Guest break drops are lost when the break report is swallowed

- Status: Review
- Priority: Medium
- Category: Network / sync coverage / items (guest-created drops)
- Source: `review/guest-block-mutation-re-report.md` (W1) implementation — the block state now converges when a break's air write is lost, but the drops it carries did not
- Related: `review/guest-command-loss-reconciliation.md` (the item keyframe's in-flight reconciliation gap), `todo/carried-inventory-registration-re-report.md`

## Problem (evidence)

A guest's block break travels as two messages: the air write (`BlockPlaced`, the
first-writer proof) and, one frame later, one `BlockDamaged` carrying the break
plus every block/building drop
(`src/CasualtiesUnknownOnline.GameAdapter/World/BlockBreakSync.cs`
`FlushPendingBlockBreak`). The guest registers its own drops only on the
host/solo path (`if (_session.Role != SessionRole.Guest)`), so the host learns
them exclusively from that message:

- If the `BlockDamaged` is swallowed (the ~30 s lazy-P2P window swallows every
  send), the host never registers the drops and the peers never materialize
  them; the guest keeps a locally-created item the host's item table does not
  know about. The item keyframe cannot heal it: the drops were never registered
  anywhere, so there is no kernel fact to reconcile from — worse, the periodic
  keyframe's reconcile would have KILLED the breaker's own copy (the 5-30 s
  keyframe is far inside any re-report window) had the copy not been protected.
- If only the air write is swallowed, the drops-carrying report arrives while
  the host's block is still standing and was applied as damage only; the W1 fix
  recorded and relayed the resulting air transition, but the drops in that
  message were ignored (`OnRemoteDamageBrokeBlock` logged the loss).

## Landed (2026-09-18)

**Mechanism (the ticket's design option 1, adapted to the existing wire).** No
new `NetMsg` and no new protobuf member: the recovery reuses the `BlockDamaged`
family, exactly as W1 reused `BlockPlaced`.

- **Guest side — record before send.** `FlushPendingBlockBreak` now calls
  `ReportBreakDrops` for a guest BEFORE the live send, recording the break's
  cell, its reported position and both drop families in
  `PendingBreakDropTable` (`PendingBlockReportTable`'s third sibling; cap
  65 536, a second break at the same cell APPENDS — its drops are real items).
  `GuestBreakDropReportBookkeeping` owns the table and the
  one-per-overflow-episode log latch, `GuestReportRecovery` owns the send and
  answer halves, and the third `PendingReportFallback` window re-reports every
  outstanding set as one `BlockDamaged` per break (same 60 s cadence, same
  pump: `GuestReportFallbacks` keeps the three channels' windows together, which
  is also what kept `WorldService` inside the 600-line aggregate gate).
- **Host side — an idempotent verdict.** `BlockBreakArbitration.TryAccept` now
  returns a `Verdict` instead of a bool: `Fresh` (the sender's applied air-write
  record — consumed, first-writer-wins unchanged), `Repeat` (this sender's break
  for this cell was already accepted — the acknowledgement relay was the lost
  message — so re-register and re-relay idempotently instead of refusing), and
  `Refused` (anything else, including a report naming a cell the host still
  holds: that shape looks attributable but is not, see the review note below).
  The accepted record is purged on its own longer window (90 s) than the
  unconsumed air-write records (3 s), and a repeat refreshes it.
- **Acknowledgement.** The relay now goes to EVERY member including the
  reporter (`BroadcastBlockDamaged(0, …)`), mirroring W1's accepted-report
  relay: the reporter's echo is what clears its pending drop report. The
  duplicate guard is the drop's ITEM ID, never the cell alone — the receiving
  registration (`RegisterWorldItemIfAbsent`, the adapter's `SpawnWorldItem`
  guard) was already idempotent per item id, so a re-relay cannot
  double-materialize.
- **The breaker's own copy survives.** `DropProtectionGuard` now also consults
  the pending drop report (`IWorldControl.IsBreakDropPending`), so the item
  keyframe's reconcile leaves a locally-created drop alone until the host has
  answered it. Without this the 5-30 s keyframe would destroy the drop long
  before the 60 s fallback could carry it.
- **Refusals converge.** A lost first-writer race still rolls the loser's drops
  back (`ItemReject`), and the adapter now also forgets the refused item from
  the pending set, so a break is never re-reported forever for an item that no
  longer exists.

**Wire / protocol: no bump (deliberate).** `ProtocolVersion.Current` stays 22.
The message shape is untouched — no new `NetMsg`, no new `ProtoMember`, and no
existing field's meaning changes — and the mixed-version behavior is benign in
both directions: a v22 guest simply never re-reports its drops (the fix does not
apply to it) and a v22 peer receiving the accepted relay for a break it already
has hits the same per-item-id idempotency guards. This is the same call
`review/block-damage-table-capacity-alignment.md` recorded for a change that
reused an existing message.

**Independent adversarial review — TWO rounds, fresh contexts, all findings fixed in this same cycle.**

Round 1 found two defects and several smaller items, all fixed in-cycle: an
empty-drop break could park a permanent, unanswerable entry (it is reachable —
the game's drop roll has `Random.value < 0.5f` branches); the host's arbitration
records were never cleared at a world boundary; the verdict and the attribution
commit were one call, so a report that did not actually break the block still
attributed the cell; a repeat did not refresh the attribution window; the docs
claimed a `GetBlock` check can *never* decide first-writer; and the adapter
forgot a refused drop from inside a scope that could return early.

Round 2 attacked those fixes and **rejected the first attempt at the
world-boundary fix, correctly**: `Reset()` alone did not close the defect,
because the accepting branch it had added (`StandingBlock`) needed no record at
all — so after a descent a stale report of the previous layer's break took that
verdict and `DamageBlock` broke a freshly generated block with the report's REAL
damage (only the fallback's re-send uses zero). The reset had turned a harmless
repeat into a lethal accept. The branch was therefore REMOVED, not patched: a
break report naming a cell the host still holds is refused, which is the
pre-change behaviour and the only conservative answer while no message carries a
generation identity to attribute across a layer boundary. The consequence is
recorded as a limitation below rather than claimed as recovered.

Round 2 also found the empty-drop guard's regression test did not exercise the
guard (it lives in a Unity method the suite does not run), so that test now pins
the host-side PREMISE — that a payload-free report is never relayed, hence
unanswerable — and says so instead of implying coverage; the replay double was
relaying a fresh break with the reporter excluded, unlike production, and was
corrected.

**Known limitations (recorded, not hidden).**

- The adapter's half is not exercisable in the test host: the recording call
  inside `FlushPendingBlockBreak`, the world→cell conversion, the scene
  materialization of a relayed drop, the `ItemReject`-driven `ForgetBreakDrop`
  call, and the `DropProtectionGuard` query are Unity-side. Those rest on code
  review plus the unified dual-client acceptance pass (the same boundary W1/W2
  recorded). The test doubles substitute `(int)pos.X` for the real conversion,
  so a conversion error is outside their reach by construction.
- The acknowledgement is the relay echo, so a break whose relay is lost is
  re-reported once per window (bounded, idempotent) — not a per-item ack, which
  would need a wire member.
- **A break report that arrives while the host's block still stands is refused.**
  That is the case where the guest's air-write report was lost along with the
  drops report: the drops are rejected and destroyed on the breaker, and the
  block state converges through the W1 air-write channel instead. Accepting it
  was tried and reverted — without a generation identity on the wire, a stale
  report of a previous layer's break is indistinguishable from a legitimate one,
  and accepting it breaks a block in the new layer. **Closed since** by the wire
  member carrying the world/layer identity: see
  `review/world-layer-generation-identity.md` (the same-generation report is now
  accepted as `Verdict.LostAirWrite`, and a stale one is refused before the
  verdict).
- The empty-drop recording guard lives in `BlockBreakSync.FlushPendingBlockBreak`
  (Unity-side): the suite pins the premise that makes it necessary (a
  payload-free report is never relayed, so an entry for it could never be
  answered) and the guard itself rests on code review, like the rest of the
  adapter half.
- The accepted-record window bounds attributability: past it (90 s since that
  break's last report) a re-report is refused like any unattributed break and the
  breaker's local drop is destroyed. That is the same recovery bound the rest of
  the family has, and it is now refreshed by each repeat.
- `PendingBreakDropTable.IsPending` scans the entries' drop lists; it is called
  once per item the keyframe reconcile would otherwise kill (5-30 s cadence).
  Unstated rather than measured: no realistic entry/drop count was constructed
  that makes the linear scan matter.

**Evidence.** `docs/evidence/sync-coverage-matrix.md` row W1 extended with the
drop half (35 anchors). Tests: `GuestBreakDropRecoveryTests` (12, the acceptance
matrix below), `PendingBreakDropTableTests` (8, the table contract including the
cap and the per-item acknowledgement rule), `BlockBreakArbitrationTests` (16,
the three verdicts, both purge windows and the layer-boundary reset),
`BlockBreakSimulationTests` reworked for the repeat/re-relay semantics. Whole
solution 3 254 + 56 gates green; `dotnet format` clean; build 0 warnings /
0 errors. The first review's two findings landed BEFORE the commit, in the same
cycle, as the workflow requires.

### Acceptance matrix coverage

| # | Covered by |
|---|---|
| 1 | `SwallowedBreakReport_TheFallbackReReportIsWhatRegistersTheDrop` (only the drops report is lost — nothing inline can register them, so the fallback's re-report is provably the carrier; the host's table then holds the drop exactly once) |
| 2 | `LostAirWrite_WithoutAGenerationStamp_IsRefused_AndTheRefusalReachesTheBreaker (the unverified shape; a same-generation report is now accepted — see review/world-layer-generation-identity.md)` (a report naming a cell the host still holds is refused — the conservative answer, since the wire carries no generation identity to attribute it; the refusal reaches the breaker and stops the re-report) |
| 2b | `DropsFreeBreak_TheHostNeverAnswersIt_WhichIsWhyTheAdapterDoesNotRecordIt` (pins the host-side premise: a payload-free report is never relayed, so a recorded entry for it could never be answered) |
| 3 | `LostDropsReport_IsReReported_AndAnsweredByTheRelayEcho` (only the drops report is lost; the next window carries it and the relay echo clears the entry) |
| 4 | `DuplicateReReport_RegistersAndMaterializesExactlyOncePerDrop` (the idempotent repeat: one table entry, one relay per accepted report, no second materialization) |
| 5 | `AnotherSendersBreakOfTheSameCell_RefusesTheLosersDrops` (+ `BlockBreakSimulationTests.TwoGuestsBreakSameCellAtTheSameTime_FirstWriterWins_LoserRejected`) |
| 6 | `ThirdParty_SeesTheSameDropIdentities` (the relay carries the same item ids to every member) |
| 7 | `WorldReset_DropsThePreviousWorldsPendingReports` + `SessionEnd_ClearsThePendingDropReports` (a new world/layer baseline and the session end both clear the table, so no stale re-report; the host's side of the boundary is `BlockBreakArbitrationTests.Reset_DropsEveryRecord_SoAStaleReportCannotClaimTheNewLayer`) |

Supporting: `PendingDrop_IsQueryableUntilTheHostAnswers` (the protection
surface) and `HostRole_NeverRecordsAPendingDropReport` (the guest-only gate).
