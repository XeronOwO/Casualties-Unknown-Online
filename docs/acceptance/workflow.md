# Agent acceptance workflow

The procedure this repository uses to accept a ticket that sits in `review/`. It replaces the
previous "wait for the user's unified acceptance pass": the agent runs the real game, judges every
acceptance row, writes a record, and moves the ticket. The rules that bind this procedure are in
[AGENTS.md](AGENTS.md); the dependency table is [dependencies.md](dependencies.md).

Read the two kinds of value from [AGENTS.local.md](AGENTS.local.md) (this directory, gitignored,
auto-loaded only while working here) and never write one into a committed file:

| Key | What it is |
|---|---|
| `game-dir` | the physical machine's game install |
| `steam-exe` / `game-app-id` | how the host client is launched |
| `sandboxie-exe`, `sandbox-guest-root` | the second client's environment |
| `hotrepl-host-url`, `hotrepl-guest-url` | the two in-process probe endpoints |
| `acceptance-artifacts-dir` | where frames, recordings and probe dumps are written (never committed) |

## 1. Entry and scope

The unit of acceptance is a **batch**: the tickets waiting in `docs/backlog/review/` that the available
dependencies can serve, accepted in one run. Acceptance costs a build, a deploy, two game clients and
the setup of every scenario, so the run is planned across tickets — deploy once, launch once, work
through every ticket's scenarios in that session — and is never started for a single ticket while
another one waits on the same capability.

Each ticket keeps its own rows: the tables headed `Goal and acceptance criteria`,
`Reproduction / acceptance matrix` or `Final-acceptance procedure`, plus every behavioural claim its
`What landed` section makes. When a ticket states its expectations only in prose, the run writes the
row table first and records it, so the verdict cannot be reverse-engineered from the result.

A batch is bounded by the world it needs: tickets whose scenarios need different worlds, saves or
player counts are split into separate runs, and that split is part of the plan. Runs are serialised —
only one two-client session exists on the machine at a time.

## 2. Dependency preflight — a gate, not a formality

```powershell
powershell -ExecutionPolicy Bypass -File tools/acceptance/preflight.ps1
```

The preflight is read-only. It reports, per dependency: present / missing / unknown, plus the local
facts it could not resolve. Its exit code is `0` when a full two-client run is possible, `2` when a
capability is missing and `1` on its own error.

- **Everything present** → continue.
- **Something missing** → the run does not degrade silently. Ask the user in plain language (what is
  missing, why acceptance needs it, the options: install it, point the agent at an existing copy, or
  leave this capability out), record the dependency on the ticket as an external blocker, mark every
  row that depends on it `blocked`, and carry on with the rows that do not. A missing capability is
  never replaced by a weaker check.
- **A game or Steam instance is already running** → do not kill it. Report it and ask. Only processes
  this run started may be stopped by this run.

## 3. Acceptance plan

For every ticket in the batch, classify each row before running, and write the classification into
that ticket's record:

| Class | Meaning | Evidence it needs |
|---|---|---|
| `machine` | the outcome is a state, a message or a number (a report arrived once, a value converged, a log line appeared) | a log excerpt or a probe result |
| `visual` | the outcome is something on screen (a flash, a pose, a panel, a layout) | a captured frame the agent reads |
| `feel` | the outcome is how something plays (smoothness, lag, absence of teleporting) | a recording, analysed frame by frame; what stays subjective becomes a residual for the user |
| `blocked` | the capability it needs is missing (preflight) | the missing dependency and the question asked |

The plan then groups the batch into shared setups: which client is host, which scenarios are reached
in the same world, which tickets reuse one another's setup, and what has to be true in the world before
a row can be observed (a world entered, an item held, a peer in range). A ticket whose setup cannot
share the session already running gets its own run, and that decision is recorded.

## 4. Build, deploy, artifact identity

Acceptance runs against a commit, never against a dirty tree with an unrecorded build.

```powershell
dotnet build CasualtiesUnknownOnline.slnx
powershell -ExecutionPolicy Bypass -File tools/deploy.ps1 -GameDir "<game-dir>"
powershell -ExecutionPolicy Bypass -File tools/verify-deploy.ps1 -GameDir "<game-dir>"
```

`verify-deploy.ps1` prints the deployed plugin's `ProductVersion` with its `+<sha>`; that value is the
artifact identity the record carries. A build that passed without the latest artifacts running is not
acceptance. Before a sandboxed client starts, apply the shadow rule in `AGENTS.local.md`: a sandbox
copy that is older than the physical one runs old code, and the shadow files are removed file by file
(never recursively).

## 5. The two-client session

One session serves the whole batch: the plan's setups run in order, and each ticket's evidence is
collected while its scenario is up.

- The **host** runs on the physical machine, launched through Steam (`game-app-id`) so Steamworks
  initialises.
- The **guest** runs in the Sandboxie sandbox named by `sandbox-guest-root`, started after the deploy.
- Both join through the game's own Online UI (host creates the lobby, guest joins with the lobby id).
  The native UI is the only lobby surface CUO has; the run reuses it.
