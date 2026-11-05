# Make the round boundary mechanical, and keep the step rules inside their budget

- Status: Done — both defects are fixed and measured on 2026-11-05; the readings are in the acceptance rows and the run sheet.
- Priority: Medium (it blocks nothing, but it decides whether the process is obeyed for its own reasons or by habit)
- Category: Process / step rules and the step-read gate
- Source: the body-verb split cycle (2026-11-05). Its first commit moved backlog files on the strength of a step record
  left over from 2026-11-03, and that round printed a 167-line working-mode file yet still broke three of its rules.

## Design

The workflow has two measured holes.

**A step record never expires.** `.acceptance/step-reads.txt` is one file shared by every round; the entry printer keeps
older lines and the commit hook matches a step by its SHA alone. Measured 2026-11-05: the file's own header still read
`2026-11-03 00:10` and carried steps 03-13 while that round had printed only steps 00, 01 and 02, so a change touching
`src/`, `tests/` or `tools/` would have been gated by a record two days old. `check-step-read.ps1` already carries a
`-NewRound` mode - nothing ever calls it.

**The step rules are over their own budget.** The rules README states "never above 3 KB"; three files exceed it:
`00-work-mode.md` at 12,686 bytes (the ONE file printed every round), `13-acceptance.md` at 6,175, `02-design.md` at
4,074. Their bulk is measured history and procedure that already has a home: the batch procedure lives in
`docs/acceptance/workflow.md`, the corpus rules in `docs/design/AGENTS.md`. The long read costs every round, and on
2026-11-05 that round read all fourteen files and still broke three of its own rules - so length is not buying
compliance either.

The fix uses the shape this repository already trusts, a stated rule plus a machine that checks it:

- **The round boundary is explicit.** A round starts when step 0 is printed. `act.ps1 -Step 0` opens a fresh record
  through `check-step-read.ps1 -NewRound` before printing; `act.ps1 -Step n` (n other than 0) and
  `check-step-read.ps1 -Record` refuse while the record carries no current-round header. A leftover line can never open
  a gate again.
- **The budget is a gate.** A normative gate asserts every committed `NN-*.md` step file is under 3 KB and that the set
  is complete (a floor), pinned with a synthetic oversized sample, so the README's limit stops being advice.
- **The files say the rules; the reasons move.** Each compressed file keeps every checkable rule as one sentence and
  points at the page that already owns the detail. The measured history moves to `docs/development/rules/why.md`.
  Nothing is deleted: a rule survives in its step file, the evidence for it survives in `why.md`.

## What must NOT change

The gates' wiring (`require-active-step` in the build, deploy and driver; the pre-commit hook's invocation), the
45-minute arming window, the per-step SHA comparison, the rule files' names, and every existing normative gate. This
change makes the round record stricter and the per-round read shorter; it must not weaken a check.

## Self-check table

| mechanism | change | evidence |
|---|---|---|
| the round record | a new round truncates it; a non-zero step refuses without the header | live readings: the inherited record (header `2026-11-03`) and the refusal after the reset |
| the commit hook | same invocation and SHA match | `check-step-read -WorkingTree` exit 0 only after the touched steps are re-read |
| the rule budget | gate added, three files compressed | the gate refuses a synthetic 4,000-byte file; the real set measures under 3 KB |
| the per-round read | step 0 prints a checklist | line and byte counts before and after |

## Verification design

- Measure the defect first: the inherited record (header date, step list) and the three oversized files.
- Then drive the round boundary: reset, refusal of an unread touched step, re-entry, pass.
- The budget gate is red on its synthetic sample and green on the real set.
- Focused gates and the full suite before the commit; no deploy (tools, tests and docs do not deploy).

## Acceptance rows

Judged on the tree in the round that landed it (2026-11-05, step 0 re-opened at 23:16).

| # | Action | Expectation | Verdict | Reading |
|---|---|---|---|---|
| 1 | a record from an earlier round meets a new round | no gate accepts it | pass | before the change the record's header read `2026-11-03 00:10` and carried 12 steps; after `act.ps1 -Step 0` it held only the header plus step 00, and `check-step-read -WorkingTree` refused with `missing: 07 08 11 12 13` (exit 1) |
| 2 | a step read in the current round | its record opens the gate; a touched step without a record is refused | pass | after re-entering 07/08/11/12/13 the same check answered `every touched step is recorded (07, 08, 11, 12, 13)` (exit 0); a header-less record made `act.ps1 -Step 3` and `check-step-read -Record 5` both refuse at exit 2 |
| 3 | the per-round read | prints the checklist and its last line, under the 3 KB budget | pass | step 0's print went from 167 lines / 12,686 bytes to 49 lines / 2,772 bytes, and its last line printed |
| 4 | every committed step rule file | under 3 KB, enforced by a gate that also fails a synthetic oversized file | pass | a probe file of 8,207 bytes made 2 of the 4 gate tests fail (over budget + set mismatch); with the probe removed all 4 pass, and the three compressed files measure 2,772 / 3,071 / 2,995 bytes |
| 5 | the process as a whole | build, format, the gates and the full suite green | pass | build 0 warnings / 0 errors, format exit 0, gates 816 passed, the behavioural suite 6598 passed |

## Run sheet

- [x] 1. Measure the inherited record and the three oversized files; record them above — evidence: header `2026-11-03 00:10` with 12 steps, and 12,686 / 6,175 / 4,074 bytes.
- [x] 2. Give `act.ps1 -Step 0` the round reset, and make `-Step n` and `-Record` require a current round — evidence: the refusals at exit 2 above.
- [x] 3. Add the step-rules budget gate with its floor and its synthetic sample — evidence: `StepRulesBudgetGateTests`, 4 tests, red on an 8,207-byte probe.
- [x] 4. Compress the three rule files; move the measured history to `why.md` — evidence: 2,772 / 3,071 / 2,995 bytes, with every rule kept and the evidence in `why.md`.
- [x] 5. Update the rules README and the tools guide; move the census rows and the index with them — evidence: the README's `why.md` row and the budget paragraph; the tools guide measures 5,086 of its 5,120-byte ceiling.
- [x] 6. Drive the round boundary end to end and record the readings — evidence: rows 1-2 above.
- [x] 7. Build, format, gates, full suite; fill the delivery checklist; commit and push — evidence: row 5 above and the cycle archive.
