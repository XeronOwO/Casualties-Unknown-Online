# Guest pending-report fallback: flat 60 s first resend

- Status: Review
- Priority: Low-Medium
- Category: Network / sync coverage / world blocks
- Source: Sync cadence review 2026-09-19 (`review/sync-cadence-review.md`, finding 4 — the same 60 s first-resend family, opposite direction)
- Related: `review/guest-block-mutation-re-report.md` (W1 — the guest→host report half), `review/guest-command-loss-reconciliation.md` (the item-command family's 5 s × 12 window), `review/session-control-convergence.md` (the guest's own entry window), `review/sync-cadence-review.md` (the host→guest half of the same finding)

## Problem (evidence)

`PendingReportFallback` (`src/CasualtiesUnknownOnline.Runtime/Session/World/PendingReportFallback.cs`)
is the shared cadence for the guest's unacknowledged reports — the world-block report table
(row W1) and the runtime-entity creation table (row E3). Its window arms on the FIRST outstanding
entry and then re-sends once per `IntervalMs = 60_000`, flat: there is no dense phase.

The live report is sent immediately at the trigger, so the fallback only heals a swallowed one —
but the documented lazy-P2P swallow window is up to ~30 s after world entry, and a guest mutation
made inside it (a broken block, its drops, a runtime-created entity) stays invisible on the host
until the 60 s mark. The host→guest direction of the same family was tightened on 2026-09-19
(`review/sync-cadence-review.md`: the entry repair answers a still-open readiness window inside the
guest's own 5 s window); this direction was measured there and left unchanged.

The sibling item-command family already converges this way: `GuestCommandReconciliation` re-sends
every unacknowledged item report in a bounded 5 s × 12 window (`review/guest-command-loss-reconciliation.md`).

## Goal

A guest report swallowed during the entry swallow window converges on the host well inside that
window (the family's order of magnitude: one 5 s step), while the steady cost stays at one
re-report a minute.

## Acceptance matrix (results)

| # | Scenario | Expected | Result |
|---|---|---|---|
| 1 | Guest breaks a block inside the swallow window | Host converges within the chosen first-resend latency | OK — `GuestBlockReportRecoveryTests.SwallowedReportInsideTheEntryWindow_ConvergesOnTheEntryStep`: a report swallowed 29 s into the window is re-reported 5 s later, the host adopts the break, and its echo clears the entry |
| 2 | Guest creates a runtime entity inside the swallow window | Host converges within the same latency | OK — `GuestEntityReportRecoveryTests.SwallowedCreationReportInsideTheEntryWindow_ConvergesOnTheEntryStep`: the same 29 s + 5 s scenario on the creation half |
| 3 | Report acknowledged before the window | No duplicate re-send | OK — arming never re-sends, so the live report is not duplicated, and a set the host answers before a step elapses pays nothing (`PendingReportFallbackTests.DrainedSet_DisarmsAndPaysNothing`) |
| 4 | Steady state (long after entry) | Unchanged: one re-report a minute per outstanding set | OK — `PendingReportFallbackTests.SteadyPhase_WithoutAWorldEntry_ReSendsOncePerMinute` (this is the pre-existing cadence, unchanged) and the family counter-example `GuestBlockReportRecoveryTests.SwallowedReportAfterTheEntryWindow_IsOnTheSteadyStep` |
| 5 | Clock wrap | Window re-bases instead of stalling (the existing test) | OK — `PendingReportFallbackTests.BackwardsClock_ReArmsTheWindowAndRetiresTheStaleEntryAnchor`: the window re-bases on the new reading AND the stale entry anchor is retired on the first frame that reads backwards, so the dense step cannot come back with it. The family-level wrap case already existed (`GuestBlockReportRecoveryTests.BackwardsClock_ReArmsInsteadOfStallingTheFallback`) |
| 6 | Bandwidth baseline | No regression beyond the recorded baseline | OK — the steady cadence is untouched; the entry phase adds at most 11 dense re-sends of an outstanding set per entry phase (the twelfth deadline lands exactly on the phase boundary, where the steady step governs), measured at 5 bytes for the one-cell report frame (≤ 55 bytes across the entry minute). `docs/evidence/sync-cadence-measurements.md` |

## What landed

`PendingReportFallback` now has TWO phases, and the guest's own world entry is the edge between them:

- **Entry phase** — the guest's InWorld report (`ISessionControl.LocalSceneReported`; the same edge
  `SessionControlConvergence` already arms its readiness window on, and no new message) opens a 60 s
  phase in which an outstanding set is re-sent every **5 s** (`DenseIntervalMs`): 12 × 5 s, the entry
  budget the readiness window uses.
- **Steady phase** — `IntervalMs` (60 s), unchanged: it still governs everything a clean entry
  produces and every set that first becomes outstanding after the phase.

The phase is anchored by `Pump` on the frame after the edge (the class reads no clock of its own, so
the anchor is the clock it is handed) and is bounded by that anchor rather than by an answer: a set
that first becomes outstanding at the END of the documented ~30 s swallow window is still re-sent 5 s
later. A set that first becomes outstanding in the last seconds of the phase is ~30 s past the
swallow window — its live send is delivered, and the steady step heals a lost one exactly as it did
before this phase existed. That boundary is DECLARED and pinned by
`PendingReportFallbackTests.SetArmedAtTheEndOfTheEntryPhase_IsOnTheSteadyStep`, not left implied.
Leaving the world (`InMenu`) closes the phase, `Reset()` (session end, new world/layer baseline)
clears it, and an anchor the clock reads backwards past (an `Environment.TickCount` wrap) is retired,
so a wrap can at most fall back to the steady step.

The ticket's "one policy, two owners" was stale against the tree and is corrected here: the class is
shared by FIVE windows under FOUR owners — `GuestReportFallbacks` (world blocks W1, partial damage
W2, break drops W1's drop half), `RuntimeEntityChannel` (runtime entity creations E3) and
`CraftSyncService` (the recipe-unlock set I6). All five take the phase from the one policy; no owner
carries cadence logic of its own.

Records moved with the change: matrix rows W1/W2/E3/I6 (their guest→host cadence sentences), the
cadence-findings decision list (finding 7), `docs/evidence/sync-coverage-evidence.json` (976 → 982
entries; W1 47 → 52, E3 71 → 72), `docs/evidence/sync-cadence-measurements.md` (the two-phase table
and the measured frame size), `docs/decisions/active.md` entry 190, and the source comments that
called this cadence a flat 60 s (`RuntimeEntityChannel`, `CraftSyncService`, `BlockBreakArbitration`,
`DropProtectionGuard`, `RuntimeEntitySnapshotMsg`, `RuntimeEntityRejectedMsg`, `ProtocolVersion`).

## Verification

- Red recorded against HEAD's implementation (`git show HEAD:src/.../PendingReportFallback.cs > ...`):
  the three policy cases that need the entry step (`EntryPhase_ReSendsOnTheFiveSecondStep` and
  `ReportInsideTheSwallowWindow_GetsItsFirstReSendFiveSecondsLater` pin the 5 s step,
  `DrainedSet_DisarmsAndPaysNothing` re-arms inside the phase) and the two family convergence
  cases fail — 5 failed / 30 passed of 35.
- Green with the change: 35/35 —
  `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~PendingReportFallback|FullyQualifiedName~GuestBlockReportRecovery|FullyQualifiedName~GuestEntityReportRecovery"`.
- Normative gates and the full suite: see `## Verification numbers` below (independently
  reproduced in review round 2).
- Independent adversarial review: see the review section below.

## Verification numbers

- Focused run (`dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~PendingReportFallback|FullyQualifiedName~GuestBlockReportRecovery|FullyQualifiedName~GuestEntityReportRecovery"`):
  **35 passed / 0 failed** with the change; **30 passed / 5 failed** with HEAD's
  `PendingReportFallback.cs` restored (`git show HEAD:<path> > <path>`), which is the recorded red.
- Normative gates: **69/69** (`dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests`),
  including the sync-coverage gate's per-row anchor counts and quote checks against the JSON's 982
  entries.
- Full suite with build: **3448 passed / 0 failed** (`dotnet test CasualtiesUnknownOnline.slnx`) plus
  the gates project's 69. `dotnet build`: 0 warnings / 0 errors; `dotnet format`: exit 0.
- Independent adversarial review: two rounds on the frozen tree before the commit — see the
  section below; the findings and their fixes land in the same commit.

## Independent adversarial review (2026-09-19, fresh context, FULL tier, read-only, frozen tree)

**Round 1 was static only**: the reviewer's environment had no command runner, so it could not
re-run any suite and reported that as its blocker (unverified numbers) instead of pretending to have
reproduced them. Its findings and their disposition:

- **MAJOR-1 — refuted with a negative sample.** "`BackwardsClock_ReArmsTheWindowAndRetiresTheStaleEntryAnchor`
  does not pin the retirement it is named for: its assertions hold whether or not the guard runs."
  Disabling the guard's `_entryOpen = false` in a scratch run makes exactly that case fail
  (`Expected: 1, Actual: 2`) — the stale anchor reads as "the future" after the wrap, so the dense
  step fires at +5 s. The case does cover the branch; the falsification is recorded in the case's own
  comment so a later reader does not have to re-derive it.
- **MINOR-1 — fixed.** "≤ 12 re-sends" over-counted: the twelfth deadline lands exactly on
  `DenseWindowMs` (`60 000 < 60 000` is false), so an outstanding set gets at most **11** dense steps,
  and ≤ 55 bytes for the one-cell frame. Corrected here, in the measurement table, in the matrix's
  decision 7 and in decision 190.
- **MINOR-2 — fixed.** The matrix rows read as if every affected channel had its own entry-window
  test; only W1 and E3 do. Decision 7 now states that W2 and I6 inherit the phase through the shared
  policy and are covered by the policy cases (this ticket's *Declared limits* already said so).
- **NIT-1 — fixed.** The checklist's evidence said "11 source comments"; the diff touches 10 source
  files (8 Runtime, 2 GameAdapter).
- **NIT-2 — refuted.** The evidence JSON's declared `count` IS asserted: `SyncCoverageGateTests` fails
  with "evidence file declares count=… but carries … entries", so 982 is gate-backed, not grep-only.
- **NIT-3 — fixed.** The red record now says which three policy cases need the entry step.
- **NIT-4 — fixed.** The class comment presented the reconnect re-anchor as unconditional; it is
  guarded by `RunCoordinator.OnSessionActivated`'s in-world-with-a-live-local-body check.

What the reviewer verified statically (and could not falsify): the two-phase arithmetic and the
boundary case, "12 x 5 s is the readiness window's entry budget", the documented ~30 s swallow window,
that the edge is raised before the send and stamped on a latch, the reconnect re-report path, five
windows under four DI singletons with no unsubscribe hazard, no wire change, and the reference
integrity of the index / folder / JSON counts.

**Round 2 (same tier, runner-capable, frozen tree) reproduced all three numbers independently on
this tree — focused 35/35, gates 69/69, full suite 3448 + 69, each WITH build — and hash-verified
that no reviewed file changed while it ran. It confirmed both refutations (MAJOR-1 by code-path
derivation, NIT-2 by citing the gate's own count check and its negative sample) and both fixes.
Three further items it raised were fixed in the same commit: a stale "60 s re-report" line in
`review/generation-identity-remaining-families.md` (the E3 cadence this change replaced), the
checklist's own line count (the class is 165 lines, not the 164 written before the review edits),
and this ticket's forward references. It also recorded a pre-existing debt it did NOT attribute to
this change: a few hand-written `file:line` citations in the evidence matrix have drifted from the
tree (rows W5 and P2), which that document's own "stale historical evidence" convention covers.**

## Non-goals

- Changing what a report carries, or the host's arbitration of it.
- A per-cell cadence (the window stays per outstanding set).

## Declared limits

- **The entry phase is the entry's, not the set's.** A set that first becomes outstanding in the
  last seconds of the phase gets the steady step (pinned above). Justification: at that point the
  live send is ~30 s past the documented swallow window, so the hazard this phase exists for is
  gone; the failure mode is the pre-existing one (heal within a minute), not a regression.
- **The dense phase is proven in simulation, not on the wire.** The 5 s/60 s arithmetic, the edge,
  the anchor and the wrap are pinned by `PendingReportFallbackTests`; the family convergence and the
  5-byte frame are driven through the simulation world. Real transport timing — the ~30 s swallow
  window and the real 5 s / 60 s rhythms — is the user's dual-client acceptance, not something this
  suite can prove.
- **The frame size is the sender's frame, not the wire.** 5 bytes is what `PacketSender` hands the
  transport for a one-cell report in the simulation world (no world/layer generation stamp committed
  there); Steam's own framing is not measured by this suite.
- **The recipe-unlock set (I6) and the partial-damage row (W2) inherit the phase unchanged.** They
  share the policy, so their entry-window behavior moves with it; neither has a dedicated
  entry-window test of its own beyond the shared policy cases.

## Notes for the implementer

Kept as filed, for the record. The "two tables / two owners" count was already stale when the
ticket was written — the tree shares this policy across five windows under four owners (see
*What landed*); the implementation corrected the count instead of following it.

- The arming edge should be the guest's own world entry — `ISessionControl.LocalSceneReported`
  already publishes it and `SessionControlConvergence` consumes it — not a new message.
- `PendingReportFallback` is shared by two tables, so the window shape belongs in this class (one
  policy, two owners) rather than in either table.
- Nothing covers this class today: `PendingReportFallback` has no test of its own, and the flat
  cadence and the clock-wrap branch are exercised only indirectly by the world-report simulations
  — the window test is ADDED rather than extended, and it should pin both phases and the wrap.
