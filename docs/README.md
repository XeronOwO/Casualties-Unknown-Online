# CUO documentation

This page is the way in: the task you are doing decides which pages you read, and each task below has
a moment that forces the reading. A page with no such moment does not belong here; the census records
that judgement per document.

Chinese readers: [`zh/README.md`](zh/README.md) carries the same tree in Chinese.

The binding rules are in [`AGENTS.md`](AGENTS.md); the registries are in [`standard/`](standard/README.md),
and the machine baselines code and gates read are in [`contracts/`](contracts/README.md). The
reader-facing books — start here, do one thing, understand why, look something up, work on CUO itself —
are [`en/README.md`](en/README.md).

## Start from the task, not from the tree

| The task | Read first | What forces it |
|---|---|---|
| change product behaviour | the area page under [`design/`](design/INDEX.md), then [`architecture/current.md`](architecture/current.md), then the symptom in [`acceptance/lessons.md`](acceptance/lessons.md) | `pwsh -File tools/knowledge.ps1 -Query "<the words a player or a log would use>"` prints the lines that carry the topic |
| write or repair an acceptance row | [`acceptance/AGENTS.md`](acceptance/AGENTS.md), [`acceptance/workflow.md`](acceptance/workflow.md) | the 14-step workflow: `tools/act.ps1 -Step 13` prints the rules and records the read |
| deploy or replay a game update | [`development/game-update-runbook.md`](development/game-update-runbook.md) | the runbook's numbered steps, each with the command it runs |
| land a document or a rule | [`AGENTS.md`](AGENTS.md) and [`standard/document-census.txt`](standard/document-census.txt) | the census gate on every build: a new page needs its reader and its reach |
| answer "why is it like this?" | [`decisions/active.md`](decisions/active.md) | every ticket and design page cites the decision it follows |
| point at evidence | [`evidence/verification.md`](evidence/verification.md) | the claim cites the record; the record carries the command and the reading |
| reopen a closed item | [`backlog/README.md`](backlog/README.md) | the index row names the ticket's file and its state |

## The two forced moments

- `tools/act.ps1 -Step <n>` prints that step's two rule files in full and records the read; a step
  walked without its rules is redone.
- `tools/knowledge.ps1` is the other one: `-Query` before designing, `-Changed` before the commit,
  `-Map` to print the table above.

## Archives — read through a citation, never front to back

- [`evidence/`](evidence/) holds one record per acceptance round or investigation; it is reached from
  the claim that cites it, and its census row says so.
- [`backlog/done/`](backlog/done/) holds closed tickets; the open queue is [`backlog/README.md`](backlog/README.md).
- `.acceptance/<run>/` holds a run's report, logs and frames; `tools/clean.ps1` keeps the runs the docs
  name and the last two days, and deletes the rest.
