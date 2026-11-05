# The step rules

Fourteen steps, one file each, plus a local sibling beside it (`NN-<step>.local.md`, never committed). The root
`AGENTS.md` and `AGENTS.local.md` carry only the step list; every normative rule lives in exactly one file here.

## Reading discipline

At the start of a step, read **both** files of that step in full — the committed one and the local one. Every round,
even when the previous round read them, even when the step looks routine. The whole point of this layout is that the
rule is in front of the agent at the moment it acts, instead of being remembered from the top of a long conversation.

A round starts when step 0 is printed: `tools/act.ps1 -Step 0` opens this round's record, and a step printed before it
belongs to an earlier round and is void. That is why the commit hook can refuse a step this round never read.

Step 0 is special: its local file points at the user-level rules, which are read first, before anything else in a
round.

## What lives where

| file | holds |
|---|---|
| a committed `NN-*.md` | rules that are true for anyone working on this repository |
| its `NN-*.local.md` | machine facts and personal working notes: paths, ports, accounts, sandbox names, device quirks |
| `why.md` | what measured each rule; opened when a rule seems fussy or is about to change, never a second copy of a check |

A rule that applies only to this machine never enters the committed file; a rule that applies to every checkout never
enters the local one. When a topic genuinely has both halves, the committed file states the rule, the local file states
the values, and each names the other so the pair reads as one instruction.

## The files

| step | committed | local (never committed) |
|---|---|---|
| 0 | `docs/development/rules/00-work-mode.md` | `docs/development/rules/00-work-mode.local.md` |
| 1 | `docs/development/rules/01-task-frame.md` | `docs/development/rules/01-task-frame.local.md` |
| 2 | `docs/development/rules/02-design.md` | `docs/development/rules/02-design.local.md` |
| 3 | `docs/development/rules/03-entry-mapping.md` | `docs/development/rules/03-entry-mapping.local.md` |
| 4 | `docs/development/rules/04-family.md` | `docs/development/rules/04-family.local.md` |
| 5 | `docs/development/rules/05-plan.md` | `docs/development/rules/05-plan.local.md` |
| 6 | `docs/development/rules/06-red-first.md` | `docs/development/rules/06-red-first.local.md` |
| 7 | `docs/development/rules/07-implement.md` | `docs/development/rules/07-implement.local.md` |
| 8 | `docs/development/rules/08-verify.md` | `docs/development/rules/08-verify.local.md` |
| 9 | `docs/development/rules/09-self-review.md` | `docs/development/rules/09-self-review.local.md` |
| 10 | `docs/development/rules/10-structure.md` | `docs/development/rules/10-structure.local.md` |
| 11 | `docs/development/rules/11-commit.md` | `docs/development/rules/11-commit.local.md` |
| 12 | `docs/development/rules/12-report.md` | `docs/development/rules/12-report.local.md` |
| 13 | `docs/development/rules/13-acceptance.md` | `docs/development/rules/13-acceptance.local.md` |
| index | `docs/development/rules/README.md` | — |
| reasons | `docs/development/rules/why.md` | — |

## Editing

One subject per file, and **never above 3 KB**: `StepRulesBudgetGateTests` fails a committed step file over 3,072
bytes, because a file that explains itself is a file that gets skipped — and the one printed every round (step 0) is the
one where that costs most. Measured 2026-11-05: `00-work-mode.md` was 12,686 bytes and the round that read it still
broke three of its rules. Keep the checkable rules here and move the measured history to `why.md`; a new rule that does
not fit belongs in a different step's file — not duplicated into two.

