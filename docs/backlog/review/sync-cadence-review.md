# Sync cadence review: fallback stretch limits and first-resend latency

- Status: Review
- Priority: Medium
- Category: Network / sync coverage / cadence tuning
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` cadence findings; user request 2026-09-09 — "如果有觉得同步时长不合理的，也可以提出来")
- Related: `review/global-adaptive-report-rate-stage-1-global-governor.md`, `review/global-adaptive-report-rate-stage-4-high-frequency-domains.md`, `review/network-traffic-baseline.md`, `review/session-control-convergence.md`, `review/carried-inventory-registration-re-report.md`

## Problem (evidence)

The adaptive governor only changes cadence (verified in matrix row R5), but several
stretch caps and resend latencies looked longer than the divergence they are meant
to heal. The audit named four of them; a fifth arrived with the carried-inventory
landing.

1. **World-item keyframe: 5 s base → 30 s max.**
   `src/CasualtiesUnknownOnline.Runtime/Session/AdaptiveSync/AdaptiveStreamCatalog.cs`
   (`BaseIntervalMs: 5000`, `MaxIntervalMs: 30_000`); the policy stretches it under
   pressure (`AdaptiveRatePolicy.GetEffectiveIntervalMs`). The keyframe is the only
   absolute heal for top-level item state (condition / liquids / components) and for
   `ItemReconcile`'s removal of phantom items, so 30 s of divergence is user-visible
   (a drained/filled bottle, a broken tool).
2. **Trader-state fallback: 5 s base → 30 s max.** Same catalog entry; the send is
   reliable. Trader interactions broadcast immediately, so the fallback only heals a
   swallowed event.
3. **Fluid full-viewport reconciliation: 1 s base → 10 s max.** Same catalog entry;
   the 10 Hz diff stream covers changes and the full viewport is the anchor-move
   fallback, but the ticket has to record the measured worst-case stale cell.
4. **Block / damage / keypad / geyser first resend: a single hardcoded 60 s.**
   `src/CasualtiesUnknownOnline.GameAdapter/World/WorldEventSync.cs` (the same
   hardcoded threshold in `GeyserStateSync` and the keypad cycle). The documented
   lazy-P2P swallow window is up to ~30 s after world entry, so a mutation in that
   window could stay invisible for up to a minute.
5. **Carried-inventory registration: 5 s × 12 then 60 s steady** (landed 2026-09-19
   with `review/carried-inventory-registration-re-report.md`): a new hardcoded rhythm
   that this review has to measure or explicitly accept.
6. **`WorldSnapshotComplete` / late-join readiness** and **enemy snapshot / attack**
   stay tracked in their own rows/tickets (R3, N1).

## Goal

Every stretch cap and resend latency the matrix names is either measured and accepted
(recorded in the matrix row) or tightened with an A/B measurement, with no regression
against the recorded network traffic baseline.

## Acceptance matrix

| # | Scenario | Expected | Result |
|---|---|---|---|
| 1 | Pressure-simulated peer | Governor still only stretches cadence, never drops a fallback | OK — unchanged; the change moves two `MaxIntervalMs` values, so every fallback still runs (`AdaptiveStreamCatalogTests.AllProfiles_HaveValidHzRange`) |
| 2 | Item keyframe under max pressure | Worst-case divergence measured; cap decided and recorded | OK — 30 s measured, tightened to 10 s |
| 3 | Trader fallback under max pressure | Worst-case stock divergence measured; cap decided and recorded | OK — 30 s measured, tightened to 15 s |
| 4 | Fluid full viewport under max pressure | Worst-case stale viewport cell measured; cap decided and recorded | OK — 6 667 ms measured and accepted (the 10 s cap never binds) |
| 5 | Host mutation during the P2P swallow window | Guest converges within the chosen first-resend latency | OK in simulation — a swallowed entry group converges on the member's first repeat: the window step is 5 s (`SyncCadenceDecisionTests`) and the test bounds the wait at two steps, `EntryRepairConvergenceTests.SwallowedEntryGroup_IsHealedByTheRepeatAnswer` |
| 6 | Bandwidth baseline | No regression beyond the recorded baseline | OK — `NetworkTrafficBaselineTests` green; the one added send is the repair pass, measured at 2 438 bytes and bounded at ≤ 6 passes per open entry window |

## Landed (2026-09-19)

**Measurement first.** `tests/CasualtiesUnknownOnline.Tests/Session/SyncCadenceDecisionTests.cs`
derives every worst case from the production catalog and policy — never from a copy of the
arithmetic — and asserts each cell, so a cap can no longer drift away from the number the matrix
records. The measured table, the byte costs and the reproduce commands are in
`docs/evidence/sync-cadence-measurements.md`.

**Decision 1 — item keyframe cap 30 s → 10 s.** Measured worst case:
5 000 / 8 333 / 10 000 / 10 000 ms for Optimal / Moderate / High / Critical (the cap now binds at
High as well as Critical; it was 5 000 / 8 333 / 14 286 / 30 000). The cap is applied after the
byte budget by design, so tightening it deliberately outranks byte preservation under pressure;
the cost is small because the fallback payload belongs to the item-table family the traffic
baseline measures (its 600-item checkpoint is asserted under 24 KB), which keeps the 10 s worst
case in the low KB/s against the profile's 1 MB/s budget.

**Decision 2 — trader fallback cap 30 s → 15 s.** Measured worst case:
5 000 / 8 333 / 14 286 / 15 000 ms (High is unchanged by the cap; Critical drops from 30 s).
The fallback only heals a swallowed interaction broadcast, so it may trail the item keyframe —
but not past the cap.

**Decision 3 — fluid full viewport accepted at the measured 6 667 ms.** 1 000 / 1 667 / 2 857 /
6 667 ms; the profile's 10 s cap never binds, so what the matrix row records is the measured
value, not the cap.

**Decision 4 — the entry group's first resend is an entry repair on the guest's own repeat
report.** The host used to send the entry group once and wait for its 60 s cycle while the
swallow window is up to ~30 s. The heal now rides the guest's readiness window
(`SessionControlConvergence`: 5 s repeats until the entry-group marker AND the start-gate release
are in), which makes a repeat mean two things at once — the entry answer did not complete this
member, and its uplink is up now — so it is a better trigger than a blind timer at the edge, and it costs nothing for an entry whose
window closed on BOTH control facts (a window a still-armed start gate holds open is answered
and bounded — measured by `EntryRepairConvergenceTests.HeldGate_PaysBoundedRepairsWithoutASwallow`). `SceneStateHandler` answers the first repeat (and a window
that stays open, at `EntryRepairSchedule.RepairIntervalMs` = 10 s, ≤ 6 passes inside the guest's
60 s window) with `WorldEntryFanout.SendInSessionRepair` — the absolute in-session tables,
kernel checkpoint first — plus `ISessionControl.EntryRepairRequested` for the adapter-owned entry
tables (`WorldEventSync`'s keypad codes, `GeyserStateSync`'s liquid types), and only THEN the two
control facts, so the repair the marker completes is always ahead of it — the marker's own meaning
stays narrower than "everything the entry needs", because the fan-out's entry-only members (the
item snapshot and the radiation line) are not part of the repair.
The entry fan-out itself is still never re-run (its entry-only members — the item snapshot and
the radiation line — remain entry contracts). The 60 s steady cycles stay as the durable
fallback.

**Decision 5 — carried-inventory registration cadence accepted.** 5 s burst step (the first
resend latency of a swallowed registration), 12 × 5 s = 60 s dense window, then one report a
minute (row I8) — the same order as the entry repair above.

**The wire is unchanged as a fact.** No `NetMsg` member was added, removed or reshaped, no
message changed direction or reliability, and `ProtocolVersion.Current` stays 31. That is a
record of this change, not a constraint on it: the compatibility boundary is the handshake
check, and this mechanism needed no wire field.

**What the matrix and evidence record.** Rows I3, N4, F1, W1, W4, W5, W7, R3 and I8 now carry
the measured value and the decision (the W7 row's repair-set list was also corrected: it names
the enemy snapshot, the recipe-unlock set and the roster, which had been missing since the N1 and
I6 landings); the verdict summary is unchanged because no verdict moved; the audit's gap row is
closed; the cadence-findings section is rewritten as decisions; the evidence JSON's declared
total went 959 → 973 and the touched rows' anchor cells were re-derived from it.

**Declared limits (what this automation cannot prove).** The runtime half of the entry repair is
proven end to end in the simulation (a dropped entry group, the repeat, the ordering of the
marker, the per-entry bound, third-party isolation, the byte cost). The Game Adapter's half —
the keypad and geyser re-sends, which sit behind the Unity world being alive — cannot run in this
suite: it is pinned by
`SourceShapeGateTests.EntryRepair_IsBoundByBothAdapterEntryTableOwners` and named by an
Information log line in each handler. Three reachable limits are
named rather than implied: (a) a repeat does not say WHICH control fact is missing, so a window
held open by a slow start gate also receives the repair — bounded at ≤ 6 idempotent passes per
entry, and the gate's force-start is itself 30 s; (b) the keypad and geyser channels have no
per-member send today, so their repair half is a broadcast — third parties receive a duplicate of
a table they already hold; (c) the repair is the heal for the tables in
`SendInSessionRepair`, so the entry-only item snapshot and radiation line still wait for the next
edge. Real transport timing — the 5 s window, the ~30 s swallow window, the real 10 s / 15 s /
60 s rhythms — is the user's dual-client acceptance, not something this suite can prove.
(d) The SAME finding's guest→host direction (the world-block and runtime-entity pending-report
tables, `PendingReportFallback`) is measured here and NOT changed: the window arms on the first
outstanding entry and re-sends flat at 60 s, so a guest report swallowed in the entry window stays
invisible on the host until then. It is a different seam (the guest's own arming) and is recorded
as an open residual with its own ticket (landed 2026-09-19 as
`review/guest-report-fallback-first-resend.md`), rather than
accepted silently. That ticket landed the same day: the window now has an entry
phase (12 x 5 s after the guest's own InWorld report, then the steady 60 s described above), so the
flat-60 s sentence above records the state this review measured, not the current one.

**Independent adversarial review (2026-09-19, fresh context, FULL tier, read-only, frozen tree).**
Verdict: no blocker — 2 major and 5 minor findings, all record-accuracy or gate-strength defects;
the mechanism survived attack. Reproduced independently: focused 9/9, neighbour 36/36, gates 63/63,
full 3429 + 63; the evidence JSON's 973 = 973 entries and all nine anchor cells; the new gate is RED
at HEAD (0 occurrences of `EntryRepairRequested` in either adapter owner at HEAD, 3 each in the
working tree); the cadence arithmetic recomputed by hand from the production factors
(1.0 / 0.60 / 0.35 / 0.15); the state-before-marker ordering; production reachability of the repeat
(the guest's window re-asserts through `ResendSceneState`, and its InWorld report is made only after
generation finishes); `TryClaim` semantics including the tick wrap; the adapter bind/unbind
symmetry; and every copied claim (30 s gate force-start, ~30 s swallow window, the broadcast-only
keypad/geyser channels, the repair set's exact membership). M1 (major): "a clean entry never pays"
was false — an existing test already runs the counter-example (a held start gate keeps the window
open, so a member that lost nothing is repaired up to four times); the claim was rescoped in all six
places and the cost is now measured by `HeldGate_PaysBoundedRepairsWithoutASwallow`. M2 (major):
matrix row W1 still carried only the pre-decision finding; it now records the decision and the
guest→host residual. Minors fixed in the same revision: the 2 438-byte pass is asserted exactly (so
the documented command reproduces it); the repeat's `EntryRepairRequested` raise is asserted; the
source gate anchors the bind pair inside `BindToSession`/`Unbind` and carries a matcher
self-test; the marker comment was narrowed to what the repair actually carries; the dangling
"recorded below" pointer is filled. One finding of my own was fixed alongside: the
reconnect-while-InWorld path (`HandshakeHandler`) re-runs the entry group without passing through
the InWorld edge, so it now re-arms the member's repair budget too — a reconnect can no longer
inherit the previous entry's cooldown. **Verification round (2026-09-19, same reviewer, read-only, on the committed revision
`dee766e4`).** Verdict: no blocker; five of the six round-1 findings plus M2 genuinely closed, the
reconnect re-arm confirmed correct, harmless and non-transmitting, and every number reproduced
(focused 15/15, neighbours 36/36, gates 68/68, full 3435 + 68, JSON 978). It also caught that this
record was ahead of the tree: three places still said "a clean entry never pays for it" (matrix row
W7, the audit gap row, and the convergence test's class summary) — all three now carry the scoped
wording — and that two of the anchors added to row W1 were byte-identical duplicates (dropped, so
the JSON declares 976 and W1 declares 47 distinct anchors). Its remaining findings were fixed the
same way: the residual ticket no longer cites a window test that does not exist (the class has NO
test today — the note now says the test is added, not extended), the held-gate bound reads
"≤ 3 inside the gate's 30 s force-start (≤ 4 over the ~36 s the test runs)", the source gate drops
commented-out lines before reading the bind pair (with an in-body comment sample in its
self-test), the `EntryRepair` field doc names both arming sites, and the delivery checklist's
structure-review evidence names the actually largest touched class.

**Verification (2026-09-19, final).** `dotnet build` 0 warnings / 0 errors; `dotnet format` exit 0;
focused `dotnet test … --filter "FullyQualifiedName~SyncCadenceDecision|FullyQualifiedName~EntryRepairConvergence|FullyQualifiedName~EntryRepairSchedule"`
15/15; the session/adaptive neighbour families re-run green
(`FullyQualifiedName~SessionControlConvergence|FullyQualifiedName~AdaptiveStreamCatalog|FullyQualifiedName~AdaptiveRatePolicy`,
36/36); normative gates 69/69; full suite with build 3435 (main) + 69 (gates) green.
The evidence JSON's declared total is 976 entries after the review's anchors were added (two
byte-identical duplicates the verification round found were dropped).

## Non-goals

- Rewriting the adaptive governor.
- Adding new streams or messages (those belong to the gap tickets above).
