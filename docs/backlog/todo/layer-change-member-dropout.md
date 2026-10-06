# Consecutive layer changes drop the members out of the world and storm the log

- Status: Todo — Rejected (batch `20261007-a`, 2026-10-07: the three-client row ran and BOTH of its
  expectations fail on the real shape — the members do NOT stay in the world, and a THIRD producer of the
  same log family grows at the pre-fix rate; the two producers the 2026-10-06 cycle bounded are confirmed
  bounded on the same shape, so the fix stands and is incomplete. See
  `## Acceptance readings (batch 20261007-a)` below and
  `docs/evidence/acceptance/layer-change-member-dropout-20261007-a.md`). The member-recovery half stays
  split out in `todo/layer-change-member-recovery.md`, whose attribution reading that batch took.
- Priority: Medium
- Category: World generation / layer transition / observability
- Source: agent acceptance batch `20261005-b` (2026-10-05), observed while staging that run's row 4 and
  recorded there as a limit; promoted to development work by the user's 2026-10-06 ruling that "not
  introduced by this change" is never a reason to leave a defect alone.
- Related: `done/layer-mod-baseline-divergence-on-continue.md` (the same warning from another producer),
  `todo/layer-change-member-recovery.md` (the split-out recovery half),
  `todo/remote-clone-warning-storm-on-member-dropout.md` (the third producer of this family, filed by the
  2026-10-07 batch), `done/guest-generation-segments-over-host-absence.md`,
  `done/reenter-baseline-adoption.md`,
  `review/steam-transport-send-limit-runaway.md` (the sibling unbounded-warning family),
  `docs/evidence/acceptance/layer-change-member-dropout-20261007-a.md` (the rejection's own record)

## Symptom (evidence)

Batch `20261005-b` needed a layer change to empty the host's world-item table (that row's shape). Its own
record (`docs/evidence/acceptance/guest-command-loss-reconciliation-20261005-b.md`) states that after the
first staging attempt's layer change the two members were out of the world with a repeating
`[LayerMod] baseline divergence` warning storm: the guest's rolling log grew from 0.8 MB to 33.4 MB in
about four minutes. Nothing recovered on its own — the batch restarted all three clients cold and judged
the remaining rows in a second session.

The same record's limits name the two facts that make this a mechanism rather than a one-off:

1. Each staging attempt costs a layer change, and **a second layer advance follows the first on its own
   about 9 s later** — so one command is already a consecutive change.
2. At +4.1 s after the first attempt's change the member had no local body at all
   (`container-read mode=local` → `no-local-body`), which is why its destroy probe found nothing to
   destroy.

The run's third attempt (`r4d-*`: one change with both ends' inbound parked for 9.7 s) succeeded, so the
hazard did not block that ticket's row; it cost the batch a session. Artifacts: `r4-*`, `r4b-*`, `r4c-*`,
`r4d-*` and the two sessions' log excerpts, in the directory named by `acceptance-artifacts-dir`.

## Ownership (checked 2026-10-06)

