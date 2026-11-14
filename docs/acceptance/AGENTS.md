# docs/acceptance/ — agent-run acceptance

Agent-only area (no `README.md`, never translated, outside the human navigation). Acceptance is the agent's duty, not a step handed
to the user: a ticket in `review/` is code-complete and waits for this run, and the run moves it. Machine facts live in the
gitignored [AGENTS.local.md](AGENTS.local.md): read it first, never copy machine values into a committed page.

## Index

- [workflow.md](workflow.md) — the end-to-end run and the capability table.
- [dependencies.md](dependencies.md) — every dependency, its detection, and the degradation ladder.
- [lessons.md](lessons.md) — what the runs keep teaching.
- [`tools/acceptance/preflight.ps1`](../../tools/acceptance/preflight.ps1) — the executable half of the dependency table.
- [`tools/acceptance/drive-in-process.ps1`](../../tools/acceptance/drive-in-process.ps1) — the in-process driver of a running
  client (`-ListActions` names its vocabulary) with its recipes under [`tools/acceptance/recipes/`](../../tools/acceptance/recipes/).
- [`tools/acceptance/session-environment.ps1`](../../tools/acceptance/session-environment.ps1) — the BepInEx-tree gate.

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
10. `[CRITICAL]` **Wall time is a measured fact.** Report connect / setup / case per case and classify it `InRun`, `NewWorld` or
    `ClientRestart`. Target **seconds per case and tens of seconds per group**; an overrun is a finding to optimize (reuse a healthy
    pair, cut dead waits, restore through the game path), not a normal cost. `--cost` and `--reuse` select groups; the report says
    when a pair was reused.
11. `[CRITICAL]` **Every case counts the logs it caused.** Mark both clients' log offsets before it starts; count every `Warning` and
    `Error` written since. The report carries both counts; the case asserts its declared error budget (zero by default) and warning
    budget. An unexplained error fails even when behaviour passed; known warnings are named and bounded, never filtered out.
12. `[CRITICAL]` **Assertions prove the design, not the happy path.** Name the design claim and read the player-visible state on each
    affected client — host and member, watcher and watched — plus the unchanged side or a control where a false pass is possible.
    "Non-empty", "ok" and "connected" are preconditions, never verdicts; a coarse case is rewritten, and a green case that would
    still pass with the feature removed is not evidence.
13. `[CRITICAL]` **The automated suite is the last acceptance gate.** A `review/` row closes only when an automated case covers it, ran
    against the deployed artifact in this batch, and its report (including log counts and per-client readings) is cited. Manual play
    may explain a failure but never substitutes for a missing case; a row without a case stays open.

## Related

[`../backlog/README.md`](../backlog/README.md) — status meaning and transitions · [`../../AGENTS.md`](../../AGENTS.md) — the cycle
before acceptance · [`../evidence/delivery-checklist.md`](../evidence/delivery-checklist.md) — the delivery gate
