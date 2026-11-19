# docs/acceptance/ — agent-run acceptance

Agent-only area (no `README.md`, never translated, outside the human navigation). Acceptance is the agent's duty, not a step handed
to the user: a ticket in `review/` is code-complete and waits for this run, and the run moves it. Machine facts live in the
gitignored [AGENTS.local.md](AGENTS.local.md): read it first, never copy machine values into a committed page.

## Index

- [workflow.md](workflow.md) — the end-to-end run and the capability table.
- [dependencies.md](dependencies.md) — every dependency, its detection, and the degradation ladder.
- [lessons.md](lessons.md) — what the runs keep teaching.
- [`tools/acceptance/`](../../tools/acceptance/) — the preflight, the in-process driver (`-ListActions` names its actions and
  its recipes), and the BepInEx-tree gate; [workflow.md](workflow.md) says how each is used.

## Binding rules

1. `[CRITICAL]` **Preflight first.** A game/Steam process the run did not start, or any missing dependency, is an external blocker:
   record it on the affected tickets, leave dependent rows `blocked`, and never substitute a weaker check.
2. `[CRITICAL]` **Acceptance is batched**: one run covers every `review/` ticket the dependencies can serve — build/deploy/launch
   once; work each ticket; one record per ticket naming its batch.
3. `[CRITICAL]` **A row passes only on evidence from THIS run against the deployed artifact**: the evidence pointer (log excerpt,
   probe result, captured frame) is part of the verdict. An earlier green test is not evidence; `unproven` is not `pass`.
4. `[RULE]` **Judge by reading the evidence, frames included**: a visual row is judged from a captured frame the agent inspected,
   captured per client window (never the desktop). A subjective judgement is a residual, never a self-pass.
5. `[RULE]` **Reproducible from the record**: commit, deployed artifact identity, rows, one verdict/evidence pointer per row,
   dependencies used, residuals.
6. `[RULE]` **The machine is shared property**: never kill a process the run did not start, never delete sandbox files
   outside the shadow rule in `AGENTS.local.md`, stop cleanly.
7. `[RULE]` **No machine path, no personal data in git**: placeholders only; frames and dumps stay in the local artifact directory
   and are cited by artifact id.
8. `[RULE]` **A failed row is a finding, not a stopper**: move the ticket back to `todo/` with `- Status: Todo — Rejected (…)`,
   naming the failing row and its evidence; tickets whose rows all pass move to `done/`. Index rows move in the same change.
9. `[RULE]` **Every run leaves this area better**: fold a reusable lesson into [lessons.md](lessons.md), a machine value into
   `AGENTS.local.md`, a wrong or missing rule into this file — and into `docs/AGENTS.md` or the root `AGENTS.md` when it binds the
   repository. Closing an acceptance conversation runs this harvest unprompted.
10. `[CRITICAL]` **Wall time is a measured fact.** Report connect / setup / case per case and classify the case `InRun`,
    `NewWorld` or `ClientRestart`; seconds per case and tens of seconds per group are the target, and an overrun is a finding
    to optimize (reuse a healthy pair, cut dead waits), not a normal cost. `--cost` and `--reuse` select groups.
11. `[CRITICAL]` **Every case counts the logs it caused.** Mark both clients' log offsets before it starts; count every `Warning`
    and `Error` written since, and assert the case's declared budget (zero errors by default). An unexplained error fails even
    when behaviour passed; known warnings are named and bounded, never filtered out.
12. `[CRITICAL]` **Assertions prove the design, not the happy path.** Name the design claim and read the player-visible state on
    every affected client — host and member, watcher and watched — plus the unchanged side or a control where a false pass is
    possible. "Non-empty", "ok" and "connected" are preconditions, never verdicts.
13. `[CRITICAL]` **The automated suite is the last acceptance gate.** A `review/` row closes only when an automated case covers
    it, ran against the deployed artifact, and its report (log counts and per-client readings included) is cited. Manual play
    may explain a failure, never substitute for a missing case; a row without a case stays open.
14. `[CRITICAL]` **The full batch's wall time is a recorded baseline: 39 rows in 301.0 s** (`.acceptance/host-run3/report/report.md`,
    2026-11-19, 39 of 39 green; 282.2 s stays as history because three green runs that day read 294.3/301.0/307.1 s); five
    minutes is the ceiling. A change that adds a row or about a second first finds the time back or records on its ticket why
    it cannot; the baseline moves only with a green run whose reading replaces it. Measurement: [workflow.md](workflow.md).

## Related

[`../backlog/README.md`](../backlog/README.md) — status meaning and transitions · [`../../AGENTS.md`](../../AGENTS.md) — the cycle
before acceptance · [`../evidence/delivery-checklist.md`](../evidence/delivery-checklist.md) — the delivery gate
