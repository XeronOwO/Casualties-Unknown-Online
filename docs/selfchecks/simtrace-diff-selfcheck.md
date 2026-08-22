# SimTrace Diff Automation — Self-Check (2026-08-16)

Delivery fact sheet for the Tooling/testing backlog closeout: the real-log →
replay / SimTrace diff step was manual (extract both sides, eyeball or
external-diff the normalized sequences). `tools/compare-itemtrace.ps1` now
automates the whole comparison, including whole-session log noise, gzip logs,
the three matching strengths, leak handling and SimTrace regeneration.

## Mechanism inventory (complete side-effect table)

| # | Mechanism | Current behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | SimTrace emission | every `*.replay` run writes raw `[ItemTrace]` begin/end pairs to `SimTraces/<file>.trace` | unchanged — the new tool consumes those files | SimTrace.cs:25-63; ReplayRunner.cs:69 |
| 2 | Real log shape | `OperationTrace` lines carry timestamp/thread/category noise, session-global op ids, and game-side item/origin values the simulation cannot mirror | the tool reads raw lines from `latest.log` or a `.log.gz` archive, drops the same surfaces the existing extractor drops | OperationTrace.cs:31-37; compare-itemtrace.ps1:53-126 |
| 3 | Normalization surface | `extract-itemtrace.ps1` keeps op/result/events (or begin) and drops origin/item | the new tool uses the same fidelity surface and additionally canonicalizes begin EVENT (raw begin lines) so a begin cannot all look identical; op ids are dropped because real and simulation counters start at different values | compare-itemtrace.ps1:81-126 |
| 4 | Whole-session noise | a real `latest.log` contains unrelated operations before/between/after the gesture battery | default SUBSEQUENCE matcher finds the expected replay token sequence inside the real token stream and reports the original log line span | compare-itemtrace.ps1:152-171,336-341 |
| 5 | Stronger matching | manual diff had no formal "windowed" mode | `-Contiguous` requires one consecutive run (no interleaved ItemTrace lines); `-Strict` requires exact equality for an already-windowed log | compare-itemtrace.ps1:127-150,310-331 |
| 6 | Begin surface | begins were extractable but the manual diff had to decide how to use them | begin-event tokens are compared by default; `-NoBegins` switches to the result-only surface and disables leak detection | compare-itemtrace.ps1:96-103,269-294 |
| 7 | Leak contract | ReplayTests asserts the simulation has no begin-without-end; real logs had no automated equivalent | expected trace leaks always fail; real-log leaks warn by default and fail with `-FailOnLeak` | compare-itemtrace.ps1:278-294; ReplayTests.cs:181-189 |
| 8 | Trace resolution | the operator had to locate `SimTraces/<file>.trace` under `bin/` manually | `-Replay <name-or-file>` resolves `Debug/net48` (or another `-Configuration`), falls back to any generated trace, and `-Refresh` re-runs the replay theory first | compare-itemtrace.ps1:202-265 |
| 9 | Archived logs | real sessions rotate to `.log.gz`; both tools only read plain logs | the compare tool reads `.log.gz` directly through `GZipStream` | compare-itemtrace.ps1:53-79 |
| 10 | Production runtime | — | no production/wire change, no ProtocolVersion bump | only `tools/` + `tests/` touched |

## Design

- **One normalization function for both inputs.** Real log and SimTrace pass
  through the exact same parser, so a shape drift between production
  `OperationTrace` and simulation `SimTrace` fails the comparison instead of
  being masked by two different extractors.
- **Default subsequence, explicit stronger modes.** A whole-session log is the
  normal input; subsequence matching is the honest automation (the gesture
  battery must appear in order, unrelated operations may interleave).
  `-Contiguous` and `-Strict` are opt-in when the log is already windowed or
  when a stricter CI check is wanted.
- **Begin events are compared, origin/item/op are not.** This is the recorded
  fidelity boundary: the simulation has no hook chain, so origin/item cannot
  line up; result and the decision-chain events are the arbitration-semantics
  surface, and the begin event pins each operation's true start.
- **Leak semantics match the existing split.** Simulation leaks are already a
  test failure (ReplayTests), so the tool also refuses them. A real session can
  end mid-operation, so real leaks warn by default and `-FailOnLeak` escalates
  them for CI.
- **The tool stays out of the build path.** It is a developer/CI tool, not a
  commit gate: no `AGENTS.md` build list entry, no fake real-log dependency in
  `dotnet test`.

## Verification design

1. L0 end-to-end script tests (`CompareItemTraceScriptTests`, 9 cases) invoke
   the real PowerShell tool against raw fixtures:
   - subsequence match through whole-session noise + original-line span;
   - gzip real log read directly;
   - mismatch prints both normalized sequences;
   - interleaved noise passes subsequence and fails contiguous;
   - strict requires the exact window;
   - `-NoBegins` compares only the result sequence;
   - expected leak fails; real leak warns by default and fails with
     `-FailOnLeak`;
   - missing files / zero `[ItemTrace]` lines fail loudly.
2. Assertion-effectiveness proof: temporarily mutating the subsequence
   matcher (`-cne` → `-eq`) turned 5 of the 9 cases red (wrong matched line
   spans and false passes), then restoring the matcher returned 9/9 green.
3. Static real-log evidence: the tool parsed
   `2026-08-10-1.log.gz` (330 `[ItemTrace]` tokens) and an existing generated
   SimTrace from the replay suite; no crash, leak list and token lines
   reported. It correctly reports FAIL when the real log does not contain the
   replay's gesture sequence (that archived session predates the heater-cook
   gesture).
4. Full suite: 887 tests green (878 baseline + 9 new) after the change.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Real log reader | plain + gzip, raw line numbers preserved | compare-itemtrace.ps1:53-126; `GzipRealLog_IsReadDirectly` |
| Normalization | one parser both sides; drops origin/item/op; compares begin-event + result + events | compare-itemtrace.ps1:81-126; ReplayTests.cs:39-45 (format contract) |
| Subsequence match | default whole-log matching, reports matched original lines | compare-itemtrace.ps1:152-171,336-341; test line 43 |
| Contiguous / strict | opt-in stronger matching | compare-itemtrace.ps1:127-150,310-331; tests line 84/106 |
| NoBegins | result-only surface | compare-itemtrace.ps1:269-294; test line 124 |
| Leak handling | expected always fails; real warns / `-FailOnLeak` | compare-itemtrace.ps1:278-294; tests line 145/161 |
| Trace resolution | name/path → generated trace, `-Refresh` reruns replay theory | compare-itemtrace.ps1:202-265 |
| Failure loudness | exit 1 + both token sequences on every mismatch | compare-itemtrace.ps1:48-51,343-348; test line 68 |
| Production runtime | unchanged | only `tools/` + `tests/` in the change set |
| Structure | new test class under 600 lines, one top-level type; script outside the C# gate | tools/check-architecture.ps1 |

## Delivery evidence (2026-08-16)

- `dotnet build` 0 warnings/errors; `dotnet format` clean; `check-architecture`,
  `check-event-replay`, `check-entity-event-dispatch` all passed.
- 887 tests green (9 new).
- Runtime verification marked from L0 simulation + static evidence per the
  user's zero-manual-acceptance rule (2026-08-16) — no manual acceptance.
- Deployed via `tools/deploy.ps1` to the real game dir as the delivery-cycle
  formality; the change set itself contains no production assemblies, so the
  game runtime is unchanged.