| Fact | Origin |
|---|---|
| The warning and the decision it reports | `LayerModifierSync` (the `[LayerMod] baseline divergence` line) landed with `63ee8b4a` ("the layer modifier is host-authoritative"; the member replays the host's decision) and was last changed by `32fd0ebd` ("rewind the layer-modifier decision to the segment start") — both this repository |
| The residual-modifier clear at a layer boundary | `WorldGeneration.world.ResetLayerModifiers()` with the `[LayerMod] non-none layer — cleared the previous layer's residual modifiers` line, landed with `c3c2df11` (the tutorial/debug layer-modifier residual fix) and moved by `0e0665a4` (the GameAdapter split) — this repository |
| The trigger | `skiplayer` is the game's own console command: the word appears nowhere under `src/`, and the run sends it through CUO's console action |

So the diagnostic, the host-authoritative modifier model and the segment/adoption behaviour around a layer
change are ours; only the command that performs the debug skip is native. That makes the hazard this
repository's to act on — an external observation is not the whole story.

## What is not known yet

1. Which way the causation runs: the members leaving the world may be a consequence of the divergence, its
   cause, or a third effect of a layer change the game's debug path performs outside the generation
   boundary CUO expects. **Still open** — see `todo/layer-change-member-recovery.md`.
2. Whether an ordinary layer-by-layer descent (the elevator, not the debug skip) reproduces any of it.
   **Still open.**
3. Whether the warning is emitted once per periodic world-item snapshot with no bound at all: the growth
   figure suggests a per-cycle line for minutes, but the emission path has not been read yet. **Answered
   2026-10-06** — see *Landed*.

## Landed (2026-10-06)

Item 3 of *Required work* (the emission path) is answered and item 2 (bound the diagnostic) is done; the
attribution the batch recorded was corrected on the way, and both corrections are measured rather than argued:

- **The warning is emitted once per ARRIVING SNAPSHOT** (`LayerModifierSync.ApplyIndex`, reached from both
  the periodic world-item snapshot and the item snapshot), and that stream is the 5-second keyframe
  (`AdaptiveStreamId.WorldItemSnapshotStream`: `BaseHz: 1`, `BaseIntervalMs: 5000`, `MaxIntervalMs: 10_000`,
  and the interval wins over the Hz) — measured in the surviving log at 8.3-second gaps, i.e. a few dozen
  lines over the four minutes the batch ran, not the 2,400 this ticket first claimed. The first draft of that
  figure read it off the 10 Hz item MOVEMENT stream, which never reaches `ApplyIndex`; the correction's
  direction is unchanged (the warning is even less able to fill 33 MB than claimed), and the wrong number was
  withdrawn from every document that had copied it.
- **The volume is the item follow pump's per-frame correction line.** `[ItemPhysics] settle` is written at
  Information inside the per-frame ease branch, one line per item per frame while a copy's gap to the host's
  state does not close — which is exactly what a diverged world looks like. In the client log that survived
  (the storm's own log is gone: that client was restarted and its current log belongs to batch `20261006-h`,
  so this is the same shape and not the same event) it is 4,445 of 7,368 lines, with `[Fluid] region` second
  at 415.
- **Both are bounded now**, without weakening the detector and without touching the wire or a save shape: a
  repeatable diagnostic asks `LogRepetitionGuard` — one window per SUBJECT, so an unchanged fact costs a
  bounded window of lines and its end reports what the window swallowed, while a subject that moves reports
  again — through `ItemDistanceLog` for the two per-frame correction lines (whose subjects are
  (item, distance band) and whose `Finished` re-arms the window when a gap closes, so a divergence that
  returns is news again), directly for the two snapshot diagnostics, and for the 10 Hz fluid region receive
  handler, which now reports a rectangle's first regions at Information and repeats at Debug.
- **Pinned by** `tests/CasualtiesUnknownOnline.NormativeGates.Tests/LogVolumeGateTests.cs` (the producers'
  bodies read as syntax — the log must be GATED by the window, not merely preceded by it — the handler's
  Debug path matched as a call, three census floors and three matcher theories with positive and negative
  samples; mutation-checked by replacing the settle line's window call with `if (false)` and reading the red)
  and `tests/CasualtiesUnknownOnline.Tests/Items/LogRepetitionGuardTests.cs` (the window's, the counter's and
  the shell's contract, 16 cases). Fact sheet:
  `docs/evidence/selfchecks/items/layer-change-warning-storm-selfcheck.md`.
- **This cycle's independent review found the first three defects of this record** — the wrong cadence figure
  (the one above), a sentence claiming a flush the code did not do, and the missing re-arm on resolution —
  plus the gate's two matchers that did not do what they said. All five are fixed in the same change rather
  than deferred, which is why the wording of this section and the gate's own assertions are the shape they are.
- **Item 4's family sweep is bounded by a census, not by a rate model**: the gate pins the sites this cycle
  read, and a new per-frame or per-10-Hz Information line elsewhere is caught when it is named there — the
  census floors make a renamed producer loud instead of silent, which the self-check states as a limit.
- **The runtime delivery of item 2 was left to the next batch by this cycle, and that batch ran it**:
  `## Acceptance readings (batch 20261007-a)` below reads the row — the log growth as a size, and the
  members' staying in the world — and rejects both halves of the row while confirming this cycle's bound.

## Acceptance readings (batch `20261007-a`, 2026-10-07)

The batch drove the row on three clients — operator = physical-machine host, both members = sandbox guests —
three times in one session, the attempts differing only in whether a member's inbound dispatch was parked
through the change. Full reading and evidence pointers:
`docs/evidence/acceptance/layer-change-member-dropout-20261007-a.md`.

1. **The row's first expectation fails.** With the park ineffective (attempt 1, a 0.1-second window) and with
   no park at all (attempt 2) BOTH members were out of the world at +5 s (`inWorld: false`; attempt 2's
   `container-read mode=local` answered `no-local-body` at +4.2 s). With one member's inbound parked for
   9.4 s (attempt 3) that member held its body and its carried item while the unprotected one was still out
   at +60 s and answering `no-local-body`.
2. **The row's second expectation fails, on a producer this cycle did not name.** The third client's log grew
   8.796 MB in ~90 s — 58,148 of its 58,960 lines are
   `Remote body: no Body component in "Experiment" clone.` — and a further 10-second sample measured
   1.212 MB / 10 s = 7.25 MB/min at 639–866 lines/s, still climbing when the session closed (82,034 such
   lines in the file it ended with), against ~8.2 MB/min before the fix. Filed as
   `todo/remote-clone-warning-storm-on-member-dropout.md`.
3. **What this cycle bounded IS bounded on the real shape**, which is why this rejects the row and not the
   fix: `[ItemPhysics] settle` read 14 and 13 lines (was 4,445 of 7,368), `[Fluid] region` 5 and 7 (was 415),
   `[LayerMod] baseline divergence` 0–3 lines across the three attempts (the corrected 5-second keyframe
   cadence), and no client logged `[ERR][Unity:Exception]` in any attempt.
4. **The exit has a trigger and it is on the message path.** The attempts differ only in the park: held
   inbound, the member stays; not held, the member leaves. That is the answer `## What is not known yet`
   item 1 was waiting for, and `todo/layer-change-member-recovery.md`'s record carries the reading.

## Required work

Status of each item after the 2026-10-06 cycle:

1. **Attribute it first** — **partly done**: the storm's volume and the warning's cadence are attributed from
   the batch's record, its artifacts and the client log that survived. The member's exit is NOT attributed
   and moved to `todo/layer-change-member-recovery.md` (item 2 there is the red-first reading).
2. **Bound the diagnostic** — **done** (see *Landed*); the runtime row is the next batch's.
3. **Recover the member** — **open, moved** to `todo/layer-change-member-recovery.md`, with the reason: the
   cause of the missing body is unknown, and a recovery written before the reading would be a guess.
4. **Audit the family** — **done for the sites this cycle could name**, and stated as a census rather than a
   rate model: every per-frame or per-arriving-message Information line in the read surface is now either
   behind a window or demonstrably low-frequency (the trader state stream, the 5-second world-time resend,
   the 1 Hz character snapshot). A NEW high-frequency line elsewhere is caught only once it is named in
   `LogVolumeGateTests`; `review/steam-transport-send-limit-runaway.md` stays its own ticket.
5. **Real-machine acceptance** — **ran as batch `20261007-a` and REJECTED the row**: the fix's half passes
   and both of the row's expectations fail, see `## Acceptance readings (batch 20261007-a)` above. The re-run
   owes the third producer's bound, and for the staying-in-the-world half an attempt that holds BOTH members.

## Non-goals

- Removing or weakening the divergence detector: it caught a real defect once and stays.
- Guarding the debug skip command itself: it is the game's own console command.