- Probes and actuation use the in-process evaluator at `hotrepl-host-url` / `hotrepl-guest-url`:
  reading live state, forcing a setup, or asserting that a message was handled. The committed driver
  helper `tools/acceptance/drive-in-process.ps1` carries the scenario vocabulary — `state`,
  `open-window`, `goto-page`, `click`, `set-text`, `create-lobby`, `join-lobby`, `start-run`, `quit`
  (`-ListActions` prints it) — and reproduces a setup through the Online UI's own registered controls,
  never through OS-level keyboard or mouse. Ad-hoc probes stay evals of the same channel. The socket
  dies with the game process, so a restart means reconnecting before the next probe.
- Capture is per row, not per run: one frame (or recording) per `visual` / `feel` row, captured from the
  client's own window (never the desktop, so the run never needs the clients in front), written to `acceptance-artifacts-dir` under an artifact id, plus the log excerpts a `machine` row needs.
- The run closes cleanly: quit both clients through the game, then stop what the run started.

## 6. Verdicts

Every row ends in exactly one of: `pass`, `fail`, `unproven`, `blocked`, `residual`.

- `pass` requires evidence produced by this run, against the deployed artifact, and the evidence is
  named in the record. Reading the captured frame is part of judging a visual row.
- `fail` names what was observed against what was expected; it is the input to the rejection path.
- `unproven` is honest, not a soft pass: the run could not put the world into the state the row
  needs, or the evidence is ambiguous. An `unproven` row keeps the ticket open.
- `blocked` names the missing dependency.
- `residual` is a row only a person can judge; it goes to the user's list, never to a self-issued
  pass.

## 7. The record

One record per ticket — `docs/evidence/acceptance/<ticket-slug>-<yyyymmdd>.md` — committed, English,
no machine values: the local artifact directory is referenced by the key name, the frames by artifact
id. Every record names the batch it belongs to, so a later reader can find the sibling tickets, the
shared build and the session they came from.

```text
# Acceptance record — <ticket title>

- Ticket: <slug> — verdict: moved to done/ | back to todo/ (status field: `- Status: Todo — Rejected (…)`) | stays in review/
- Batch: <run id, e.g. 20260927-a> — tickets <slug>, <slug>, …
- Commit: <sha> · Deployed artifact: CasualtiesUnknownOnline.dll, ProductVersion +<sha>
- Run: <start> → <end> · Host: physical machine · Guest: sandbox
- Dependencies: <ids used, from dependencies.md>
- Artifacts: <artifact-id> (<kind>) in the directory named by acceptance-artifacts-dir

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | … | visual | pass | <artifact-id> |

## Residuals for the user
<the rows a person must judge, each with its artifact id, in plain language>

## Limits
<what this run could not prove, and why>
```

## 8. Ticket transition

- Every ticket in the batch whose rows all `pass`, or whose remainder are `residual`, moves to
  `docs/backlog/done/`; the index rows move in the same change and each ticket links its record.
- A ticket with a `fail` or `unproven` row moves back to `docs/backlog/todo/` with
  `- Status: Todo — Rejected (…)` (the field repeats the folder's label) and the failing row named;
  the fix is ordinary development work.
- A ticket with a `blocked` row stays in `review/`, the blocker and the question asked are recorded
  in the ticket, and the acceptance record says so.
- `BacklogIntegrityGateTests` refuses a ticket that is not listed exactly once in its own section, so
  the moves and the index are one edit.

## 9. Residuals to the user

Residuals are batched: one message per run, plain language, each item saying what to look at and
which artifact shows it. The user's answer is recorded back into the records — a rejected residual
returns that ticket to `todo/` with the same rejection marking as a failed row.

## 10. Lessons and the closing harvest

Before the run closes, fold what it taught ([AGENTS.md](AGENTS.md) rule 9): reusable lessons into
[lessons.md](lessons.md), machine values and local gotchas into `AGENTS.local.md`, and anything the run
proved wrong or missing about this page into this page. A batch that ends without a lessons pass is not
finished — the next batch pays for whatever this one noticed and did not write down.

An acceptance conversation runs the same pass on its own before it ends, unasked: the agent summarizes
what the run taught — what worked, what it stepped on, and the scripts or snippets it wrote. A helper
general enough to reuse moves into `tools/acceptance/` (or the local artifact area named in the closing
report when it is machine-specific) so the next run starts from it instead of rediscovering it; a script
that stays only in a transcript is lost. The closing report names what was harvested and where it landed.

## Capability status

Which steps of this page are executable today, and which are still being built:

| Step | Status |
|---|---|
| 2 — dependency preflight | **executable**: `tools/acceptance/preflight.ps1` |
| 1, 3, 6, 7, 8 — plan, verdicts, record, transition | **executable** as a procedure; the run is driven by the agent, not yet by a script |
| 4 — build, deploy, identity, shadow rule | **executable**: existing `tools/*.ps1` plus the local shadow rule |
| 5 — session, probes, capture | **partly executable**: a running client is driven through the committed in-process helper (`tools/acceptance/drive-in-process.ps1`, whose `-ListActions` names the scenario vocabulary), which acts through the Online UI's own registered controls; launching both clients, frame capture and the log channels keep their steps, and each run records which mechanism produced each piece of evidence |

## Limits

- A run proves what it observed; it cannot prove the absence of a rare race by playing once. A row
  about timing or frequency gets several repetitions and says how many it ran.
- Frames are read, not measured: a judgement call that depends on millisecond feel stays a residual.
- The dependency table is the boundary of what a run can do. Anything outside it is not "untested" —
  it is `blocked`, with the question asked.
