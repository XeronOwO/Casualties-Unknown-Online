# The full behavioural suite takes four minutes — measure it and cut the wall clock

- Status: Todo — filed 2026-10-18 from the user's observation while landing the `/summon` verb.
- Priority: Medium
- Category: Engineering / test feedback loop
- Source: the user, 2026-10-18: the full `dotnet test` run took **4 m 05 s** ("整整跑了 4 分钟？落个优化票"). The same
  tree's three runs in that session measured **1 m 31 s**, **1 m 37 s** and **4 m 05 s** for the behavioural suite
  (orchestration logs `.acceptance/test-full-1.log`, `-2.log`, `-3.log`, 5 790 / 5 790 / 5 798 cases), so the spread
  itself is a finding: a 2.5× wall-clock difference between runs of one tree has no recorded cause.
- Related: `docs/evidence/test-parallelization.md` (the parallel model, the measurement method and the Stage 1-3
  numbers this ticket must refresh), `docs/en/contributing/build-and-test.md` (the tier commands as contract),
  `done/test-suite-parallelization.md` (the cycle that introduced the tiers), `done/validation-and-source-checks.md`
  (why the gates project exists)

## Problem (evidence)

- **The run is long enough to change how work is done.** The repository's own rule is the tier ladder (focused →
  gates → full), and it exists because the full run is the slow step; at four minutes a single full run is a
  meaningful share of a work item, and a rule that says "run it before every commit" then competes with the
  evidence it is supposed to provide.
- **The variance is unexplained.** Three runs of the same tree, on the same host, one after another:
  1 m 31 s, 1 m 37 s, 4 m 05 s. `docs/evidence/test-parallelization.md` §2 already says absolute wall clock
  drifts more than ±10 % between batches on a busy host and that a change must be compared with three-run medians;
  a 160 % jump is outside that note, and no one has looked for the cause. The 4-minute run also ran while the tree
  and the host were otherwise idle, which is exactly the case the note does not cover.
- **The tier numbers are stale.** The documented inner-loop split is from the 2026-09-22 census (2 097 fast cases
  in ~17 s, 1 698 tagged in ~31 s). Today's suite is 5 798 cases; the split's size, its wall clock and the class
  census behind it have not been re-measured since, so the "fast loop" number an operator plans around is a
  September figure.
- **No one knows the long pole.** xUnit v2 runs one collection per class and every case inside a class serially; the
  wall clock is therefore bounded below by the longest class chain. The suite cap is 40 cases per class
  (`TestClassSizeGateTests`), but a 40-case class that builds the full stack is a very different cost from a 40-case
  pure-domain class, and nothing records which classes are actually at the top.
- **The gate project rides the same invocation.** `dotnet test CasualtiesUnknownOnline.slnx` runs the net8.0 gate
  project (11 s) and the net48 behavioural suite in one command; the two have different failure profiles and
  different costs, and the split is not recorded as a deliberate choice.

## What this ticket owes

1. **A current measurement, mined before it is run.** The wall clock is already observed three times on one tree
   (1 m 31 s, 1 m 37 s, 4 m 05 s, §*Problem*), so a baseline does NOT start with three new full runs: that is
   12 minutes spent to re-learn what the orchestration logs already say. The steps, cheapest first:
   - read the existing runs' own numbers (vstest duration, case count) from `.acceptance/test-full-1/2/3.log`, and
     decide from them whether the 4 m 05 s was a TEST slowdown (its vstest duration and summed time grew) or a
     WALL-CLOCK-only effect (scheduling, load, first-run JIT/AV) — the two live in different columns and point at
     different fixes;
   - if the existing logs cannot separate them, run the suite ONCE with `--logger trx`, and read the per-class
     durations from that single run — one run carries everything the per-class table needs;
   - three-run medians are the page's rule for an A/B COMPARISON (§2), so they are spent only after a candidate
     change exists — never to establish a baseline that the logs already hold.

2. **The spread explained or bounded.** Name the cause of the 1 m 31 s → 4 m 05 s difference, or bound it with
   evidence: a class whose duration depends on machine scheduling, the thread budget, a first-run JIT/AV effect, or
   background load. "It varies" is not an answer the next operator can plan around; if it really is host load, the
   page states how it was ruled in or out and what to do about it (for example a pinned quick run for the commit
   gate versus a full run for the cycle's end).
3. **The top of the cost curve, named.** The ten slowest classes with their medians, and for each the reason it is
   slow (full-stack fixture, a serial chain, real I/O, a sleep/clock tick). A class that is slow because it waits on
   a virtual clock is a different fix from one that is slow because it composes the production root per case.
4. **One decision, taken from the measurement** (and only then):
   - if the long pole is one or a few classes, split or re-case them so no single collection dominates;
   - if it is the per-case full-stack setup, reuse a fixture the way the existing harnesses already do;
   - if it is the thread budget, judge `maxParallelThreads` from a paired A/B, never from a single run (§7.5), and
     record the effect;
   - if it is the machine, record it and change nothing — the ticket then closes with the measurement as its result.
5. **The tier commands re-measured and the doc refreshed**, so the fast loop an operator is told to run is the loop
   the current tree actually has.

## Acceptance rows

| # | Row | Expected |
|---|---|---|
| 1 | The measurement, mined from the data that exists before any new run | the per-run columns the logs already carry (wall clock, vstest duration, case count), and — only if those cannot separate a test slowdown from a wall-clock effect — ONE `--logger trx` run whose per-class durations are the table |
| 2 | The same-host spread | either a named cause with its evidence, or a bounded statement of what the host adds and how the commit gate should be planned around it |
| 3 | The ten slowest classes | per-class medians from the TRXs, each with the reason it is slow |
| 4 | The tier split re-measured | `Category!=Integration` and the tagged subset, cases and wall clock, replacing the 2026-09-22 figures |
| 5 | The decision | one of rows 4's outcomes, stated with its measurement; "no change" is a valid outcome only with the numbers that justify it |
| 6 | If code changes: the same case count, the same behaviour, the full suite green | the suite is a contract; optimization never trades a case away, and every removed or merged case is named |

## Non-goals

- Not a change of the test architecture (the harnesses, the simulation worlds or the DI composition).
- Not a coverage trade: no case is deleted to make the suite faster; a case that is genuinely redundant is a
  separate ticket with its own argument.
- Not a comparison across thread settings: summed test time is a wall-clock metric and inflates with concurrency
  (`test-parallelization.md` §2/§7.5), so only medians at a fixed setting are comparable.
- Not the gate project's own cost unless the measurement says it matters.
