# docs/acceptance/ — agent-run acceptance

Agent-only area (no `README.md`, never translated, outside human navigation). Acceptance is the agent's duty: a `review/`
ticket waits for this run, which moves it. Machine facts live in the gitignored
[AGENTS.local.md](AGENTS.local.md) — read it first; never copy machine values into committed pages.

## Index

- [workflow.md](workflow.md) — the end-to-end run and capability table.
- [dependencies.md](dependencies.md) — every dependency, its detection, and the degradation ladder.
- [lessons.md](lessons.md) — what the runs keep teaching.
- [`tools/acceptance/`](../../tools/acceptance/) — the preflight, the in-process driver (`-ListActions` names its actions and
  recipes), and the BepInEx-tree gate; [workflow.md](workflow.md) says how each is used.

## Binding rules

1. `[CRITICAL]` **Preflight first.** A game/Steam process the run did not start, or a missing dependency, is an external blocker:
   record it on the affected tickets, leave dependent rows `blocked`, never weaken the check.
2. `[CRITICAL]` **Acceptance is batched**: one run covers every `review/` ticket the dependencies can serve — build/deploy/launch
   once; one record per ticket naming its batch.
3. `[CRITICAL]` **A row passes only on evidence from THIS run against the deployed artifact**: the evidence pointer (log excerpt,
   probe result, captured frame) is the verdict. An earlier green run is not evidence; `unproven` is not `pass`.
4. `[RULE]` **Judge by reading the evidence, frames included**: a visual row is judged from a frame the agent inspected,
   captured per client window (never the desktop). A subjective judgement is a residual, never a self-pass. A click that
   does nothing is read from the frame first: the panel above the target is the first suspect — our own window swallows
   the press.
5. `[RULE]` **Reproducible from the record**: commit, deployed artifact identity, rows, one verdict/evidence pointer each,
   dependencies, residuals.
6. `[RULE]` **The machine is shared property**: never kill a process the run did not start, never delete sandbox files
   outside the shadow rule in `AGENTS.local.md`, stop cleanly.
7. `[RULE]` **No machine path, no personal data in git**: placeholders only; frames and dumps stay in the local artifact
   directory, cited by artifact id.
8. `[RULE]` **A failed row is a finding, not a stopper**: move the ticket back to `todo/` with `- Status: Todo — Rejected (…)`,
   naming the failing row and its evidence; tickets whose rows all pass move to `done/`, their index rows in the same change.
9. `[RULE]` **Every run leaves this area better**: a reusable lesson into [lessons.md](lessons.md), a machine value into
   `AGENTS.local.md`, a wrong rule into this file — and into `docs/AGENTS.md` or the root `AGENTS.md` when it binds the repo.
   Closing an acceptance conversation runs this harvest; a probe worth running twice becomes a driver action.
10. `[CRITICAL]` **Wall time is a measured fact.** Report setup and case time per case, classified `InRun`, `NewWorld` or
    `ClientRestart`; seconds per case are the target, an overrun is a finding to optimize, not a normal cost.
    **Setup above case time is a batch defect**: a fresh run is paid only when the previous case left or changed the run;
    every other precondition is a cheap probe inside the case's own world (measured 2026-11-25: giving each row its own
    world made setup 2000 s against 500 s of case work).
11. `[CRITICAL]` **Every case counts the logs it caused.** Mark both clients' log offsets before it starts; count every
    `Warning` and `Error` since, and assert the case's declared budget (zero errors by default). An unexplained error fails
    even when behaviour passed; known warnings are named and bounded, never filtered out.
12. `[CRITICAL]` **Assertions prove the design, not the happy path.** Name the design claim and read the player-visible state on
    every affected client — host and member, watcher and watched — plus the unchanged side or a control.
    "Non-empty", "ok" and "connected" are preconditions, never verdicts.
13. `[CRITICAL]` **The automated suite is the last acceptance gate.** A `review/` row closes only when an automated case
    covers it on the deployed artifact and its report (log counts, per-client readings) cited. Manual play may explain
    a failure, never substitute for a case; a row without a case stays open.
14. `[CRITICAL]` **One run covers every case.** Acceptance happens once, at a work item's end: one command runs every
    registered case and every row is green. No case is dropped to save time; a case that needs a world or client restart
    declares that cost, and one that cannot run here (a missing client) is named as not run.

## Related

[`../backlog/README.md`](../backlog/README.md) — status meaning and transitions · [`../../AGENTS.md`](../../AGENTS.md) — the cycle
before acceptance · [`../evidence/delivery-checklist.md`](../evidence/delivery-checklist.md) — the delivery gate
